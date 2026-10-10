using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    private const int BaselineIntervals = 32, PressureIntervals = 192;
    private const int SpinIterations = 100_000, MaximumRequests = 512;
    private readonly record struct Observation(int Index, long ZeroBytes, long PositiveBytes,
        int RequestsBefore, int RequestsAfter, int Gen2Before, int Gen2After);
    private sealed class RetainedNode(int number) { internal readonly int Number = number; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ZeroWork()
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        Thread.SpinWait(SpinIterations);
        long end = GC.GetAllocatedBytesForCurrentThread();
        return end - start;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object PositiveAllocation() => new byte[37];
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long PositiveWork()
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        object allocated = PositiveAllocation();
        Thread.SpinWait(SpinIterations);
        long end = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(allocated);
        return end - start;
    }
    private static int Main()
    {
        var deadline = Stopwatch.StartNew(); // Deadline only; this is not a timing benchmark.
        var baseline = new Observation[BaselineIntervals];
        var pressure = new Observation[PressureIntervals];
        int requests = 0;
        Exception? workerError = null;
        using var ready = new ManualResetEventSlim();
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (int i = 0; i < baseline.Length; i++)
        {
            int gen2 = GC.CollectionCount(2);
            long zero = ZeroWork(), positive = PositiveWork();
            baseline[i] = new(i, zero, positive, 0, 0, gen2, GC.CollectionCount(2));
        }
        var worker = new Thread(() =>
        {
            try
            {
                // Bounded retained heap: 16 MiB ring + one replacement, ~2 MiB
                // root references and ~8 MiB small nodes (under 28 MiB live).
                var ring = new byte[16][];
                var roots = new RetainedNode[262_144];
                for (int i = 0; i < ring.Length; i++) ring[i] = new byte[1024 * 1024];
                for (int i = 0; i < roots.Length; i++) roots[i] = new(i);
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
                ready.Set();
                for (int i = 0; i < MaximumRequests && !shutdown.IsCancellationRequested; i++)
                {
                    ring[i % ring.Length] = new byte[1024 * 1024];
                    Interlocked.Increment(ref requests);
                    // Explicit diagnostic pressure, not a runtime configuration change.
                    GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: false);
                    Thread.Sleep(4);
                }
                GC.KeepAlive(ring);
                GC.KeepAlive(roots);
            }
            catch (Exception error) { workerError = error; ready.Set(); }
        }) { IsBackground = true, Name = "Bounded allocation-counter control" };
        string? errorText = null;
        long backgroundIndexBefore = 0, backgroundIndexAfter = 0;
        worker.Start();
        try
        {
            if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Worker startup deadline.");
            if (workerError is not null) throw new InvalidOperationException("Worker initialization.", workerError);
            backgroundIndexBefore = GC.GetGCMemoryInfo(GCKind.Background).Index;
            for (int i = 0; i < pressure.Length; i++)
            {
                if (deadline.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Control deadline.");
                int requestStart = Volatile.Read(ref requests), gen2 = GC.CollectionCount(2);
                // Allocation-context anchor is outside either measured interval.
                object anchor = PositiveAllocation();
                long zero = ZeroWork(), positive = PositiveWork();
                pressure[i] = new(i, zero, positive, requestStart,
                    Volatile.Read(ref requests), gen2, GC.CollectionCount(2));
                GC.KeepAlive(anchor);
            }
        }
        catch (Exception error) { errorText = error.ToString(); }
        finally
        {
            shutdown.Cancel();
            if (!worker.Join(TimeSpan.FromSeconds(5))) errorText = "Worker shutdown deadline.";
        }
        backgroundIndexAfter = GC.GetGCMemoryInfo(GCKind.Background).Index;
        if (workerError is not null) errorText = workerError.ToString();
        int baselineZeroNonzero = baseline.Count(r => r.ZeroBytes != 0);
        int baselinePositive64 = baseline.Count(r => r.PositiveBytes == 64);
        int pressureZeroNonzero = pressure.Count(r => r.ZeroBytes != 0);
        int pressurePositive64 = pressure.Count(r => r.PositiveBytes == 64);
        int exit = errorText is not null ? 3 : baselineZeroNonzero != 0 || baselinePositive64 != baseline.Length ? 2
            : pressureZeroNonzero != 0 || pressurePositive64 != pressure.Length ? 1 : 0;
        // Reflection, storage and output occur after all counter intervals.
        var zeroMethod = typeof(Program).GetMethod(nameof(ZeroWork), BindingFlags.NonPublic | BindingFlags.Static)!;
        var positiveMethod = typeof(Program).GetMethod(nameof(PositiveWork), BindingFlags.NonPublic | BindingFlags.Static)!;
        Console.WriteLine(JsonSerializer.Serialize(new {
            sourceRevision = Environment.GetEnvironmentVariable("XEMELE_CONTROL_SOURCE_REVISION"),
            runtime = Environment.Version.ToString(), framework = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), serverGC = System.Runtime.GCSettings.IsServerGC,
            defaultSettings = "No GC/JIT/runtime configuration overrides; explicit bounded GC worker only",
            BaselineIntervals, PressureIntervals, SpinIterations, MaximumRequests,
            requests, gen2 = GC.CollectionCount(2), backgroundIndexBefore, backgroundIndexAfter,
            backgroundGcObserved = backgroundIndexAfter > backgroundIndexBefore, baselineZeroNonzero, baselinePositive64,
            pressureZeroNonzero, pressurePositive64, baseline, pressure,
            zeroMethodFlags = zeroMethod.GetMethodImplementationFlags().ToString(),
            zeroMethodIl = Convert.ToHexString(zeroMethod.GetMethodBody()!.GetILAsByteArray()!),
            positiveMethodFlags = positiveMethod.GetMethodImplementationFlags().ToString(),
            positiveMethodIl = Convert.ToHexString(positiveMethod.GetMethodBody()!.GetILAsByteArray()!),
            errorText, exit,
            scope = "Package-free synchronous counter controls; no Xemele/test framework; no historical-failure attribution"
        }));
        return exit;
    }
}
