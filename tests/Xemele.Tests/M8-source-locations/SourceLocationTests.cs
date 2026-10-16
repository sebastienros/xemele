using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SourceLocations;

public sealed class SourceLocationTests
{
    internal const string Xml = "<r xmlns:p='urn:p'>\r\n <decl fixed='one'/>\r\n <use fixed='two'>根😀<n a='v'/></use>\r\n</r>";
    private static readonly Uri Source = new("https://example.test/original/schema.xsd");

    [Fact]
    public void CharacterCoordinatesPreserveDistinctLocalConstraintsAndScalarColumns()
    {
        var scope = new SchemaCompileContext(new()
        {
            BaseUri = Source
        }).Parse(Xml);
        Check(scope, bytes: false);
        Assert.Equal(At("<r", false), scope.Location(scope.Document.DocumentNode));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void ChunkedByteCoordinatesKeepOriginalOffsetsAndOriginWithoutDeclaration(int chunk)
    {
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(Xml), chunk);
        var document = XmlDocument.Load(stream, null, Source, TestContext.Current.CancellationToken);
        Assert.Null(document.ValidationDeclarationLocation.SourceUri);
        var scope = new SchemaCompileContext(new()
        {
            BaseUri = new("urn:wrong:fallback")
        }).Open(document);
        Assert.Equal(Source, scope.SourceUri);
        Check(scope, bytes: true);
    }

    [Fact]
    public async Task AsyncBytePathRetainsSameOriginalPositions()
    {
        using var stream = new ChunkedStream(Encoding.UTF8.GetBytes(Xml), 1);
        var document = await XmlDocument.LoadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var scope = new SchemaCompileContext(new()
        {
            BaseUri = Source
        }).Open(document);
        Check(scope, bytes: true);
    }

    [Fact]
    public void RedirectedResourceKeepsFinalOriginWhenOpenedByAnotherCompileContext()
    {
        var context = new SchemaCompileContext(new()
        {
            Resolver = new Resolver()
        });
        var document = context.Resources.Load(new("urn:request", null, SchemaResourcePurpose.XsdInclude, default), context.Operation);
        var reopened = new SchemaCompileContext().Open(document);
        Assert.Equal(Source, reopened.SourceUri);
        Check(reopened, bytes: true);
    }

    [Fact]
    public void MutationsInvalidateCoordinatesBeforeOpenAndFailAnExistingTraversal()
    {
        var context = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        var scope = context.Parse(Xml);
        var document = scope.Document;
        var root = document.RootElement;
        root.SetAttribute("changed", "yes");
        Assert.Throws<InvalidOperationException>(() => scope.Location(root));
        var reopened = context.Open(document);
        Assert.Equal(new XmlLocation(Source, 0, 0, null), reopened.Location(root));
        Assert.Equal(new XmlLocation(Source, 0, 0, null), reopened.Location(root.Attributes[0]));
        Assert.Equal(new XmlLocation(Source, 0, 0, null), reopened.Location(root.NamespaceDeclarations[0]));
        Assert.True(document.SourceInputComplete);
    }

    [Fact]
    public void ProgrammaticCloneAndImportNodesDoNotAcquireInventedCoordinates()
    {
        var document = XmlDocument.Parse("<r a='v' xmlns:p='urn:p'/>", cancellationToken: TestContext.Current.CancellationToken);
        var clone = document.RootElement.Clone(); // Detached creation does not necessarily mutate attached content.
        var scope = new SchemaCompileContext().Open(document);
        Assert.Equal(default, scope.Location(clone));
        Assert.Equal(default, scope.Location(clone.Attributes[0]));
        Assert.Equal(default, scope.Location(clone.NamespaceDeclarations[0]));
        var target = new XmlDocument();
        var imported = target.ImportNode(document.RootElement);
        target.DocumentNode.AppendChild(imported);
        var importedScope = new SchemaCompileContext().Open(target);
        Assert.Equal(default, importedScope.Location(imported));
        Assert.Equal(default, importedScope.Location(imported.Attributes[0]));
        var fresh = new XmlDocument();
        var element = fresh.CreateElement("schema");
        fresh.DocumentNode.AppendChild(element);
        Assert.Equal(default, new SchemaCompileContext().Open(fresh).Location(element));
    }

