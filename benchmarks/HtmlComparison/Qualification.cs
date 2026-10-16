using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Xemele.Css;
using Xemele.Html;
using AngleSharp.Html.Parser;
using XDocument = Xemele.Html.HtmlDocument;

namespace HtmlComparison;

public static class Qualification
{
    public static void Equal(string expected, string actual, string label)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{label} mismatch. AngleSharp: {expected}\nXemele: {actual}");
    }
    private static string Quirks(IDocument d) => ((AngleSharp.Html.Construction.IConstructableDocument)d).QuirksMode switch
    {
        QuirksMode.Off => "NoQuirks",
        QuirksMode.Limited => "LimitedQuirks",
        _ => "Quirks"
    };
    public static void Document(string input)
    {
        var a = Fixtures.Parser().ParseDocument(input);
        var x = Html5Parser.Parse(input);
        Equal(Snapshot(a), Snapshot(x.DocumentNode), "DOM");
        Equal(Quirks(a), x.QuirksMode.ToString(), "quirks");
    }
    public static void Stream(byte[] input)
    {
        using var aStream = new MemoryStream(input, false);
        using var xStream = new MemoryStream(input, false);
        var a = Fixtures.Parser().ParseDocument(aStream);
        var x = Html5Parser.Load(xStream);
        Equal(Snapshot(a), Snapshot(x.DocumentNode), "stream DOM");
        Equal(Quirks(a), x.QuirksMode.ToString(), "stream quirks");
    }
    public static void Query(IDocument a, XDocument x, string selector)
    {
        Equal(JsonSerializer.Serialize(a.QuerySelectorAll(selector).Select(e => Snapshot(e))),
            JsonSerializer.Serialize(CssSelector.Compile(selector).Select(x.DocumentNode).Select(Snapshot)), "query order/content");
    }
    public static void Fragment(string input, string context)
    {
        var p = Fixtures.Parser();
        var ac = p.ParseDocument("<!doctype html>").CreateElement(context);
        var xc = new XDocument().CreateElement(context);
        Equal(JsonSerializer.Serialize(p.ParseFragment(input, ac).Select(Snapshot)),
            JsonSerializer.Serialize(Html5Parser.ParseFragment(input, new(xc)).Children.Select(Snapshot)), "fragment DOM");
    }
    // Structural JSON compares namespace/name/value, sorted expanded attributes,
    // ordered children, doctype identifiers and the separate template content edge.
    // It does not compare internal representation, source positions or force-quirks flags.
    public static string Snapshot(HtmlNode n) => JsonSerializer.Serialize(XShape(n));
    public static string Snapshot(INode n) => JsonSerializer.Serialize(AShape(n));
    private static object XShape(HtmlNode n) => new object?[]
    {
        n.Kind.ToString(), n.NamespaceUri, n.LocalName, n.Value,
        n.Attributes.OrderBy(a => a.NamespaceUri, StringComparer.Ordinal).ThenBy(a => a.LocalName, StringComparer.Ordinal)
            .Select(a => new[] { a.NamespaceUri, a.LocalName, a.Prefix, a.Value }).ToArray(),
        n.Doctype is {} d ? new[] { d.Name, d.PublicIdentifier ?? "", d.SystemIdentifier ?? "" } : null,
        n.Children.Select(XShape).ToArray(), n.TemplateContent is {} t ? XShape(t) : null
    };
    private static object AShape(INode n) => new object?[]
    {
        n.NodeType switch { NodeType.Document => "Document", NodeType.DocumentFragment => "Fragment", NodeType.Element => "Element", NodeType.Text => "Text", NodeType.Comment => "Comment", NodeType.DocumentType => "DocumentType", NodeType.ProcessingInstruction => "ProcessingInstruction", _ => throw new InvalidOperationException(n.NodeType.ToString()) },
        n is IElement e ? e.NamespaceUri ?? "" : "",
        n is IElement el ? el.LocalName : n is IDocumentType dt ? dt.Name : n is IProcessingInstruction pi ? pi.Target : "",
        n is ICharacterData c ? c.Data : "",
        n is IElement elem ? elem.Attributes.OrderBy(a => a.NamespaceUri ?? "", StringComparer.Ordinal).ThenBy(a => a.LocalName, StringComparer.Ordinal)
            .Select(a => new[] { a.NamespaceUri ?? "", a.LocalName, a.Prefix ?? "", a.Value }).ToArray() : Array.Empty<string[]>(),
        n is IDocumentType d ? new[] { d.Name, d.PublicIdentifier, d.SystemIdentifier } : null,
        n.ChildNodes.Select(AShape).ToArray(), n is IHtmlTemplateElement t ? AShape(t.Content) : null
    };
    public static void Run()
    {
        int failures = 0, total = 0;
        void Check(string name, Action action)
        {
            total++;
            try
            {
                action();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
        }
        foreach (string name in Fixtures.Names)
        {
            string input = Fixtures.Document(name);
            Console.WriteLine($"FIXTURE {name}: UTF16={input.Length}; UTF8={Encoding.UTF8.GetByteCount(input)}; SHA256={Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))}");
            Check(name + " document", () => Document(input));
            Check(name + " stream", () => Stream(Encoding.UTF8.GetBytes(input)));
            Check(name + " serialize", () => Equal(Fixtures.Parser().ParseDocument(input).ToHtml(), HtmlSerializer.Serialize(Html5Parser.Parse(input)), name));
            Check(name + " query", () => Query(Fixtures.Parser().ParseDocument(input), Html5Parser.Parse(input), Fixtures.Selector));
        }
        foreach (string name in new[] { "Table", "Textarea", "Template" })
        {
            var f = Fixtures.Fragment(name);
            Check(name + " fragment", () => Fragment(f.Input, f.Context));
        }
        (string Name, string Input)[] probes =
        [
            ("implied structure", "<p>Hello"),
            ("adoption agency", "<p><b>1<i>2</b>3</i>4"),
            ("foster parenting", "<table>A<tr><td>B</table>C"),
            ("duplicate attributes", "<DIV ID=first id=second>x</DIV>"),
            ("named numeric references", "<p>&NotEqualTilde; &notit; &#x80; &#0;"),
            ("null states", "<p>a\0b<textarea>a\0b</textarea><!--a\0b-->"),
            ("raw script", "<script>if (a < b) { x = '&amp;'; }</script>"),
            ("noscript disabled", "<noscript><p>shown</p></noscript>"),
            ("svg math integration", "<svg viewBox='0 0 1 1'><foreignObject><p>x</p></foreignObject></svg><math><mtext><b>y</b></mtext></math>"),
            ("template edge", "<template><table><tr><td>x</table></template>"),
            ("limited quirks", "<!DOCTYPE html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN' 'x'><p>x"),
            ("processing instruction default", "<?target data?><p>x"),
            ("selectedcontent", "<!doctype html><select><button><selectedcontent></selectedcontent></button><option>A</option></select>")
        ];
        foreach (var p in probes)
            Check("probe " + p.Name, () =>
            {
                if (p.Name is "processing instruction default" or "selectedcontent")
                {
                    if (Snapshot(Fixtures.Parser().ParseDocument(p.Input)) == Snapshot(Html5Parser.Parse(p.Input).DocumentNode))
                        throw new InvalidOperationException("Expected documented difference disappeared; review report.");
                    Console.WriteLine("DIFFERENCE " + p.Name);
                }
                else
                    Document(p.Input);
            });
        Check("processing instruction opt-in difference", () =>
        {
            const string input = "<?target data?><p>x";
            var a = new AngleSharp.Html.Parser.HtmlParser(new HtmlParserOptions { IsSupportingProcessingInstructions = true, IsScripting = false }).ParseDocument(input);
            if (Snapshot(a) == Snapshot(Html5Parser.Parse(input).DocumentNode))
                throw new InvalidOperationException("Expected PI opt-in difference disappeared; review report.");
            Console.WriteLine("DIFFERENCE PI opt-in target/data");
        });
        Check("undeclared UTF8 stream default difference", () =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes("<!doctype html><p>尾😀");
            using var a = new MemoryStream(bytes);
            using var x = new MemoryStream(bytes);
            if (Snapshot(Fixtures.Parser().ParseDocument(a)) == Snapshot(Html5Parser.Load(x).DocumentNode))
                throw new InvalidOperationException("Expected encoding difference disappeared; review report.");
            Console.WriteLine("DIFFERENCE undeclared UTF8 stream default");
        });
        string cssInput = "<!doctype html><main><article class=entry data-id=0><h2>A</h2><p>B</p></article><article class=entry data-id=1><h2>C</h2></article></main>";
        foreach (string selector in new[] { "*", "article.entry", "[data-id]", "article > h2", "h2 + p", "h2 ~ p", "article:first-child", "article:nth-child(2n+1)", "article:not([data-id='1'])", ":root", "main:has(p)", ":is(h2,p)", "[class='ENTRY' i]" })
            Check("selector " + selector, () =>
            {
                if (selector is "main:has(p)" or ":is(h2,p)" or "[class='ENTRY' i]")
                {
                    if (Fixtures.Parser().ParseDocument(cssInput).QuerySelectorAll(selector).Length == 0)
                        throw new InvalidOperationException("AngleSharp feature probe matched nothing.");
                    try
                    {
                        CssSelector.Compile(selector);
                    }
                    catch (CssSelectorException) { Console.WriteLine("DIFFERENCE Xemele rejects " + selector); return; }
                    throw new InvalidOperationException("Expected selector rejection disappeared; review report.");
                }
                Query(Fixtures.Parser().ParseDocument(cssInput), Html5Parser.Parse(cssInput), selector);
            });
        Console.WriteLine($"QUALIFICATION {total - failures}/{total} checks passed; {failures} unexpected failures; 7 documented differences. Focused probes are not a full HTML5 conformance suite.");
        if (failures != 0)
            throw new InvalidOperationException("Qualification failed.");
        // Known feature differences are recorded by the probe runner. Benchmark setup
        // independently requires exact equivalence for every measured workload.
    }
}
