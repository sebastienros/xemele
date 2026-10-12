using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using Xemele.Validation;
using Xunit;

namespace Xemele.Conformance.Tests.Schematron;

public sealed class UpstreamTests
{
    private static readonly XNamespace Suite = "tag:dmaus@dmaus.name,2019:Schematron:Testsuite";
    private static readonly XNamespace Sch = "http://purl.oclc.org/dsdl/schematron";
    internal static string Root
    {
        get
        {
            for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
                if (File.Exists(Path.Combine(p.FullName, "global.json"))) return p.FullName;
            throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    internal static string Corpus => Path.Combine(Root, "tests/corpora/schematron");
    public static IEnumerable<object[]> Cases()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "inventory.json")));
        return inventory.RootElement.GetProperty("cases").EnumerateArray().Select(c => new object[] { c.GetProperty("id").GetString()!, c.GetProperty("path").GetString()!, c.GetProperty("sha256").GetString()! }).ToArray();
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOriginalCaseExecutesWithVisibleFrozenProfileDifferences(string id, string path, string digest)
    {
        byte[] bytes = XmlSubset.CorpusTests.VerifiedBytes(Corpus, path, digest);
        Assert.Equal(digest, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var root = XElement.Parse(Encoding.UTF8.GetString(bytes), LoadOptions.PreserveWhitespace);
        var schemas = root.Element(Suite + "schemas")!;
        var documents = root.Element(Suite + "documents")!;
        var primary = documents.Element(Suite + "primary")!.Elements().Single();
        var resources = documents.Elements(Suite + "secondary").ToDictionary(d => new Uri(new Uri("https://corpus.test/"), (string)d.Attribute("filename")!), d => d.Elements().Single().ToString(SaveOptions.DisableFormatting));
        var options = new SchematronCompileOptions { Schema = new() { BaseUri = new("https://corpus.test/schema.sch"), Resolver = new Resolver(resources) } };
        foreach (var source in schemas.Elements(Sch + "schema"))
        {
            string xml = source.ToString(SaveOptions.DisableFormatting);
            // Each difference is preserved in applicability.json and is asserted,
            // rather than treating a different oracle as an exclusion or pass.
            bool unsupported = source.Descendants(Sch + "pattern").Any(p => p.Attribute("documents") is not null) || source.Descendants(Sch + "let").Any(l => l.HasElements) || source.Descendants().Any(e => e.Name.NamespaceName == "http://www.w3.org/1999/XSL/Transform");
            bool scopedVariableError = id == "let-pattern-global-01";
            bool upstreamError = (string?)root.Attribute("expect") == "error" && id is not ("let-name-collision-error-05" or "let-name-collision-error-06");
            if (unsupported || scopedVariableError || upstreamError)
            {
                var error = Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(xml, options));
                Assert.NotEmpty(error.Diagnostics);
                Assert.Contains(error.Diagnostics, d => d.Code == (unsupported ? "SCH.QueryBinding" : scopedVariableError || id.StartsWith("let-reference-", StringComparison.Ordinal) ? "SCH.XPath" : "SCH.SchemaRestriction"));
                continue;
            }
            var compiled = SchematronSchema.Compile(xml, options);
            var result = compiled.Validate(primary.ToString(SaveOptions.DisableFormatting), new() { Phase = (string?)schemas.Attribute("phase") });
            Assert.True(result.Validation.IsComplete, id);
            bool hasFindings = (string?)root.Attribute("expect") == "invalid";
            Assert.Equal(hasFindings, result.Findings.Count != 0);
            Assert.Equal(!result.Findings.Any(f => f.Kind == SchematronFindingKind.FailedAssert), result.Validation.IsValid);
            string svrl = SchematronReport.SerializeSvrl(result, TestContext.Current.CancellationToken);
            using var output = new MemoryStream();
            SchematronReport.SaveSvrl(result, output, TestContext.Current.CancellationToken);
            Assert.Equal(Encoding.UTF8.GetBytes(svrl), output.ToArray()); Assert.True(output.CanWrite);
            var report = System.Xml.Linq.XDocument.Parse(svrl);
            var manager = new System.Xml.XmlNamespaceManager(new System.Xml.NameTable()); manager.AddNamespace("svrl", "http://purl.oclc.org/dsdl/svrl");
            foreach (var expectation in root.Element(Suite + "expectations")?.Elements() ?? [])
            {
                string expression = (string)expectation.Attribute("test")!;
                bool expected = id is not ("svrl-name-path-01" or "svrl-diagnostic-02");
                Assert.Equal(expected, (bool)report.XPathEvaluate(expression, manager));
            }
            if (id == "svrl-name-path-01") Assert.Equal("attribute", Assert.Single(result.Findings).Text.Trim());
            if (id == "svrl-diagnostic-02") Assert.Equal("Context:", Assert.Single(Assert.Single(result.Findings).Diagnostics).Text.Trim());
        }
    }
    [Fact]
    public void CompletePinnedDiscoveryHasNoMissingOriginalCasesOrVariants()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "inventory.json")));
        var cases = inventory.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(50, cases.Length); Assert.Equal(51, cases.Sum(c => c.GetProperty("schemaVariants").GetInt32()));
        Assert.Equal(50, cases.Select(c => c.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(cases.Select(c => c.GetProperty("path").GetString()).Order(), Directory.GetFiles(Path.Combine(Corpus, "upstream"), "*.xml", SearchOption.AllDirectories).Where(p => !p.EndsWith("pom.xml", StringComparison.Ordinal)).Select(p => Path.GetRelativePath(Corpus, p)).Order());
        using var ledger = JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "applicability.json")));
        Assert.Equal(cases.Select(c => c.GetProperty("id").GetString()).Order(), ledger.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("id").GetString()).Order());
        foreach (var c in ledger.RootElement.GetProperty("cases").EnumerateArray()) Assert.NotEmpty(c.GetProperty("reason").GetString()!);
    }
    private sealed class Resolver(Dictionary<Uri, string> resources) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(XmlResourceKind.Schema, request.Kind);
            Uri uri = new(request.Referrer!, request.SystemId!);
            return resources.TryGetValue(uri, out string? content) ? new(new MemoryStream(Encoding.UTF8.GetBytes(content)), uri) : null;
        }
    }
}
