using Xemele.Streaming;
using Xunit;
namespace Xemele.Tests.Callbacks;
public sealed class CallbackTests
{
    internal static readonly XmlLocation Location = new(new Uri("file:///source.xml"), 2, 3, 7);
    internal static readonly XmlAttributeValuePart[] Parts = [new XmlKnownAttributeText("before", Location), new XmlUnknownAttributeReference("x", "&x;", Location)];
    internal static readonly AttributeRow[] Rows = [new("a", "p", "urn:p", "value", false, true, Parts, Location), new("b", "", "", "lossy", true, false, Parts, Location)];
    internal static readonly NamespaceRow[] Namespaces = [new("p", "urn:p", Location)];
    internal static readonly XmlIncompleteAttributeDefault Incomplete = new("root", "a", "&x;", false, true, Location, Location, Parts, 4);
    internal static readonly XmlDiagnostic DiagnosticValue = new("test", XmlDiagnosticSeverity.Warning, "message", Location);
    [Fact] public void EveryDelegateMatchesSubclassAndOptionalHandlersAreNoOps()
    {
        var expected = new Recorder(); var actual = new Recorder();
        Dispatch(expected);
        var adapter = Create(actual); Dispatch(adapter);
        Assert.Equal(Enumerable.Range(0, 16), actual.Events);
        Assert.Equal(expected.Events, actual.Events);
        Dispatch(new NoOp()); Dispatch(new XmlSaxCallbacks());
        var optional = new Recorder(); Dispatch(new XmlSaxCallbacks { OnText = optional.Text });
        Assert.Equal(new[] { 4 }, optional.Events);
    }
    [Fact] public void EveryCallbackPropagatesTheIdenticalException()
    {
        Exception[] exceptions = [new InvalidOperationException("callback"), new XmlParseException([DiagnosticValue])];
        foreach (var failure in exceptions)
        {
            var hStartDocument = new XmlSaxCallbacks { OnStartDocument = () => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hStartDocument.StartDocument()));
            var hEndDocument = new XmlSaxCallbacks { OnEndDocument = () => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hEndDocument.EndDocument()));
            var hStartElement = new XmlSaxCallbacks { OnStartElement = (element) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hStartElement.StartElement(new("root", "p", "urn:p", new(Rows), new(Namespaces), true, Location))));
            var hEndElement = new XmlSaxCallbacks { OnEndElement = (element) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hEndElement.EndElement(new("root", "p", "urn:p", Location))));
            var hText = new XmlSaxCallbacks { OnText = (text) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hText.Text("text Ω")));
            var hComment = new XmlSaxCallbacks { OnComment = (text) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hComment.Comment("comment")));
            var hProcessingInstruction = new XmlSaxCallbacks { OnProcessingInstruction = (target, data) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hProcessingInstruction.ProcessingInstruction("target", "data")));
            var hStartCData = new XmlSaxCallbacks { OnStartCData = () => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hStartCData.StartCData()));
            var hEndCData = new XmlSaxCallbacks { OnEndCData = () => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hEndCData.EndCData()));
            var hDeclaration = new XmlSaxCallbacks { OnDeclaration = (declaration) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hDeclaration.Declaration(new("1.0", "utf-8", true, Location))));
            var hStartDtd = new XmlSaxCallbacks { OnStartDtd = (documentType) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hStartDtd.StartDtd(new("root", "", "file.dtd", true, true, Location))));
            var hDtdDeclaration = new XmlSaxCallbacks { OnDtdDeclaration = (declaration) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hDtdDeclaration.DtdDeclaration(new(XmlDtdDeclarationKind.Notation, "n", "", "", "", XmlDtdDefaultKind.None, "", false, "", true, "", false, "", false, null, Location))));
            var hEndDtd = new XmlSaxCallbacks { OnEndDtd = () => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hEndDtd.EndDtd()));
            var hSkippedEntity = new XmlSaxCallbacks { OnSkippedEntity = (entity) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hSkippedEntity.SkippedEntity(new("unknown", "&unknown;", false, Location))));
            var hIncompleteAttributeDefault = new XmlSaxCallbacks { OnIncompleteAttributeDefault = (declaration) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hIncompleteAttributeDefault.IncompleteAttributeDefault(Incomplete)));
            var hDiagnostic = new XmlSaxCallbacks { OnDiagnostic = (diagnostic) => throw failure };
            Assert.Same(failure, Assert.Throws(failure.GetType(), () => hDiagnostic.Diagnostic(DiagnosticValue)));
        }
    }
    private sealed class NoOp : XmlSaxHandler { }
    internal static XmlSaxCallbacks Create(Recorder r) => new()
    {
        OnStartDocument = r.StartDocument,
        OnEndDocument = r.EndDocument,
        OnStartElement = r.StartElement,
        OnEndElement = r.EndElement,
        OnText = r.Text,
        OnComment = r.Comment,
        OnProcessingInstruction = r.ProcessingInstruction,
        OnStartCData = r.StartCData,
        OnEndCData = r.EndCData,
        OnDeclaration = r.Declaration,
        OnStartDtd = r.StartDtd,
        OnDtdDeclaration = r.DtdDeclaration,
        OnEndDtd = r.EndDtd,
        OnSkippedEntity = r.SkippedEntity,
        OnIncompleteAttributeDefault = r.IncompleteAttributeDefault,
        OnDiagnostic = r.Diagnostic,
    };
    internal static void Dispatch(IXmlSaxHandler handler)
    {
        handler.StartDocument();
        handler.EndDocument();
        handler.StartElement(new("root", "p", "urn:p", new(Rows), new(Namespaces), true, Location));
        handler.EndElement(new("root", "p", "urn:p", Location));
        handler.Text("text Ω");
        handler.Comment("comment");
        handler.ProcessingInstruction("target", "data");
        handler.StartCData();
        handler.EndCData();
        handler.Declaration(new("1.0", "utf-8", true, Location));
        handler.StartDtd(new("root", "", "file.dtd", true, true, Location));
        handler.DtdDeclaration(new(XmlDtdDeclarationKind.Notation, "n", "", "", "", XmlDtdDefaultKind.None, "", false, "", true, "", false, "", false, null, Location));
        handler.EndDtd();
        handler.SkippedEntity(new("unknown", "&unknown;", false, Location));
        handler.IncompleteAttributeDefault(Incomplete);
        handler.Diagnostic(DiagnosticValue);
    }
    internal sealed class Recorder : XmlSaxHandler
    {
        internal readonly List<int> Events = [];
        public override void StartDocument() { Events.Add(0);  }
        public override void EndDocument() { Events.Add(1);  }
        public override void StartElement(XmlStartElement element) { Events.Add(2); Assert.Equal("root", element.LocalName.ToString()); Assert.Equal("p", element.Prefix.ToString()); Assert.Equal("urn:p", element.NamespaceUri.ToString()); Assert.True(element.IsEmptyElement); Assert.Equal(Location, element.Location); Assert.Equal(2, element.Attributes.Count); Assert.Equal(1, element.NamespaceDeclarations.Count); }
        public override void EndElement(XmlEndElement element) { Events.Add(3); Assert.Equal("root", element.LocalName.ToString()); Assert.Equal("p", element.Prefix.ToString()); Assert.Equal("urn:p", element.NamespaceUri.ToString()); Assert.Equal(Location, element.Location); }
        public override void Text(ReadOnlySpan<char> text) { Events.Add(4); Assert.Equal("text Ω", text.ToString()); }
        public override void Comment(ReadOnlySpan<char> text) { Events.Add(5); Assert.Equal("comment", text.ToString()); }
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) { Events.Add(6); Assert.Equal("target", target.ToString()); Assert.Equal("data", data.ToString()); }
        public override void StartCData() { Events.Add(7);  }
        public override void EndCData() { Events.Add(8);  }
        public override void Declaration(XmlDeclaration declaration) { Events.Add(9); Assert.Equal("1.0", declaration.Version.ToString()); Assert.Equal("utf-8", declaration.EncodingName.ToString()); Assert.True(declaration.Standalone); Assert.Equal(Location, declaration.Location); }
        public override void StartDtd(XmlDocumentTypeView documentType) { Events.Add(10); Assert.Equal("root", documentType.Name.ToString()); Assert.True(documentType.HasPublicId); Assert.True(documentType.PublicId.IsEmpty); Assert.True(documentType.HasSystemId); Assert.Equal("file.dtd", documentType.SystemId.ToString()); Assert.Equal(Location, documentType.Location); }
        public override void DtdDeclaration(XmlDtdDeclaration declaration) { Events.Add(11); Assert.Equal(XmlDtdDeclarationKind.Notation, declaration.Kind); Assert.Equal("n", declaration.Name.ToString()); Assert.Null(declaration.IsExternal); Assert.True(declaration.HasSystemId); Assert.True(declaration.SystemId.IsEmpty); Assert.False(declaration.HasPublicId); Assert.False(declaration.HasValue); Assert.False(declaration.IsParameter); Assert.Equal(XmlDtdDefaultKind.None, declaration.DefaultKind); Assert.True(declaration.ElementName.IsEmpty); Assert.True(declaration.ContentModel.IsEmpty); Assert.True(declaration.AttributeType.IsEmpty); Assert.True(declaration.Value.IsEmpty); Assert.True(declaration.PublicId.IsEmpty); Assert.True(declaration.NotationName.IsEmpty); Assert.Equal(Location, declaration.Location); }
        public override void EndDtd() { Events.Add(12);  }
        public override void SkippedEntity(XmlSkippedEntity entity) { Events.Add(13); Assert.Equal("unknown", entity.Name.ToString()); Assert.Equal("&unknown;", entity.LexicalReference.ToString()); Assert.False(entity.IsParameter); Assert.Equal(Location, entity.Location); }
        public override void IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) { Events.Add(14); Assert.Same(Incomplete, declaration); }
        public override void Diagnostic(XmlDiagnostic diagnostic) { Events.Add(15); Assert.Same(DiagnosticValue, diagnostic); }
    }
}
