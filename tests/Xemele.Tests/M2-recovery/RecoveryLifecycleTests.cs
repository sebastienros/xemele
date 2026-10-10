using System.Text;
using Xemele.Parsing.Recovery;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.Recovery;

public sealed class RecoveryLifecycleTests
{
    private static XmlParseOptions Options(XmlResourceResolver? resolver = null) => new() { Recover = true, DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
    [Fact]
    public void ImpliedCloseDeliveryCanPauseWithoutReplayAndFatalDoesNotResume()
    {
        using var core = new XmlParserCore(Options()); var errors = new RecoveryDiagnostics(128); core.EnableRecovery(errors);
        var sink = new Sink { Pause = true }; const string input = "<r><a></r>"; int consumed = 0;
        ScanResult result;
        do { result = core.Append(input.AsSpan(consumed), [], true, sink, TestContext.Current.CancellationToken); consumed += result.Consumed; } while (result.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, result.Step); Assert.Equal(input.Length, consumed);
        Assert.Equal(new[] { "+r", "+a", "-a", "-r" }, sink.Elements); Assert.Equal(1, sink.DocumentStarts); Assert.Equal(1, sink.DocumentEnds); Assert.Single(errors.Items);
        using var fatal = new XmlParserCore(Options()); fatal.EnableRecovery(new(128)); var rejectSink = new Sink();
        result = fatal.Append("<r><bad a=broken>", [], true, rejectSink, TestContext.Current.CancellationToken);
        Assert.Equal(ScanStep.Rejected, result.Step); Assert.Equal(new[] { "+r" }, rejectSink.Elements);
        Assert.Throws<InvalidOperationException>(() => fatal.Append([], [], true, rejectSink, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void EofClosuresAndDiscardedTreesHaveExactCallbacks()
    {
        using var core = new XmlParserCore(Options()); core.EnableRecovery(new(128)); var sink = new Sink { Pause = true };
        const string input = "<r/><s><a/><!--c--><?p?>text</s>"; int consumed = 0; ScanResult result;
        do { result = core.Append(input.AsSpan(consumed), [], true, sink, TestContext.Current.CancellationToken); consumed += result.Consumed; } while (result.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, result.Step); Assert.Equal(new[] { "+r", "-r" }, sink.Elements); Assert.Equal(0, sink.TextCalls);
        using var eof = new XmlParserCore(Options()); eof.EnableRecovery(new(128)); sink = new Sink { Pause = true }; consumed = 0;
        do { result = eof.Append("<r><a>".AsSpan(consumed), [], true, sink, TestContext.Current.CancellationToken); consumed += result.Consumed; } while (result.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, result.Step); Assert.Equal(new[] { "+r", "+a", "-a", "-r" }, sink.Elements);
    }
    [Fact]
    public void CallbackExceptionsAndCancellationFaultAndPropagateUnchanged()
    {
        var exception = new XmlParseException([new("USER", XmlDiagnosticSeverity.Error, "callback", default)]);
        using var core = new XmlParserCore(Options()); core.EnableRecovery(new(128)); var sink = new Sink { Hook = () => throw exception };
        Assert.Same(exception, Assert.Throws<XmlParseException>(() => core.Append("<r>", [], true, sink, TestContext.Current.CancellationToken)));
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource(); using var cancelCore = new XmlParserCore(Options()); cancelCore.EnableRecovery(new(128));
        sink = new Sink { Hook = canceled.Cancel };
        Assert.Throws<OperationCanceledException>(() => cancelCore.Append("<r><a>", [], true, sink, canceled.Token));
        Assert.Throws<InvalidOperationException>(() => cancelCore.Append([], [], true, sink, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("<!DOCTYPE r [<!ENTITY e '<a>'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY e '</r>'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY e '<a></r>'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY e '&e;'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:missing'>]><r>&e;</r>")]
    public void EntityBoundariesCyclesAndRefusedResourcesRemainFatal(string input)
    {
        var result = XmlDocument.ParseWithDiagnostics(input, Options(), TestContext.Current.CancellationToken);
        Assert.False(result.IsComplete); Assert.False(result.IsWellFormed); Assert.NotNull(result.Value); Assert.NotEmpty(result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code.StartsWith("XML.Recovery."));
    }
    [Fact]
    public void ExternalResourcesKeepMappingsFinalUriOwnershipAndCallbackExceptionProvenance()
    {
        const string input = "<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:external'>]><r>&e;</r>";
        var resolver = new Resolver("<a></missing></a>");
        var result = XmlDocument.ParseWithDiagnostics(input, Options(resolver), TestContext.Current.CancellationToken);
        Assert.True(result.IsComplete); Assert.False(result.IsWellFormed); Assert.Equal(1, resolver.Disposals); Assert.Equal(1, resolver.Opens);
        var location = Assert.Single(result.Diagnostics).Location;
        Assert.Equal(new XmlLocation(new Uri("https://offline.invalid/final.xml"), 1, 4, 3), location);
        resolver = new Resolver("<a>");
        result = XmlDocument.ParseWithDiagnostics(input, Options(resolver), TestContext.Current.CancellationToken);
        Assert.False(result.IsComplete); Assert.Equal(1, resolver.Disposals);
        resolver = new Resolver("<a/>"); var options = Options(resolver) with { Limits = new() { ExternalBytes = 3 } };
        result = XmlDocument.ParseWithDiagnostics(input, options, TestContext.Current.CancellationToken);
        Assert.False(result.IsComplete); Assert.Equal(1, resolver.Disposals);
        var exception = new XmlParseException([new("USER", XmlDiagnosticSeverity.Error, "resolver", default)]);
        resolver = new Resolver("<a/>") { Error = exception };
        Assert.Same(exception, Assert.Throws<XmlParseException>(() => XmlDocument.ParseWithDiagnostics(input, Options(resolver), TestContext.Current.CancellationToken)));
        Assert.Throws<ArgumentException>(() => XmlDocument.Parse(input, Options(resolver), TestContext.Current.CancellationToken)); Assert.Equal(1, resolver.Opens);
    }
    [Fact]
    public void ChunkedStreamsRemainOpenAndDtdMetadataComposesWithRecovery()
    {
        const string input = "<?xml version='1.0'?><!DOCTYPE r [<!ENTITY e 'text'><!ATTLIST r a CDATA 'default'>]><r>&e;";
        foreach (int size in new[] { 1, 2, 3, 7, 4096 })
        {
            using var stream = new Bounded(Encoding.UTF8.GetBytes(input), size);
            var result = XmlDocument.LoadWithDiagnostics(stream, Options(), TestContext.Current.CancellationToken);
            Assert.True(result.IsComplete); Assert.NotNull(result.Value!.Dtd); Assert.Equal("text", result.Value.RootElement.GetTextContent()); Assert.Equal("default", result.Value.RootElement.GetAttribute("a"));
            Assert.True(stream.CanRead); Assert.Equal(0, stream.Disposals); Assert.Single(result.Diagnostics);
        }
    }
    [Fact]
    public void DiscardedRootsRetainNeitherUnknownContentNorSymbolicAttributes()
    {
        const string input = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST s a CDATA '&unknown;'>]><r/><s>&missing;</s>";
        var result = XmlDocument.ParseWithDiagnostics(input, Options(), TestContext.Current.CancellationToken);
        Assert.True(result.IsComplete); Assert.False(result.IsWellFormed); Assert.Single(result.Diagnostics);
        Assert.NotNull(result.Value); Assert.Empty(result.Value.UnresolvedReferences); Assert.Empty(result.Value.IncompleteAttributeValues);
        Assert.True(result.Value.RootElement.NextSibling.IsDefault);
        Assert.Equal(0, result.Value.RootElement.Children.Count);
    }
    private sealed class Bounded(byte[] bytes, int size) : MemoryStream(bytes, false)
    {
        internal int Disposals;
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(size, buffer.Length)]);
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
    private sealed class Resolver(string body) : XmlResourceResolver
    {
        internal int Opens, Disposals; internal Exception? Error;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Opens++; if (Error is not null) throw Error;
            return new(new Tracked(Encoding.UTF8.GetBytes(body), this), new Uri("https://offline.invalid/final.xml"));
        }
        private sealed class Tracked(byte[] bytes, Resolver owner) : MemoryStream(bytes, false)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
    internal sealed class Sink : IXmlEventSink
    {
        internal readonly List<string> Elements = []; internal bool Pause; internal Action? Hook; internal int DocumentStarts, DocumentEnds, TextCalls;
        private bool Next() { Hook?.Invoke(); return !Pause; }
        public bool StartDocument(XmlLocation l) { DocumentStarts++; return Next(); }
        public bool Declaration(XmlDeclarationView d) => Next();
        public bool StartElement(XmlNameView n, bool empty, XmlLocation l) { Elements.Add("+" + n.LocalName.ToString()); return Next(); }
        public bool NamespaceDeclaration(XmlNamespaceView n) => Next();
        public bool Attribute(XmlAttributeView a) => Next();
        public bool StartElementContent() => Next();
        public bool EndElement(XmlNameView n, XmlLocation l) { Elements.Add("-" + n.LocalName.ToString()); return Next(); }
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) { TextCalls++; return Next(); }
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) { TextCalls++; return Next(); }
        public bool EndDocument(XmlLocation l) { DocumentEnds++; return Next(); }
    }
}
