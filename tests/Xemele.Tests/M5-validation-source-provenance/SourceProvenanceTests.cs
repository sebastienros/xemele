using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xemele.Parsing.Dom;
using Xunit;

namespace Xemele.Tests.M5ValidationSourceProvenance;

public sealed class SourceProvenanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static bool Traceable(XmlDocument d) => d.SourceInputComplete &&
        d.ValidationSourceContentRevision is >= 0 && d.ValidationSourceContentRevision == d.ContentRevision;

    [Theory]
    [InlineData("<r/>")]
    [InlineData("<?xml version='1.0' standalone='yes'?><r>&#32;<![CDATA[ ]]>&#x9;</r>")]
    [InlineData("<?xml version='1.0' standalone='no'?><r>β😀</r>")]
    public async Task CompletionCapturedAcrossCharacterByteSyncAsyncBoundaries(string xml)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        using var sync = new ChunkStream(bytes, false);
        using var async = new ChunkStream(bytes, true);
        XmlDocument[] docs = [XmlDocument.Parse(xml, cancellationToken: Ct),
            XmlDocument.Parse(xml.AsSpan(), cancellationToken: Ct),
            XmlDocument.Parse(bytes.AsSpan(), cancellationToken: Ct),
            XmlDocument.Load(sync, cancellationToken: Ct), await XmlDocument.LoadAsync(async, cancellationToken: Ct)];
        foreach (var d in docs)
        {
            Assert.True(Traceable(d));
            Assert.Equal(docs[0].RootElement.GetTextContent(), d.RootElement.GetTextContent());
            Assert.Equal(docs[0].ValidationStandalone, d.ValidationStandalone);
            if (xml.Contains("CDATA", StringComparison.Ordinal))
            {
                Assert.Equal("  \t", d.RootElement.GetTextContent());
                bool cdata = false;
                foreach (var child in d.RootElement.Children) cdata |= child.Kind == XmlNodeKind.CData;
                Assert.True(cdata);
                Assert.True(d.ValidationStandalone);
            }
        }
        Assert.True(sync.CanRead); Assert.True(async.CanRead);
    }

    [Fact]
    public void ProgrammaticClonedAndUnfinishedDocumentsHaveUnknownSource()
    {
        var programmatic = new XmlDocument();
        programmatic.DocumentNode.AppendChild(programmatic.CreateElement("r"));
        Assert.Null(programmatic.ValidationSourceContentRevision); Assert.False(Traceable(programmatic));
        var parsed = XmlDocument.Parse("<r/>", cancellationToken: Ct);
        Assert.True(Traceable(parsed));
        Assert.Null(parsed.DocumentNode.Clone().OwnerDocument.ValidationSourceContentRevision);
        var sink = new DomSink(new());
        Assert.True(sink.StartDocument(default));
        Assert.Null(sink.Document.ValidationSourceContentRevision);
    }

    [Fact]
    public void EveryMutationAndRestoredValueInvalidateTheOriginalStamp()
    {
        var d = XmlDocument.Parse("<r xmlns:p='urn:p' p:a='A'>A<c/></r>", cancellationToken: Ct);
        var r = d.RootElement; var text = r.FirstChild; var a = r.Attributes[0];
        long? stamp = d.ValidationSourceContentRevision;
        void Mutate(Action action)
        {
            long before = d.ContentRevision; action(); Assert.True(d.ContentRevision > before);
            Assert.Equal(stamp, d.ValidationSourceContentRevision); Assert.False(Traceable(d));
        }
        Mutate(() => text.Value = "B"); Mutate(() => text.Value = "A");
        Mutate(() => a.Value = "B"); Mutate(() => a.Value = "A");
        Mutate(() => r.SetAttribute("a", "A", "urn:p", "q"));
        Mutate(() => r.SetNamespaceDeclaration("p", "urn:other"));
        var child = r.LastChild; Mutate(() => child.Remove()); Mutate(() => r.AppendChild(child));
        Mutate(() => r.RemoveAttribute("a", "urn:p")); Mutate(() => r.SetAttribute("a", "A", "urn:p", "p"));
        Mutate(() => r.RemoveNamespaceDeclaration("p")); Mutate(() => r.SetNamespaceDeclaration("p", "urn:p"));
    }

    [Theory]
    [InlineData("<r/>")]
    [InlineData("<r>")]
    [InlineData("<r><![CDATA[broken")]
    [InlineData("<r>&#0;</r>")]
    public async Task RecoveryNeverAssertsUnchangedStrictSourceProvenance(string xml)
    {
        var options = new XmlParseOptions { Recover = true }; byte[] bytes = Encoding.UTF8.GetBytes(xml);
        var results = new[] { XmlDocument.ParseWithDiagnostics(xml, options, Ct),
            XmlDocument.ParseWithDiagnostics(bytes.AsSpan(), options, Ct),
            XmlDocument.LoadWithDiagnostics(new MemoryStream(bytes), options, Ct),
            await XmlDocument.LoadWithDiagnosticsAsync(new MemoryStream(bytes), options, Ct) };
        foreach (var result in results)
        {
            Assert.NotNull(result.Value);
            Assert.Null(result.Value.ValidationSourceContentRevision); Assert.False(Traceable(result.Value));
            Assert.Equal(result.IsComplete, result.Value.SourceInputComplete);
        }
    }

    [Fact]
    public void CancellationAtCompletionDoesNotStampAndRetainsOriginalToken()
    {
        using var cts = new CancellationTokenSource();
        var sink = new DomSink(new(), cancellationToken: cts.Token);
        cts.Cancel();
        Assert.Equal(cts.Token, Assert.Throws<OperationCanceledException>(() => sink.EndDocument(default)).CancellationToken);
        Assert.Null(sink.Document.ValidationSourceContentRevision);
    }

    [Fact]
    public async Task MidInputCancellationPropagatesFromAsyncFactory()
    {
        using var cts = new CancellationTokenSource();
        using var input = new CancelStream("<r>text</r>"u8.ToArray(), cts);
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => XmlDocument.LoadAsync(input, cancellationToken: cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Fact]
    public void IncompleteAndExhaustedRevisionCannotBeTrusted()
    {
        var d = XmlDocument.Parse("<r/>", cancellationToken: Ct);
        d.MarkSourceIncomplete(); Assert.False(Traceable(d));
        typeof(XmlDocument).GetField("<ContentRevision>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(d, -1L);
        d.MarkValidationSourceComplete(); Assert.Equal(-1L, d.ValidationSourceContentRevision); Assert.False(Traceable(d));
    }

    [Fact]
    public void ScalarPayloadAndGeneratedGetterSetterHaveNoHeapConstruction()
    {
        Assert.Equal(16, Unsafe.SizeOf<long?>());
        var getter = typeof(XmlDocument).GetProperty("ValidationSourceContentRevision", BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!;
        byte[] get = getter.GetMethodBody()!.GetILAsByteArray()!;
        Assert.Equal(0x02, get[0]); Assert.Equal(0x7B, get[1]); Assert.Equal(0x2A, get[^1]); Assert.Equal(7, get.Length);
        var setter = typeof(XmlDocument).GetMethod("MarkValidationSourceComplete", BindingFlags.NonPublic | BindingFlags.Instance)!;
        byte[] il = setter.GetMethodBody()!.GetILAsByteArray()!;
        Assert.Equal(0x02, il[0]); Assert.Equal(0x02, il[1]); Assert.Equal(0x28, il[2]);
        Assert.Equal("get_ContentRevision", setter.Module.ResolveMethod(BitConverter.ToInt32(il, 3))!.Name);
        Assert.Equal(0x73, il[7]); // newobj Nullable<long>: inline value construction, no heap object.
        Assert.Equal(typeof(long?), setter.Module.ResolveMethod(BitConverter.ToInt32(il, 8))!.DeclaringType);
        Assert.Equal(0x7D, il[12]); Assert.Equal(typeof(long?), setter.Module.ResolveField(BitConverter.ToInt32(il, 13))!.FieldType);
        Assert.Equal(0x2A, il[17]); Assert.Equal(18, il.Length);
        Assert.Null(typeof(XmlDocument).GetProperty("ValidationSourceContentRevision", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(XmlDocument).GetMethod("MarkValidationSourceComplete", BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
    }

    private sealed class ChunkStream(byte[] bytes, bool asyncOnly) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> buffer)
        {
            if (asyncOnly) throw new InvalidOperationException("No synchronous read.");
            return base.Read(buffer[..Math.Min(1, buffer.Length)]);
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer.Span[..Math.Min(1, buffer.Length)]);
        }
    }
    private sealed class CancelStream(byte[] bytes, CancellationTokenSource source) : MemoryStream(bytes, false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = base.Read(buffer.Span[..Math.Min(1, buffer.Length)]);
            source.Cancel(); return ValueTask.FromResult(read);
        }
    }
}
