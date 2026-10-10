#if INCOMPLETE_DRIVER
global using EntityBaseBenchmarks = Xemele.Benchmarks.IncompleteAttributeBenchmarks;
#endif
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Dtd;
namespace Xemele.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class IncompleteAttributeBenchmarks
{
    private const string Prefix = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST item a NMTOKENS ' A &x; B&#9;'>]>";
    private const string Item = "<item/>";
    private const string Document = Prefix + "<r>" + Item + Item + "</r>";
    private readonly XmlParseOptions options = new() { DtdProcessing = XmlDtdProcessing.Parse };
    private readonly OracleSink sink = new();
    [GlobalSetup] public void Setup() { EntityBaseProvenance.Gate("before"); Check(); }
    [GlobalCleanup] public void Cleanup() => EntityBaseProvenance.Gate("after");
    [Benchmark] public object InternalNestedModel()
    {
        sink.Reset(); using var core = new XmlParserCore(options);
        var result = core.Append(Document, [], true, sink, default);
        if (result.Step != ScanStep.Completed || result.Consumed != Document.Length || result.Diagnostic is not null ||
            sink.Values != 2 || sink.Defaults != 1 || sink.Ends != 3 || sink.Stage != 5)
            throw new Exception("Complete actual optional event oracle.");
        return sink;
    }
    [Benchmark] public object RedirectedComposition() => XmlDocument.Parse(Document, options);
    public void Check()
    {
        InternalNestedModel(); var doc = (XmlDocument)RedirectedComposition();
        if (doc.RootElement.LocalName != "r" || doc.RootElement.NamespaceUri != "" || doc.RootElement.Prefix != "" ||
            doc.RootElement.Parent != doc.DocumentNode || doc.DocumentNode.FirstChild != doc.RootElement ||
            doc.DocumentNode.LastChild != doc.RootElement || doc.RootElement.Children.Count != 2 ||
            doc.IncompleteAttributeDefaults.Count != 1 || doc.IncompleteAttributeValues.Count != 2 || doc.UnresolvedReferences.Count != 0)
            throw new Exception("Actual owned document/root oracle.");
        var declaration = doc.IncompleteAttributeDefaults[0]; CheckParts(declaration.Parts);
        if (declaration.ElementName != "item" || declaration.AttributeName != "a" || declaration.LexicalValue != " A &x; B&#9;" ||
            !declaration.IsTokenized || declaration.IsExternal || declaration.Sequence != 0 ||
            declaration.DeclarationLocation != new XmlLocation(null, 1, Prefix.IndexOf(" a NMTOKENS", StringComparison.Ordinal) + 2, null) ||
            declaration.ValueLocation != new XmlLocation(null, 1, Prefix.IndexOf(" A &x;", StringComparison.Ordinal) + 1, null))
            throw new Exception("Actual owned declaration oracle.");
        XmlNode previous = default; int index = 0;
        foreach (var child in doc.RootElement.Children)
        {
            var value = doc.IncompleteAttributeValues[index];
            if (child.Kind != XmlNodeKind.Element || child.LocalName != "item" || child.NamespaceUri != "" || child.Prefix != "" ||
                child.Parent != doc.RootElement || child.PreviousSibling != previous || !child.FirstChild.IsDefault ||
                child.Attributes.Count != 1 || child.NamespaceDeclarations.Count != 0 || value.Parent != child ||
                value.Attribute.OwnerElement != child || value.Attribute.LocalName != "a" || value.Attribute.NamespaceUri != "" ||
                value.Attribute.Prefix != "" || !value.Attribute.IsAttached || value.Sequence != index || !ReferenceEquals(value.Default, declaration) ||
                !ReferenceEquals(value.Parts, declaration.Parts) || value.ValueLocation != declaration.ValueLocation ||
                value.LexicalValue != declaration.LexicalValue || !value.IsTokenized)
                throw new Exception("Actual owned member/default/topology oracle.");
            CheckParts(value.Parts);
            try { _ = value.Attribute.Value; throw new Exception("Missing getter refusal."); }
            catch (InvalidOperationException e) when (e.Message == "Attribute value contains unresolved entity references.") { }
            previous = child; index++;
        }
        if (index != 2 || !previous.NextSibling.IsDefault || doc.RootElement.FirstChild != doc.RootElement.Children[0] ||
            doc.RootElement.LastChild != previous || doc.RootElement.Attributes.Count != 0 || doc.RootElement.NamespaceDeclarations.Count != 0 ||
            !doc.RootElement.PreviousSibling.IsDefault || !doc.RootElement.NextSibling.IsDefault)
            throw new Exception("Actual complete topology oracle.");
        CheckMetadata(doc.DocumentType!, doc.Dtd!, At(0));
        try { doc.ToXmlString(); throw new Exception("Missing owner refusal."); }
        catch (InvalidOperationException e) when (e.Message == "Document contains unresolved entity references.") { }
        if (Environment.GetCommandLineArgs().Contains("--allocation")) BorrowedAllocation();
    }
    private static XmlLocation At(int offset) => new(null, 1, offset + 1, null);
    private static readonly string Literal = " A &x; B&#9;";
    private static readonly XmlLocation DeclarationAt = At(Prefix.IndexOf(" a NMTOKENS", StringComparison.Ordinal) + 1);
    private static readonly XmlLocation ValueAt = At(Prefix.IndexOf(Literal, StringComparison.Ordinal));
    private static void CheckParts(IReadOnlyList<XmlAttributeValuePart> parts)
    {
        if (parts.Count != 3) throw new Exception("Owned part count.");
        CheckPart(parts[0], 0); CheckPart(parts[1], 1); CheckPart(parts[2], 2);
    }
    private static void CheckParts(ReadOnlySpan<XmlAttributeValuePart> parts)
    {
        if (parts.Length != 3) throw new Exception("Borrowed part count.");
        for (int i = 0; i < parts.Length; i++) CheckPart(parts[i], i);
    }
    private static void CheckPart(XmlAttributeValuePart part, int index)
    {
        bool valid = index switch
        {
            0 => part is XmlKnownAttributeText { Text: " A " } && part.Location == ValueAt,
            1 => part is XmlUnknownAttributeReference { Name: "x", LexicalReference: "&x;" } && part.Location == At(Prefix.IndexOf("&x;", StringComparison.Ordinal)),
            2 => part is XmlKnownAttributeText { Text: " B\t" } && part.Location == At(Prefix.IndexOf(" B&#9;", StringComparison.Ordinal)),
            _ => false
        };
        if (!valid) throw new Exception("Complete stage-one text/reference/location oracle.");
    }
    private static void CheckMetadata(XmlDocumentType type, DtdModel model, XmlLocation location)
    {
        if (type.Name != "r" || type.PublicId is not null || type.SystemId is not null ||
            type.InternalSubset != Prefix[(Prefix.IndexOf('[') + 1)..Prefix.LastIndexOf(']')] || location != At(0) ||
            model.Elements.Count != 0 || model.GeneralEntities.Count != 0 || model.Attributes.Count != 1 ||
            model.ParameterEntities.Count != 1 || model.Notations.Count != 0)
            throw new Exception("Complete DOCTYPE/effective table oracle.");
        var parameter = model.ParameterEntities[0]; var attribute = model.Attributes[0];
        if (parameter.Name != "p" || !parameter.IsParameter || parameter.ReplacementText != "" || parameter.SystemId is not null ||
            parameter.PublicId is not null || parameter.NotationName is not null || parameter.IsExternal ||
            parameter.Location != At(Prefix.IndexOf("<!ENTITY", StringComparison.Ordinal)) ||
            attribute.ElementName != "item" || attribute.Name != "a" || attribute.Type != DtdAttributeType.NmTokens ||
            attribute.Tokens.Count != 0 || attribute.DefaultKind != DtdDefaultKind.Value || attribute.DefaultValue != Literal ||
            attribute.IsExternal || attribute.Location != DeclarationAt)
            throw new Exception("Complete DTD declaration field/location oracle.");
        if (!DtdLiteralInfo.Defaults.TryGetValue(attribute, out var literal) || !DtdLiteralInfo.Entities.TryGetValue(parameter, out var empty))
            throw new Exception("Missing retained literal sidecars.");
        CheckLiteral(literal, Literal, ValueAt, DeclarationAt, true);
        CheckLiteral(empty, "", At(Prefix.IndexOf("''", StringComparison.Ordinal) + 1), null, false);
    }
    private static void CheckLiteral(DtdLiteralInfo literal, string text, XmlLocation valueAt, XmlLocation? declarationAt, bool preceding)
    {
        if (literal.Text != text || literal.LexicalValue != text || literal.PhysicalStream != text || literal.ValueLocation != valueAt ||
            literal.DeclarationLocation != declarationAt || literal.Locations.Length != text.Length || literal.ExternalOrigins.Length != text.Length ||
            literal.PhysicalStarts?.Length != text.Length || literal.PhysicalLengths?.Length != text.Length || literal.PhysicalIdentities?.Length != text.Length ||
            (preceding ? literal.PrecedingEntities?.Count != 0 : literal.PrecedingEntities is not null))
            throw new Exception("Complete retained literal/array/table oracle.");
        for (int i = 0; i < text.Length; i++)
            if (literal.Locations[i] != new XmlLocation(null, 1, valueAt.LinePosition + i, null) || literal.ExternalOrigins[i] ||
                literal.PhysicalStarts![i] != i || literal.PhysicalLengths![i] != 1 || literal.PhysicalIdentities![i].GetType() != typeof(object) ||
                !ReferenceEquals(literal.PhysicalIdentities[0], literal.PhysicalIdentities[i]))
                throw new Exception("Retained physical maps/origin/owner oracle.");
    }
    private void BorrowedAllocation()
    {
        var borrowed = new OracleSink(); using var core = new XmlParserCore(options);
        if (core.Append(Prefix + "<r>", [], false, borrowed, default).Step != ScanStep.NeedInput) throw new Exception("Borrowed initialization.");
        for (int i = 0; i < 128; i++) AppendBorrowed(core, borrowed);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) AppendBorrowed(core, borrowed);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
        if (bytes != 0 || borrowed.Values != 1152 || borrowed.Defaults != 1 || borrowed.Ends != 1152 || borrowed.Stage != 3)
            throw new Exception("Warm actual borrowed default dispatch/count oracle: " + bytes);
        if (core.Append("</r>", [], true, borrowed, default).Step != ScanStep.Completed || borrowed.Stage != 5)
            throw new Exception("Borrowed finalization.");
        Console.WriteLine("INCOMPLETE_BORROWED " + JsonSerializer.Serialize(new { iterations = 1024, bytes, utf16PerOperation = Item.Length,
            persistedInterpretations = 1, persistedParts = 3, reusedApplications = 1152, emptyOrdinaryLists = true, coldAndOwnedExcluded = true }));
        long before = GC.GetAllocatedBytesForCurrentThread(); var ordinary = new XmlDocument();
        _ = ordinary.IncompleteAttributeDefaults.Count; _ = ordinary.IncompleteAttributeValues.Count;
        long emptySetupBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) if (ordinary.IncompleteAttributeDefaults.Count != 0 || ordinary.IncompleteAttributeValues.Count != 0) throw new Exception("Ordinary empty-list oracle.");
        long emptyGetterBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        if (emptyGetterBytes != 0) throw new Exception("Ordinary warm empty-list getter allocation.");
        var retainedDocuments = new XmlDocument[128];
        long retainedBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < retainedDocuments.Length; i++) retainedDocuments[i] = XmlDocument.Parse(Document, options);
        long retainedBytes = GC.GetTotalMemory(true) - retainedBefore;
        GC.KeepAlive(retainedDocuments);
        foreach (var retained in retainedDocuments) CheckMetadata(retained.DocumentType!, retained.Dtd!, At(0));
        DtdLiteralInfo.Defaults.TryGetValue(retainedDocuments[0].Dtd!.Attributes[0], out var literal);
        if (literal is null || literal.LexicalValue != " A &x; B&#9;" || literal.Locations.Length != literal.Text.Length ||
            literal.ExternalOrigins.Length != literal.Text.Length || literal.PrecedingEntities?.Count != 0)
            throw new Exception("Retained literal/location/table sidecar oracle.");
        Console.WriteLine("INCOMPLETE_EMPTY " + JsonSerializer.Serialize(new { iterations = 1024, emptySetupBytes, emptyGetterBytes,
            retainedDocuments = retainedDocuments.Length, retainedBytes, literalLocationEntries = literal.Locations.Length,
            literalOriginEntries = literal.ExternalOrigins.Length, physicalStartEntries = literal.PhysicalStarts!.Length,
            physicalLengthEntries = literal.PhysicalLengths!.Length, physicalIdentityEntries = literal.PhysicalIdentities!.Length, precedingEntityEntries = literal.PrecedingEntities!.Count,
            boundedDefaultCount = 1, boundedPartCount = 3, boundedLexicalUnits = " A &x; B&#9;".Length, noSourceBuffersRetained = true }));
    }
    private static void AppendBorrowed(XmlParserCore core, OracleSink sink)
    {
        var result = core.Append(Item, [], false, sink, default);
        if (result.Step != ScanStep.NeedInput || result.Consumed != Item.Length || result.Diagnostic is not null) throw new Exception("Borrowed actual append oracle.");
    }
    private sealed class OracleSink : IXmlEventSink, IXmlIncompleteAttributeSink, IXmlDtdSink
    {
        internal int Values, Defaults, Ends, Stage;
        private bool inItem, attributeDelivered;
        internal void Reset() { Values = Defaults = Ends = Stage = 0; inItem = attributeDelivered = false; }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView view)
        {
            if (Stage != 1 || Defaults != 0 || !view.ElementName.SequenceEqual("item") ||
                !view.AttributeName.SequenceEqual("a") || !view.LexicalValue.SequenceEqual(" A &x; B&#9;") ||
                !view.IsTokenized || view.IsExternal || view.DeclarationSequence != 0 || view.Parts.Length != 3 ||
                view.Parts[1] is not XmlUnknownAttributeReference { Name: "x", LexicalReference: "&x;" }) throw new Exception("Borrowed default oracle.");
            if (view.DeclarationLocation != DeclarationAt || view.ValueLocation != ValueAt) throw new Exception("Borrowed declaration locations.");
            CheckParts(view.Parts); Defaults++; return true;
        }
        public bool IncompleteAttribute(XmlIncompleteAttributeView view)
        {
            if (!inItem || attributeDelivered || Stage != 3 || Defaults != 1 || !view.Name.LocalName.SequenceEqual("a") ||
                !view.Name.NamespaceUri.IsEmpty || !view.Name.Prefix.IsEmpty || !view.IsTokenized || view.DefaultDeclarationSequence != 0 ||
                !view.LexicalValue.SequenceEqual(" A &x; B&#9;") || view.Parts.Length != 3 ||
                view.Parts[0] is not XmlKnownAttributeText { Text: " A " } || view.Parts[1] is not XmlUnknownAttributeReference { Name: "x", LexicalReference: "&x;" } ||
                view.Parts[2] is not XmlKnownAttributeText { Text: " B\t" } ||
                view.Parts[1].Location != new XmlLocation(null, 1, Prefix.IndexOf("&x;", StringComparison.Ordinal) + 1, null)) throw new Exception("Borrowed resolved name/parts/order oracle.");
            if (view.ValueLocation != ValueAt) throw new Exception("Borrowed value location.");
            CheckParts(view.Parts); Values++; attributeDelivered = true; return true;
        }
        public bool DocumentType(XmlDocumentType t, DtdModel m, XmlLocation l)
        { if (Stage != 1) throw new Exception("Borrowed metadata order."); CheckMetadata(t, m, l); return true; }
        public bool StartDocument(XmlLocation l) { if (Stage != 0 || l != new XmlLocation(null, 1, 1, null)) throw new Exception("Document order."); Stage = 1; return true; }
        public bool Declaration(XmlDeclarationView v) => throw new Exception("Unexpected declaration.");
        public bool StartElement(XmlNameView v, bool empty, XmlLocation l)
        {
            if (Stage == 1) { if (!v.LocalName.SequenceEqual("r") || empty || Defaults != 1) throw new Exception("Root order."); Stage = 2; }
            else { if (Stage != 3 || !v.LocalName.SequenceEqual("item") || !empty || inItem) throw new Exception("Item order."); inItem = true; attributeDelivered = false; }
            if (!v.NamespaceUri.IsEmpty || !v.Prefix.IsEmpty) throw new Exception("Resolved name."); return true;
        }
        public bool NamespaceDeclaration(XmlNamespaceView v) => throw new Exception("Unexpected namespace.");
        public bool Attribute(XmlAttributeView v) => throw new Exception("Unexpected complete attribute.");
        public bool StartElementContent()
        { if (Stage == 2) Stage = 3; else if (!inItem || !attributeDelivered) throw new Exception("Content order."); return true; }
        public bool EndElement(XmlNameView v, XmlLocation l)
        {
            if (inItem) { if (!attributeDelivered || !v.LocalName.SequenceEqual("item")) throw new Exception("Item end."); inItem = false; }
            else { if (Stage != 3 || !v.LocalName.SequenceEqual("r")) throw new Exception("Root end."); Stage = 4; }
            Ends++; return true;
        }
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => throw new Exception("Unexpected text.");
        public bool TextFragment(ReadOnlySpan<char> t, bool ended, XmlLocation l) => throw new Exception("Unexpected fragment.");
        public bool EndDocument(XmlLocation l) { if (Stage != 4 || inItem) throw new Exception("Document end."); Stage = 5; return true; }
    }
}
