using System.Text;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Dtd;
using CoreAttribute = Xemele.Parsing.Syntax.XmlAttributeView;
using CoreNamespace = Xemele.Parsing.Syntax.XmlNamespaceView;

namespace Xemele.Streaming;

internal sealed partial class XmlStreamingPump : IXmlEventSink, IXmlDtdSink, IXmlIncompleteAttributeSink, IXmlUnresolvedReferenceSink
{
    private AttributeRow[] attributeRows = new AttributeRow[8];
    private NamespaceRow[] namespaceRows = new NamespaceRow[8];
    private int attributeCount, namespaceCount;
    private string localName = "", elementPrefix = "", elementUri = "";
    private XmlLocation elementLocation, lexicalLocation;
    private bool emptyElement;
    private XmlTextKind textKind;
    private string piTarget = "";
    private readonly StringBuilder token = new();

    bool IXmlEventSink.StartDocument(XmlLocation location) => true;
    public bool Declaration(XmlDeclarationView declaration)
    {
        if (decoder is not null) ValidateDeclaration(declaration.EncodingName, declaration.Location);
        if (Diagnostic is not null) return false;
        return Accept(sink!.Declaration(new(declaration.Version, declaration.EncodingName, declaration.Standalone, declaration.Location)));
    }
    public bool StartElement(XmlNameView name, bool isEmptyElement, XmlLocation location)
    {
        // Names/values persisted for exactly one tag. Core interns separately;
        // copying its borrowed names here is an explicit owner boundary.
        localName = new(name.LocalName); elementPrefix = new(name.Prefix); elementUri = new(name.NamespaceUri);
        elementLocation = location; emptyElement = isEmptyElement;
        attributeCount = namespaceCount = 0; return true;
    }
    public bool NamespaceDeclaration(CoreNamespace declaration)
    {
        if (namespaceCount == namespaceRows.Length) Array.Resize(ref namespaceRows, checked(namespaceRows.Length * 2));
        namespaceRows[namespaceCount++] = new(new(declaration.Prefix), new(declaration.NamespaceUri), declaration.Location);
        return true;
    }
    public bool Attribute(CoreAttribute attribute)
    {
        AddAttribute(new(new(attribute.Name.LocalName), new(attribute.Name.Prefix), new(attribute.Name.NamespaceUri),
            new(attribute.Value), attribute.IsDefaulted, true, default, attribute.Location));
        return true;
    }
    public bool IncompleteAttribute(XmlIncompleteAttributeView attribute)
    {
        AddAttribute(new(new(attribute.Name.LocalName), new(attribute.Name.Prefix), new(attribute.Name.NamespaceUri),
            "", attribute.IsDefaulted, false, attribute.Parts.ToArray(), attribute.IsDefaulted ? elementLocation : attribute.ValueLocation));
        return true;
    }
    private void AddAttribute(AttributeRow row)
    {
        if (attributeCount == attributeRows.Length) Array.Resize(ref attributeRows, checked(attributeRows.Length * 2));
        attributeRows[attributeCount++] = row;
    }
    public bool StartElementContent()
    {
        try
        {
            return Accept(sink!.StartElement(new(localName, elementPrefix, elementUri,
                new(attributeRows.AsSpan(0, attributeCount)), new(namespaceRows.AsSpan(0, namespaceCount)), emptyElement, elementLocation)));
        }
        finally
        {
            // Views expire at callback return, even when the sink accepts/pauses.
            Array.Clear(attributeRows, 0, attributeCount); Array.Clear(namespaceRows, 0, namespaceCount);
            attributeCount = namespaceCount = 0; localName = elementPrefix = elementUri = "";
        }
    }
    public bool EndElement(XmlNameView name, XmlLocation location) => Accept(sink!.EndElement(new(name.LocalName, name.Prefix, name.NamespaceUri, location)));
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location)
    {
        textKind = kind; lexicalLocation = location;
        if (kind == XmlTextKind.ProcessingInstruction) piTarget = new(target);
        if (kind is XmlTextKind.Comment or XmlTextKind.ProcessingInstruction) token.Clear();
        return kind != XmlTextKind.CData || Accept(sink!.StartCData(location));
    }
    public bool TextFragment(ReadOnlySpan<char> text, bool tokenEnded, XmlLocation location)
    {
        if (textKind is XmlTextKind.Comment or XmlTextKind.ProcessingInstruction)
        {
            token.Append(text);
            if (!tokenEnded) return true;
            // One complete lexical token; no per-fragment strings or event list.
            // StringBuilder's storage is retained scratch and the final string
            // is the bounded callback owner, released at callback return.
            string body = token.ToString(); token.Clear();
            if (textKind == XmlTextKind.Comment) return Accept(sink!.Comment(body, lexicalLocation));
            try { return Accept(sink!.ProcessingInstruction(piTarget, body, lexicalLocation)); }
            finally { piTarget = ""; }
        }
        if (textKind == XmlTextKind.CData && tokenEnded) return Accept(sink!.EndCData(location));
        if (text.IsEmpty) return true;
        return Accept(sink!.Text(text, location));
    }
    public bool EndDocument(XmlLocation location)
    {
        if (recovery is { Items.Count: > 0 }) return true;
        return Accept(sink!.EndDocument(location));
    }
    public bool UnresolvedReference(XmlUnresolvedReferenceView reference) => Accept(sink!.SkippedEntity(new(reference.Name, reference.LexicalReference, false, reference.Location)));
    public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView declaration) => Accept(sink!.IncompleteAttributeDefault(
        new(new(declaration.ElementName), new(declaration.AttributeName), new(declaration.LexicalValue), declaration.IsTokenized,
            declaration.IsExternal, declaration.DeclarationLocation, declaration.ValueLocation, Array.AsReadOnly(declaration.Parts.ToArray()), declaration.DeclarationSequence)));

    private DtdModel? model;
    private XmlDocumentType? documentType;
    private XmlLocation dtdLocation;
    private int modelCollection, modelIndex;
    public bool DocumentType(XmlDocumentType declaration, DtdModel effectiveModel, XmlLocation location)
    {
        documentType = declaration; model = effectiveModel; dtdLocation = location; modelCollection = -1; modelIndex = 0;
        // Internal pause captures the exact consumed closing scalar before any
        // following input or defaults can run. No public notification is replayed.
        return false;
    }
    private bool DrainModel()
    {
        while (model is not null)
        {
            cancellation.ThrowIfCancellationRequested();
            if (modelCollection == -1)
            {
                modelCollection = 0;
                if (!sink!.StartDtd(new(documentType!.Name, documentType.PublicId, documentType.SystemId,
                    documentType.PublicId is not null, documentType.SystemId is not null, dtdLocation))) return false;
                continue;
            }
            if (modelCollection == 5)
            {
                model = null; documentType = null;
                if (!sink!.EndDtd(lastPrimaryLocation)) return false;
                continue;
            }
            int count = modelCollection switch { 0 => model.Elements.Count, 1 => model.Attributes.Count, 2 => model.GeneralEntities.Count, 3 => model.ParameterEntities.Count, _ => model.Notations.Count };
            if (modelIndex == count) { modelIndex = 0; modelCollection++; continue; }
            int index = modelIndex++;
            bool accepted;
            switch (modelCollection)
            {
                case 0:
                    var element = model.Elements[index];
                    accepted = sink!.DtdDeclaration(new(XmlDtdDeclarationKind.Element, element.Name, [], ProjectContent(element.Content), [],
                        XmlDtdDefaultKind.None, [], false, [], false, [], false, [], false, element.IsExternal, element.Location)); break;
                case 1:
                    var attribute = model.Attributes[index];
                    accepted = sink!.DtdDeclaration(new(XmlDtdDeclarationKind.Attribute, attribute.Name, attribute.ElementName, [], ProjectType(attribute),
                        (XmlDtdDefaultKind)((int)attribute.DefaultKind + 1), attribute.DefaultValue, attribute.DefaultValue is not null,
                        [], false, [], false, [], false, attribute.IsExternal, attribute.Location)); break;
                case 2: case 3:
                    var entity = modelCollection == 2 ? model.GeneralEntities[index] : model.ParameterEntities[index];
                    accepted = sink!.DtdDeclaration(new(XmlDtdDeclarationKind.Entity, entity.Name, [], [], [], XmlDtdDefaultKind.None,
                        entity.ReplacementText, entity.ReplacementText is not null, entity.SystemId, entity.SystemId is not null, entity.PublicId,
                        entity.PublicId is not null, entity.NotationName, entity.IsParameter, entity.IsExternal, entity.Location)); break;
                default:
                    var notation = model.Notations[index];
                    accepted = sink!.DtdDeclaration(new(XmlDtdDeclarationKind.Notation, notation.Name, [], [], [], XmlDtdDefaultKind.None,
                        [], false, notation.SystemId, notation.SystemId is not null, notation.PublicId, notation.PublicId is not null, [], false, null, notation.Location)); break;
            }
            if (!accepted) return false;
        }
        return true;
    }
    private readonly List<(DtdParticle Particle, int Child)> particles = [];
    private string ProjectContent(DtdParticle root)
    {
        token.Clear(); particles.Clear(); particles.Add((root, -1));
        while (particles.Count != 0)
        {
            cancellation.ThrowIfCancellationRequested();
            int top = particles.Count - 1;
            var (node, child) = particles[top];
            if (child == -1)
            {
                switch (node.Kind)
                {
                    case DtdParticleKind.Empty: token.Append("EMPTY"); break;
                    case DtdParticleKind.Any: token.Append("ANY"); break;
                    case DtdParticleKind.Name: token.Append(node.Name); break;
                    case DtdParticleKind.Mixed: token.Append("(#PCDATA"); break;
                    default: token.Append('('); break;
                }
                child = 0;
            }
            if (child < node.Children.Count)
            {
                if (child != 0 || node.Kind == DtdParticleKind.Mixed) token.Append(node.Kind == DtdParticleKind.Sequence ? ',' : '|');
                particles[top] = (node, child + 1); particles.Add((node.Children[child], -1)); continue;
            }
            if (node.Kind is DtdParticleKind.Sequence or DtdParticleKind.Choice or DtdParticleKind.Mixed) token.Append(')');
            token.Append(node.Occurrence switch { DtdOccurrence.Optional => "?", DtdOccurrence.ZeroOrMore => "*", DtdOccurrence.OneOrMore => "+", _ => "" });
            particles.RemoveAt(top);
        }
        string value = token.ToString(); token.Clear(); return value;
    }
    private string ProjectType(DtdAttributeDeclaration attribute)
    {
        if (attribute.Type is not (DtdAttributeType.Enumeration or DtdAttributeType.Notation)) return attribute.Type switch
        {
            DtdAttributeType.CData => "CDATA", DtdAttributeType.Id => "ID", DtdAttributeType.IdRef => "IDREF", DtdAttributeType.IdRefs => "IDREFS",
            DtdAttributeType.Entity => "ENTITY", DtdAttributeType.Entities => "ENTITIES", DtdAttributeType.NmToken => "NMTOKEN", _ => "NMTOKENS"
        };
        token.Clear();
        if (attribute.Type == DtdAttributeType.Notation) token.Append("NOTATION ");
        token.Append('(');
        for (int i = 0; i < attribute.Tokens.Count; i++) { cancellation.ThrowIfCancellationRequested(); if (i != 0) token.Append('|'); token.Append(attribute.Tokens[i]); }
        token.Append(')'); string result = token.ToString(); token.Clear(); return result;
    }
}
