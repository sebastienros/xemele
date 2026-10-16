using System.Collections;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.EntitiesFoundation;

public sealed class FoundationTests
{
    [Fact]
    public void DefaultsAndSnapshotsPreserveAdditiveOptions()
    {
        var defaults = new XmlParseOptions();
        Assert.Equal(XmlDtdProcessing.Prohibit, defaults.DtdProcessing);
        Assert.Null(defaults.Resolver);
        Assert.Equal(16_777_216, defaults.Limits.DocumentCharacters);
        var resolver = new RefusingResolver();
        var original = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
        var clone = original with
        {
            Limits = original.Limits with
            {
                Depth = 12
            }
        };
        var snapshot = new OperationBudget(clone, default).Options;
        Assert.NotSame(clone, snapshot);
        Assert.NotSame(clone.Limits, snapshot.Limits);
        Assert.Same(resolver, snapshot.Resolver);
        Assert.Equal(XmlDtdProcessing.Parse, snapshot.DtdProcessing);
        Assert.Equal(12, snapshot.Limits.Depth);
        Assert.Equal(256, original.Limits.Depth);
        Assert.Equal(original, original with
        {
        });
        Assert.NotEqual(original, original with
        {
            DtdProcessing = XmlDtdProcessing.Prohibit
        });
    }
    [Fact]
    public void MetadataRecordRetainsExactNullableLexicalValuesAndCloneSemantics()
    {
        var metadata = new XmlDocumentType("p:root", null, "../relative.dtd", " \r\n<!ENTITY e 'text'> ");
        var (name, publicId, systemId, subset) = metadata;
        Assert.Equal("p:root", name);
        Assert.Null(publicId);
        Assert.Equal("../relative.dtd", systemId);
        Assert.Equal(" \r\n<!ENTITY e 'text'> ", subset);
        Assert.Equal(metadata, metadata with
        {
        });
        var changed = metadata with
        {
            Name = "changed",
            PublicId = "public"
        };
        Assert.NotEqual(metadata, changed);
        Assert.Equal("p:root", metadata.Name);
        // The positional record is lexical storage, not a grammar validator.
        Assert.Equal("", new XmlDocumentType("", null, null, null).Name);
        Assert.Null(new XmlDocumentType(null!, null, null, null).Name);
    }
    [Fact]
    public void DocumentOwnsImmutableModelWithoutBorrowedArrayLifetime()
    {
        var source = "root".ToCharArray();
        var metadata = new XmlDocumentType(new string(source), null, null, "<!ENTITY e 'owned'>");
        var entity = new DtdEntityDeclaration("e", false, "owned", null, null, null, false, default);
        var array = new[] { entity };
        var model = new Xemele.Parsing.Dtd.DtdModel([], [], array, [], []);
        var document = new XmlDocument();
        Assert.Null(document.DocumentType);
        Assert.Null(document.Dtd);
        document.SetDtdMetadata(metadata, model);
        Array.Fill(source, 'x');
        array[0] = entity with
        {
            ReplacementText = "changed"
        };
        Assert.Same(metadata, document.DocumentType);
        Assert.Same(model, document.Dtd);
        Assert.Equal("root", document.DocumentType!.Name);
        Assert.Equal("owned", document.Dtd!.GeneralEntities[0].ReplacementText);
        Assert.Throws<NotSupportedException>(() => ((IList)model.GeneralEntities).Clear());
        var root = document.CreateElement("root");
        document.DocumentNode.AppendChild(root);
        Assert.Same(metadata, document.DocumentType);
        Assert.Same(model, document.Dtd);
        Assert.Equal(XmlNodeKind.Element, document.RootElement.Kind);
    }
    [Fact]
    public void RequiredStorageArgumentsAreCheckedBeforeEitherFieldChanges()
    {
        var document = new XmlDocument();
        var metadata = new XmlDocumentType("root", null, null, null);
        var model = new Xemele.Parsing.Dtd.DtdModel([], [], [], [], []);
        document.SetDtdMetadata(metadata, model);
        Assert.Equal("declaration", Assert.Throws<ArgumentNullException>(() => document.SetDtdMetadata(null!, model)).ParamName);
        Assert.Equal("model", Assert.Throws<ArgumentNullException>(() => document.SetDtdMetadata(metadata, null!)).ParamName);
        Assert.Same(metadata, document.DocumentType);
        Assert.Same(model, document.Dtd);
    }
    [Fact]
    public void OptionalDomCallbackStoresMetadataWithoutRequiredEventChanges()
    {
        IXmlEventSink old = new OldSink();
        Assert.False(old is IXmlDtdSink);
        var sink = new DomSink(new());
        IXmlDtdSink optional = sink;
        var metadata = new XmlDocumentType("root", null, null, null);
        var model = new Xemele.Parsing.Dtd.DtdModel([], [], [], [], []);
        Assert.True(optional.DocumentType(metadata, model, default));
        Assert.Same(metadata, sink.Document.DocumentType);
        Assert.Same(model, sink.Document.Dtd);
        Assert.True(sink.Document.RootElement.IsDefault);
        Assert.True(sink.StartDocument(default));
        Assert.True(sink.StartElement(new("root", [], []), false, default));
        Assert.True(sink.StartElementContent());
        Assert.True(sink.EndElement(new("root", [], []), default));
        Assert.True(sink.EndDocument(default));
        Assert.Equal("root", sink.Document.RootElement.LocalName);
        Assert.Same(metadata, sink.Document.DocumentType);
    }
    [Theory]
    [InlineData(XmlDtdProcessing.Prohibit)]
    [InlineData(XmlDtdProcessing.Parse)]
    public void DefaultProhibitionAndNoUnrequestedResolverWorkRemain(XmlDtdProcessing mode)
    {
        var resolver = new RefusingResolver();
        var options = new XmlParseOptions { DtdProcessing = mode, Resolver = resolver };
        Assert.True(XmlDocument.TryParse("<root/>", out var document, out _, options, TestContext.Current.CancellationToken));
        Assert.Null(document!.DocumentType);
        var admitted = XmlDocument.TryParse("<!DOCTYPE root><root/>", out var typed, out var diagnostics, options, TestContext.Current.CancellationToken);
        Assert.Equal(mode == XmlDtdProcessing.Parse, admitted);
        if (admitted)
        {
            Assert.Equal("root", typed!.DocumentType!.Name);
            Assert.Empty(diagnostics);
        }
        else
        {
            Assert.Equal("XML.DtdProhibited", diagnostics[0].Code);
        }

        Assert.False(XmlDocument.TryParse("<root>&unknown;</root>", out _, out diagnostics, options, TestContext.Current.CancellationToken));
        Assert.Equal("XML.UndeclaredEntity", diagnostics[0].Code);
        Assert.Equal(0, resolver.Calls);
    }
    [Fact]
    public void EntityRequestAndContextUseOwnedModelAndBorrowedLookup()
    {
        var entity = new DtdEntityDeclaration("e", false, "value", null, null, null, false, default);
        IXmlEntityContext context = new ModelContext(new([], [], [entity], [], []));
        var request = new EntityExpansionRequest("e", EntityUse.Attribute, new(null, 2, 3, null));
        Assert.Equal(EntityUse.Attribute, request.Use);
        Assert.Equal(2, request.ReferenceLocation.LineNumber);
        Assert.True(context.TryGetGeneralEntity(request.Name.AsSpan(), out var found));
        Assert.Same(entity, found);
        Assert.False(context.TryGetGeneralEntity("missing", out found));
        Assert.Null(found);
        Assert.Empty(context.GetAttributes("root"));
        Assert.Single(context.Model.GeneralEntities);
    }
    [Fact]
    public void WarmMetadataAssignmentAndCallbackDoNotAllocate()
    {
        var sink = new DomSink(new());
        var metadata = new XmlDocumentType("root", null, null, null);
        var model = new Xemele.Parsing.Dtd.DtdModel([], [], [], [], []);
        IXmlDtdSink callback = sink;
        for (var i = 0; i < 10000; i++)
        {
            callback.DocumentType(metadata, model, default);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            callback.DocumentType(metadata, model, default);
        }

        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(metadata, sink.Document.DocumentType);
        Assert.Equal(0, bytes);
    }
    private sealed class ModelContext(Xemele.Parsing.Dtd.DtdModel model) : IXmlEntityContext
    {
        public Xemele.Parsing.Dtd.DtdModel Model => model;
        public bool TryGetGeneralEntity(ReadOnlySpan<char> name, out DtdEntityDeclaration? declaration)
        {
            foreach (var entity in Model.GeneralEntities)
            {
                if (name.SequenceEqual(entity.Name))
                {
                    declaration = entity;
                    return true;
                }
            }

            declaration = null;
            return false;
        }
        public IReadOnlyList<DtdAttributeDeclaration> GetAttributes(ReadOnlySpan<char> elementName) => Model.Attributes;
    }
    private sealed class RefusingResolver : XmlResourceResolver
    {
        public int Calls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return null;
        }
        public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult<XmlResolvedResource?>(null);
        }
    }
    private sealed class OldSink : IXmlEventSink
    {
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => true;
        public bool EndDocument(XmlLocation location) => true;
    }
}
