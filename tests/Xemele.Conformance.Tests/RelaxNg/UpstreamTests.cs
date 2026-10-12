using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.Validation;
using Xunit;

namespace Xemele.Conformance.Tests.RelaxNg;

public sealed class UpstreamTests
{
    internal static string Root
    {
        get
        {
            for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
                if (File.Exists(Path.Combine(p.FullName, "global.json"))) return p.FullName;
            throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    internal static string Corpus => Path.Combine(Root, "tests/corpora/relaxng");
    public static IEnumerable<object[]> Cases()
    {
        var rows = new List<object[]>();
        foreach (string name in new[] { "jing-inventory.json", "libxml2-inventory.json" })
        { using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, name))); rows.AddRange(inventory.RootElement.GetProperty("cases").EnumerateArray().Select(c => new object[] { c.GetProperty("id").GetString()!, c.GetRawText() })); }
        return rows;
    }
    internal static byte[] Bytes(JsonElement file) => XmlSubset.CorpusTests.VerifiedBytes(Corpus, file.GetProperty("path").GetString()!, file.GetProperty("sha256").GetString()!);
    private sealed class Resolver : XmlResourceResolver
    {
        private readonly Dictionary<Uri, byte[]> resources;
        internal Resolver()
        {
            resources = [];
            foreach (string name in new[] { "jing-inventory.json", "libxml2-inventory.json" })
            { using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, name))); foreach (var f in inventory.RootElement.GetProperty("resources").EnumerateArray()) resources.Add(new Uri(Path.Combine(Corpus, f.GetProperty("path").GetString()!)), Bytes(f)); }
        }
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            return resources.TryGetValue(request.Uri, out var bytes) ? new(new MemoryStream(bytes, writable: false), request.Uri) : null;
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public void OriginalSchemaAndInstancePredicatesAcrossPublicTransports(string id, string json)
    {
        using var record = JsonDocument.Parse(json); var row = record.RootElement;
        byte[] schemaBytes = Bytes(row.GetProperty("schema")); string source = Encoding.UTF8.GetString(schemaBytes);
        var sourceDocument = XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
        string before = sourceDocument.ToXmlString(cancellationToken: TestContext.Current.CancellationToken);
        var options = new SchemaCompileOptions { BaseUri = new(Path.Combine(Corpus, row.GetProperty("schema").GetProperty("path").GetString()!)), Resolver = new Resolver(), CancellationToken = TestContext.Current.CancellationToken };
        var failures = new List<string>();
        for (int compileTransport = 0; compileTransport < 3; compileTransport++)
        {
            // Offset and one-byte reads exercise caller ownership/current-position semantics.
            using var stream = new ShortReadStream([0x78, .. schemaBytes]); stream.Position = 1;
            RelaxNgSchema? schema = null;
            try
            {
                schema = compileTransport switch { 0 => RelaxNgSchema.Compile(source, options), 1 => RelaxNgSchema.Compile(sourceDocument, options), _ => RelaxNgSchema.Compile(stream, options) };
                if (row.GetProperty("schemaExpected").GetString() == "rejected") failures.Add($"{id} compile transport {compileTransport}: accepted authored incorrect schema");
            }
            catch (SchemaCompileException e)
            {
                Assert.NotEmpty(e.Diagnostics); Assert.Contains(e.Diagnostics, d => d.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal);
                if (row.GetProperty("schemaExpected").GetString() == "accepted") failures.Add($"{id} compile transport {compileTransport}: rejected authored correct schema: {string.Join("; ", e.Diagnostics.Select(d => d.Code + ": " + d.Message))}");
            }
            Assert.True(stream.CanRead); Assert.Equal(before, sourceDocument.ToXmlString(cancellationToken: TestContext.Current.CancellationToken));
            if (schema is null || row.GetProperty("schemaExpected").GetString() == "rejected") continue;
            foreach (var instance in row.GetProperty("instances").EnumerateArray())
            {
                byte[] bytes = Bytes(instance); string xml = Encoding.UTF8.GetString(bytes);
                var document = XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken); string original = document.ToXmlString(cancellationToken: TestContext.Current.CancellationToken);
                for (int transport = 0; transport < 3; transport++)
                {
                    using var input = new ShortReadStream([0x78, .. bytes]); input.Position = 1;
                    var validation = new ValidationOptions { CancellationToken = TestContext.Current.CancellationToken };
                    var result = transport switch { 0 => schema.Validate(xml, validation), 1 => schema.Validate(document, validation), _ => schema.Validate(input, validation) };
                    bool expected = instance.GetProperty("expected").GetString() == "valid";
                    if (!result.IsComplete || result.IsValid != expected) failures.Add($"{instance.GetProperty("id").GetString()} compile {compileTransport}/instance {transport}: expected complete {expected}, actual complete {result.IsComplete}/valid {result.IsValid}; {string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message))}");
                    Assert.True(input.CanRead); Assert.Equal(original, document.ToXmlString(cancellationToken: TestContext.Current.CancellationToken));
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    [Fact]
    public void PinnedDiscoveryAndHashesAccountForEveryCandidate()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "jing-inventory.json")));
        var root = inventory.RootElement; Assert.Equal("a6bc0041035988325dfbfe7823ef2c098fc56597", root.GetProperty("revision").GetString());
        var cases = root.GetProperty("cases").EnumerateArray().ToArray(); Assert.Equal(385, cases.Length); Assert.Equal(580, cases.Sum(c => c.GetProperty("instances").GetArrayLength()));
        Assert.Equal(385, cases.Select(c => c.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(0, root.GetProperty("missingCases").GetInt32()); Assert.Equal(0, root.GetProperty("missingResources").GetInt32());
        foreach (var c in cases) { Assert.Equal("applicable", c.GetProperty("classification").GetString()); Assert.NotEmpty(c.GetProperty("reason").GetString()!); Bytes(c.GetProperty("schema")); foreach (var i in c.GetProperty("instances").EnumerateArray()) Bytes(i); }
        foreach (string group in new[] { "resources", "sources" }) foreach (var f in root.GetProperty(group).EnumerateArray()) Assert.Equal(f.GetProperty("bytes").GetInt32(), Bytes(f).Length);
    }
    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    { public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]); }
}
