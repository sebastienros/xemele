using System.Text;
using System.Text.Json;
using Xunit;
namespace Xemele.Tests.Serialization;
public class SerializerTests
{
    private static XmlSaveOptions Omit => new() { OmitXmlDeclaration = true };
    private static XmlSaveOptions Repair => Omit with { NamespaceHandling = XmlNamespaceHandling.Repair };
    private static XmlDocument Root(out XmlNode root, string uri = "", string prefix = "")
    {
        var d = new XmlDocument(); root = d.CreateElement("r", uri, prefix); d.DocumentNode.AppendChild(root); return d;
    }
    private static string Repository()
    {
        for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName, "tests/Contracts/Fixtures/M1/serialization-vectors.json"))) return p.FullName;
        throw new InvalidOperationException("Cannot locate repository vectors.");
    }
    [Fact]
    public void ApprovedVectorsExecuteAgainstIndependentTrees()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repository(), "tests/Contracts/Fixtures/M1/serialization-vectors.json")));
        int count = 0;
        foreach (var v in vectors.RootElement.EnumerateArray())
        {
            var d = new XmlDocument(); XmlNode root = default;
            foreach (var action in v.GetProperty("actions").EnumerateArray())
            {
                string S(int i) => action[i].GetString()!;
                switch (S(0))
                {
                    case "element": root = d.CreateElement(S(1), action.GetArrayLength() > 2 ? S(2) : null, action.GetArrayLength() > 3 ? S(3) : null); d.DocumentNode.AppendChild(root); break;
                    case "attribute": root.SetAttribute(S(1), S(2), action.GetArrayLength() > 3 ? S(3) : null, action.GetArrayLength() > 4 ? S(4) : null); break;
                    case "namespace": root.SetNamespaceDeclaration(S(1), S(2)); break;
                    case "text": root.AppendChild(d.CreateText(S(1))); break;
                    default: throw new InvalidOperationException("Unknown approved vector action.");
                }
            }
            var j = v.GetProperty("options");
            var options = new XmlSaveOptions
            {
                OmitXmlDeclaration = j.TryGetProperty("OmitXmlDeclaration", out var o) && o.GetBoolean(),
                EmitByteOrderMark = j.TryGetProperty("EmitByteOrderMark", out var b) && b.GetBoolean(),
                NamespaceHandling = j.TryGetProperty("NamespaceHandling", out var n) ? Enum.Parse<XmlNamespaceHandling>(n.GetString()!) : XmlNamespaceHandling.Strict,
                Encoding = v.GetProperty("encoding").GetString() switch { "utf-16le" => new UnicodeEncoding(false, true, false), "utf-16be" => new UnicodeEncoding(true, true, false), _ => new UTF8Encoding(true, false) }
            };
            Assert.Equal(v.GetProperty("string").GetString(), d.ToXmlString(options, cancellationToken: TestContext.Current.CancellationToken));
            using var stream = new MemoryStream(); d.Save(stream, options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(v.GetProperty("expectedHex").GetString(), Convert.ToHexStringLower(stream.ToArray())); count++;
        }
        Assert.Equal(6, count);
    }
    [Fact]
    public void AdjacentTextAndLexicalKindsEscapeWithoutCoalescing()
    {
        var d = Root(out var r); var t = d.CreateText("]"); r.AppendChild(t); r.AppendChild(d.CreateText("]")); r.AppendChild(d.CreateText(">&<\r\n\t🙂"));
        r.AppendChild(d.CreateComment("c")); r.AppendChild(d.CreateCData("<raw>")); r.AppendChild(d.CreateProcessingInstruction("p:q", "data")); r.AppendChild(d.CreateProcessingInstruction("z", ""));
        r.AppendChild(d.CreateText(">"));
        Assert.Equal("<r>]]&gt;&amp;&lt;&#xD;\n\t🙂<!--c--><![CDATA[<raw>]]><?p:q data?><?z?>></r>", d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(t, r.FirstChild); Assert.Equal(8, r.Children.Count);
    }
    [Fact]
    public void IndentationPreservesMixedWhitespaceAndFormatsDescendants()
    {
        var d = Root(out var r); var a = d.CreateElement("a"); var b = d.CreateElement("b"); var c = d.CreateElement("c");
        r.AppendChild(d.CreateText(" ")); r.AppendChild(a); a.AppendChild(b); b.AppendChild(c); r.AppendChild(d.CreateCData(" "));
        Assert.Equal("<r> <a>\n    <b>\n      <c/>\n    </b>\n  </a><![CDATA[ ]]></r>", d.ToXmlString(Omit with { Indent = true }, cancellationToken: TestContext.Current.CancellationToken));
        var n = Root(out var root); root.AppendChild(n.CreateElement("a")); root.AppendChild(n.CreateComment("c"));
        n.DocumentNode.InsertBefore(n.CreateProcessingInstruction("p", ""), root); n.DocumentNode.AppendChild(n.CreateComment("after"));
        Assert.Equal("<?xml version=\"1.0\"?>\n<?p?>\n<r>\n  <a/>\n  <!--c-->\n</r>\n<!--after-->", n.ToXmlString(new() { Indent = true }, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void StrictChecksDefaultInheritedAttributeAndReservedBindings()
    {
        var d = Root(out var r, "u"); Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken)); r.SetNamespaceDeclaration("", "u");
        var c = d.CreateElement("c"); r.AppendChild(c); Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken)); c.SetNamespaceDeclaration("", "");
        c.SetAttribute("a", "v", "a"); Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken)); c.SetAttribute("a", "v", "a", "p");
        r.SetNamespaceDeclaration("p", "a"); c.SetAttribute("lang", "en", "http://www.w3.org/XML/1998/namespace", "xml");
        Assert.Equal("<r xmlns=\"u\" xmlns:p=\"a\"><c xmlns=\"\" p:a=\"v\" xml:lang=\"en\"/></r>", d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
        c.SetNamespaceDeclaration("p", "b"); Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void RepairConflictsReuseOrderingAndSiblingRestoration()
    {
        var d = Root(out var r, "u", "p"); r.SetNamespaceDeclaration("p", "wrong"); r.SetNamespaceDeclaration("z", "attr"); r.SetNamespaceDeclaration("a", "attr");
        r.SetAttribute("x", "1", "attr", "p"); r.SetAttribute("y", "2", "other", "ns2");
        var child = d.CreateElement("c", "u", "p"); child.SetNamespaceDeclaration("ns3", "blocked"); r.AppendChild(child);
        var sibling = d.CreateElement("s", "other", "ns2"); r.AppendChild(sibling);
        var attr = r.Attributes[0]; long version = d.MutationVersion;
        Assert.Equal("<ns1:r xmlns:p=\"wrong\" xmlns:z=\"attr\" xmlns:a=\"attr\" xmlns:ns1=\"u\" xmlns:ns3=\"other\" a:x=\"1\" ns3:y=\"2\"><p:c xmlns:ns3=\"blocked\" xmlns:p=\"u\"/><ns2:s xmlns:ns2=\"other\"/></ns1:r>", d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(version, d.MutationVersion); Assert.Equal("p", r.Prefix); Assert.Equal("p", attr.Prefix); Assert.Equal("wrong", r.NamespaceDeclarations[0].Value);
        Assert.Equal(d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken), d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void RepairDefaultReplacementAndAttributeUpdatesPreserveOrder()
    {
        var d = Root(out var r); r.SetNamespaceDeclaration("", "bad"); r.SetNamespaceDeclaration("q", "u&\r");
        r.SetAttribute("a", "1"); r.SetAttribute("b", "2"); r.SetAttribute("a", "3"); r.RemoveAttribute("b"); r.SetAttribute("b", "4");
        var child = d.CreateElement("c", "u"); child.SetNamespaceDeclaration("", "conflict"); r.AppendChild(child);
        Assert.Equal("<r xmlns=\"\" xmlns:q=\"u&amp;&#xD;\" a=\"3\" b=\"4\"><ns1:c xmlns=\"conflict\" xmlns:ns1=\"u\"/></r>", d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("bad", r.NamespaceDeclarations[0].Value);
    }
    [Theory]
    [InlineData(65001, false, false)] [InlineData(65001, true, false)] [InlineData(65001, true, true)]
    [InlineData(1200, false, false)] [InlineData(1200, true, false)] [InlineData(1200, true, true)]
    [InlineData(1201, false, false)] [InlineData(1201, true, false)] [InlineData(1201, true, true)]
    public void AllTransportsAndBomControls(int page, bool bom, bool omit)
    {
        var d = Root(out var r); r.AppendChild(d.CreateText(new string('a', 2047) + "🙂" + new string('界', 4096)));
        var encoding = Encoding.GetEncoding(page); var o = new XmlSaveOptions { Encoding = encoding, EmitByteOrderMark = bom, OmitXmlDeclaration = omit };
        string label = page == 65001 ? "utf-8" : page == 1200 ? "utf-16le" : "utf-16be";
        string expected = (omit ? "" : $"<?xml version=\"1.0\" encoding=\"{label}\"?>") + "<r>" + r.FirstChild.Value + "</r>";
        byte[] preamble = !bom ? [] : page == 65001 ? [0xef, 0xbb, 0xbf] : page == 1200 ? [0xff, 0xfe] : [0xfe, 0xff];
        using var s = new MemoryStream(); d.Save(s, o, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(preamble.Concat(encoding.GetBytes(expected)), s.ToArray());
        Assert.Equal((omit ? "" : "<?xml version=\"1.0\"?>") + "<r>" + r.FirstChild.Value + "</r>", d.ToXmlString(o, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(XmlNodeKind.CData, 65001)] [InlineData(XmlNodeKind.Comment, 65001)] [InlineData(XmlNodeKind.ProcessingInstruction, 65001)]
    [InlineData(XmlNodeKind.CData, 1200)] [InlineData(XmlNodeKind.Comment, 1200)] [InlineData(XmlNodeKind.ProcessingInstruction, 1200)]
    [InlineData(XmlNodeKind.CData, 1201)] [InlineData(XmlNodeKind.Comment, 1201)] [InlineData(XmlNodeKind.ProcessingInstruction, 1201)]
    public void RawLexicalChunksHandleEncoderSurrogateCarry(XmlNodeKind kind, int page)
    {
        // Vary prefix alignment so a retained high surrogate is followed by a
        // low surrogate plus 2047 three-byte BMP scalars with 6144 bytes free.
        foreach (int prefixLength in new[] { 0, 1, 952, 953, 2045, 2046, 2047, 3000 })
        foreach (int targetLength in new[] { 2048, 3000 })
        {
            var d = Root(out var r);
            if (prefixLength != 0) r.AppendChild(d.CreateText(new string('a', prefixLength)));
            string value = new string('a', 2047) + "🙂" + new string('界', 2047);
            var node = kind switch
            {
                XmlNodeKind.CData => d.CreateCData(value),
                XmlNodeKind.Comment => d.CreateComment(value),
                _ => d.CreateProcessingInstruction(new string('p', targetLength), value)
            };
            r.AppendChild(node);
            var encoding = Encoding.GetEncoding(page);
            using var stream = new MemoryStream(); d.Save(stream, Omit with { Encoding = encoding }, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(encoding.GetBytes(d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken)), stream.ToArray());
        }
    }
    [Fact]
    public void Utf8CDataExactCarryCapacityRegression()
    {
        var d = Root(out var r); r.AppendChild(d.CreateText(new string('x', 2040)));
        r.AppendChild(d.CreateCData(new string('a', 2046) + "é🙂" + new string('界', 2047)));
        using var stream = new MemoryStream(); d.Save(stream, Omit, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken)), stream.ToArray());
        Assert.True(stream.CanWrite);
    }
    [Fact]
    public void OptionsRootAndStreamValidationPrecedeOutput()
    {
        using var s = new RecordingStream(); var d = Root(out _);
        Assert.Throws<ArgumentNullException>(() => XmlSerializer.Serialize(null!, s, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => d.Save(null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => d.Save(new MemoryStream([], false), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => d.Save(s, new() { Encoding = Encoding.ASCII }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => d.Save(s, new() { Encoding = null! }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => d.Save(s, new() { NamespaceHandling = (XmlNamespaceHandling)2 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => new XmlDocument().Save(s, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(0, s.Length); Assert.False(s.Closed); Assert.Equal(0, s.Flushes);
    }
    [Fact]
    public void IoFailureAndCancellationPropagateAndLeaveCallerOpen()
    {
        var d = Root(out var r); r.AppendChild(d.CreateText(new string('x', 100_000)));
        using var success = new RecordingStream(); d.Save(success, cancellationToken: TestContext.Current.CancellationToken); Assert.True(success.Length > 100000); Assert.False(success.Closed); Assert.Equal(0, success.Flushes); Assert.True(success.MaxWrite <= 8192);
        var error = new IOException("identity"); using var bad = new RecordingStream { Failure = error };
        Assert.Same(error, Assert.Throws<IOException>(() => d.Save(bad, cancellationToken: TestContext.Current.CancellationToken))); Assert.True(bad.Length > 0); Assert.False(bad.Closed);
        using var source = new CancellationTokenSource(); using var canceled = new RecordingStream { Cancellation = source };
        var ex = Assert.Throws<OperationCanceledException>(() => d.Save(canceled, cancellationToken: source.Token));
        Assert.Equal(source.Token, ex.CancellationToken); Assert.True(canceled.Length > 0); Assert.False(canceled.Closed);
        using var pre = new RecordingStream(); Assert.Throws<OperationCanceledException>(() => d.Save(pre, cancellationToken: source.Token)); Assert.Equal(0, pre.Length);
        // Scratch returned on faults; a new operation remains usable.
        Assert.Equal(d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken), d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(XmlNodeKind.Text, "\0")] [InlineData(XmlNodeKind.Text, "\ud800")]
    [InlineData(XmlNodeKind.Text, "\udc00")] [InlineData(XmlNodeKind.Text, "\ufffe")]
    [InlineData(XmlNodeKind.Comment, "--")] [InlineData(XmlNodeKind.Comment, "x-")]
    [InlineData(XmlNodeKind.CData, "]]>")] [InlineData(XmlNodeKind.ProcessingInstruction, "?>")]
    public void RevalidatesLexicalStateAsOperationErrors(XmlNodeKind kind, string value)
    {
        var d = Root(out var r); var node = kind switch { XmlNodeKind.Text => d.CreateText("ok"), XmlNodeKind.Comment => d.CreateComment("ok"), XmlNodeKind.CData => d.CreateCData("ok"), _ => d.CreateProcessingInstruction("pi", "ok") };
        r.AppendChild(node); d.Nodes[node.Slot].Value = value;
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
        using var s = new RecordingStream(); Assert.Throws<InvalidOperationException>(() => d.Save(s, cancellationToken: TestContext.Current.CancellationToken)); Assert.False(s.Closed);
    }
    [Fact]
    public void RevalidatesNamesBindingsAndAttributeScalars()
    {
        var d = Root(out var r); var a = r.SetAttribute("a", "ok"); d.AttributeSlots[a.Slot].Value = "\0";
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
        a.Value = "ok"; d.AttributeSlots[a.Slot].Prefix = "bad:name"; Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
        d.AttributeSlots[a.Slot].Prefix = "";
        var n = r.SetNamespaceDeclaration("p", "u"); d.NamespaceSlots[n.Slot].Value = "http://www.w3.org/2000/xmlns/";
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("bad:name", "", "")] [InlineData("1bad", "", "")]
    [InlineData("ok", "u", "xmlns")] [InlineData("ok", "wrong", "xml")]
    [InlineData("ok", "http://www.w3.org/XML/1998/namespace", "p")]
    [InlineData("xmlns", "", "")]
    public void RevalidatesElementNamesAndReservedUris(string localName, string uri, string prefix)
    {
        var d = Root(out var r);
        d.Nodes[r.Slot] = new XmlDocument.NodeData(XmlNodeKind.Element, localName, uri, prefix, "") { Parent = 0 };
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("XmL")] [InlineData("1pi")] [InlineData("\ud800")]
    public void RevalidatesPiTargets(string target)
    {
        var d = Root(out var r); var pi = d.CreateProcessingInstruction("pi", "data"); r.AppendChild(pi);
        d.Nodes[pi.Slot] = new XmlDocument.NodeData(XmlNodeKind.ProcessingInstruction, target, "", "", "data") { Parent = r.Slot };
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Omit, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void DuplicateDeclarationsAreRejectedEvenInRepair()
    {
        var d = Root(out var r); r.SetNamespaceDeclaration("p", "u"); var n = r.SetNamespaceDeclaration("q", "v");
        d.NamespaceSlots[n.Slot].Prefix = "p";
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Repair, cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void DeepTraversalAndScopeUndoHaveLinearCountedWork()
    {
        static (long Steps, long Bindings, int Depth) Measure(int count)
        {
            var d = Root(out var r); var current = r;
            // Build bottom-up: tree construction ancestor checks do not dominate.
            for (int i = 0; i < count; i++)
            {
                var parent = d.CreateElement("n", "u", "p"); parent.SetNamespaceDeclaration("p", "u"); parent.AppendChild(current);
                current = parent;
            }
            d.DocumentNode.AppendChild(current); current.AppendChild(d.CreateText(""));
            var m = XmlSerializer.Measure(d, Repair); return (m.Steps, m.Bindings, m.Depth);
        }
        var small = Measure(2000); var large = Measure(10000);
        Assert.Equal(10000, large.Depth); Assert.Equal(20000, large.Bindings);
        Assert.True(large.Steps <= small.Steps * 5 + 32);
    }
    private sealed class RecordingStream : MemoryStream
    {
        internal bool Closed; internal int Flushes, MaxWrite; internal IOException? Failure; internal CancellationTokenSource? Cancellation;
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            MaxWrite = Math.Max(MaxWrite, buffer.Length);
            if (Failure is not null && Length != 0) throw Failure;
            base.Write(buffer); Cancellation?.Cancel();
        }
        public override void Flush() { Flushes++; base.Flush(); }
        protected override void Dispose(bool disposing) { Closed = true; base.Dispose(disposing); }
    }
}
