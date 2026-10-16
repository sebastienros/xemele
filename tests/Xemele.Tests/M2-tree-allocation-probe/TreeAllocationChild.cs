using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Xemele.Tests.Tree;

internal static class TreeAllocationChild
{
    internal const string Fact = "Xemele.Tests.Tree.TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing";
    private const string ModeKey = "XEMELE_TREE_CHILD_MODE";
    private const string RequestKey = "XEMELE_TREE_CHILD_REQUEST";
    private const string TokenKey = "XEMELE_TREE_CHILD_TOKEN";
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    internal sealed record Provenance(string Executable, string ExecutableSha256,
        string TestAssembly, string TestSha256, string TestVersion, string ProductAssembly,
        string ProductSha256, string ProductVersion, string RuntimeVersion,
        string CoreLibrary, string CoreLibrarySha256, string NativeRuntimeSha256,
        string HelperIlSha256, string RuntimeConfigSha256);
    internal sealed record Request(int Version, string Mode, string Token, int ParentPid,
        string Directory, string XmlPath, string ReportPath, Provenance Expected);
    internal sealed record Report(int Version, string Mode, string Token, int ProcessId,
        Provenance Actual, int WarmIterations, int MeasuredIterations, long WarmBytes,
        long AllocatedBytes, long WarmConsumed, long MeasuredConsumed, bool AttributeIdentity,
        int EscapedObjectLength);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
    private static string Canonical(string path) => new FileInfo(Path.GetFullPath(path)).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(path);
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static string Version(Assembly assembly) => assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Missing compiled source version.");
    private static string Repository()
    {
        for (DirectoryInfo? d = new(Path.GetDirectoryName(typeof(TreeAllocationChild).Assembly.Location)!); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "Xemele.slnx")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException("The Tree allocation test requires its repository artifacts directory.");
    }
    private static string RuntimeDotnet()
    {
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var path = Path.Combine(runtime.Parent!.Parent!.Parent!.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        Require(File.Exists(path), "Missing host beside the active runtime.");
        return Canonical(path);
    }
    private static string Host()
    {
        var runtimeHost = RuntimeDotnet();
        var configured = Environment.GetEnvironmentVariable("XEMELE_DOTNET");
        if (configured is not null)
        {
            Require(File.Exists(configured) && Canonical(configured) == runtimeHost && Hash(configured) == Hash(runtimeHost), "Configured dotnet differs from the active runtime installation.");
        }

        var process = Environment.ProcessPath;
        if (process is not null && File.Exists(process))
        {
            var candidate = Canonical(process);
            if (Path.GetFileNameWithoutExtension(candidate) == "dotnet")
            {
                Require(candidate == runtimeHost && Hash(candidate) == Hash(runtimeHost), "Current host differs from the active runtime installation.");
                return candidate;
            }
            var apphost = Path.ChangeExtension(typeof(TreeAllocationChild).Assembly.Location, OperatingSystem.IsWindows() ? ".exe" : null);
            if (File.Exists(apphost) && candidate == Canonical(apphost))
            {
                return candidate;
            }
        }
        return runtimeHost;
    }
    private static Provenance Fingerprint(string executable)
    {
        Assembly test = typeof(TreeAllocationChild).Assembly, product = typeof(XmlDocument).Assembly;
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var native = Path.Combine(runtime, OperatingSystem.IsMacOS() ? "libcoreclr.dylib" : OperatingSystem.IsWindows() ? "coreclr.dll" : "libcoreclr.so");
        var helper = typeof(TreeAllocationProbe).GetMethod(nameof(TreeAllocationProbe.Walk), BindingFlags.Static | BindingFlags.NonPublic)!;
        return new(Canonical(executable), Hash(executable), Canonical(test.Location), Hash(test.Location), Version(test),
            Canonical(product.Location), Hash(product.Location), Version(product), Environment.Version.ToString(),
            Canonical(typeof(object).Assembly.Location), Hash(typeof(object).Assembly.Location), Hash(native),
            Convert.ToHexString(SHA256.HashData(helper.GetMethodBody()!.GetILAsByteArray()!)),
            Hash(Path.ChangeExtension(test.Location, ".runtimeconfig.json")));
    }
    internal static Request? EnterChild()
    {
        string? mode = Environment.GetEnvironmentVariable(ModeKey), path = Environment.GetEnvironmentVariable(RequestKey), token = Environment.GetEnvironmentVariable(TokenKey);
        if (mode is null)
        {
            Require(path is null && token is null, "Incomplete internal Tree child guard.");
            return null;
        }
        Require(mode is "real" or "positive" && path is not null && token is not null, "Invalid internal Tree child guard.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path!), StrictJson) ?? throw new InvalidOperationException("Missing child request.");
        Require(request.Version == 1 && request.Mode == mode && request.Token == token && Guid.TryParseExact(token, "N", out _), "Mismatched child capability.");
        Require(request.ParentPid != Environment.ProcessId && request.ParentPid > 0, "Invalid child parent identity.");
        using (var parent = Process.GetProcessById(request.ParentPid))
        {
            Require(!parent.HasExited, "Child parent is no longer running.");
        }

        var artifactRoot = Path.Combine(Repository(), "artifacts", "M2-tree-allocation-probe", "child-runs") + Path.DirectorySeparatorChar;
        Require(Canonical(request.Directory).StartsWith(Canonical(artifactRoot.TrimEnd(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar, StringComparison.Ordinal) && request.Directory == Path.GetFullPath(request.Directory), "Child request outside owned artifacts.");
        Require(Canonical(path!) == Canonical(Path.Combine(request.Directory, mode + ".request.json"))
            && request.ReportPath == Path.Combine(request.Directory, mode + ".report.json")
            && request.XmlPath == Path.Combine(request.Directory, mode + ".xml"), "Invalid child output paths.");
        var arguments = Environment.GetCommandLineArgs();
        int filter = Array.IndexOf(arguments, "-method"), xml = Array.IndexOf(arguments, "-result-xml");
        Require(filter >= 0 && filter + 1 < arguments.Length && arguments[filter + 1] == Fact
            && arguments.Count(a => a == "-method") == 1 && xml >= 0 && xml + 1 < arguments.Length
            && arguments[xml + 1] == request.XmlPath && arguments.Length == 6
            && arguments[1] == "-noColor" && filter == 2 && xml == 4, "Child guard requires the exact standalone Fact invocation.");
        Require(request.Expected is not null && Fingerprint(Environment.ProcessPath!) == request.Expected, "Child executable/compiled product/runtime provenance mismatch.");
        return request;
    }
    internal static void Write(Request request, long warmBytes, long allocated,
        long warmConsumed, long measuredConsumed, bool attributeIdentity, int escapedLength)
    {
        var report = new Report(1, request.Mode, request.Token, Environment.ProcessId, Fingerprint(Environment.ProcessPath!),
            1000, 1000, warmBytes, allocated, warmConsumed, measuredConsumed, attributeIdentity, escapedLength);
        File.WriteAllText(request.ReportPath, JsonSerializer.Serialize(report));
    }
    internal static (Report Real, Report Positive, string Directory) RunPair()
    {
        var executable = Host();
        var expected = Fingerprint(executable);
        Require(expected.RuntimeVersion.StartsWith("10.", StringComparison.Ordinal), "Tree allocation scope is net10.0 only.");
        var token = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Repository(), "artifacts", "M2-tree-allocation-probe", "child-runs", Environment.ProcessId + "-" + token);
        System.IO.Directory.CreateDirectory(directory);
        Report real = Run("real"), positive = Run("positive"); // Real always first; no child retries.
        return (real, positive, directory);

        Report Run(string mode)
        {
            var request = new Request(1, mode, token, Environment.ProcessId, directory,
                Path.Combine(directory, mode + ".xml"), Path.Combine(directory, mode + ".report.json"), expected);
            var requestPath = Path.Combine(directory, mode + ".request.json");
            File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo(executable) { WorkingDirectory = Repository(), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(executable) == "dotnet")
            {
                start.ArgumentList.Add(expected.TestAssembly);
            }

            foreach (var argument in new[] { "-noColor", "-method", Fact, "-result-xml", request.XmlPath })
            {
                start.ArgumentList.Add(argument);
            }

            start.Environment[ModeKey] = mode;
            start.Environment[RequestKey] = requestPath;
            start.Environment[TokenKey] = token;
            File.WriteAllText(Path.Combine(directory, mode + ".invocation.json"), JsonSerializer.Serialize(new
            {
                executable,
                arguments = start.ArgumentList.ToArray(),
                expected,
                requestPath
            }));
            using var process = new Process { StartInfo = start };
            using var stdout = File.Create(Path.Combine(directory, mode + ".stdout.txt"));
            using var stderr = File.Create(Path.Combine(directory, mode + ".stderr.txt"));
            bool started = false, exited = false, drained = false;
            int? exitCode = null, pid = null;
            try
            {
                started = process.Start();
                Require(started, "Tree child did not start: " + directory);
                pid = process.Id;
                Task output = process.StandardOutput.BaseStream.CopyToAsync(stdout), error = process.StandardError.BaseStream.CopyToAsync(stderr);
                exited = process.WaitForExit(30_000);
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    Require(process.WaitForExit(5_000), "Tree child did not terminate: " + directory);
                }
                exitCode = process.ExitCode;
                drained = Task.WaitAll([output, error], 5_000);
            }
            finally
            {
                stdout.Flush();
                stderr.Flush();
                File.WriteAllText(Path.Combine(directory, mode + ".exit.json"), JsonSerializer.Serialize(new
                {
                    started,
                    exited,
                    drained,
                    exitCode,
                    pid
                }));
            }
            Require(exited && drained && exitCode == 0, "Tree " + mode + " child failed/timeout; retained artifacts: " + directory);
            Require(File.Exists(request.ReportPath) && File.Exists(request.XmlPath), "Tree child output missing: " + directory);
            var report = JsonSerializer.Deserialize<Report>(File.ReadAllText(request.ReportPath), StrictJson) ?? throw new InvalidOperationException("Missing Tree report: " + directory);
            var document = XDocument.Load(request.XmlPath);
            var assemblies = document.Descendants("assembly").ToArray();
            Require(assemblies.Length == 1 && (string?)assemblies[0].Attribute("total") == "1"
                && (string?)assemblies[0].Attribute("passed") == "1" && (string?)assemblies[0].Attribute("failed") == "0"
                && (string?)assemblies[0].Attribute("skipped") == "0" && !document.Descendants("failure").Any(), "Invalid child XML totals: " + directory);
            var tests = document.Descendants("test").ToArray();
            Require(tests.Length == 1 && (string?)tests[0].Attribute("name") == Fact && (string?)tests[0].Attribute("result") == "Pass", "Child must run exactly the allocation Fact: " + directory);
            Require(report.Version == 1 && report.Mode == mode && report.Token == token && report.ProcessId == process.Id
                && report.Actual == expected && Fingerprint(executable) == expected, "Tree child provenance/report mismatch: " + directory);
            Require(report.WarmIterations == 1000 && report.MeasuredIterations == 1000
                && report.WarmConsumed == 21000 && report.MeasuredConsumed == 21000 && report.AttributeIdentity,
                "Tree child workload/identity mismatch: " + directory);
            Require(mode == "real" ? report.AllocatedBytes == 0 && report.EscapedObjectLength == 0 : report.AllocatedBytes > 0 && report.EscapedObjectLength == 37,
                "Tree child allocation meter/control mismatch: " + directory);
            return report;
        }
    }
}
