using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xemele.Validation;
using Xunit;

namespace Xemele.Conformance.Tests.Schematron;

public sealed class ProfileTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(UpstreamTests.Corpus, "profile.json")));
        return data.RootElement.GetProperty("cases").EnumerateArray().Select(c => new object[] { c.GetProperty("id").GetString()!, c.GetRawText() }).ToArray();
    }
    [Theory, MemberData(nameof(Cases))]
    public void FrozenStructuredResultsAndCompleteSvrlMatchIndependentRowsAcrossAllTransports(string id, string raw)
    {
        using var data = JsonDocument.Parse(raw); var row = data.RootElement;
        var resources = row.GetProperty("resources").EnumerateObject().ToDictionary(p => new Uri(new Uri("https://profile.test/"), p.Name), p => p.Value.GetString()!);
        var options = new SchematronCompileOptions { Schema = new() { BaseUri = new("https://profile.test/schema.sch"), Resolver = new Resolver(resources) } };
        string source = row.GetProperty("schema").GetString()!, input = row.GetProperty("document").GetString()!;
        var validation = new SchematronValidationOptions { Phase = row.GetProperty("phase").GetString() };
        using var schemaInput = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var schemas = new[] { SchematronSchema.Compile(source, options), SchematronSchema.Compile(XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken), options), SchematronSchema.Compile(schemaInput, options) };
        Assert.True(schemaInput.CanRead);
        foreach (var schema in schemas)
        {
            using var instanceInput = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var instance = XmlDocument.Parse(input, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var result in new[] { schema.Validate(input, validation), schema.Validate(instance, validation), schema.Validate(instanceInput, validation) })
            {
                AssertResult(row.GetProperty("expected"), result);
                string serialized = SchematronReport.SerializeSvrl(result, TestContext.Current.CancellationToken);
                Assert.Equal(Canonical(row.GetProperty("expectedSvrl").GetString()!), Canonical(serialized));
                using var output = new MemoryStream(); SchematronReport.SaveSvrl(result, output, TestContext.Current.CancellationToken);
                Assert.Equal(Encoding.UTF8.GetBytes(serialized), output.ToArray()); Assert.True(output.CanWrite);
                foreach (var f in result.Findings)
                {
                    if (f.Location.Length == 0) { Assert.Equal(0, f.SourceLocation.LineNumber); Assert.Null(f.SourceLocation.ByteOffset); }
                    else Assert.True(f.SourceLocation.LineNumber >= 1, id);
                    Assert.True(f.ContextSourceLocation.LineNumber >= 1, id);
                }
            }
            Assert.True(instanceInput.CanRead);
            var detached = schema.Validate(instance, validation);
            string before = SchematronReport.SerializeSvrl(detached, TestContext.Current.CancellationToken);
            instance.RootElement.SetAttribute("changed", "after");
            Assert.Equal(before, SchematronReport.SerializeSvrl(detached, TestContext.Current.CancellationToken));
        }
    }
    private static void AssertResult(JsonElement expected, SchematronResult result)
    {
        var actual = new
        {
            phase = result.Phase, title = result.Title, namespaces = result.Namespaces,
            activePatterns = result.ActivePatterns.Select(p => new { key = p.Key, id = p.Id, name = p.Name }),
            firedRules = result.FiredRules.Select(r => new { key = r.Key, patternKey = r.PatternKey, ruleKey = r.RuleKey, context = r.Context, location = r.Location }),
            findings = result.Findings.Select(f => new { kind = f.Kind.ToString(), id = f.Id, role = f.Role, flag = f.Flag, patternKey = f.PatternKey, firedRuleKey = f.FiredRuleKey, patternId = f.PatternId, context = f.RuleContext, test = f.Test, location = f.Location, contextLocation = f.ContextLocation, text = f.Text, diagnostics = f.Diagnostics.Select(d => new { id = d.Id, text = d.Text }), properties = f.Properties.Select(p => new { id = p.Id, text = p.Text }) }),
            isValid = result.Validation.IsValid, isComplete = result.Validation.IsComplete
        };
        var oracle = JsonNode.Parse(expected.GetRawText())!;
        if (oracle["fatalCode"] is { } code) { Assert.Contains(result.Validation.Diagnostics, d => d.Code == code.GetValue<string>()); oracle.AsObject().Remove("fatalCode"); }
        var observed = JsonSerializer.SerializeToNode(actual);
        Assert.True(JsonNode.DeepEquals(oracle, observed), "expected " + oracle + " actual " + observed);
        Assert.Equal(result.Findings.Count(f => f.Kind == SchematronFindingKind.FailedAssert), result.Validation.Diagnostics.Count(d => d.Code == "SCH.Assert"));
        Assert.Equal(result.Findings.Count(f => f.Kind == SchematronFindingKind.SuccessfulReport), result.Validation.Diagnostics.Count(d => d.Code == "SCH.Report"));
    }
    // XML Infoset comparison preserves every element, attribute and text value;
    // only prefix spelling and attribute order are canonicalized.
    internal static string Canonical(string xml)
    {
        string Walk(XElement e) => JsonSerializer.Serialize(new { name = e.Name.ToString(), attributes = e.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => new { name = a.Name.ToString(), value = a.Value }), content = e.Nodes().Select(n => n is XElement child ? Walk(child) : n is XText t ? t.Value : throw new InvalidDataException("Unexpected report node.")) });
        return Walk(XElement.Parse(xml));
    }
    [Theory]
    [InlineData("xslt2")][InlineData("xslt3")][InlineData("xpath")][InlineData("xpath2")][InlineData("xpath3")][InlineData("XPath1")][InlineData("XSLT")][InlineData(" xslt")][InlineData("xslt ")][InlineData("")][InlineData("exslt")]
    public void EveryAlternateBindingIsAnExplicitCompileError(string binding)
    {
        string source = "<schema xmlns='http://purl.oclc.org/dsdl/schematron' queryBinding='" + binding + "'><pattern><rule context='item'><assert test='true()'/></rule></pattern></schema>";
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(source)).Diagnostics, d => d.Code == "SCH.QueryBinding");
    }
    [Theory]
    [InlineData("<pattern id='x'/><pattern id='x'/>", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='item'><extends rule='missing'/></rule></pattern>", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='item'><assert test='true()' diagnostics='missing'/></rule></pattern>", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='item'><assert test='true()' properties='missing'/></rule></pattern>", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='item'><let name='x' value='$y'/><let name='y' value='$x'/><assert test='true()'/></rule></pattern>", "SCH.XPath")]
    [InlineData("<pattern><rule context='item'><assert test='document(&quot;x&quot;)'/></rule></pattern>", "SCH.XPath")]
    [InlineData("<pattern><rule context='key(&quot;k&quot;,&quot;v&quot;)'><assert test='true()'/></rule></pattern>", "SCH.XPath")]
    [InlineData("<phase id='p'><active pattern='missing'/></phase><pattern/>", "SCH.Phase")]
    public void NegativeSchemasRetainExplicitDiagnostics(string body, string code)
    {
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile("<schema xmlns='http://purl.oclc.org/dsdl/schematron'>" + body + "</schema>")).Diagnostics, d => d.Code == code);
    }
    private sealed class Resolver(Dictionary<Uri, string> resources) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(XmlResourceKind.Schema, request.Kind); var uri = new Uri(request.Referrer!, request.SystemId!);
            return resources.TryGetValue(uri, out string? text) ? new(new MemoryStream(Encoding.UTF8.GetBytes(text)), uri) : null;
        }
    }
}