    [Fact]
    public void ForeignAndDefaultHandlesRefuseAndCancellationStillPropagates()
    {
        var scope = new SchemaCompileContext().Parse("<r a='x' xmlns:p='urn:p'/>");
        var other = XmlDocument.Parse("<o a='y' xmlns:q='urn:q'/>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => scope.Location(other.RootElement));
        Assert.Throws<ArgumentException>(() => scope.Location(other.RootElement.Attributes[0]));
        Assert.Throws<ArgumentException>(() => scope.Location(other.RootElement.NamespaceDeclarations[0]));
        Assert.Throws<ArgumentException>(() => scope.Location(default(XmlNode)));
        using var cancellation = new CancellationTokenSource();
        var canceled = new SchemaCompileContext(new()
        {
            CancellationToken = cancellation.Token
        }).Parse("<r/>");
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.Location(canceled.Document.RootElement));
    }

    [Fact]
    public void RecoveryAndPartialInputNeverPublishCurrentCoordinates()
    {
        var recovered = XmlDocument.ParseWithDiagnostics("<r><x></r>", new()
        {
            Recover = true
        }, TestContext.Current.CancellationToken).Value!;
        Assert.Equal(default, recovered.OriginalLocation(recovered.RootElement));
        var partialResult = XmlDocument.ParseWithDiagnostics("<r>\u0000", new()
        {
            Recover = true
        }, TestContext.Current.CancellationToken);
        Assert.False(partialResult.IsComplete);
        var partial = partialResult.Value!;
        Assert.Equal(default, partial.OriginalLocation(partial.RootElement));
        Assert.Throws<SchemaCompileException>(() => new SchemaCompileContext().Open(partial));
    }

    private static void Check(SchemaDocumentScope scope, bool bytes)
    {
        var root = scope.Document.RootElement;
        var elements = new List<XmlNode>();
        foreach (var node in root.Children)
        {
            if (node.Kind == XmlNodeKind.Element)
            {
                elements.Add(node);
            }
        }

        var declaration = elements[0];
        var use = elements[1];
        var nested = use.LastChild;
        Assert.Equal(At("<r", bytes), scope.Location(root));
        Assert.Equal(At("xmlns:p", bytes), scope.Location(root.NamespaceDeclarations[0]));
        Assert.Equal(At("<decl", bytes), scope.Location(declaration));
        Assert.Equal(At("fixed='one'", bytes), scope.Location(declaration.Attributes[0]));
        Assert.Equal(At("<use", bytes), scope.Location(use));
        Assert.Equal(At("fixed='two'", bytes), scope.Location(use.Attributes[0]));
        Assert.Equal(At("根", bytes), scope.Location(use.FirstChild));
        Assert.Equal(At("<n", bytes), scope.Location(nested));
        Assert.Equal(At("a='v'", bytes), scope.Location(nested.Attributes[0]));
    }
    private static XmlLocation At(string token, bool bytes)
    {
        var index = Xml.IndexOf(token, StringComparison.Ordinal);
        string prefix = Xml[..index], normalized = prefix.Replace("\r\n", "\n").Replace('\r', '\n');
        var newline = normalized.LastIndexOf('\n');
        return new(Source, normalized.Count(c => c == '\n') + 1, normalized[(newline + 1)..].EnumerateRunes().Count() + 1,
            bytes ? Encoding.UTF8.GetByteCount(prefix) : null);
    }
    private sealed class Resolver : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
            => new(new ChunkedStream(Encoding.UTF8.GetBytes(Xml), 1), Source);
    }
    private sealed class ChunkedStream(byte[] input, int chunk) : MemoryStream(input, writable: false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(chunk, buffer.Length)]);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(chunk, buffer.Length)], cancellationToken);
    }
}
