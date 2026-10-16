using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.ValidationMetadata;

public sealed class ValidationMetadataTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("<?xml version='1.0' standalone='yes'?>", true)]
    [InlineData("<?xml version='1.0' standalone='no'?>", false)]
    [InlineData("<?xml version='1.0'?>", null)]
    [InlineData("", null)]
    public async Task DeclarationProvenanceSurvivesAllDomTransports(string declaration, bool? standalone)
    {
        var xml = declaration + "<r>β😀</r>";
        var bytes = Encoding.UTF8.GetBytes(xml);
        Check(XmlDocument.Parse(xml, cancellationToken: Ct), standalone, declaration, null);
        Check(XmlDocument.Parse(xml.AsSpan(), cancellationToken: Ct), standalone, declaration, null);
        Check(XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct), standalone, declaration, 0);
        using var sync = new ChunkStream(bytes, asyncOnly: false);
        Check(XmlDocument.Load(sync, cancellationToken: Ct), standalone, declaration, 0);
        using var async = new ChunkStream(bytes, asyncOnly: true);
        Check(await XmlDocument.LoadAsync(async, cancellationToken: Ct), standalone, declaration, 0);
        Assert.True(sync.CanRead);
        Assert.True(async.CanRead);
    }

    private static void Check(XmlDocument document, bool? standalone, string declaration, long? offset)
    {
        Assert.Equal(standalone, document.ValidationStandalone);
        Assert.Equal(declaration.Length == 0 ? default : new XmlLocation(null, 1, 1, offset), document.ValidationDeclarationLocation);
        Assert.Equal("β😀", document.RootElement.GetTextContent());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void DeclarationHookCopiesLocationAndUriBeforeCharacterEarlyReturn(bool? standalone)
    {
        var uri = new Uri("urn:source:declaration");
        var location = new XmlLocation(uri, 7, 13, 42);
        var sink = new DomSink(new());
        Assert.True(sink.Declaration(new XmlDeclarationView("1.0", "UTF-8", standalone, location)));
        Assert.Equal(standalone, sink.Document.ValidationStandalone);
        Assert.Equal(location, sink.Document.ValidationDeclarationLocation);
        Assert.Same(uri, sink.Document.ValidationDeclarationLocation.SourceUri);
        Assert.False(sink.DeclarationValidated); // Character input does not invoke a byte decoder.
        Assert.Null(sink.Diagnostic);
    }

    [Theory]
    [InlineData("UTF-8", false, 0)]
    [InlineData("UTF-8", true, 3)]
    [InlineData("UTF-16", true, 2)]
    public async Task OriginalByteOffsetsSurviveBomAndAsyncChunkBoundaries(string label, bool bom, long offset)
    {
        var encoding = label == "UTF-8" ? new UTF8Encoding(bom) : Encoding.Unicode;
        var xml = $"<?xml version='1.0' encoding='{label}' standalone='no'?><r/>";
        byte[] bytes = [.. (bom ? encoding.GetPreamble() : []), .. encoding.GetBytes(xml)];
        var expected = new XmlLocation(null, 1, 1, offset);
        var parsed = XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct);
        Assert.False(parsed.ValidationStandalone);
        Assert.Equal(expected, parsed.ValidationDeclarationLocation);
        using var input = new ChunkStream(bytes, asyncOnly: true);
        var loaded = await XmlDocument.LoadAsync(input, cancellationToken: Ct);
        Assert.False(loaded.ValidationStandalone);
        Assert.Equal(expected, loaded.ValidationDeclarationLocation);
    }

    [Fact]
    public async Task LegacyEncodingSelectionStillDecodesBodyAfterMetadataCapture()
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("<?xml version='1.0' encoding='Windows-1252' standalone='yes'?><r>"), 0x80, .. "</r>"u8];
        var parsed = XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct);
        Assert.True(parsed.ValidationStandalone);
        Assert.Equal("€", parsed.RootElement.GetTextContent());
        using var input = new ChunkStream(bytes, asyncOnly: true);
        var loaded = await XmlDocument.LoadAsync(input, cancellationToken: Ct);
        Assert.True(loaded.ValidationStandalone);
        Assert.Equal("€", loaded.RootElement.GetTextContent());
    }

    [Theory]
    [InlineData("<?xml version='1.0' standalone='YES'?><r/>")]
    [InlineData("<?xml version='1.0' standalone='maybe'?><r/>")]
    [InlineData("<?xml version='1.0' standalone='yes' standalone='no'?><r/>")]
    public async Task InvalidStandaloneStillRejectsCharacterByteAndAsyncInput(string xml)
    {
        Assert.Throws<XmlParseException>(() => XmlDocument.Parse(xml, cancellationToken: Ct));
        var bytes = Encoding.UTF8.GetBytes(xml);
        Assert.Throws<XmlParseException>(() => XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct));
        using var input = new ChunkStream(bytes, asyncOnly: true);
        await Assert.ThrowsAsync<XmlParseException>(() => XmlDocument.LoadAsync(input, cancellationToken: Ct));
    }

    [Fact]
    public async Task EncodingMismatchStillRejectsByteAndAsyncInput()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. "<?xml version='1.0' encoding='UTF-16' standalone='yes'?><r/>"u8];
        var error = Assert.Throws<XmlParseException>(() => XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct));
        Assert.Equal("INPUT.EncodingMismatch", Assert.Single(error.Diagnostics).Code);
        using var input = new ChunkStream(bytes, asyncOnly: true);
        var asyncError = await Assert.ThrowsAsync<XmlParseException>(() => XmlDocument.LoadAsync(input, cancellationToken: Ct));
        Assert.Equal(error.Diagnostics, asyncError.Diagnostics);
    }

    [Fact]
    public void ManuallyConstructedDocumentHasUnknownProvenanceAndNoPublicMetadata()
    {
        var document = new XmlDocument();
        Assert.Null(document.ValidationStandalone);
        Assert.Equal(default, document.ValidationDeclarationLocation);
        Assert.Null(typeof(XmlDocument).GetProperty("ValidationStandalone", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(XmlDocument).GetProperty("ValidationDeclarationLocation", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(XmlDocument).GetMethod("SetValidationDeclaration", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void SetterIlContainsOnlyTwoValueFieldStoresAndReturn()
    {
        var method = typeof(XmlDocument).GetMethod("SetValidationDeclaration", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;
        // Debug builds may emit nop; field tokens occupy four bytes each.
        var index = 0;
        while (il[index] == 0)
        {
            index++;
        }

        Assert.Equal(0x02, il[index++]); // ldarg.0
        Assert.Equal(0x03, il[index++]); // ldarg.1
        Assert.Equal(0x7D, il[index++]); // stfld
        var standalone = method.Module.ResolveField(BitConverter.ToInt32(il, index))!;
        Assert.Equal(typeof(bool?), standalone.FieldType);
        index += 4;
        Assert.Equal(0x02, il[index++]);
        Assert.Equal(0x04, il[index++]); // ldarg.2
        Assert.Equal(0x7D, il[index++]);
        var location = method.Module.ResolveField(BitConverter.ToInt32(il, index))!;
        Assert.Equal(typeof(XmlLocation), location.FieldType);
        index += 4;
        Assert.Equal(0x2A, il[index++]); // ret; no call, box, newobj, newarr or lexical materialization.
        Assert.Equal(il.Length, index);
        Assert.Equal(2, Unsafe.SizeOf<bool?>());
        Assert.Equal(32, Unsafe.SizeOf<XmlLocation>());
    }

    [Fact]
    public void ActualTestHostUsesPinnedRuntime()
    {
        Assert.StartsWith(".NET 10.", RuntimeInformation.FrameworkDescription);
    }

    private sealed class ChunkStream(byte[] bytes, bool asyncOnly) : MemoryStream(bytes, writable: false)
    {
        public override int Read(Span<byte> buffer)
        {
            if (asyncOnly)
            {
                throw new InvalidOperationException("Async input must not use sync reads.");
            }

            return base.Read(buffer[..Math.Min(1, buffer.Length)]);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer.Span[..Math.Min(1, buffer.Length)]);
        }
    }
}
