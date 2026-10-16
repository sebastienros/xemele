using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Xemele.Validation;
using Xunit;

namespace Xemele.Differential.Tests.Schematron;

public sealed class XPathReferenceTests
{
    private static readonly XNamespace Sch = "http://purl.oclc.org/dsdl/schematron", Svrl = "http://purl.oclc.org/dsdl/svrl";
    public static IEnumerable<object[]> Cases()
    {
        string[] expressions = ["true()", "false()", "@code", "@missing", "count(n:child)=2", "count(n:child)=0", "sum(n:child/@n)>2", "number(@code)>1", "string(@code)", "normalize-space(.)", "contains(., 'A')", "starts-with(@code, '1')", "substring('12345', 1.5, 2.6)", "substring-before('a:b', ':')", "substring-after('a:b', ':')", "translate('abcabc', 'ac', 'X')", "string-length(.)", "floor(-1.2)", "ceiling(-1.2)", "round(-1.5)", "0 div 0", "1 div 0", "position()=1 and last()=1", "name(n:child[1])", "local-name(n:child[1])", "namespace-uri(n:child[1])", "not(n:child)", "n:child/@n = 2", "n:child/@n != 2", "n:child[1]/following-sibling::n:child", "../item[1]/@code = @code", "concat(name(), ':', @code)"];
        string[] documents = ["<root><item code='12'> A <n:child xmlns:n='urn:n' n='1'/><n:child xmlns:n='urn:n' n='2'/> B </item><item code='0'/></root>", "<item code='-3'>𐀀Ω A</item>", "<root><item code='NaN'/><item/><item code='1'>\tB\n</item></root>"];
        return expressions.SelectMany((e, i) => documents.Select((d, j) => new object[] { "bcl-xpath-" + i + "-" + j, e, d }));
    }
    [Theory, MemberData(nameof(Cases))]
    public void DirectM4FindingsMessagesAndSvrlMatchIndependentBclXPath(string id, string expression, string input)
    {
        var manager = new XmlNamespaceManager(new NameTable());
        manager.AddNamespace("n", "urn:n");
        using var reader = XmlReader.Create(new StringReader(input), new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        var reference = new XPathDocument(reader).CreateNavigator();
        var focus = reference.Select("//item", manager);
        var expected = new List<(bool Passed, string Text, string Path)>();
        while (focus.MoveNext())
        {
            var item = focus.Current!;
            // Evaluate on an individual navigator: assertion focus is a singleton.
            var text = (string)item.Evaluate("string(" + expression + ")", manager);
            if (id == "bcl-xpath-16-1")
            {
                // XPath1 section 3.6 counts a supplementary Unicode character
                // once. Preserve this exact BCL UTF16 limitation, never exclude it.
                Assert.Equal("5", text);
                text = "4";
            }
            expected.Add(((bool)item.Evaluate("boolean(" + expression + ")", manager), text, Path(item)));
        }
        var source = new XElement(Sch + "schema", new XElement(Sch + "ns", new XAttribute("prefix", "n"), new XAttribute("uri", "urn:n")),
            new XElement(Sch + "pattern", new XElement(Sch + "rule", new XAttribute("context", "item"),
                Check("assert", "a", "failed:"), Check("report", "r", "report:"))),
            new XElement(Sch + "diagnostics", new XElement(Sch + "diagnostic", new XAttribute("id", "d"), "D:", new XElement(Sch + "value-of", new XAttribute("select", expression)))),
            new XElement(Sch + "properties", new XElement(Sch + "property", new XAttribute("id", "p"), "P:", new XElement(Sch + "value-of", new XAttribute("select", expression)))));
        XElement Check(string kind, string identifier, string prefix) => new(Sch + kind, new XAttribute("test", expression), new XAttribute("id", identifier), new XAttribute("diagnostics", "d"), new XAttribute("properties", "p"), prefix, new XElement(Sch + "value-of", new XAttribute("select", expression)));
        var result = SchematronSchema.Compile(source.ToString(SaveOptions.DisableFormatting)).Validate(input);
        Assert.True(result.Validation.IsComplete, id);
        Assert.Equal(expected.All(e => e.Passed), result.Validation.IsValid);
        Assert.Equal(expected.Count, result.FiredRules.Count);
        Assert.Equal(expected.Count, result.Findings.Count);
        var report = new XElement(Svrl + "schematron-output", new XAttribute("phase", "#ALL"),
            new XElement(Svrl + "ns-prefix-in-attribute-values", new XAttribute("prefix", "n"), new XAttribute("uri", "urn:n")), new XElement(Svrl + "active-pattern"));
        for (var i = 0; i < expected.Count; i++)
        {
            var oracle = expected[i];
            var finding = result.Findings[i];
            Assert.Equal(i, finding.FiredRuleKey);
            Assert.Equal(0, finding.PatternKey);
            Assert.Equal(oracle.Path, finding.Location);
            Assert.Equal(oracle.Path, finding.ContextLocation);
            Assert.Equal(oracle.Passed ? SchematronFindingKind.SuccessfulReport : SchematronFindingKind.FailedAssert, finding.Kind);
            Assert.Equal((oracle.Passed ? "report:" : "failed:") + oracle.Text, finding.Text);
            Assert.Equal("D:" + oracle.Text, Assert.Single(finding.Diagnostics).Text);
            Assert.Equal("P:" + oracle.Text, Assert.Single(finding.Properties).Text);
            report.Add(new XElement(Svrl + "fired-rule", new XAttribute("context", "item")));
            report.Add(new XElement(Svrl + (oracle.Passed ? "successful-report" : "failed-assert"), new XAttribute("test", expression), new XAttribute("location", oracle.Path), new XAttribute("id", oracle.Passed ? "r" : "a"),
                new XElement(Svrl + "text", (oracle.Passed ? "report:" : "failed:") + oracle.Text), new XElement(Svrl + "diagnostic-reference", new XAttribute("diagnostic", "d"), "D:" + oracle.Text), new XElement(Svrl + "property-reference", new XAttribute("property", "p"), "P:" + oracle.Text)));
        }
        var actual = SchematronReport.SerializeSvrl(result, TestContext.Current.CancellationToken);
        Assert.Equal(Canonical(report), Canonical(XElement.Parse(actual)));
        using var output = new MemoryStream();
        SchematronReport.SaveSvrl(result, output, TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(actual), output.ToArray());
    }
    private static string Path(XPathNavigator node)
    {
        var current = node.Clone();
        var parts = new List<string>();
        do
        {
            if (current.NodeType != XPathNodeType.Element)
            {
                continue;
            }

            string local = current.LocalName, uri = current.NamespaceURI;
            var position = 1;
            var sibling = current.Clone();
            while (sibling.MoveToPrevious())
            {
                if (sibling.NodeType == XPathNodeType.Element && sibling.LocalName == local && sibling.NamespaceURI == uri)
                {
                    position++;
                }
            }

            parts.Add("*[local-name()='" + local + "' and namespace-uri()='" + uri + "'][" + position.ToString(CultureInfo.InvariantCulture) + "]");
        } while (current.MoveToParent());
        parts.Reverse();
        return "/" + string.Join('/', parts);
    }
    private static string Canonical(XElement e) => System.Text.Json.JsonSerializer.Serialize(new { name = e.Name.ToString(), attributes = e.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => new { name = a.Name.ToString(), value = a.Value }), content = e.Nodes().Select(n => n is XElement c ? Canonical(c) : ((XText)n).Value) });
}
