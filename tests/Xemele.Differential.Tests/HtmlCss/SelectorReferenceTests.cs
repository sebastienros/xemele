using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.XPath;
using Xemele.Css;
using Xemele.Html;
using Xunit;

namespace Xemele.Differential.Tests.HtmlCss;

public sealed class SelectorReferenceTests
{
    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "docs/tasks/M7-corpus.json"))) return d.FullName;
        throw new InvalidOperationException();
    }
    [Theory]
    [InlineData("")]
    [InlineData("tr-TR")]
    [InlineData("fr-FR")]
    public void GeneratedTwoGraphSelectorsMatchIndependentBclXPath(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var corpus = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root(), "tests/corpora/css/bcl-overlap.json")));
            uint state = corpus.RootElement.GetProperty("seed").GetUInt32();
            int Next(int maximum) { state = unchecked(state * 1664525 + 1013904223); return (int)(state % (uint)maximum); }
            for (int tree = 0; tree < corpus.RootElement.GetProperty("trees").GetInt32(); tree++)
            {
                int serial = 0;
                XElement Build(string name, int depth)
                {
                    var element = new XElement(name, new XAttribute("id", "i" + serial++), new XAttribute("class", Next(2) == 0 ? "a\t b" : "b"));
                    if (Next(3) != 0) element.Add(new XAttribute("data", new[] { "en", "en-US", "tail-US", "other" }[Next(4)]));
                    element.Add(new XComment("ignored"), new XProcessingInstruction("p", "ignored"));
                    if (Next(3) == 0) element.Add(new XText(Next(2) == 0 ? " " : "é😀"));
                    if (depth != 0) for (int i = 0, count = 1 + Next(5); i < count; i++) element.Add(Build(Next(3) == 0 ? "other" : "item", depth - 1));
                    return element;
                }
                var reference = Build("root", 3); string source = reference.ToString(SaveOptions.DisableFormatting);
                var xml = Xemele.XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
                var html = Html5Parser.Parse("<body>" + source);
                var htmlRoot = Assert.Single(CssSelector.Compile("root").Select(html.DocumentNode));
                foreach (var vector in corpus.RootElement.GetProperty("vectors").EnumerateArray())
                {
                    var css = CssSelector.Compile(vector.GetProperty("css").GetString()!);
                    string[] Expected() => reference.XPathSelectElements(vector.GetProperty("xpath").GetString()!).Select(n => n.Attribute("id")!.Value).ToArray();
                    var expected = Expected();
                    var xs = css.Select(xml.RootElement); var hs = css.Select(htmlRoot);
                    Assert.Equal(expected, xs.Select(n => n.GetAttribute("id")));
                    Assert.Equal(expected, hs.Select(n => n.GetAttribute("id")));
                    // Query snapshots preserve membership when all three graphs mutate.
                    var re = new XElement("item", new XAttribute("id", "added"), new XAttribute("class", "a b")); reference.Add(re);
                    var xe = xml.CreateElement("item"); xe.SetAttribute("id", "added"); xe.SetAttribute("class", "a b"); xml.RootElement.AppendChild(xe);
                    var he = html.CreateElement("item"); he.SetAttribute(new("id", "added")); he.SetAttribute(new("class", "a b")); htmlRoot.AppendChild(he);
                    Assert.Equal(expected, xs.Select(n => n.GetAttribute("id"))); Assert.Equal(expected, hs.Select(n => n.GetAttribute("id")));
                    Assert.Equal(Expected(), css.Select(xml.RootElement).Select(n => n.GetAttribute("id")));
                    Assert.Equal(Expected(), css.Select(htmlRoot).Select(n => n.GetAttribute("id")));
                    re.Remove(); xe.Remove(); he.Remove();
                }
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
