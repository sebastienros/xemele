using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Xemele.CorpusInfrastructure;

namespace Xemele.Build;

internal static class Program
{
    internal static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Xemele.slnx")))
                    return directory.FullName;
            }
            throw new InvalidOperationException("Build tools require the repository checkout.");
        }
    }
    internal static string Configuration => new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["prepare", var repository])
            {
                await CorpusData.PrepareAsync(Path.GetFullPath(repository));
                return 0;
            }
            switch (args.SingleOrDefault())
            {
                case "prepare":
                    await CorpusData.PrepareAsync(Root);
                    break;
                case "verify":
                    await VerifyAsync();
                    break;
                case "test":
                    await TestAsync();
                    break;
                default:
                    Console.Error.WriteLine("Usage: dotnet run --project tools/Build -- prepare|verify|test");
                    return 2;
            }
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException or
            JsonException or HttpRequestException or OperationCanceledException)
        {
            Console.Error.WriteLine("Build validation failed: " + error);
            return 1;
        }
    }

    internal static async Task<string> RunAsync(IEnumerable<string> arguments, int expected = 0, bool quiet = false)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start dotnet.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        var text = await output + await errors;
        if (!quiet || process.ExitCode != expected)
            Console.Write(text);
        if (process.ExitCode != expected)
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)}: expected exit {expected}, got {process.ExitCode}.");
        return text;
    }

    private static async Task VerifyAsync()
    {
        var entries = ApiFragments.Read(Root);
        var manifests = CorpusManifest.Discover(Root);
        var count = 0;
        foreach (var manifest in manifests)
        {
            if (manifest.Id == "xemele-bootstrap-xml")
            {
                if (manifest.Revision != "sha256:" + manifest.Sha256 || manifest.License != "MIT" ||
                    !File.ReadAllText(Path.Combine(manifest.Directory, "NOTICE.txt")).Contains("MIT License", StringComparison.Ordinal))
                    throw new InvalidDataException("Bootstrap corpus provenance mismatch.");
                new CorpusProvisioner(Root, Path.Combine(Root, "artifacts/test-data/bootstrap")).ImportSource(manifest);
            }
            foreach (var fixture in manifest.Fixtures)
            {
                var path = ContainedPath.Relative(Root, manifest.Directory, fixture.Path);
                if (CorpusData.Hash(path) != fixture.Sha256)
                    throw new InvalidDataException("Corpus checksum mismatch: " + path);
                if (manifest.Id == "xemele-bootstrap-xml")
                {
                    var accepted = true;
                    try
                    {
                        using var reader = System.Xml.XmlReader.Create(path);
                        while (reader.Read())
                        { }
                    }
                    catch (System.Xml.XmlException)
                    {
                        accepted = false;
                    }
                    if (fixture.Classification != (accepted ? "accepted" : "rejected"))
                        throw new InvalidDataException("Independent bootstrap classification mismatch: " + path);
                }
                count++;
            }
        }
        Console.WriteLine($"Validated {entries.Count} API members, {manifests.Count} manifests, {count} fixture hashes.");
        var output = Path.Combine(Root, "artifacts/api");
        Directory.CreateDirectory(output);
        var temporary = Path.Combine(output, Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(temporary, ApiFragments.Compose(entries), new UTF8Encoding(false));
            var checker = Path.Combine(Root, $"tools/ApiBaseline/Checker/bin/{Configuration}/net10.0/ApiCheck.dll");
            var assembly = Path.Combine(Root, $"src/Xemele/bin/{Configuration}/net10.0/Xemele.dll");
            var result = await RunAsync([checker, "compare", assembly, temporary]);
            await File.WriteAllTextAsync(Path.Combine(output, "runtime-api.json"), JsonSerializer.Serialize(new
            {
                matched = true,
                assembly,
                assemblySha256 = CorpusData.Hash(assembly),
                checkerOutput = result.Trim(),
                provenance = entries
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            foreach (var stage in new[] { "M1", "M2", "ApiProbe" })
            {
                var name = stage == "ApiProbe" ? stage : "Foundation" + stage;
                var fixture = Path.Combine(Root, $"tests/Contracts/Foundation/Fixtures/{stage}/bin/{Configuration}/net10.0/{name}.dll");
                await RunAsync([checker, "compare", fixture, Path.Combine(Root, $"tests/Contracts/Fixtures/foundation/{stage}.golden.txt")]);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static async Task TestAsync()
    {
        var projects = new[] { "Xemele.Tests", "Xemele.Conformance.Tests", "Xemele.Differential.Tests" };
        const string method = "RunnerExecutesAndPropagatesControlledFailure";
        var directory = Path.Combine(Root, "artifacts/test-results", "mtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string[] Command(string selector, string path) =>
            ["test", selector, path, "-c", Configuration, "--no-build", "--no-restore", "--no-ansi", "--no-progress",
                "--parallel", "none", "--culture", "invariant", "--max-parallel-test-modules", "1"];
        foreach (var project in projects)
        {
            var discovery = await RunAsync([.. Command("--project", $"tests/{project}/{project}.csproj"), "--list-tests"], quiet: true);
            if (!discovery.Contains($"{project}.Infrastructure.MtpRunnerSmokeTests.{method}", StringComparison.Ordinal))
                throw new InvalidDataException("Smoke test missing from discovery: " + project);
        }
        var success = Path.Combine(directory, "success");
        await RunAsync([.. Command("--solution", "Xemele.slnx"), "--minimum-expected-tests", "1",
            "--report-xunit-trx", "--report-xunit-xml", "--results-directory", success]);
        static XElement[] Tests(string path) => Directory.GetFiles(path, "*.xunit.xml")
            .SelectMany(file => XDocument.Load(file).Descendants("test")).ToArray();
        var executed = Tests(success);
        foreach (var project in projects)
        {
            var smoke = executed.Where(test => (string?)test.Attribute("name") == $"{project}.Infrastructure.MtpRunnerSmokeTests.{method}").ToArray();
            if (smoke.Length != 1 || (string?)smoke[0].Attribute("result") != "Pass")
                throw new InvalidDataException("Smoke test did not pass: " + project);
            var failure = Path.Combine(directory, "controlled-failure-" + project);
            await RunAsync([.. Command("--project", $"tests/{project}/{project}.csproj"),
                "--filter-class", $"{project}.Infrastructure.MtpRunnerSmokeTests", "--environment", "XEMELE_MTP_SMOKE_FAIL=1",
                "--report-xunit-xml", "--results-directory", failure], expected: 2);
            var failed = Tests(failure);
            if (failed.Length != 1 || (string?)failed[0].Attribute("result") != "Fail" ||
                !failed[0].Value.Contains("Intentional infrastructure failure", StringComparison.Ordinal))
                throw new InvalidDataException("Controlled assertion failure missing: " + project);
        }
        var infrastructure = executed.Count(test => test.Descendants("trait").Any(trait =>
            (string?)trait.Attribute("name") == "Category" && (string?)trait.Attribute("value") == "Infrastructure"));
        var summary = $"Tests: {executed.Length}; infrastructure: {infrastructure}; product: {executed.Length - infrastructure}.\n" +
            "All three controlled assertion failures propagated exit 2. Infrastructure success is not product conformance evidence.\n";
        await File.WriteAllTextAsync(Path.Combine(directory, "runner-verification.txt"), summary);
        Console.Write(summary);
    }
}
