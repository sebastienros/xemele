using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace Xemele.M3AsyncWriterMeasurements;

// Standalone counters run only under the coordinator's exclusive measurement lease.
public static class Probe
{
    private static readonly XmlSaveOptions Options = new() { OmitXmlDeclaration = true };
    private static readonly string Text = "x&α😀]]";
    public static async Task<int> Main(string[] args)
    {
        bool prepare = args.Contains("--prepare", StringComparer.Ordinal);
        var immediate = new Sink(false);
        if (prepare)
        {
            await Exercise(immediate, "α😀&<\r]]>", 32);
            await Exercise(new Sink(true), new string('x', 32768), 2);
            Console.WriteLine("Preparation passed: immediate and delayed async-only sinks, no counters enabled."); return 0;
        }
        long coldBefore = GC.GetAllocatedBytesForCurrentThread();
        var cold = XmlWriter.Create(immediate, Options); long coldSetup = GC.GetAllocatedBytesForCurrentThread() - coldBefore; cold.Dispose();
        using var writer = XmlWriter.Create(immediate, Options);
        Complete(writer.WriteStartElementAsync("r")); Complete(writer.WriteTextAsync(ReadOnlyMemory<char>.Empty));
        for (int i = 0; i < 100; i++) { Primitive(writer); Complete(writer.FlushAsync()); }
        long primitive = 0; int bufferedOperations = 0;
        for (int batch = 0; batch < 1000; batch++)
        {
            Complete(writer.FlushAsync()); long writes = immediate.Writes;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++) Primitive(writer);
            primitive += GC.GetAllocatedBytesForCurrentThread() - before;
            if (immediate.Writes != writes) throw new InvalidOperationException("Buffered primitive scope performed I/O.");
            bufferedOperations += 16;
        }
        long positiveBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < bufferedOperations; i++) GC.KeepAlive(new byte[256]);
        long positive = GC.GetAllocatedBytesForCurrentThread() - positiveBefore;
        Complete(writer.FlushAsync());
        long setupBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { using var next = XmlWriter.Create(immediate, Options); }
        long warmSetup = GC.GetAllocatedBytesForCurrentThread() - setupBefore;
        object output = typeof(XmlWriter).GetField("output", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(writer)!;
        byte[] scratch = (byte[])output.GetType().GetField("Bytes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(output)!;
        var delayed = new Sink(true); string longText = string.Concat(Enumerable.Repeat("界😀&<\r]]>", 8192));
        await Exercise(delayed, longText, 1); // continuation/JIT warm-up outside counter scope
        long asyncBefore = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < 10; i++) await Exercise(delayed, longText, 1);
        long continuation = GC.GetTotalAllocatedBytes(precise: true) - asyncBefore;
        Console.WriteLine(JsonSerializer.Serialize(new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription,
            coldSetupBytes = coldSetup, warmSetupBytesPerWriter = warmSetup / 1000.0,
            bufferedOperations, bufferedPrimitiveBytes = primitive, bufferedPrimitiveBytesPerOperation = primitive / (double)bufferedOperations,
            positiveControlBytes = positive, positiveControlBytesPerOperation = positive / (double)bufferedOperations,
            retainedScratchBytes = scratch.Length, pendingScope = "one pending tag plus depth/scope; text owners expire on completion",
            delayedDocuments = 10, delayedTextChars = longText.Length, delayedTotalProcessBytes = continuation,
            delayedBytesPerDocument = continuation / 10.0, delayedMaxWrite = delayed.MaxWrite, delayedBytes = delayed.Count,
            continuationScope = "process-wide counters include writer setup and genuine suspended continuations; scheduling noise is informational" }));
        if (primitive != 0 || positive <= 0 || scratch.Length != 8192 || delayed.MaxWrite > 8192) return 1;
        return 0;
    }
    private static void Primitive(XmlWriter w)
    {
        Complete(w.WriteTextAsync(Text.AsMemory())); Complete(w.WriteTextAsync(">".AsMemory()));
        Complete(w.WriteStartElementAsync("x")); Complete(w.WriteAttributeAsync("a", "v")); Complete(w.WriteEndElementAsync());
    }
    private static void Complete(ValueTask operation)
    { if (!operation.IsCompletedSuccessfully) throw new InvalidOperationException("Expected a buffered synchronous completion."); operation.GetAwaiter().GetResult(); }
    private static async Task Exercise(Sink stream, string text, int repeats)
    {
        await using var writer = XmlWriter.Create(stream, Options);
        await writer.WriteStartElementAsync("r");
        for (int i = 0; i < repeats; i++) { await writer.WriteStartElementAsync("x"); await writer.WriteAttributeAsync("a", "v"); await writer.WriteTextAsync(text.AsMemory()); await writer.WriteEndElementAsync(); }
        await writer.WriteEndElementAsync(); await writer.WriteEndDocumentAsync();
    }
    private sealed class Sink(bool delayed) : Stream
    {
        internal long Count, Writes; internal int MaxWrite;
        public override bool CanWrite => true; public override bool CanRead => false; public override bool CanSeek => false;
        public override long Length => Count; public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (delayed) return Delay(buffer, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); Count += buffer.Length; Writes++; MaxWrite = Math.Max(MaxWrite, buffer.Length); return default;
        }
        private async ValueTask Delay(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); Count += buffer.Length; Writes++; MaxWrite = Math.Max(MaxWrite, buffer.Length); }
        public override Task FlushAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public override void Flush() => throw new InvalidOperationException("sync flush");
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("sync write");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("sync write");
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
