using System.Text;
using Xunit;
using Writer = Xemele.XmlWriter;
namespace Xemele.Tests.M3Writer;

public sealed class WriterTests
{
    private static XmlSaveOptions Omit => new() { OmitXmlDeclaration = true };
    private static string Emit(Action<Writer> write, XmlSaveOptions? options = null)
    {
        using var stream = new MemoryStream(); using var writer = Writer.Create(stream, options ?? Omit, TestContext.Current.CancellationToken);
        write(writer); writer.WriteEndDocument(); return Encoding.UTF8.GetString(stream.ToArray());
    }
    [Fact]
    public void IndependentGoldenMixedEscapingAndLexicalNodes()
    {
        Assert.Equal("<!--p--><?go yes?><r a=\"&quot;&lt;&amp;&#x9;&#xA;&#xD;\">α😀&amp;&lt;&#xD;]]&gt;<x/><![CDATA[]]><!--c--><?go?></r><!--e-->", Emit(w =>
        {
            w.WriteComment("p"); w.WriteProcessingInstruction("go", "yes"); w.WriteStartElement("r");
            w.WriteAttribute("a", "\"<&\t\n\r"); w.WriteText("α😀&<\r]]"); w.WriteText(">");
            w.WriteStartElement("x"); w.WriteEndElement(); w.WriteCData(""); w.WriteComment("c");
            w.WriteProcessingInstruction("go", ""); w.WriteEndElement(); w.WriteComment("e");
        }));
    }
    [Fact]
    public void PendingTagAcceptsLateBindingsAndOrdersDeclarationsFirst()
    {
        Assert.Equal("<p:r xmlns:p=\"u\" xmlns:q=\"v\" q:a=\"1\" z=\"2\"/>", Emit(w =>
        { w.WriteStartElement("r", "u", "p"); w.WriteAttribute("a", "1", "v", "q"); w.WriteAttribute("z", "2"); w.WriteNamespace("p", "u"); w.WriteNamespace("q", "v"); w.WriteEndElement(); }));
    }
    [Fact]
    public void RepairNamespaceGolden()
    {
        var repair = Omit with { NamespaceHandling = XmlNamespaceHandling.Repair };
        Assert.Equal("<ns2:r xmlns:p=\"other\" xmlns:z=\"a\" xmlns:b=\"a\" xmlns:ns1=\"k\" xmlns:ns2=\"u\" b:a=\"1\" ns1:keep=\"3\"/>", Emit(w =>
        {
            w.WriteStartElement("r", "u", "p"); w.WriteNamespace("p", "other"); w.WriteNamespace("z", "a"); w.WriteNamespace("b", "a");
            w.WriteAttribute("a", "1", "a"); w.WriteAttribute("keep", "3", "k", "ns1"); w.WriteNamespace("ns1", "k"); w.WriteEndElement();
        }, repair));
        Assert.Equal("<r xmlns=\"u\"><x xmlns=\"\"/></r>", Emit(w =>
        { w.WriteStartElement("r", "u"); w.WriteStartElement("x"); w.WriteNamespace("", "conflict"); w.WriteEndElement(); w.WriteEndElement(); }, repair));
        Assert.Equal("<r xmlns:p=\"u\"><p:x/><p:y/></r>", Emit(w =>
        { w.WriteStartElement("r"); w.WriteNamespace("p", "u"); w.WriteStartElement("x", "u", "p"); w.WriteEndElement(); w.WriteStartElement("y", "u", "p"); w.WriteEndElement(); w.WriteEndElement(); }));
    }
    [Fact]
    public void StrictRejectsUnboundNamesAndDefaultUndeclaration()
    {
        using var stream = new MemoryStream(); using var w = Writer.Create(stream, Omit, TestContext.Current.CancellationToken);
        w.WriteStartElement("r", "u"); w.WriteNamespace("", "u"); w.WriteStartElement("x");
        Assert.Throws<InvalidOperationException>(w.WriteEndElement);
        Assert.Throws<InvalidOperationException>(w.Flush);
        Assert.Equal("", Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal("<r xmlns=\"u\"><x xmlns=\"\"/></r>", Emit(writer =>
        { writer.WriteStartElement("r", "u"); writer.WriteNamespace("", "u"); writer.WriteStartElement("x"); writer.WriteNamespace("", ""); writer.WriteEndElement(); writer.WriteEndElement(); }));
    }
    [Fact]
    public void FrozenIndentCommitmentAndEmptyText()
    {
        var indent = Omit with { Indent = true };
        Assert.Equal("<!--p-->\n<r>\n  <x/>\n  <!--c-->\n</r>\n<?e?>", Emit(w =>
        { w.WriteComment("p"); w.WriteStartElement("r"); w.WriteStartElement("x"); w.WriteEndElement(); w.WriteComment("c"); w.WriteEndElement(); w.WriteProcessingInstruction("e", ""); }, indent));
        Assert.Equal("<r><x/></r>", Emit(w => { w.WriteStartElement("r"); w.WriteText(""); w.WriteStartElement("x"); w.WriteEndElement(); w.WriteEndElement(); }, indent));
        Assert.Equal("<r></r>", Emit(w => { w.WriteStartElement("r"); w.WriteText(""); w.WriteEndElement(); }));
        using var stream = new MemoryStream(); using var writer = Writer.Create(stream, indent, TestContext.Current.CancellationToken);
        writer.WriteStartElement("r"); writer.WriteComment("c");
        Assert.Throws<InvalidOperationException>(() => writer.WriteText(""));
        Assert.Throws<InvalidOperationException>(() => writer.WriteCData("x"));
        writer.WriteEndElement(); writer.WriteEndDocument();
    }
    [Theory]
    [InlineData(65001, false, "utf-8", "")]
    [InlineData(65001, true, "utf-8", "efbbbf")]
    [InlineData(1200, false, "utf-16le", "")]
    [InlineData(1200, true, "utf-16le", "fffe")]
    [InlineData(1201, false, "utf-16be", "")]
    [InlineData(1201, true, "utf-16be", "feff")]
    public void StrictEncodingBomAndDeclaration(int page, bool bom, string label, string hex)
    {
        Encoding encoding = page == 65001 ? new UTF8Encoding(true, false) : new UnicodeEncoding(page == 1201, true, false);
        using var stream = new MemoryStream(); using var w = Writer.Create(stream, new() { Encoding = encoding, EmitByteOrderMark = bom }, TestContext.Current.CancellationToken);
        w.WriteStartDocument(); w.WriteStartElement("r"); w.WriteText("α😀"); w.WriteEndElement(); w.WriteEndDocument();
        byte[] prefix = Convert.FromHexString(hex); byte[] bytes = stream.ToArray();
        Assert.Equal(prefix, bytes[..prefix.Length]);
        Assert.Equal($"<?xml version=\"1.0\" encoding=\"{label}\"?><r>α😀</r>", encoding.GetString(bytes[prefix.Length..]));
        Assert.True(stream.CanWrite);
    }
    [Fact]
    public void AllWrongStatesAndValidationFailuresLeaveIdleWriterUsable()
    {
        using var stream = new CountingStream(); using var w = Writer.Create(stream, Omit, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(w.WriteEndElement); Assert.Throws<InvalidOperationException>(w.WriteEndDocument);
        Assert.Throws<InvalidOperationException>(() => w.WriteText("x")); Assert.Throws<InvalidOperationException>(() => w.WriteCData("x"));
        Assert.Throws<InvalidOperationException>(() => w.WriteAttribute("a", "1")); Assert.Throws<InvalidOperationException>(() => w.WriteNamespace("p", "u"));
        Assert.Throws<ArgumentException>(() => w.WriteStartElement("a:b"));
        w.WriteStartDocument(); Assert.Throws<InvalidOperationException>(w.WriteStartDocument); w.WriteStartElement("r");
        Assert.Throws<ArgumentNullException>(() => w.WriteAttribute("a", null!));
        Assert.Throws<ArgumentException>(() => w.WriteNamespace("xml", "wrong"));
        Assert.Throws<ArgumentException>(() => w.WriteNamespace("p", ""));
        Assert.Throws<ArgumentException>(() => w.WriteAttribute("xmlns", "bad"));
        w.WriteNamespace("p", "u"); Assert.Throws<InvalidOperationException>(() => w.WriteNamespace("p", "v"));
        w.WriteAttribute("a", "1", "u", "p"); Assert.Throws<InvalidOperationException>(() => w.WriteAttribute("a", "2", "u", "q"));
        Assert.Throws<ArgumentException>(() => w.WriteText("\ud800")); Assert.Throws<ArgumentException>(() => w.WriteText("\u0001"));
        Assert.Throws<ArgumentException>(() => w.WriteComment("x--x")); Assert.Throws<ArgumentException>(() => w.WriteComment("x-"));
        Assert.Throws<ArgumentException>(() => w.WriteCData("]]>")); Assert.Throws<ArgumentException>(() => w.WriteProcessingInstruction("XML", ""));
        Assert.Throws<ArgumentException>(() => w.WriteProcessingInstruction("p", "?>"));
        w.Flush(); Assert.Equal(1, stream.Flushes);
        Assert.Throws<InvalidOperationException>(() => w.WriteAttribute("b", "1")); Assert.Throws<InvalidOperationException>(() => w.WriteNamespace("q", "v"));
        Assert.Throws<InvalidOperationException>(w.WriteEndDocument); w.WriteEndElement();
        Assert.Throws<InvalidOperationException>(() => w.WriteStartElement("second")); w.WriteEndDocument();
        Assert.Throws<InvalidOperationException>(w.WriteEndDocument); Assert.Throws<InvalidOperationException>(() => w.WriteComment("x"));
        w.Flush(); Assert.Equal(2, stream.Flushes); w.Dispose(); w.Dispose(); Assert.True(stream.CanWrite);
        Assert.Throws<ObjectDisposedException>(w.Flush);
    }
    [Fact]
    public void CreateAndEmptyFlushDoNotEmitAndDisposalAborts()
    {
        using var stream = new CountingStream(); var w = Writer.Create(stream, new() { EmitByteOrderMark = true }, TestContext.Current.CancellationToken);
        Assert.Equal(0, stream.Length); w.Flush(); Assert.Equal(0, stream.Length); Assert.Equal(1, stream.Flushes);
        w.WriteStartElement("r"); w.Dispose(); w.Dispose(); Assert.Equal(3, stream.Length); Assert.Equal(1, stream.Flushes); Assert.True(stream.CanWrite);
        using var other = new CountingStream(); var a = Writer.Create(other, Omit, TestContext.Current.CancellationToken); a.WriteStartElement("r"); a.Flush(); a.Dispose();
        Assert.Equal("<r>", Encoding.UTF8.GetString(other.ToArray())); Assert.Equal(1, other.Flushes);
    }
    [Fact]
    public void CancellationIoAndReentrancyHavePermanentFaultRules()
    {
        using var cts = new CancellationTokenSource(); using var stream = new CountingStream(); using var w = Writer.Create(stream, Omit, cts.Token);
        w.WriteStartElement("r"); cts.Cancel(); Assert.Throws<OperationCanceledException>(() => w.WriteText("x")); Assert.Throws<InvalidOperationException>(w.Flush);
        using var broken = new CountingStream { Failure = new IOException("failure") }; using var writer = Writer.Create(broken, Omit, TestContext.Current.CancellationToken);
        writer.WriteStartElement("r"); writer.WriteEndElement(); Assert.Same(broken.Failure, Assert.Throws<IOException>(writer.WriteEndDocument));
        Assert.Throws<InvalidOperationException>(writer.Flush);
        using var reentrant = new CountingStream(); using var r = Writer.Create(reentrant, Omit, TestContext.Current.CancellationToken);
        int rejected = 0; reentrant.OnWrite = () => { Assert.Throws<InvalidOperationException>(r.Flush); Assert.Throws<InvalidOperationException>(r.Dispose); rejected++; };
        r.WriteStartElement("r"); r.WriteEndElement(); r.WriteEndDocument(); Assert.Equal(1, rejected);
    }
    [Fact]
    public void ChunkBoundariesLargeUnicodeAndLexicalValidationBeforeDelimiter()
    {
        string text = new string('a', 2047) + "😀" + new string('b', 10000) + "]]";
        Assert.Equal("<r>" + text + "&gt;</r>", Emit(w => { w.WriteStartElement("r"); w.WriteText(text); w.Flush(); w.WriteText(">"); w.WriteEndElement(); }));
        using var stream = new CountingStream(); using var writer = Writer.Create(stream, Omit, TestContext.Current.CancellationToken);
        writer.WriteStartElement("r"); writer.Flush(); long length = stream.Length;
        Assert.Throws<ArgumentException>(() => writer.WriteCData(new string('a', 2047) + "]]>"));
        Assert.Throws<ArgumentException>(() => writer.WriteComment(new string('a', 2047) + "--"));
        Assert.Equal(length, stream.Length); writer.WriteEndElement(); writer.WriteEndDocument();
    }
    [Fact]
    public void WarmBorrowedTextAndRepeatedNamesAllocateZero()
    {
        using var writer = Writer.Create(Stream.Null, Omit, TestContext.Current.CancellationToken);
        writer.WriteStartElement("r"); writer.WriteText("");
        for (int i = 0; i < 100; i++) { writer.WriteText("x&α😀]]"); writer.WriteText(">"); writer.WriteStartElement("x"); writer.WriteAttribute("a", "v"); writer.WriteEndElement(); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { writer.WriteText("x&α😀]]"); writer.WriteText(">"); writer.WriteStartElement("x"); writer.WriteAttribute("a", "v"); writer.WriteEndElement(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        writer.WriteEndElement(); writer.WriteEndDocument();
    }
    [Fact]
    public void DeepTraversalIsIterativeAndNamespaceScopesRestore()
    {
        using var writer = Writer.Create(Stream.Null, Omit, TestContext.Current.CancellationToken);
        for (int i = 0; i < 10000; i++) writer.WriteStartElement("r");
        for (int i = 0; i < 10000; i++) writer.WriteEndElement();
        writer.WriteEndDocument();
        Assert.Equal("<r xmlns:p=\"u\"><p:x xmlns:p=\"v\"/><p:y/></r>", Emit(w =>
        { w.WriteStartElement("r"); w.WriteNamespace("p", "u"); w.WriteStartElement("x", "v", "p"); w.WriteNamespace("p", "v"); w.WriteEndElement(); w.WriteStartElement("y", "u", "p"); w.WriteEndElement(); w.WriteEndElement(); }));
    }
    [Fact]
    public void CallerTextAndSealedAttributesAreNotRetained()
    {
        using var writer = Writer.Create(Stream.Null, Omit, TestContext.Current.CancellationToken);
        writer.WriteStartElement("r");
        var refs = SupplyTransientValues(writer);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(refs.Text.IsAlive); Assert.False(refs.Attribute.IsAlive);
        writer.WriteEndElement(); writer.WriteEndDocument(); GC.KeepAlive(writer);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Text, WeakReference Attribute) SupplyTransientValues(Writer writer)
    {
        string attribute = new('a', 1024 * 1024); writer.WriteAttribute("a", attribute);
        string text = new('t', 1024 * 1024); writer.WriteText(text); writer.Flush();
        return (new(text), new(attribute));
    }
    [Fact]
    public void OptionValidationAndCallerEncodingMutationAreIndependent()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => Writer.Create(null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Writer.Create(stream, new() { Encoding = null! }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Writer.Create(stream, new() { Encoding = Encoding.ASCII }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Writer.Create(stream, new() { NamespaceHandling = (XmlNamespaceHandling)9 }, TestContext.Current.CancellationToken));
        using var readOnly = new MemoryStream([], false);
        Assert.Throws<ArgumentException>(() => Writer.Create(readOnly, cancellationToken: TestContext.Current.CancellationToken));
        var encoding = (Encoding)new UTF8Encoding(false, false).Clone();
        using var w = Writer.Create(stream, Omit with { Encoding = encoding }, TestContext.Current.CancellationToken);
        encoding.EncoderFallback = EncoderFallback.ReplacementFallback;
        w.WriteStartElement("r"); Assert.Throws<ArgumentException>(() => w.WriteText("\udc00"));
        w.WriteText("ok"); w.WriteEndElement(); w.WriteEndDocument(); Assert.Equal("<r>ok</r>", Encoding.UTF8.GetString(stream.ToArray()));
    }
    private sealed class CountingStream : MemoryStream
    {
        internal int Flushes; internal Exception? Failure; internal Action? OnWrite;
        public override void Flush() { Flushes++; if (Failure is not null) throw Failure; base.Flush(); }
        public override void Write(ReadOnlySpan<byte> buffer)
        { OnWrite?.Invoke(); if (Failure is not null) throw Failure; base.Write(buffer); }
    }
}
