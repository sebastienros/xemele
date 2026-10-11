using System.Text;
using System.Text.Json;
using Xemele.Html;
using Xunit;
namespace Xemele.Tests.HtmlSerialization;
public sealed class SerializationTests
{
    internal static string Fixture(string name)
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "docs/tasks/M7-serialize.json"))) path = Directory.GetParent(path)?.FullName ?? throw new InvalidOperationException();
        return Path.Combine(path, "tests/fixtures/M7-serialize", name);
    }
    public static IEnumerable<object[]> NativeCases()
    { using var json = JsonDocument.Parse(File.ReadAllText(Fixture("native.json"))); foreach (var c in json.RootElement.EnumerateArray()) yield return [c.Clone()]; }
    [Theory, MemberData(nameof(NativeCases))]
    public void ExactPinnedNativeGraphSerialization(JsonElement item)
    {
        var doc = new HtmlDocument(); var nodes = new List<HtmlNode>();
        foreach (var n in item.GetProperty("tree").EnumerateArray()) doc.DocumentNode.AppendChild(Build(doc, n, nodes));
        var options = new HtmlSaveOptions { Mode = HtmlSerializationMode.Legacy };
        var oracle = item.GetProperty("output");
        Assert.Equal(oracle.GetProperty("document").GetString(), HtmlSerializer.Serialize(doc, options));
        Assert.Equal(HtmlSerializer.Serialize(doc, options), HtmlSerializer.Serialize(doc.DocumentNode, options));
        var outputs = oracle.GetProperty("nodes").EnumerateArray().ToArray(); Assert.Equal(outputs.Length, nodes.Count);
        for (int i = 0; i < nodes.Count; i++) Assert.Equal(outputs[i].GetString(), HtmlSerializer.Serialize(nodes[i], options));
        using var stream = new MemoryStream(); HtmlSerializer.Save(doc, stream, options, TestContext.Current.CancellationToken);
        Assert.True(stream.CanWrite); Assert.Equal(Encoding.UTF8.GetBytes(oracle.GetProperty("document").GetString()!), stream.ToArray());
    }
    internal static HtmlNode Build(HtmlDocument doc, JsonElement n, List<HtmlNode> nodes)
    {
        string name = n.GetProperty("name").GetString()!, value = n.GetProperty("value").GetString()!;
        var node = n.GetProperty("kind").GetString() switch
        {
            "Element" => doc.CreateElement(name, n.TryGetProperty("uri", out var uri) ? uri.GetString()! : "http://www.w3.org/1999/xhtml", n.TryGetProperty("prefix", out var p) ? p.GetString()! : ""),
            "Text" => doc.CreateText(value), "Comment" => doc.CreateComment(value),
            "ProcessingInstruction" => doc.CreateProcessingInstruction(name, value),
            "DocumentType" => doc.CreateDocumentType(new(name, n.GetProperty("public").GetString(), n.GetProperty("system").GetString(), false)),
            _ => throw new InvalidOperationException()
        };
        nodes.Add(node);
        foreach (var a in n.GetProperty("attributes").EnumerateArray())
            node.SetAttribute(a.ValueKind == JsonValueKind.Array ? new(a[0].GetString()!, a[1].GetString()!) : new(a.GetProperty("name").GetString()!, a.GetProperty("value").GetString()!, a.GetProperty("uri").GetString()!, a.GetProperty("prefix").GetString()!));
        foreach (var c in n.GetProperty("children").EnumerateArray()) (node.TemplateContent ?? node).AppendChild(Build(doc, c, nodes));
        return node;
    }
    public static IEnumerable<object[]> Html5libCases()
    { using var json = JsonDocument.Parse(File.ReadAllText(Fixture("html5lib-dispositions.json"))); foreach (var c in json.RootElement.EnumerateArray()) yield return [c.Clone()]; }
    [Theory, MemberData(nameof(Html5libCases))]
    public void EveryOriginalHtml5libSerializerCaseHasAnExactDisposition(JsonElement item)
    {
        using var original = JsonDocument.Parse(File.ReadAllText(Fixture("html5lib/" + item.GetProperty("source").GetString())));
        Assert.True(JsonElement.DeepEquals(original.RootElement.GetProperty("tests")[item.GetProperty("index").GetInt32()], item.GetProperty("original")));
        if (item.GetProperty("tree").ValueKind == JsonValueKind.Null)
        {
            Assert.Equal("unrepresentable-token-control", item.GetProperty("classification").GetString());
            Assert.Contains(item.GetProperty("reasons").EnumerateArray(), x => x.GetString()!.StartsWith("unmatched end tokens", StringComparison.Ordinal));
            return;
        }
        var d = new HtmlDocument(); var nodes = new List<HtmlNode>();
        var container = item.GetProperty("container").GetString() == "Document" ? d.DocumentNode : d.CreateFragment();
        foreach (var n in item.GetProperty("tree").EnumerateArray()) container.AppendChild(Build(d, n, nodes));
        string actual = HtmlSerializer.Serialize(container, new() { Mode = HtmlSerializationMode.Html5 });
        Assert.Equal(item.GetProperty("expected").GetString(), actual);
        if (item.GetProperty("classification").GetString() == "exact-original-overlap")
            Assert.Contains(item.GetProperty("original").GetProperty("expected").EnumerateArray(), x => x.GetString() == actual);
    }
    [Theory]
    [InlineData("area")][InlineData("base")][InlineData("basefont")][InlineData("bgsound")][InlineData("br")][InlineData("col")][InlineData("embed")][InlineData("frame")][InlineData("hr")][InlineData("img")][InlineData("input")][InlineData("keygen")][InlineData("link")][InlineData("meta")][InlineData("param")][InlineData("source")][InlineData("track")][InlineData("wbr")]
    public void Html5VoidDropsChildrenButForeignNamesHaveEnds(string name)
    {
        var d = new HtmlDocument(); var h = d.CreateElement(name); h.AppendChild(d.CreateText("hidden"));
        Assert.Equal("<" + name + ">", HtmlSerializer.Serialize(h, new() { Mode = HtmlSerializationMode.Html5 }));
        foreach (string uri in new[] { "http://www.w3.org/2000/svg", "http://www.w3.org/1998/Math/MathML", "urn:other", "" })
        { var f = d.CreateElement(name, uri, "p"); f.AppendChild(d.CreateText("<>&\u00a0")); string tag = uri is "http://www.w3.org/2000/svg" or "http://www.w3.org/1998/Math/MathML" ? name : "p:" + name; Assert.Equal("<" + tag + ">&lt;&gt;&amp;&nbsp;</" + tag + ">", HtmlSerializer.Serialize(f)); }
    }
    [Theory]
    [InlineData("style")][InlineData("script")][InlineData("xmp")][InlineData("iframe")][InlineData("noembed")][InlineData("noframes")][InlineData("plaintext")]
    public void Html5RawTextOnlyUsesTheImmediateHtmlParent(string name)
    {
        var d = new HtmlDocument(); var h = d.CreateElement(name); var t = d.CreateText("<>&\u00a0"); h.AppendChild(t);
        Assert.Equal("<" + name + "><>&\u00a0</" + name + ">", HtmlSerializer.Serialize(h)); Assert.Equal("<>&\u00a0", HtmlSerializer.Serialize(t));
        var inner = d.CreateElement("div"); h.AppendChild(inner); inner.AppendChild(d.CreateText("<>&\u00a0")); Assert.EndsWith("<div>&lt;&gt;&amp;&nbsp;</div></" + name + ">", HtmlSerializer.Serialize(h));
        var foreign = d.CreateElement(name, "http://www.w3.org/2000/svg"); foreign.AppendChild(d.CreateText("<>&\u00a0")); Assert.Equal("<" + name + ">&lt;&gt;&amp;&nbsp;</" + name + ">", HtmlSerializer.Serialize(foreign));
    }
    [Fact] public void Html5AllNamespaceAndAttributeRulesAreLiteralAndOrdered()
    {
        var d = new HtmlDocument(); var e = d.CreateElement("Node", "urn:foreign", "p");
        e.SetAttribute(new("local", "<&>\"\u00a0", "", "ignored")); e.SetAttribute(new("lang", "x", "http://www.w3.org/XML/1998/namespace", "ignored"));
        e.SetAttribute(new("xmlns", "uri", "http://www.w3.org/2000/xmlns/", "ignored")); e.SetAttribute(new("q", "uri", "http://www.w3.org/2000/xmlns/", "ignored"));
        e.SetAttribute(new("href", "url", "http://www.w3.org/1999/xlink", "ignored")); e.SetAttribute(new("foo", "v", "urn:other", "a")); e.SetAttribute(new("checked", ""));
        Assert.Equal("<p:Node local=\"<&amp;>&quot;&nbsp;\" xml:lang=\"x\" xmlns=\"uri\" xmlns:q=\"uri\" xlink:href=\"url\" a:foo=\"v\" checked=\"\"></p:Node>", HtmlSerializer.Serialize(e));
        Assert.Equal(HtmlSerializer.Serialize(e), HtmlSerializer.Serialize(e, new() { Mode = HtmlSerializationMode.Html5 }));
        var noPrefix = d.CreateElement("Node", "urn:other"); Assert.Equal("<Node></Node>", HtmlSerializer.Serialize(noPrefix));
    }
    [Fact] public void Html5CommentsDoctypeAndProcessingInstructionsPreserveAllData()
    {
        var d = new HtmlDocument(); d.DocumentNode.AppendChild(d.CreateDocumentType(new("hTmL", "public", "system", true)));
        d.DocumentNode.AppendChild(d.CreateComment("x-->--tail-\0<&>")); d.DocumentNode.AppendChild(d.CreateProcessingInstruction("Target", ""));
        var h = d.CreateElement("div"); d.DocumentNode.AppendChild(h); h.AppendChild(d.CreateProcessingInstruction("_Target", "x?>\0<&>"));
        Assert.Equal("<!DOCTYPE hTmL><!--x-->--tail-\0<&>--><?Target ?><div><?_Target x?>\0<&>?></div>", HtmlSerializer.Serialize(d));
        Assert.Equal("<!DOCTYPE hTmL>", HtmlSerializer.Serialize(d.DocumentNode.FirstChild!));
    }
    [Fact] public void FragmentsAndTemplatesUseTheirOwnContentGraph()
    {
        var d = new HtmlDocument(); var f = d.CreateFragment(); var t = d.CreateElement("template"); f.AppendChild(t);
        t.AppendChild(d.CreateText("ignored ordinary children")); var nested = d.CreateElement("template"); t.TemplateContent!.AppendChild(nested);
        nested.TemplateContent!.AppendChild(d.CreateText("<>&\u00a0")); nested.TemplateContent.AppendChild(d.CreateProcessingInstruction("p", "")); f.AppendChild(d.CreateComment("tail"));
        const string expected = "<template><template>&lt;&gt;&amp;&nbsp;<?p ?></template></template><!--tail-->";
        Assert.Equal(expected, HtmlSerializer.Serialize(f)); Assert.Equal("&lt;&gt;&amp;&nbsp;<?p ?>", HtmlSerializer.Serialize(nested.TemplateContent));
        Assert.Equal("", HtmlSerializer.Serialize(d)); Assert.Equal("", HtmlSerializer.Serialize(d.CreateFragment()));
    }
    [Fact] public void ExactOriginalWptNoscriptInnerHtmlProjectionAndOrdinaryScripting()
    {
        string original = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Fixture("native.json"))!, "noscript-template-querySelector.html"));
        Assert.Contains("assert_equals(noscriptNode.innerHTML, \"&lt;div&gt;&lt;/div&gt;\")", original);
        const string input = "<template id=\"template1\"><noscript><div></div></noscript></template>";
        var d = Html5Parser.Parse(input, new() { ScriptingEnabled = true });
        var template = d.RootElement.Children[0].FirstChild!; var noscript = template.TemplateContent!.FirstChild!;
        Assert.Equal(HtmlNodeKind.Text, noscript.FirstChild!.Kind); Assert.Equal("<div></div>", noscript.GetTextContent());
        string outer = HtmlSerializer.Serialize(noscript); Assert.Equal("<noscript>&lt;div&gt;&lt;/div&gt;</noscript>", outer);
        Assert.Equal("&lt;div&gt;&lt;/div&gt;", outer[10..^11]); Assert.Equal("&lt;div&gt;&lt;/div&gt;", HtmlSerializer.Serialize(noscript.FirstChild));
        var ordinary = Html5Parser.Parse("<body><noscript><div></div></noscript>", new() { ScriptingEnabled = true });
        Assert.Equal("<noscript><div></div></noscript>", HtmlSerializer.Serialize(ordinary.RootElement.Children[1].FirstChild!));
        var disabled = Html5Parser.Parse("<body><noscript>&lt;div&gt;&lt;/div&gt;</noscript>", new() { ScriptingEnabled = false });
        Assert.Equal("<noscript>&lt;div&gt;&lt;/div&gt;</noscript>", HtmlSerializer.Serialize(disabled.RootElement.Children[1].FirstChild!));
    }
    [Fact] public void SaveStreamsUtf8WithoutBomAndKeepsCallerOpenOnEveryExit()
    {
        var d = Html5Parser.Parse("<p>é😀&amp;\u00a0"); string expected = HtmlSerializer.Serialize(d);
        using var output = new MemoryStream(); HtmlSerializer.Save(d, output, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(Encoding.UTF8.GetBytes(expected), output.ToArray()); Assert.True(output.CanWrite);
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); Assert.Throws<OperationCanceledException>(() => HtmlSerializer.Save(d, output, cancellationToken: canceled.Token)); Assert.True(output.CanWrite);
        using var readOnly = new MemoryStream([], false); Assert.Throws<ArgumentException>(() => HtmlSerializer.Save(d, readOnly, cancellationToken: TestContext.Current.CancellationToken));
        var error = new IOException("owned failure"); using var fault = new ControlledStream(error, null); Assert.Same(error, Assert.Throws<IOException>(() => HtmlSerializer.Save(d, fault, cancellationToken: TestContext.Current.CancellationToken))); Assert.False(fault.Disposed);
        var huge = new HtmlDocument(new() { TokenCharacters = null }); var root = huge.CreateElement("p"); huge.DocumentNode.AppendChild(root); root.AppendChild(huge.CreateText(new string('é', 40000)));
        using var cancel = new CancellationTokenSource(); using var bounded = new ControlledStream(null, cancel); Assert.Throws<OperationCanceledException>(() => HtmlSerializer.Save(huge, bounded, cancellationToken: cancel.Token)); Assert.False(bounded.Disposed); Assert.InRange(bounded.MaximumWrite, 1, 3075); Assert.InRange(bounded.TotalWritten, 1, 3075);
    }
    [Theory]
    [InlineData("raw")][InlineData("uri")][InlineData("doctype")]
    public void LongLegacyValuesObserveCancellationAfterPriorStreamWrites(string kind)
    {
        var d = new HtmlDocument(new() { TokenCharacters = null }); var e = d.CreateElement(kind == "raw" ? "script" : "div"); d.DocumentNode.AppendChild(e);
        if (kind == "raw") e.AppendChild(d.CreateText(new string('x', 40000) + "\0tail"));
        if (kind == "uri") e.SetAttribute(new("href", new string(' ', 30000) + "é😀"));
        if (kind == "doctype") d.DocumentNode.AppendChild(d.CreateDocumentType(new("html", new string('x', 30000) + "\"quote'", new string('x', 30000), false)));
        using var cancellation = new CancellationTokenSource(); using var stream = new ControlledStream(null, cancellation);
        Assert.Throws<OperationCanceledException>(() => HtmlSerializer.Save(d, stream, new() { Mode = HtmlSerializationMode.Legacy }, cancellation.Token));
        Assert.False(stream.Disposed); Assert.InRange(stream.TotalWritten, 1, 3075); Assert.InRange(stream.MaximumWrite, 1, 3075);
    }
    [Fact] public void UnlimitedDeepTreesAndSplitUnicodeUseBoundedTraversalAndEncoding()
    {
        var d = new HtmlDocument(new() { Depth = null, TokenCharacters = null }); var root = d.CreateElement("x"); d.DocumentNode.AppendChild(root); var current = root;
        for (int i = 0; i < 8192; i++) { var child = d.CreateElement("x"); current.AppendChild(child); current = child; }
        string value = new string('x', 1023) + "😀&"; current.AppendChild(d.CreateText(value));
        string output = HtmlSerializer.Serialize(d); Assert.Equal(8193 * 7 + 1030, output.Length);
        using var stream = new MemoryStream(); HtmlSerializer.Save(d, stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(output), stream.ToArray());
        var t = d.CreateElement("p"); t.AppendChild(d.CreateText("\ud800&\udc00"));
        Assert.Equal("<p>\ud800&amp;\udc00</p>", HtmlSerializer.Serialize(t));
    }
    [Fact] public void InvalidArgumentsAndEarlierXmlSerializationStayUnchanged()
    {
        Assert.Throws<ArgumentNullException>(() => HtmlSerializer.Serialize((HtmlDocument)null!)); Assert.Throws<ArgumentNullException>(() => HtmlSerializer.Serialize((HtmlNode)null!));
        Assert.Throws<ArgumentNullException>(() => HtmlSerializer.Save(new(), null!, cancellationToken: TestContext.Current.CancellationToken)); Assert.Throws<ArgumentOutOfRangeException>(() => HtmlSerializer.Serialize(new HtmlDocument(), new() { Mode = (HtmlSerializationMode)17 }));
        var xml = XmlDocument.Parse("<root a='&quot;'>&lt;&amp;</root>", cancellationToken: TestContext.Current.CancellationToken); Assert.Equal("<root a=\"&quot;\">&lt;&amp;</root>", XmlSerializer.Serialize(xml, new() { OmitXmlDeclaration = true }, TestContext.Current.CancellationToken));
    }
    private sealed class ControlledStream(IOException? failure, CancellationTokenSource? cancel) : Stream
    {
        public bool Disposed; public int MaximumWrite, TotalWritten;
        public override void Write(byte[] buffer, int offset, int count) { if (failure is not null) throw failure; MaximumWrite = Math.Max(MaximumWrite, count); TotalWritten += count; cancel?.Cancel(); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => !Disposed; public override long Length => TotalWritten; public override long Position { get => TotalWritten; set => throw new NotSupportedException(); }
        public override void Flush() { } public override int Read(byte[] b, int o, int c) => throw new NotSupportedException(); public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long n) => throw new NotSupportedException();
    }
}
