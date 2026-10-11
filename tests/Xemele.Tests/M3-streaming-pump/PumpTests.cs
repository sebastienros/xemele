using System.Text;
using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.StreamingPump;

public sealed class PumpTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly Uri Source = new("https://example.test/document.xml");
    private const string Document = "<?xml version='1.0'?>\r\n<!DOCTYPE r [<!ELEMENT r (#PCDATA|c)*><!ELEMENT c EMPTY><!ATTLIST r a CDATA 'default'><!ENTITY e 'β😀'><!ENTITY % p ''><!NOTATION n SYSTEM ''>]><r xmlns='urn:r' xmlns:p='urn:p' p:b='B' a='authored'>A&e;<![CDATA[]]><![CDATA[x😀]]><!--aβ--><?pi aβ?><c/></r>";
    private static XmlParseOptions Options => new() { DtdProcessing = XmlDtdProcessing.Parse };

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public void AllByteSplitsAndEveryEventPausePreserveProjectionAndLocations(string encoding)
    {
        byte[] bytes = Encode(Document, encoding);
        var baseline = Parse(bytes, bytes.Length, false);
        foreach (bool pause in new[] { false, true })
        for (int split = 0; split <= bytes.Length; split++)
        {
            var actual = Parse(bytes, split, pause);
            Assert.Equal(baseline.Events, actual.Events); Assert.Equal(baseline.Locations, actual.Locations);
        }
        Assert.Equal("document", baseline.Events[0]); Assert.Equal("done", baseline.Events[^1]);
        Assert.Equal(2, baseline.Events.Count(x => x == "cdata:start")); Assert.Equal(2, baseline.Events.Count(x => x == "cdata:end"));
        Assert.Contains("comment:aβ", baseline.Events); Assert.Contains("pi:pi:aβ", baseline.Events);
        Assert.Contains("start:r::urn:r:False:b/p/urn:p=B/authored;a//=authored/authored:ns:=urn:r;p=urn:p", baseline.Events);
        int end = baseline.Events.IndexOf("dtd:end"); int closing = Document.IndexOf("]>", StringComparison.Ordinal) + 1;
        Assert.Equal(2, baseline.Locations[end].LineNumber); Assert.Equal(ScalarCount(Document.AsSpan(Document.IndexOf('\n') + 1, closing - Document.IndexOf('\n') - 1)) + 1, baseline.Locations[end].LinePosition);
        int bom = encoding == "utf8" ? 0 : 2;
        int expectedOffset = bom + (encoding == "utf8" ? Encoding.UTF8.GetByteCount(Document.AsSpan(0, closing)) : closing * 2);
        Assert.Equal(expectedOffset, baseline.Locations[end].ByteOffset);
    }
    [Fact]
    public void CharacterSplitsPreserveOffsetsAndAvoidEncodingRoundtrip()
    {
        var baseline = ParseCharacters(Document, Document.Length, false);
        for (int split = 0; split <= Document.Length; split++)
        {
            var actual = ParseCharacters(Document, split, true);
            Assert.Equal(baseline.Events, actual.Events); Assert.Equal(baseline.Locations, actual.Locations);
            Assert.All(actual.Locations, x => Assert.Null(x.ByteOffset));
        }
    }
    [Fact]
    public void DefaultsAndIncompletePartsRetainProvenanceAndSkippedReferencePosition()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a NMTOKENS ' before &unknown; after ' b CDATA 'same'>]><r b='same'>left&missing;right</r>";
        var input = Encoding.UTF8.GetBytes(xml);
        var sink = Parse(input, 17, true);
        for (int split = 0; split <= input.Length; split++) Assert.Equal(sink.Events, Parse(input, split, true).Events);
        Assert.Single(sink.Events, x => x.StartsWith("incomplete-default:", StringComparison.Ordinal));
        Assert.Contains(sink.Events, x => x.Contains("a//=incomplete/default", StringComparison.Ordinal));
        Assert.Contains(sink.Events, x => x.Contains("b//=same/authored", StringComparison.Ordinal));
        Assert.Contains("skipped:missing:&missing;", sink.Events);
        Assert.Equal(" before |&unknown;| after ", sink.IncompleteParts);
        int opening = xml.IndexOf("<r ", StringComparison.Ordinal);
        Assert.Equal(new XmlLocation(Source, 1, opening + 1, opening), sink.IncompleteLocation);
        int unknown = xml.IndexOf("&unknown;", StringComparison.Ordinal);
        Assert.Equal(new XmlLocation(Source, 1, unknown + 1, unknown), sink.UnknownPartLocation);
        var defaulted = ParseCharacters("<!DOCTYPE r [<!ATTLIST r b CDATA 'same'>]><r/>", 0, true);
        Assert.Contains(defaulted.Events, x => x.Contains("b//=same/default", StringComparison.Ordinal));
    }
    [Fact]
    public void EffectiveDtdProjectionUsesCollectionOrderAndIterativeModels()
    {
        const string xml = "<!DOCTYPE r [<!ELEMENT r ((a,b?)|c+)><!ELEMENT a EMPTY><!ELEMENT b ANY><!ELEMENT c (#PCDATA)><!ATTLIST r x (a|b) #IMPLIED n NOTATION (n|m) #REQUIRED f CDATA #FIXED 'v'><!ENTITY e 'E'><!ENTITY ext SYSTEM 'e' NDATA n><!ENTITY % p 'P'><!NOTATION n PUBLIC 'n'><!NOTATION m SYSTEM ''>]><r/>";
        var sink = ParseCharacters(xml, 31, true);
        var declarations = sink.Events.Where(x => x.StartsWith("dtd:", StringComparison.Ordinal) && x != "dtd:start:r" && x != "dtd:end").ToArray();
        Assert.Equal(new[] {
            "dtd:Element:r:((a,b?)|c+):None::False:False", "dtd:Element:a:EMPTY:None::False:False",
            "dtd:Element:b:ANY:None::False:False", "dtd:Element:c:(#PCDATA):None::False:False",
            "dtd:Attribute:x:(a|b):Implied::False:False", "dtd:Attribute:n:NOTATION (n|m):Required::False:False",
            "dtd:Attribute:f:CDATA:Fixed:v:True:False", "dtd:Entity:e::None:E:True:False",
            "dtd:Entity:ext::None::False:False", "dtd:Entity:p::None:P:True:False",
            "dtd:Notation:n::None::False:", "dtd:Notation:m::None::False:" }, declarations);
    }
    [Theory]
    [InlineData("ISO-8859-1", "é")]
    [InlineData("WINDOWS-1252", "€")]
    [InlineData("US-ASCII", "a")]
    public void LegacyDeclarationSwitchOccursBeforeBodyDecoding(string encoding, string body)
    {
        string declaration = $"<?xml version='1.0' encoding='{encoding}'?><r>";
        var bytes = Encoding.ASCII.GetBytes(declaration).Concat(new[] { encoding == "WINDOWS-1252" ? (byte)0x80 : encoding == "ISO-8859-1" ? (byte)0xe9 : (byte)'a' }).Concat("</r>"u8.ToArray()).ToArray();
        for (int split = 0; split <= bytes.Length; split++) Assert.Contains("text:" + body, Parse(bytes, split, true).Events);
    }
    [Theory]
    [InlineData("<r>", "XML.Syntax")]
    [InlineData("<!DOCTYPE r><r/>", "XML.DtdProhibited")]
    [InlineData("<?xml version='1.0' encoding='unknown'?><r/>", "INPUT.UnsupportedEncoding")]
    public void FatalDiagnosticIsAcceptedOnceBeforeStickyRejection(string xml, string code)
    {
        using var pump = new XmlStreamingPump(new()); var sink = new RecordingSink { Pause = true };
        pump.Supply(Encoding.UTF8.GetBytes(xml), true);
        Assert.Equal(StreamingStep.Rejected, Drain(pump, sink));
        Assert.Equal(code, pump.Diagnostic!.Code); Assert.Single(sink.Events, x => x.StartsWith("diagnostic:", StringComparison.Ordinal));
        Assert.DoesNotContain("done", sink.Events); Assert.Equal(StreamingStep.Rejected, pump.Advance(sink, Ct));
        Assert.Throws<InvalidOperationException>(() => pump.Supply(default, true));
    }
    [Fact]
    public void DecodeFailureDrainsValidPrefixThenDiagnostic()
    {
        using var pump = new XmlStreamingPump(new()); var sink = new RecordingSink { Pause = true };
        pump.Supply("<r>a"u8.ToArray().Concat(new byte[] { 0xff }).ToArray(), true);
        Assert.Equal(StreamingStep.Rejected, Drain(pump, sink));
        Assert.Contains("text:a", sink.Events); Assert.Single(sink.Events, x => x.StartsWith("diagnostic:", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(new byte[] { 0xef, 0xbb, 0xbf, 60, 0xff })]
    [InlineData(new byte[] { 0xc3, 0xa9, 0xff })]
    [InlineData(new byte[] { 0xff })]
    public void InvalidEncodingDuringDeclarationDetectionRejectsWithoutDecoderMisuse(byte[] input)
    {
        using var pump = new XmlStreamingPump(new()); var sink = new RecordingSink { Pause = true };
        pump.Supply(input, true);
        Assert.Equal(StreamingStep.Rejected, Drain(pump, sink)); Assert.NotNull(pump.Diagnostic);
        Assert.Single(sink.Events, x => x.StartsWith("diagnostic:", StringComparison.Ordinal));
    }
    [Fact]
    public void CdataDelimiterLocationsAreOriginalAcrossUnicodeAndCrLf()
    {
        const string xml = "<r>β😀\r\n<![CDATA[x]]><![CDATA[]]></r>";
        var sink = ParseCharacters(xml, 6, true);
        int first = sink.Events.IndexOf("cdata:start"); int end = sink.Events.IndexOf("cdata:end");
        Assert.Equal(new XmlLocation(Source, 2, 1, null), sink.Locations[first]);
        Assert.Equal(new XmlLocation(Source, 2, 13, null), sink.Locations[end]);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RecoveryFatalEncodingDiagnosticSnapshotMatchesNotificationIdentityAndOrder(int fixture)
    {
        byte[] input = fixture switch
        {
            0 => new byte[] { 0xff },
            1 => Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='unknown'?><r/>"),
            _ => Encoding.UTF8.GetBytes("<r a='1' a='2'>ok").Concat(new byte[] { 0xff }).ToArray()
        };
        using var pump = new XmlStreamingPump(new() { Recover = true }); var sink = new RecordingSink { Pause = true };
        pump.Supply(input, true);
        Assert.Equal(StreamingStep.Rejected, Drain(pump, sink));
        Assert.Equal(fixture == 2 ? 2 : 1, sink.DiagnosticValues.Count);
        Assert.Equal(sink.DiagnosticValues.Count, pump.Diagnostics.Count);
        for (int i = 0; i < pump.Diagnostics.Count; i++) Assert.Same(sink.DiagnosticValues[i], pump.Diagnostics[i]);
        Assert.Same(pump.Diagnostic, pump.Diagnostics[^1]); Assert.DoesNotContain("done", sink.Events);
        if (fixture == 2) Assert.Equal("XML.Recovery.DuplicateAttribute", pump.Diagnostics[0].Code);
    }
    [Fact]
    public void RecoverySuppressesSuccessfulEndAndDeliversDiagnosticsOnceAcrossPauses()
    {
        using var pump = new XmlStreamingPump(new() { Recover = true }, characterInput: true); var sink = new RecordingSink { Pause = true };
        pump.SupplyCharacters("<r a='first' a='second'><c></r>".AsMemory(), true);
        Assert.Equal(StreamingStep.Completed, Drain(pump, sink)); Assert.False(pump.IsWellFormed);
        Assert.DoesNotContain("done", sink.Events); Assert.Equal(2, sink.Events.Count(x => x.StartsWith("diagnostic:", StringComparison.Ordinal)));
        Assert.Equal(StreamingStep.Completed, pump.Advance(sink, Ct));
    }
    [Fact]
    public void SupplyIdentityTerminalAndReentrantGuardsAreStable()
    {
        var pump = new XmlStreamingPump(new()); var sink = new RecordingSink { Pause = true };
        pump.Supply("<r/>"u8.ToArray(), true); Assert.Throws<InvalidOperationException>(() => pump.Supply(default, false));
        Assert.Equal(StreamingStep.Paused, pump.Advance(sink, Ct));
        Assert.Throws<InvalidOperationException>(() => pump.Advance(new RecordingSink(), Ct));
        sink.OnText = () => pump.Advance(sink, Ct);
        Assert.Equal(StreamingStep.Completed, Drain(pump, sink));
        Assert.Equal(StreamingStep.Completed, pump.Advance(sink, Ct)); pump.Dispose(); pump.Dispose();
        Assert.Throws<ObjectDisposedException>(() => pump.Advance(sink, Ct));
        using var reentrant = new XmlStreamingPump(new()); var s = new RecordingSink { OnText = () => reentrant.Dispose() };
        reentrant.Supply("<r>x</r>"u8.ToArray(), true);
        Assert.Throws<InvalidOperationException>(() => reentrant.Advance(s, Ct));
        Assert.Throws<InvalidOperationException>(() => reentrant.Advance(s, Ct));
    }
    [Fact]
    public void CallbackAndCancellationFaultWithoutDiagnosticsOrReplay()
    {
        using var pump = new XmlStreamingPump(new()); var error = new XmlParseException(new[] { new XmlDiagnostic("custom", XmlDiagnosticSeverity.Fatal, "callback", new()) });
        var sink = new RecordingSink { OnText = () => throw error }; pump.Supply("<r>x</r>"u8.ToArray(), true);
        Assert.Same(error, Assert.Throws<XmlParseException>(() => pump.Advance(sink, Ct))); Assert.Null(pump.Diagnostic);
        Assert.Throws<InvalidOperationException>(() => pump.Advance(sink, Ct)); Assert.DoesNotContain("done", sink.Events);
        using var canceled = new XmlStreamingPump(new()); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Advance(new RecordingSink(), cts.Token));
        Assert.Throws<InvalidOperationException>(() => canceled.Advance(new RecordingSink(), Ct));
    }
    [Fact]
    public void InvalidOptionsRejectBeforeAnyNotification()
    {
        Assert.Throws<ArgumentNullException>(() => new XmlStreamingPump(new() { Limits = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XmlStreamingPump(new() { Limits = new() { Depth = 0 } }));
    }
    [Fact]
    public void LongCommentAndPiRemainSingleCompleteTokensAtEveryBoundary()
    {
        string body = new('a', 10000); string xml = "<r><!--" + body + "--><?pi " + body + "?></r>";
        using var pump = new XmlStreamingPump(new()); var sink = new RecordingSink { Pause = true }; byte[] bytes = Encoding.UTF8.GetBytes(xml);
        for (int start = 0; start < bytes.Length; start += 7)
        {
            int count = Math.Min(7, bytes.Length - start); pump.Supply(bytes.AsMemory(start, count), start + count == bytes.Length);
            Assert.Equal(start + count == bytes.Length ? StreamingStep.Completed : StreamingStep.NeedInput, Drain(pump, sink));
        }
        Assert.Single(sink.Events, x => x == "comment:" + body); Assert.Single(sink.Events, x => x == "pi:pi:" + body);
    }
    private static int ScalarCount(ReadOnlySpan<char> value) { int count = 0; foreach (var rune in value.EnumerateRunes()) count++; return count; }
    private static byte[] Encode(string xml, string name) => name == "utf8" ? Encoding.UTF8.GetBytes(xml) :
        (name == "utf16le" ? Encoding.Unicode : Encoding.BigEndianUnicode).GetPreamble().Concat((name == "utf16le" ? Encoding.Unicode : Encoding.BigEndianUnicode).GetBytes(xml)).ToArray();
    private static RecordingSink Parse(byte[] bytes, int split, bool pause)
    {
        using var pump = new XmlStreamingPump(Options, Source); var sink = new RecordingSink { Pause = pause };
        pump.Supply(bytes.AsMemory(0, split), false); Assert.Equal(StreamingStep.NeedInput, Drain(pump, sink));
        pump.Supply(bytes.AsMemory(split), true); Assert.Equal(StreamingStep.Completed, Drain(pump, sink)); return sink;
    }
    private static RecordingSink ParseCharacters(string text, int split, bool pause)
    {
        using var pump = new XmlStreamingPump(Options, Source, characterInput: true); var sink = new RecordingSink { Pause = pause };
        pump.SupplyCharacters(text.AsMemory(0, split), false); Assert.Equal(StreamingStep.NeedInput, Drain(pump, sink));
        pump.SupplyCharacters(text.AsMemory(split), true); Assert.Equal(StreamingStep.Completed, Drain(pump, sink)); return sink;
    }
    internal static StreamingStep Drain(XmlStreamingPump pump, RecordingSink sink)
    {
        for (int i = 0; i < 100000; i++) { var step = pump.Advance(sink, Ct); if (step != StreamingStep.Paused) return step; }
        throw new InvalidOperationException("Non-progressing pump.");
    }
    internal sealed class RecordingSink : XmlStreamingSink
    {
        internal bool Pause;
        internal Action? OnText;
        internal readonly List<string> Events = [];
        internal readonly List<XmlLocation> Locations = [];
        internal string IncompleteParts = "";
        internal XmlLocation IncompleteLocation, UnknownPartLocation;
        internal readonly List<XmlDiagnostic> DiagnosticValues = [];
        private bool Add(string value, XmlLocation location) { Events.Add(value); Locations.Add(location); return !Pause; }
        public override bool StartDocument(XmlLocation location) => Add("document", location);
        public override bool EndDocument(XmlLocation location) => Add("done", location);
        public override bool Declaration(XmlDeclaration value) => Add("declaration:" + value.Version.ToString(), value.Location);
        public override bool StartElement(XmlStartElement value)
        {
            var attrs = new List<string>(); var ns = new List<string>();
            foreach (var a in value.Attributes)
            {
                attrs.Add($"{a.LocalName}/{a.Prefix}/{a.NamespaceUri}={(a.IsValueComplete ? a.Value.ToString() : "incomplete")}/{(a.IsDefaulted ? "default" : "authored")}");
                if (!a.IsValueComplete)
                {
                    IncompleteLocation = a.Location;
                    foreach (var part in a.ValueParts) if (part is XmlUnknownAttributeReference) UnknownPartLocation = part.Location;
                }
                if (!a.IsValueComplete) IncompleteParts = string.Join('|', a.ValueParts.ToArray().Select(p => p is XmlKnownAttributeText known ? known.Text : ((XmlUnknownAttributeReference)p).LexicalReference));
            }
            foreach (var n in value.NamespaceDeclarations) ns.Add(n.Prefix.ToString() + "=" + n.NamespaceUri.ToString());
            return Add($"start:{value.LocalName}:{value.Prefix}:{value.NamespaceUri}:{value.IsEmptyElement}:{string.Join(';', attrs)}:ns:{string.Join(';', ns)}", value.Location);
        }
        public override bool EndElement(XmlEndElement value) => Add("end:" + value.LocalName.ToString(), value.Location);
        public override bool Text(ReadOnlySpan<char> value, XmlLocation location) { OnText?.Invoke(); return Add("text:" + value.ToString(), location); }
        public override bool Comment(ReadOnlySpan<char> value, XmlLocation location) => Add("comment:" + value.ToString(), location);
        public override bool ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> value, XmlLocation location) => Add("pi:" + target.ToString() + ":" + value.ToString(), location);
        public override bool StartCData(XmlLocation location) => Add("cdata:start", location);
        public override bool EndCData(XmlLocation location) => Add("cdata:end", location);
        public override bool StartDtd(XmlDocumentTypeView value) => Add("dtd:start:" + value.Name.ToString(), value.Location);
        public override bool EndDtd(XmlLocation location) => Add("dtd:end", location);
        public override bool DtdDeclaration(XmlDtdDeclaration value) => Add($"dtd:{value.Kind}:{value.Name}:{(value.Kind == XmlDtdDeclarationKind.Element ? value.ContentModel : value.AttributeType)}:{value.DefaultKind}:{value.Value}:{value.HasValue}:{value.IsExternal}", value.Location);
        public override bool SkippedEntity(XmlSkippedEntity value) => Add("skipped:" + value.Name.ToString() + ":" + value.LexicalReference.ToString(), value.Location);
        public override bool IncompleteAttributeDefault(XmlIncompleteAttributeDefault value) => Add("incomplete-default:" + value.AttributeName, value.DeclarationLocation);
        public override bool Diagnostic(XmlDiagnostic value) { DiagnosticValues.Add(value); return Add("diagnostic:" + value.Code, value.Location); }
    }
}
