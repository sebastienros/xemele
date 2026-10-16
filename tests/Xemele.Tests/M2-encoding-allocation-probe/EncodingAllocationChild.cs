using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xemele.Parsing.Input;

namespace Xemele.Tests.EncodingCompatibility;

internal static class EncodingAllocationChild
{
    internal const string Fact = "Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero";
    private const string ModeKey = "XEMELE_ENCODING_CHILD_MODE";
    private const string RequestKey = "XEMELE_ENCODING_CHILD_REQUEST";
    private const string TokenKey = "XEMELE_ENCODING_CHILD_TOKEN";
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
        Provenance Actual, EncodingAllocationProbe.LabelReport[] Labels);

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
        for (DirectoryInfo? d = new(Path.GetDirectoryName(typeof(EncodingAllocationChild).Assembly.Location)!); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "Xemele.slnx")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException("The Encoding allocation test requires its repository artifacts directory.");
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
            var apphost = Path.ChangeExtension(typeof(EncodingAllocationChild).Assembly.Location, OperatingSystem.IsWindows() ? ".exe" : null);
            if (File.Exists(apphost) && candidate == Canonical(apphost))
            {
                return candidate;
            }
        }
        return runtimeHost;
    }
    private static Provenance Fingerprint(string executable)
    {
        Assembly test = typeof(EncodingAllocationChild).Assembly, product = typeof(XmlInputDecoder).Assembly;
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var native = Path.Combine(runtime, OperatingSystem.IsMacOS() ? "libcoreclr.dylib" : OperatingSystem.IsWindows() ? "coreclr.dll" : "libcoreclr.so");
        var helper = typeof(EncodingAllocationProbe).GetMethod(nameof(EncodingAllocationProbe.DecodeChecked), BindingFlags.Static | BindingFlags.NonPublic)!;
        Require(System.Text.RegularExpressions.Regex.IsMatch(Version(test), @"\+[0-9a-f]{40}$")
            && System.Text.RegularExpressions.Regex.IsMatch(Version(product), @"\+[0-9a-f]{40}$"), "Missing full compiled revision.");
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
            Require(path is null && token is null, "Incomplete internal Encoding child guard.");
            return null;
        }
        Require(mode is "real" or "positive" && path is not null && token is not null, "Invalid internal Encoding child guard.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(path!), StrictJson) ?? throw new InvalidOperationException("Missing child request.");
        Require(request.Version == 1 && request.Mode == mode && request.Token == token && Guid.TryParseExact(token, "N", out _), "Mismatched child capability.");
        Require(request.ParentPid != Environment.ProcessId && request.ParentPid > 0, "Invalid child parent identity.");
        using (var parent = Process.GetProcessById(request.ParentPid))
        {
            Require(!parent.HasExited, "Child parent is no longer running.");
        }

        var artifactRoot = Path.Combine(Repository(), "artifacts/M2-encoding-allocation-probe/child-runs") + Path.DirectorySeparatorChar;
        Require(Canonical(request.Directory).StartsWith(Canonical(artifactRoot.TrimEnd(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Child request outside owned artifacts.");
        Require(Canonical(path!) == Canonical(Path.Combine(request.Directory, mode + ".request.json"))
            && request.ReportPath == Path.Combine(request.Directory, mode + ".report.json")
            && request.XmlPath == Path.Combine(request.Directory, mode + ".xml"), "Invalid child output paths.");
        var arguments = Environment.GetCommandLineArgs();
        string[] known = ["-noColor", "-method", Fact, "-result-xml", request.XmlPath];
        Require(arguments.Length == known.Length + 1 && arguments.Skip(1).SequenceEqual(known),
            "Child guard requires the exact standalone Fact invocation.");
        Require(request.Expected is not null && Fingerprint(Environment.ProcessPath!) == request.Expected, "Child executable/compiled product/runtime provenance mismatch.");
        return request;
    }
    internal static void Write(Request request, EncodingAllocationProbe.LabelReport[] labels)
    {
        var report = new Report(1, request.Mode, request.Token, Environment.ProcessId,
            Fingerprint(Environment.ProcessPath!), labels);
        File.WriteAllText(request.ReportPath, JsonSerializer.Serialize(report));
    }
    internal static (Report Real, Report Positive, string Directory) RunPair()
    {
        var executable = Host();
        var expected = Fingerprint(executable);
        Require(expected.RuntimeVersion.StartsWith("10.", StringComparison.Ordinal), "Encoding allocation scope is net10.0 only.");
        var token = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Repository(), "artifacts/M2-encoding-allocation-probe/child-runs", Environment.ProcessId + "-" + token);
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
                Require(started, "Encoding child did not start: " + directory);
                pid = process.Id;
                Task output = process.StandardOutput.BaseStream.CopyToAsync(stdout), error = process.StandardError.BaseStream.CopyToAsync(stderr);
                exited = process.WaitForExit(30_000);
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    Require(process.WaitForExit(5_000), "Encoding child did not terminate: " + directory);
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
            Require(exited && drained && exitCode == 0, "Encoding " + mode + " child failed/timeout; retained artifacts: " + directory);
            Require(File.Exists(request.ReportPath) && File.Exists(request.XmlPath), "Encoding child output missing: " + directory);
            var report = JsonSerializer.Deserialize<Report>(File.ReadAllText(request.ReportPath), StrictJson) ?? throw new InvalidOperationException("Missing Encoding report: " + directory);
            var xmlDocument = XDocument.Load(request.XmlPath);
            var tests = xmlDocument.Descendants("test").ToArray();
            var assemblies = xmlDocument.Descendants("assembly").ToArray();
            var collections = xmlDocument.Descendants("collection").ToArray();
            Require(assemblies.Length == 1 && collections.Length == 1
                && assemblies.Concat(collections).All(x => (string?)x.Attribute("total") == "1"
                    && (string?)x.Attribute("passed") == "1" && (string?)x.Attribute("failed") == "0"
                    && (string?)x.Attribute("skipped") == "0"), "Child XML aggregate mismatch: " + directory);
            Require(tests.Length == 1 && (string?)tests[0].Attribute("name") == Fact && (string?)tests[0].Attribute("result") == "Pass", "Child must run exactly the allocation Fact: " + directory);
            Require(report.Version == 1 && report.Mode == mode && report.Token == token && report.ProcessId == process.Id
                && report.Actual == expected && Fingerprint(executable) == expected, "Encoding child provenance/report mismatch: " + directory);
            Require(report.Labels is { Length: 3 }, "Missing label profiles: " + directory);
            string[] labels = ["US-ASCII", "ISO-8859-1", "Windows-1252"];
            string[] encodings = ["Ascii", "Latin1", "Windows1252"];
            string[] scalars = ["abcd\r\n", "\u0080é\r\n", "€—é\r\n"];
            int[] lengths = [6, 4, 5];
            for (var i = 0; i < 3; i++)
            {
                var profile = report.Labels[i];
                Require(profile.Label == labels[i] && profile.Encoding == encodings[i]
                    && profile.InputLength == lengths[i] && profile.Scalars == scalars[i]
                    && profile.WarmCalls == 10000 && profile.MeasuredCalls == 10000
                    && profile.WarmConsumed == 10000L * lengths[i] && profile.MeasuredConsumed == 10000L * lengths[i]
                    && profile.WarmWritten == 10000L * lengths[i] && profile.MeasuredWritten == 10000L * lengths[i]
                    && profile.FirstOffset == 4L + 10000L * lengths[i]
                    && profile.LastOffset == 3L + 20000L * lengths[i], "Encoding child workload/progress mismatch: " + directory);
                Require(mode == "real" ? profile.AllocatedBytes == 0 && profile.EscapedObjectLength == 0
                    : profile.AllocatedBytes == 640000 && profile.EscapedObjectLength == 37,
                    "Encoding child allocation meter/control mismatch: " + directory);
            }
            return report;
        }
    }
}
