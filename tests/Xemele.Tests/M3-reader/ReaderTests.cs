using System.Text;
using Xemele.Streaming;
using Xunit;
using Reader = Xemele.Streaming.XmlReader;
namespace Xemele.Tests.ReaderRuntime;
public sealed class ReaderTests
{
    [Fact]
    public void CreateIsLazyAndEmptyMarkupHasSeparateEndAndEofTokens()
    {
        using var input = new CountingStream("<r a='v' xmlns='urn:r'/>", 1);
        using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, input.Reads); Assert.Equal(XmlTokenKind.None, reader.TokenKind);
        Assert.True(reader.Read()); Assert.Equal(XmlTokenKind.StartDocument, reader.TokenKind); Assert.Equal(0, input.Reads);
        Assert.True(reader.Read()); Assert.Equal(XmlTokenKind.StartElement, reader.TokenKind);
        Assert.Equal("r", reader.LocalName.ToString()); Assert.Equal("urn:r", reader.NamespaceUri.ToString()); Assert.True(reader.IsEmptyElement);
        Assert.Equal("v", reader.Attributes[0].Value.ToString()); Assert.Equal(1, reader.NamespaceDeclarations.Count);
        Assert.True(reader.ValueSpan.IsEmpty); Assert.True(reader.Declaration.Version.IsEmpty);
        Assert.True(reader.Read()); Assert.Equal(XmlTokenKind.EndElement, reader.TokenKind); Assert.Equal(0, reader.Attributes.Count); Assert.False(reader.IsEmptyElement);
        Assert.True(reader.Read()); Assert.Equal(XmlTokenKind.EndDocument, reader.TokenKind);
        Assert.False(reader.Read()); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.False(reader.Read());
        reader.Dispose(); Assert.True(input.CanRead); Assert.Throws<ObjectDisposedException>(() => reader.Read());
    }
    [Fact]
    public void AllFragmentSizesPreserveTextScalarsLexicalBoundariesAndLocations()
    {
        const string xml = "<?xml version='1.0'?><r>α😀\r\nβ<![CDATA[x😀]]><!--c--><?pi data?></r>";
        for (int chunk = 1; chunk <= Encoding.UTF8.GetByteCount(xml); chunk++)
        {
            using var input = new CountingStream(xml, chunk); using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
            var text = new StringBuilder(); var kinds = new List<XmlTokenKind>();
            while (reader.Read())
            {
                kinds.Add(reader.TokenKind); Assert.True(reader.Location.LineNumber >= 1); Assert.True(reader.Location.LinePosition >= 1); Assert.NotNull(reader.Location.ByteOffset);
                if (reader.TokenKind == XmlTokenKind.Text) { var value = reader.ValueSpan; Assert.False(value.Length > 0 && char.IsLowSurrogate(value[0])); Assert.False(value.Length > 0 && char.IsHighSurrogate(value[^1])); text.Append(value); }
                if (reader.TokenKind == XmlTokenKind.Comment) { Assert.Equal("c", reader.ValueSpan.ToString()); Assert.True(reader.Target.IsEmpty); }
                if (reader.TokenKind == XmlTokenKind.ProcessingInstruction) { Assert.Equal("pi", reader.Target.ToString()); Assert.Equal("data", reader.ValueSpan.ToString()); }
                if (reader.TokenKind == XmlTokenKind.Declaration) Assert.Equal("1.0", reader.Declaration.Version.ToString());
            }
            Assert.Equal("α😀\nβx😀", text.ToString());
            Assert.Equal(new[] { XmlTokenKind.StartDocument, XmlTokenKind.Declaration, XmlTokenKind.StartElement, XmlTokenKind.StartCData, XmlTokenKind.EndCData, XmlTokenKind.Comment, XmlTokenKind.ProcessingInstruction, XmlTokenKind.EndElement, XmlTokenKind.EndDocument }, kinds.Where(k => k != XmlTokenKind.Text));
        }
    }
    [Theory]
    [InlineData("<r a='v'>", "XML.Syntax")]
    [InlineData("<!DOCTYPE r><r/>", "XML.DtdProhibited")]
    public void FatalThrowsImmediatelyAndOwnsDiagnostics(string xml, string code)
    {
        using var input = new CountingStream(xml, 2); using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
        XmlParseException? failure = null;
        try { while (reader.Read()) Assert.NotEqual(XmlTokenKind.Diagnostic, reader.TokenKind); } catch (XmlParseException e) { failure = e; }
        Assert.NotNull(failure); Assert.Equal(code, failure.Diagnostics[^1].Code);
        Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.True(reader.LocalName.IsEmpty); Assert.Equal(0, reader.Attributes.Count);
        Assert.Throws<InvalidOperationException>(() => reader.Read()); reader.Dispose(); Assert.Equal(code, failure.Diagnostics[^1].Code); Assert.True(input.CanRead);
    }
    [Fact]
    public void CancellationInvalidatesAtInvocationAndFaultsPermanently()
    {
        using var input = new CountingStream("<r/>", 4096); using var cancel = new CancellationTokenSource(); using var reader = Reader.Create(input, cancellationToken: cancel.Token);
        Assert.True(reader.Read()); Assert.True(reader.Read()); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => reader.Read()); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.True(reader.LocalName.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => reader.Read()); Assert.True(input.CanRead);
    }
    [Fact]
    public void IoFailureIdentityAndReentrantReadDisposeGuards()
    {
        var error = new IOException("sentinel"); using var input = new CountingStream("<r/>", 4096); using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
        input.BeforeRead = () => { Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Throws<InvalidOperationException>(() => reader.Read()); Assert.Throws<InvalidOperationException>(() => reader.Dispose()); throw error; };
        Assert.True(reader.Read()); Assert.Same(error, Assert.Throws<IOException>(() => reader.Read())); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Throws<InvalidOperationException>(() => reader.Read());
    }
    [Fact]
    public void EffectiveDtdDefaultAndUnresolvedViewsRemainAvailableBetweenReads()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'v'>]><r b='x&unknown;y'>&unknown;</r>";
        using var input = new CountingStream(xml, 1); using var reader = Reader.Create(input, new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        bool dtd = false, attr = false, skipped = false;
        while (reader.Read())
        {
            if (reader.TokenKind == XmlTokenKind.StartDtd) { dtd = true; Assert.False(reader.DocumentType.HasSystemId); Assert.True(reader.DocumentType.SystemId.IsEmpty); }
            if (reader.TokenKind == XmlTokenKind.DtdDeclaration) Assert.False(reader.DtdDeclaration.Name.IsEmpty);
            if (reader.TokenKind == XmlTokenKind.StartElement)
            {
                attr = true; Assert.Equal(2, reader.Attributes.Count); var b = reader.Attributes[0]; Assert.False(b.IsValueComplete); Assert.True(b.Value.IsEmpty); Assert.Equal(3, b.ValueParts.Length);
                var a = reader.Attributes[1]; Assert.True(a.IsDefaulted); Assert.Equal("v", a.Value.ToString()); Assert.Equal(reader.Location, a.Location);
            }
            if (reader.TokenKind == XmlTokenKind.SkippedEntity) { skipped = true; Assert.Equal("&unknown;", reader.SkippedEntity.LexicalReference.ToString()); }
        }
        Assert.True(dtd && attr && skipped);
    }
    [Fact]
    public void ValidateBeforeInputAndAbortDoesNotComplete()
    {
        using var input = new CountingStream("<r/>", 1);
        Assert.Throws<ArgumentNullException>(() => Reader.Create(null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Reader.Create(input, new() { Recover = true }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reader.Create(input, new() { Limits = new() { Depth = 0 } }, TestContext.Current.CancellationToken));
        Assert.Equal(0, input.Reads); var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken); reader.Read(); reader.Dispose(); reader.Dispose(); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.Equal(0, input.Reads); Assert.True(input.CanRead);
    }
    [Fact]
    public void LongLexicalBodiesAreCompleteAndCurrentCopiesSurviveTransportRefills()
    {
        string body = new string('Ω', 10000) + "😀";
        using var input = new CountingStream("<r><!--" + body + "--><?p " + body + "?></r>", 7);
        using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
        int found = 0;
        while (reader.Read()) if (reader.TokenKind is XmlTokenKind.Comment or XmlTokenKind.ProcessingInstruction)
        {
            Assert.Equal(body, reader.ValueSpan.ToString()); Assert.Equal(body, reader.ValueSpan.ToString());
            Assert.True(reader.LocalName.IsEmpty); Assert.Equal(0, reader.Attributes.Count); found++;
        }
        Assert.Equal(2, found);
    }
    [Fact]
    public void ReaderDoesNotReadAheadToDocumentEndAndOwnedConsumerCopiesRemainStable()
    {
        using var input = new CountingStream("<r a='value'>" + new string('x', 100000) + "</r>", 4096);
        using var reader = Reader.Create(input, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(reader.Read()); Assert.True(reader.Read()); Assert.True(input.Position <= 4096);
        var owned = reader.Attributes[0].Value.ToString(); var location = reader.Location;
        Assert.Equal(new XmlLocation(null, 1, 1, 0), location);
        Assert.True(reader.Read()); Assert.Equal(XmlTokenKind.Text, reader.TokenKind); Assert.Equal(0, reader.Attributes.Count); Assert.Equal("value", owned);
        reader.Dispose(); Assert.Equal(XmlTokenKind.None, reader.TokenKind); Assert.True(reader.ValueSpan.IsEmpty); Assert.True(input.Position < input.Length);
    }
    private sealed class CountingStream(string xml, int chunk) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal int Reads;
        internal Action? BeforeRead;
        public override int Read(Span<byte> buffer) { Reads++; BeforeRead?.Invoke(); return base.Read(buffer[..Math.Min(chunk, buffer.Length)]); }
    }
}
