using System.Text;
using Xemele.Parsing.Recovery;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.Recovery;

public sealed class RecoveryTests
{
    private static readonly XmlParseOptions Recover = new() { Recover = true };
    public static IEnumerable<object[]> Repairs()
    {
        foreach (var (input, output, errors) in new (string, string, int)[]
        {
            ("<r>", "<r/>", 1), ("<r><a>text", "<r><a>text</a></r>", 2),
            ("<r><a><b/></r>", "<r><a><b/></a></r>", 1),
            ("</missing><r/>", "<r/>", 1), ("<r></missing></r>", "<r/>", 1),
            ("<r a='1' a='2'/>", "<r a=\"1\"/>", 1),
            ("<r xmlns:p='u' xmlns:q='u' p:a='1' q:a='2'/>", "<r xmlns:p=\"u\" xmlns:q=\"u\" p:a=\"1\"/>", 1),
            ("before<r/>after", "<r/>", 2),
            ("<r/><s><a/><!--discard--><?pi discard?></s>", "<r/>", 1),
            ("<![CDATA[before]]><r/><![CDATA[after]]>", "<r/>", 2),
            ("<r/><s><a></s>", "<r/>", 2)
        }) for (int transport = 0; transport < 4; transport++) yield return [input, output, errors, transport];
    }
    private static XmlParseResult<XmlDocument> Parse(string input, int transport, XmlParseOptions? options = null)
    {
        var token = TestContext.Current.CancellationToken;
        return transport switch
        {
            0 => XmlDocument.ParseWithDiagnostics(input, options, token),
            1 => XmlDocument.ParseWithDiagnostics(input.AsSpan(), options, token),
            2 => XmlDocument.ParseWithDiagnostics(Encoding.UTF8.GetBytes(input), options, token),
            _ => XmlDocument.LoadWithDiagnostics(new MemoryStream(Encoding.UTF8.GetBytes(input)), options, token)
        };
    }
    [Theory, MemberData(nameof(Repairs))]
    public void AllRepairsComposeAcrossTransports(string input, string expected, int errors, int transport)
    {
        var result = Parse(input, transport, Recover);
        Assert.True(result.IsComplete); Assert.False(result.IsWellFormed);
        Assert.NotNull(result.Value); Assert.Equal(expected, result.Value.ToXmlString(new() { OmitXmlDeclaration = true }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(errors, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d => { Assert.Equal(XmlDiagnosticSeverity.Error, d.Severity); Assert.StartsWith("XML.Recovery.", d.Code); });
        Assert.All(result.Diagnostics, d => Assert.Equal(transport >= 2, d.Location.ByteOffset is not null));
        result.Value.RootElement.SetAttribute("edited", "yes");
        Assert.Equal("yes", result.Value.RootElement.GetAttribute("edited"));
    }
    [Theory]
    [InlineData("<r a=unquoted>", false)]
    [InlineData("<r><a a=unquoted>", true)]
    [InlineData("<r xmlns:p=''/>", false)]
    [InlineData("<r xmlns:p='u' xmlns:p='v'/>", false)]
    [InlineData("<r></p:a>", true)]
    [InlineData("<r p:a='1'/>", false)]
    [InlineData("<r><p:a/>", true)]
    [InlineData("<r>&#0;</r>", true)]
    [InlineData("<r>&unfinished", true)]
    [InlineData("<!DOCTYPE r><r/>", false)]
    [InlineData("before", false)]
    [InlineData("<r/><s broken>", true)]
    [InlineData("<r/><s>&#0;</s>", true)]
    [InlineData("<r/><s xmlns:p=''/>", true)]
    [InlineData("<r><![CDATA[broken", true)]
    public void ForbiddenErrorsStayFatalAndRootCommitIsAtomic(string input, bool partial)
    {
        for (int transport = 0; transport < 4; transport++)
        {
            var result = Parse(input, transport, Recover);
            Assert.False(result.IsComplete); Assert.False(result.IsWellFormed);
            Assert.Equal(partial, result.Value is not null); Assert.NotEmpty(result.Diagnostics);
            if (partial) { Assert.Equal("r", result.Value!.RootElement.LocalName); Assert.DoesNotContain("a=", result.Value.ToXmlString(new() { OmitXmlDeclaration = true }, cancellationToken: TestContext.Current.CancellationToken)); }
        }
    }
    [Fact]
    public void StructuredStrictFailuresAndOriginalWellFormedFlags()
    {
        Assert.False(new XmlParseOptions().Recover);
        for (int transport = 0; transport < 4; transport++)
        {
            var failure = Parse("<r>", transport);
            Assert.Null(failure.Value); Assert.False(failure.IsComplete); Assert.False(failure.IsWellFormed); Assert.Single(failure.Diagnostics);
            foreach (var options in new[] { new XmlParseOptions(), Recover })
            {
                var success = Parse("<r/>", transport, options);
                Assert.NotNull(success.Value); Assert.True(success.IsComplete); Assert.True(success.IsWellFormed); Assert.Empty(success.Diagnostics);
            }
        }
    }
    [Fact]
    public void DiagnosticsAreBoundedAndOwnedWithReservedTerminalSlot()
    {
        for (int limit = 1; limit <= 4; limit++)
        {
            var result = Parse("<r></x></x></x></x></r>", 0, Recover with { Limits = new() { Diagnostics = limit } });
            Assert.False(result.IsComplete); Assert.NotNull(result.Value);
            Assert.Equal(limit, result.Diagnostics.Count); Assert.Equal("XML.DiagnosticLimit", result.Diagnostics[^1].Code);
            Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)result.Diagnostics).Clear());
        }
        var one = Parse("<r>", 0, Recover with { Limits = new() { Diagnostics = 2 } });
        Assert.True(one.IsComplete); Assert.Single(one.Diagnostics);
        var unlimited = Parse("<r></x></x></x></r>", 0, Recover with { Limits = new() { Diagnostics = null } });
        Assert.True(unlimited.IsComplete); Assert.Equal(3, unlimited.Diagnostics.Count);
        var fatal = Parse("<r></x>&#0;</r>", 0, Recover with { Limits = new() { Diagnostics = 2 } });
        Assert.Equal(2, fatal.Diagnostics.Count); Assert.Equal("XML.Syntax", fatal.Diagnostics[^1].Code);
    }
    [Fact]
    public void RepairLocationsFollowNormalizedScalarsAndOriginalByteMappings()
    {
        const string input = "<r>😀\r\n<a></r>";
        var chars = Parse(input, 0, Recover); var bytes = Parse(input, 2, Recover);
        Assert.Equal(new XmlLocation(null, 2, 4, null), Assert.Single(chars.Diagnostics).Location);
        Assert.Equal(new XmlLocation(null, 2, 4, 12), Assert.Single(bytes.Diagnostics).Location);
        var duplicate = Parse("<r a='1' a='2'/>", 0, Recover);
        Assert.Equal(new XmlLocation(null, 1, 10, null), Assert.Single(duplicate.Diagnostics).Location);
        var eof = Parse("<r>😀", 2, Recover);
        Assert.Equal(new XmlLocation(null, 1, 5, 7), Assert.Single(eof.Diagnostics).Location);
        byte[] utf16 = [0xff, 0xfe, .. Encoding.Unicode.GetBytes("<r>")];
        var result = XmlDocument.ParseWithDiagnostics(utf16, Recover, TestContext.Current.CancellationToken);
        Assert.True(result.IsComplete); Assert.Equal(8, Assert.Single(result.Diagnostics).Location.ByteOffset);
    }
    [Fact]
    public void DecoderAndDocumentQuotasNeverRecover()
    {
        var bytes = XmlDocument.ParseWithDiagnostics(new byte[] { 60, 114, 62, 255 }, Recover, TestContext.Current.CancellationToken);
        Assert.False(bytes.IsComplete); Assert.NotNull(bytes.Value); Assert.Single(bytes.Diagnostics);
        var result = Parse("<r><a/>", 0, Recover with { Limits = new() { Depth = 1 } });
        Assert.False(result.IsComplete); Assert.Equal("XML.ResourceLimit", Assert.Single(result.Diagnostics).Code);
        result = Parse("<r a='1' a='2'/>", 0, Recover with { Limits = new() { AttributesPerElement = 1 } });
        Assert.Null(result.Value); Assert.Equal("XML.ResourceLimit", Assert.Single(result.Diagnostics).Code);
        result = Parse("<r>more", 0, Recover with { Limits = new() { DocumentCharacters = 3 } });
        Assert.NotNull(result.Value); Assert.False(result.IsComplete);
    }
    [Fact]
    public void StrictAdmissionPrecedesInputAndCancellation()
    {
        using var stream = new ThrowingStream(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<ArgumentException>(() => XmlDocument.Parse((string)null!, Recover, canceled.Token));
        Assert.Throws<ArgumentException>(() => XmlDocument.Parse("<r/>".AsSpan(), Recover, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => XmlDocument.Parse("<r/>"u8, Recover, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => XmlDocument.TryParse((string)null!, out _, out _, Recover, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => XmlDocument.TryParse("<r/>".AsSpan(), out _, out _, Recover, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => XmlDocument.TryParse("<r/>"u8, out _, out _, Recover, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => XmlDocument.Load(stream, Recover, TestContext.Current.CancellationToken)); Assert.Equal(0, stream.Reads);
        Assert.Throws<OperationCanceledException>(() => XmlDocument.LoadWithDiagnostics(stream, Recover, canceled.Token)); Assert.Equal(0, stream.Reads);
        var error = Assert.Throws<IOException>(() => XmlDocument.LoadWithDiagnostics(stream, Recover, TestContext.Current.CancellationToken)); Assert.Same(stream.Error, error);
    }
    private sealed class ThrowingStream : MemoryStream
    {
        internal int Reads; internal readonly IOException Error = new("test");
        public override int Read(Span<byte> buffer) { Reads++; throw Error; }
    }
}
