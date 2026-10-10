using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xemele.Resolution.Core;

namespace Xemele.Tests.Resolution;

internal static class ResolverAllocationChild
{
    internal const string Fact = "Xemele.Tests.Resolution.ResolverTests.WarmBorrowedReadsAllocateNothing";
    private const string ModeKey = "XEMELE_RESOLVER_CHILD_MODE";
    private const string RequestKey = "XEMELE_RESOLVER_CHILD_REQUEST";
    private const string TokenKey = "XEMELE_RESOLVER_CHILD_TOKEN";
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    internal sealed record Provenance(string Executable, string ExecutableSha256,
        string TestAssembly, string TestSha256, string TestVersion, string ProductAssembly,
        string ProductSha256, string ProductVersion, string RuntimeVersion,
        bool ServerGc, string GcConfiguration, string CoreLibrary, string CoreLibrarySha256, string NativeRuntimeSha256,
        string HelperIlSha256, string RuntimeConfigSha256);
    internal sealed record Request(int Version, string Mode, string Token, int ParentPid,
        string Directory, string XmlPath, string ReportPath, Provenance Expected);
    internal sealed record Report(int Version, string Mode, string Token, int ProcessId,
        Provenance Actual, int WarmIterations, int MeasuredIterations, long WarmBytes,
        long AllocatedBytes, long WarmReadBytes, long MeasuredBytes, long TotalBytes,
        long ResourcesOpened, int ResolverCalls, int EscapedObjectLength);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string Canonical(string path) => new FileInfo(Path.GetFullPath(path)).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(path);
    private static string CanonicalDirectory(string path) => new DirectoryInfo(Path.GetFullPath(path)).ResolveLinkTarget(true)?.FullName.TrimEnd(Path.DirectorySeparatorChar)
        ?? Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static string Version(Assembly assembly) => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Missing compiled source version.");
    private static string Repository()
    {
        for (DirectoryInfo? d = new(Path.GetDirectoryName(typeof(ResolverAllocationChild).Assembly.Location)!); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "docs/tasks/M2-resolver-allocation-probe.json"))) return d.FullName;
        throw new InvalidOperationException("The Resolver allocation test requires its repository artifacts directory.");
    }
    private static string RuntimeDotnet()
    {
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        string path = Path.Combine(runtime.Parent!.Parent!.Parent!.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        Require(File.Exists(path), "Missing host beside the active runtime.");
        return Canonical(path);
    }
    private static string Host()
    {
        string runtimeHost = RuntimeDotnet();
        string? configured = Environment.GetEnvironmentVariable("XEMELE_DOTNET");
        if (configured is not null)
            Require(File.Exists(configured) && Canonical(configured) == runtimeHost && Hash(configured) == Hash(runtimeHost), "Configured dotnet differs from the active runtime installation.");
        string? process = Environment.ProcessPath;
        if (process is not null && File.Exists(process))
        {
            string candidate = Canonical(process);
            if (Path.GetFileNameWithoutExtension(candidate) == "dotnet")
            {
                Require(candidate == runtimeHost && Hash(candidate) == Hash(runtimeHost), "Current host differs from the active runtime installation.");
                return candidate;
            }
            string apphost = Path.ChangeExtension(typeof(ResolverAllocationChild).Assembly.Location, OperatingSystem.IsWindows() ? ".exe" : null);
            if (File.Exists(apphost) && candidate == Canonical(apphost)) return candidate;
        }
        return runtimeHost;
    }
    private static Provenance Fingerprint(string executable)
    {
        Assembly test = typeof(ResolverAllocationChild).Assembly, product = typeof(XmlResolutionSession).Assembly;
        Require(System.Text.RegularExpressions.Regex.IsMatch(Version(test), @"\+[0-9a-f]{40}$")
            && Version(test) == Version(product), "Missing full matching compiled source revision.");
        string runtime = RuntimeEnvironment.GetRuntimeDirectory();
        string native = Path.Combine(runtime, OperatingSystem.IsMacOS() ? "libcoreclr.dylib" : OperatingSystem.IsWindows() ? "coreclr.dll" : "libcoreclr.so");
        var helper = typeof(ResolverAllocationProbe).GetMethod(nameof(ResolverAllocationProbe.ReadChecked), BindingFlags.Static | BindingFlags.NonPublic)!;
        return new(Canonical(executable), Hash(executable), Canonical(test.Location), Hash(test.Location), Version(test),
            Canonical(product.Location), Hash(product.Location), Version(product), Environment.Version.ToString(), System.Runtime.GCSettings.IsServerGC, JsonSerializer.Serialize(GC.GetConfigurationVariables().OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value)),
            Canonical(typeof(object).Assembly.Location), Hash(typeof(object).Assembly.Location), Hash(native),
            Convert.ToHexString(SHA256.HashData(helper.GetMethodBody()!.GetILAsByteArray()!)),
            Hash(Path.ChangeExtension(test.Location, ".runtimeconfig.json")));
    }
    internal static Request? EnterChild()
    {
        string? mode = Environment.GetEnvironmentVariable(ModeKey), path = Environment.GetEnvironmentVariable(RequestKey), token = Environment.GetEnvironmentVariable(TokenKey);
        if (mode is null)
        {
            Require(path is null && token is null, "Incomplete internal Resolver child guard.");
            return null;
        }
        Require(mode is "real" or "positive" && path is not null && token is not null, "Invalid internal Resolver child guard.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path!), StrictJson) ?? throw new InvalidOperationException("Missing child request.");
        Require(request.Version == 1 && request.Mode == mode && request.Token == token && Guid.TryParseExact(token, "N", out _), "Mismatched child capability.");
        Require(request.ParentPid != Environment.ProcessId && request.ParentPid > 0, "Invalid child parent identity.");
        using (var parent = Process.GetProcessById(request.ParentPid)) Require(!parent.HasExited, "Child parent is no longer running.");
        string artifactRoot = Path.Combine(Repository(), "artifacts/M2-resolver-allocation-probe/child-runs") + Path.DirectorySeparatorChar;
        Require(CanonicalDirectory(request.Directory).StartsWith(CanonicalDirectory(artifactRoot) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && Path.GetFullPath(request.Directory) == CanonicalDirectory(request.Directory), "Child request outside owned artifacts.");
        Require(Canonical(path!) == Canonical(Path.Combine(request.Directory, mode + ".request.json"))
            && request.ReportPath == Path.Combine(request.Directory, mode + ".report.json")
            && request.XmlPath == Path.Combine(request.Directory, mode + ".xml"), "Invalid child output paths.");
        string[] arguments = Environment.GetCommandLineArgs();
        string executable = Host();
        // .NET's argv[0] is the managed entry assembly for both dotnet and apphost.
        string[] expectedArguments = [typeof(ResolverAllocationChild).Assembly.Location,
            "-noColor", "-method", Fact, "-result-xml", request.XmlPath];
        Require(arguments.SequenceEqual(expectedArguments), "Child guard requires the exact standalone Fact invocation.");
        Require(request.Expected is not null && Fingerprint(Environment.ProcessPath!) == request.Expected, "Child executable/compiled product/runtime provenance mismatch.");
        return request;
    }
    internal static void Write(Request request, long warm, long allocated, long warmReadBytes,
        long measuredBytes, long totalBytes, long resourcesOpened, int resolverCalls, int escapedLength)
    {
        var report = new Report(1, request.Mode, request.Token, Environment.ProcessId, Fingerprint(Environment.ProcessPath!),
            1000, 10000, warm, allocated, warmReadBytes, measuredBytes, totalBytes, resourcesOpened, resolverCalls, escapedLength);
        File.WriteAllText(request.ReportPath, JsonSerializer.Serialize(report));
    }
    internal static (Report Real, Report Positive, string Directory) RunPair()
    {
        string executable = Host();
        Provenance expected = Fingerprint(executable);
        Require(expected.RuntimeVersion.StartsWith("10.", StringComparison.Ordinal), "Resolver allocation scope is net10.0 only.");
        string token = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(Repository(), "artifacts/M2-resolver-allocation-probe/child-runs", Environment.ProcessId + "-" + token);
        System.IO.Directory.CreateDirectory(directory);
        Report real = Run("real"), positive = Run("positive"); // Real always first; no child retries.
        return (real, positive, directory);

        Report Run(string mode)
        {
            var request = new Request(1, mode, token, Environment.ProcessId, directory,
                Path.Combine(directory, mode + ".xml"), Path.Combine(directory, mode + ".report.json"), expected);
            string requestPath = Path.Combine(directory, mode + ".request.json");
            File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo(executable) { WorkingDirectory = Repository(), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(executable) == "dotnet") start.ArgumentList.Add(expected.TestAssembly);
            foreach (string argument in new[] { "-noColor", "-method", Fact, "-result-xml", request.XmlPath }) start.ArgumentList.Add(argument);
            start.Environment[ModeKey] = mode; start.Environment[RequestKey] = requestPath; start.Environment[TokenKey] = token;
            File.WriteAllText(Path.Combine(directory, mode + ".invocation.json"), JsonSerializer.Serialize(new { executable, arguments = start.ArgumentList.ToArray(), expected, requestPath }));
            using var process = new Process { StartInfo = start };
            using var stdout = File.Create(Path.Combine(directory, mode + ".stdout.txt"));
            using var stderr = File.Create(Path.Combine(directory, mode + ".stderr.txt"));
            bool started = false, exited = false, drained = false;
            int? exitCode = null, pid = null;
            try
            {
                started = process.Start();
                Require(started, "Resolver child did not start: " + directory);
                pid = process.Id;
                Task output = process.StandardOutput.BaseStream.CopyToAsync(stdout), error = process.StandardError.BaseStream.CopyToAsync(stderr);
                exited = process.WaitForExit(30_000);
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    Require(process.WaitForExit(5_000), "Resolver child did not terminate: " + directory);
                }
                exitCode = process.ExitCode;
                drained = Task.WaitAll([output, error], 5_000);
            }
            finally
            {
                stdout.Flush(); stderr.Flush();
                File.WriteAllText(Path.Combine(directory, mode + ".exit.json"), JsonSerializer.Serialize(new { started, exited, drained, exitCode, pid }));
            }
            Require(exited && drained && exitCode == 0, "Resolver " + mode + " child failed/timeout; retained artifacts: " + directory);
            Require(File.Exists(request.ReportPath) && File.Exists(request.XmlPath), "Resolver child output missing: " + directory);
            var report = JsonSerializer.Deserialize<Report>(File.ReadAllText(request.ReportPath), StrictJson) ?? throw new InvalidOperationException("Missing Resolver report: " + directory);
            var document = XDocument.Load(request.XmlPath);
            var assemblies = document.Descendants("assembly").ToArray();
            Require(assemblies.Length == 1 && (string?)assemblies[0].Attribute("total") == "1"
                && (string?)assemblies[0].Attribute("passed") == "1" && (string?)assemblies[0].Attribute("failed") == "0"
                && (string?)assemblies[0].Attribute("skipped") == "0", "Child XML aggregates mismatch: " + directory);
            var tests = document.Descendants("test").ToArray();
            Require(tests.Length == 1 && (string?)tests[0].Attribute("name") == Fact && (string?)tests[0].Attribute("result") == "Pass", "Child must run exactly the allocation Fact: " + directory);
            Require(report.Version == 1 && report.Mode == mode && report.Token == token && report.ProcessId == process.Id
                && report.Actual == expected && Fingerprint(executable) == expected, "Resolver child provenance/report mismatch: " + directory);
            Require(report.WarmIterations == 1000 && report.MeasuredIterations == 10000 && report.WarmReadBytes == 4096000
                && report.MeasuredBytes == 40960000 && report.TotalBytes == 45056000 && report.ResourcesOpened == 1
                && report.ResolverCalls == 1, "Resolver child workload/progress mismatch: " + directory);
            Require(mode == "real" ? report.AllocatedBytes == 0 && report.EscapedObjectLength == 0 : report.AllocatedBytes == 640000 && report.EscapedObjectLength == 37,
                "Resolver child allocation meter/control mismatch: " + directory);
            return report;
        }
    }
}
