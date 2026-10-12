using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.Validation;
using Xunit;

namespace Xemele.Differential.Tests.RelaxNg;

public sealed class ReferenceTests
{
    private static string Root
    {
        get { for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent) if (File.Exists(Path.Combine(p.FullName, "global.json"))) return p.FullName; throw new InvalidOperationException("Repository root unavailable."); }
    }
    private static string Corpus => Path.Combine(Root, "tests/corpora/relaxng");
    private static byte[] Verified(JsonElement f)
    {
        string relative = f.GetProperty("path").GetString()!;
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Contains("..")) throw new InvalidDataException("Unsafe fixture path.");
        string path = Path.GetFullPath(Path.Combine(Corpus, relative));
        if (!path.StartsWith(Corpus + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Fixture outside corpus.");
        for (FileSystemInfo? item = new FileInfo(path); item is not null; item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent) { if (item.LinkTarget is not null) throw new InvalidDataException("Symbolic fixture."); if (item.FullName == Corpus) break; }
        byte[] bytes = File.ReadAllBytes(path); Assert.Equal(f.GetProperty("sha256").GetString(), Hash(bytes)); return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Resolver(Dictionary<Uri, byte[]> resources) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Assert.Equal(XmlResourceKind.Schema, request.Kind); return resources.TryGetValue(request.Uri, out var bytes) ? new(new MemoryStream(bytes, writable: false), request.Uri) : null; }
    }
    [Theory]
    [InlineData("jing")]
    [InlineData("libxml2")]
    public async Task RealPinnedReferencesKeepIndependentSchemaAndInstanceOracles(string engine)
    {
        var inventories = new[] { "jing-inventory.json", "libxml2-inventory.json" }.Select(name => JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, name)))).ToArray();
        string directory = Path.Combine(Root, "artifacts/M8-rng-corpus/reference-" + engine); Directory.CreateDirectory(directory);
        try
        {
            var cases = inventories.SelectMany(i => i.RootElement.GetProperty("cases").EnumerateArray()).ToArray();
            var resources = inventories.SelectMany(i => i.RootElement.GetProperty("resources").EnumerateArray()).ToDictionary(f => new Uri(Path.Combine(Corpus, f.GetProperty("path").GetString()!)), Verified);
            var expectations = new Dictionary<(string Id, string Phase), string>();
            var fixtures = new Dictionary<(string Id, string Phase), JsonElement>();
            var requests = new List<string>();
            foreach (var c in cases)
            {
                string id = c.GetProperty("id").GetString()!; var schema = c.GetProperty("schema"); Verified(schema);
                expectations.Add((id, "schema"), c.GetProperty("schemaExpected").GetString()!); fixtures.Add((id, "schema"), schema);
                var paths = new List<string> { id, Path.Combine(Corpus, schema.GetProperty("path").GetString()!) };
                int j = 0;
                foreach (var i in c.GetProperty("instances").EnumerateArray()) { Verified(i); expectations.Add((id, j.ToString(System.Globalization.CultureInfo.InvariantCulture)), i.GetProperty("expected").GetString()!); fixtures.Add((id, (j++).ToString(System.Globalization.CultureInfo.InvariantCulture)), i); paths.Add(Path.Combine(Corpus, i.GetProperty("path").GetString()!)); }
                requests.Add(string.Join('\t', paths));
            }
            string input = Path.Combine(directory, "requests.tsv"); await File.WriteAllLinesAsync(input, requests, TestContext.Current.CancellationToken);
            using var identities = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "docs/reports/M8-rng-corpus/reference-build.json")));
            var identity = identities.RootElement.GetProperty(engine);
            var start = new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            if (engine == "jing")
            {
                string jar = identity.GetProperty("jarPath").GetString()!; Assert.Equal(identity.GetProperty("jarSha256").GetString(), Hash(File.ReadAllBytes(jar)));
                string classes = identity.GetProperty("classPath").GetString()!; Assert.Equal(identity.GetProperty("classSha256").GetString(), Hash(File.ReadAllBytes(Path.Combine(classes, "RngReference.class"))));
                start.FileName = "/Library/Java/JavaVirtualMachines/temurin-25.jdk/Contents/Home/bin/java";
                start.ArgumentList.Add("-cp"); start.ArgumentList.Add(classes + Path.PathSeparator + jar); start.ArgumentList.Add("RngReference");
            }
            else
            { start.FileName = identity.GetProperty("binaryPath").GetString()!; Assert.Equal(identity.GetProperty("binarySha256").GetString(), Hash(File.ReadAllBytes(start.FileName))); }
            start.ArgumentList.Add(input);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Reference process unavailable.");
            string output, errors;
            try { var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token); var stderr = process.StandardError.ReadToEndAsync(timeout.Token); await process.WaitForExitAsync(timeout.Token); output = await stdout; errors = await stderr; }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); throw; }
            Assert.True(process.ExitCode == 0, "Reference infrastructure failure: " + errors);
            var observed = new Dictionary<(string Id, string Phase), string>();
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) { string[] columns = line.TrimEnd('\r').Split('\t'); Assert.Equal(3, columns.Length); Assert.True(observed.TryAdd((columns[0], columns[1]), columns[2]), "Duplicate reference result"); }
            Assert.Equal(expectations.Count, observed.Count);
            using var ledger = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "reference-limitations.json")));
            var limits = ledger.RootElement.GetProperty("cases").EnumerateArray().Where(l => l.GetProperty("engine").GetString() == engine).ToDictionary(l => (l.GetProperty("caseId").GetString()!, l.GetProperty("phase").GetString()!));
            int agreements = 0; var failures = new List<string>();
            foreach (var (key, expected) in expectations)
            {
                Assert.True(observed.TryGetValue(key, out var actual), "Missing reference case " + key);
                if (limits.TryGetValue(key, out var limitation))
                {
                    Assert.Equal(expected, limitation.GetProperty("authoritativeExpected").GetString()); Assert.Equal(actual, limitation.GetProperty("referenceObserved").GetString());
                    Assert.Equal(fixtures[key].GetProperty("sha256").GetString(), limitation.GetProperty("sha256").GetString());
                    Assert.Equal(identity.GetProperty("revision").GetString(), limitation.GetProperty("revision").GetString()); Assert.Equal(identity.GetProperty("version").GetString(), limitation.GetProperty("version").GetString());
                    Assert.Equal("reference-limitation-not-agreement", limitation.GetProperty("classification").GetString()); Assert.NotEqual(expected, actual);
                }
                else if (actual != expected) failures.Add($"{engine} unexplained {key}: expected {expected}, observed {actual}");
                else agreements++;
            }
            Assert.Equal(engine == "jing" ? 0 : 12, limits.Count); Assert.Equal(expectations.Count - limits.Count, agreements);
            // Product is always checked against the independent upstream expectation,
            // including fixtures where libxml2 has a documented limitation.
            foreach (var c in cases)
            {
                string id = c.GetProperty("id").GetString()!; string source = Encoding.UTF8.GetString(Verified(c.GetProperty("schema")));
                var options = new SchemaCompileOptions { BaseUri = new(Path.Combine(Corpus, c.GetProperty("schema").GetProperty("path").GetString()!)), Resolver = new Resolver(resources), CancellationToken = TestContext.Current.CancellationToken };
                RelaxNgSchema? product = null; string outcome;
                try { product = RelaxNgSchema.Compile(source, options); outcome = "accepted"; }
                catch (SchemaCompileException e) { Assert.NotEmpty(e.Diagnostics); outcome = "rejected"; }
                if (outcome != expectations[(id, "schema")]) failures.Add($"product {id} schema: expected {expectations[(id, "schema")]}, observed {outcome}");
                if (product is null) continue;
                int j = 0;
                foreach (var i in c.GetProperty("instances").EnumerateArray())
                {
                    var result = product.Validate(Encoding.UTF8.GetString(Verified(i)), new() { CancellationToken = TestContext.Current.CancellationToken });
                    string actual = result.IsComplete ? result.IsValid ? "valid" : "invalid" : "incomplete"; string phase = (j++).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (actual != expectations[(id, phase)]) failures.Add($"product {id} instance {phase}: expected {expectations[(id, phase)]}, observed {actual}");
                }
            }
            Assert.True(failures.Count == 0, string.Join('\n', failures));
        }
        finally { foreach (var inventory in inventories) inventory.Dispose(); Directory.Delete(directory, recursive: true); }
    }
}
