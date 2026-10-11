using Xemele.Html;
using Xunit;

namespace Xemele.Conformance.Tests.Html;

public sealed class ByteOracleTests
{
    // Independent current WHATWG UTF-8 decoder witnesses. Native legacy decoder
    // recovery differs and is tested against its retained libxml2 oracle instead.
    [Theory]
    [InlineData(new byte[] { 0xE0, 0x80, 0x41 }, "��A")]
    [InlineData(new byte[] { 0xF0, 0x90, 0x80 }, "�")]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 }, "���")]
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 }, "����")]
    [InlineData(new byte[] { 0xC2, 0xA2 }, "¢")]
    [InlineData(new byte[] { 0xF0, 0x9F, 0x98, 0x80 }, "😀")]
    [InlineData(new byte[] { 0x80, 0x80 }, "��")]
    [InlineData(new byte[] { 0x00, 0x41 }, "A")]
    public void MalformedEncodedScalarsMatchIndependentExpectedText(byte[] payload, string expected)
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. "<p>"u8, .. payload];
        Assert.Equal(expected, Html5Parser.Parse(bytes).RootElement.GetTextContent());
        using var stream = new MemoryStream(bytes);
        Assert.Equal(expected, Html5Parser.Load(stream, cancellationToken: TestContext.Current.CancellationToken).RootElement.GetTextContent());
        Assert.True(stream.CanRead);
    }
}
