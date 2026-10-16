using System.Text;
using Xunit;

namespace Xemele.Tests.UndeclaredReferences;

public sealed class RefusalTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static XmlDocument Document() => XmlDocument.Parse(ContentTests.Prefix + "<r>a&x;b</r>", ContentTests.Options, Ct);
    private static void Refuses(Action action) => Assert.Equal("Document contains unresolved entity references.", Assert.Throws<InvalidOperationException>(action).Message);

    [Fact]
    public void AllActualOutputCopyAndTextEntrypointsRefuseOwnerBeforeEffects()
    {
        var source = Document();
        var root = source.RootElement;
        var leaf = root.FirstChild;
        var destination = XmlDocument.Parse("<d/>", cancellationToken: Ct);
        var nodes = destination.Nodes.Count;
        var version = destination.MutationVersion;
        Refuses(() => source.ToXmlString());
        Refuses(() => XmlSerializer.Serialize(source));
        foreach (var deep in new[] { false, true })
        {
            Refuses(() => source.DocumentNode.Clone(deep));
            Refuses(() => root.Clone(deep));
            Refuses(() => leaf.Clone(deep));
            Refuses(() => destination.ImportNode(root, deep));
            Assert.Equal(nodes, destination.Nodes.Count);
            Assert.Equal(version, destination.MutationVersion);
        }
        Refuses(() => source.DocumentNode.GetTextContent());
        Refuses(() => root.GetTextContent());
        Refuses(() => leaf.GetTextContent());
        Assert.Equal("a", leaf.Value);
        using var stream = new WriteProbe();
        foreach (var bom in new[] { false, true })
        {
            var options = new XmlSaveOptions { EmitByteOrderMark = bom, Encoding = new UnicodeEncoding(false, true, true) };
            Refuses(() => source.Save(stream, options));
            Refuses(() => XmlSerializer.Serialize(source, stream, options));
            Assert.Equal(0, stream.Writes);
            Assert.Equal(0, stream.Length);
        }
    }

    [Fact]
    public void ProvenanceSurvivesMutationDetachedHandlesAndCleanSourceImport()
    {
        var document = Document();
        var reference = Assert.Single(document.UnresolvedReferences);
        var first = document.RootElement.FirstChild;
        first.Remove();
        document.RootElement.SetTextContent("replacement");
        reference.Parent.Remove();
        Assert.Equal(reference, Assert.Single(document.UnresolvedReferences));
        Assert.Equal("a", reference.PreviousSibling.Value);
        Assert.True(reference.PreviousSibling.Parent.IsDefault);
        Assert.True(reference.Parent.Parent.IsDefault);
        Assert.Equal(1, reference.ChildBoundary);
        Assert.Equal(0, reference.Sequence);
        Refuses(() => first.Clone());
        Refuses(() => first.GetTextContent());
        Refuses(() => document.ToXmlString());
        var clean = XmlDocument.Parse("<clean>ok</clean>", cancellationToken: Ct);
        var imported = document.ImportNode(clean.RootElement);
        Assert.Equal("ok", imported.FirstChild.Value);
        Assert.Single(document.UnresolvedReferences);
        Assert.Empty(clean.UnresolvedReferences);
        Assert.Equal("ok", clean.RootElement.GetTextContent());
        Assert.Equal("ok", clean.RootElement.Clone().GetTextContent());
        Assert.Equal("<clean>ok</clean>", clean.ToXmlString(new()
        {
            OmitXmlDeclaration = true
        }, Ct));
        Assert.Throws<NotSupportedException>(() => ((IList<XmlUnresolvedReference>)document.UnresolvedReferences).Clear());
    }

    [Fact]
    public void ExistingValidationAndCancellationPrecedeOwnerGuard()
    {
        var document = Document();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => document.ToXmlString(cancellationToken: canceled.Token));
        Assert.Throws<ArgumentNullException>(() => document.Save(null!, cancellationToken: Ct));
        Assert.Throws<ArgumentException>(() => document.ToXmlString(new() { Encoding = Encoding.ASCII }, Ct));
        Assert.Throws<ArgumentException>(() => document.ImportNode(document.DocumentNode));
        Assert.Throws<InvalidOperationException>(() => document.ImportNode(default));
    }
    private sealed class WriteProbe : MemoryStream
    {
        internal int Writes;
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            base.Write(buffer);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes++;
            base.Write(buffer, offset, count);
        }
        public override void WriteByte(byte value)
        {
            Writes++;
            base.WriteByte(value);
        }
    }
}
