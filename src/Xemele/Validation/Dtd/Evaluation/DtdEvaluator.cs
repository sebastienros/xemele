using System.Buffers;
using System.Text;
using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

// Schema storage is immutable. All scratch, indices and diagnostics below belong
// to this one validation; borrowed QName/value spans never leave their call.
internal sealed class DtdEvaluator(CompiledDtdModel model, string? schemaRoot, XmlDocument document, ValidationOperation operation)
{
    private readonly long revision = document.ContentRevision;
    private HashSet<string>? ids, references;
    private long characters;
    private int retained;

    internal static void CheckRevision(XmlDocument document, long captured)
    {
        if (captured < 0 || document.ContentRevision != captured)
            throw new InvalidOperationException("Document changed during DTD validation.");
    }
    private bool Work(long amount = 1)
    {
        CheckRevision(document, revision);
        return operation.ChargeOperations(amount);
    }
    private void Error(string code, string message, XmlLocation location = default)
    {
        if (operation.IsComplete) operation.AddDiagnostic(new(code, XmlDiagnosticSeverity.Error, message, location));
    }

    internal static bool HasOriginalSourceProof(XmlDocument document) =>
        document.SourceInputComplete && document.ValidationSourceContentRevision is long stamp &&
        stamp >= 0 && stamp == document.ContentRevision && document.Dtd is { UnreadParameterLocation: null };

    // Symbolic values remain symbolic. All occurrences must be proven absent in
    // the fully read original associated model; a declared reference stays unknown.
    internal static bool ProvesUndeclaredReferences(XmlDocument document, ValidationOperation operation)
    {
        if (!HasOriginalSourceProof(document) || document.HasIncompleteAttributes) return false;
        long captured = document.ContentRevision;
        var occurrences = document.UnresolvedReferences;
        var declarations = document.Dtd!.GeneralEntities;
        for (int i = 0; i < occurrences.Count; i++)
        {
            CheckRevision(document, captured);
            if (!operation.ChargeOperations()) return false;
            string name = occurrences[i].Name;
            for (int d = 0; d < declarations.Count; d++)
            {
                CheckRevision(document, captured);
                if (!operation.ChargeOperations()) return false;
                if (DtdLexical.Equal(name, declarations[d].Name, operation)) return false;
                if (!operation.IsComplete) return false;
            }
        }
        CheckRevision(document, captured);
        return occurrences.Count != 0;
    }

    private void ApplyInstanceWitness()
    {
        if (!HasOriginalSourceProof(document) || document.ValidationInstanceDiagnostic is not { } diagnostic ||
            document.ValidationInstanceDeclaration is not { } declaration || !Work()) return;
        // The witness is applicable only to the same lexical declaration category.
        // Different ANY/mixed profiles never inherit original content constraints.
        var current = model.FindElement(declaration.Name, operation);
        if (!operation.IsComplete) return;
        if (current is null) { operation.Refuse("DTD.IncompleteInput", "Original source declaration profile changed."); return; }
        bool empty = declaration.Content.Kind == DtdParticleKind.Empty;
        bool applies = empty ? current.Content.Kind == DtdParticleKind.Empty :
            current.Content.Kind is DtdParticleKind.Name or DtdParticleKind.Sequence or DtdParticleKind.Choice;
        if (applies) operation.AddDiagnostic(diagnostic);
        else operation.Refuse("DTD.IncompleteInput", "Original source declaration profile changed.");
    }

    internal ValidationResult Run()
    {
        Span<char> elementScratch = stackalloc char[256];
        Span<char> memberScratch = stackalloc char[256];
        var elementNames = new QNameBuffer(elementScratch, operation);
        var memberNames = new QNameBuffer(memberScratch, operation);
        try
        {
            if (!Work()) return Finish();
            if (!document.SourceInputComplete || document.HasIncompleteAttributes || document.Dtd?.UnreadParameterLocation is not null ||
                document.ValidationStandalone == true && document.ValidationDeclarationLocation == default)
            { operation.Refuse("DTD.IncompleteInput", "Document input or standalone provenance is incomplete."); return Finish(); }
            if (document.UnresolvedReferences.Count != 0)
            {
                if (ReferenceEquals(model.Source, document.Dtd) && ProvesUndeclaredReferences(document, operation))
                    Error("DTD.UndeclaredEntity", "A general entity has no declaration in the complete associated DTD.", document.UnresolvedReferences[0].Location);
                else if (operation.IsComplete) operation.Refuse("DTD.IncompleteInput", "Unresolved entity provenance is incomplete.");
                // No hypothetical empty replacement is used for content validation.
                return Finish();
            }
            ApplyInstanceWitness();
            if (!operation.IsComplete) return Finish();
            var root = document.RootElement;
            if (root.IsDefault) { Error("DTD.Root", "A document element is required."); return Finish(); }
            string? expectedRoot = schemaRoot ?? document.DocumentType?.Name;
            if (expectedRoot is not null && !DtdLexical.Equal(elementNames.Make(root.Prefix, root.LocalName), expectedRoot, operation))
                Error("DTD.Root", "Document element does not match the DOCTYPE root name.");
            XmlNode node = document.DocumentNode.FirstChild;
            int depth = 0;
            while (!node.IsDefault && operation.IsComplete)
            {
                if (!Work()) break;
                if (node.Kind == XmlNodeKind.Element)
                {
                    if (depth == int.MaxValue || operation.Limits.Depth is int limit && depth >= limit)
                    { operation.Refuse("VALIDATION.Limit", "Element depth limit exceeded."); break; }
                    ValidateElement(node, ref elementNames, ref memberNames);
                }
                else if (node.Parent.Kind == XmlNodeKind.Document && node.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                {
                    if (!WhiteText(node.Value)) Error("DTD.Content", "Character data outside the document element is invalid.");
                }
                else if (node.Kind is XmlNodeKind.Comment or XmlNodeKind.ProcessingInstruction) Text(node.Value);
                if (!operation.IsComplete) break;
                if (!node.FirstChild.IsDefault) { node = node.FirstChild; depth++; continue; }
                while (node.NextSibling.IsDefault)
                {
                    if (!Work()) break;
                    node = node.Parent;
                    if (node.Kind == XmlNodeKind.Document) { node = default; break; }
                    depth--;
                }
                if (!node.IsDefault && operation.IsComplete) node = node.NextSibling;
            }
            if (references is not null)
                foreach (string reference in references)
                {
                    if (!Work(reference.Length + 1L)) break;
                    if (ids?.Contains(reference) != true) Error("DTD.IdRef", "An IDREF has no matching ID.");
                }
            return Finish();
        }
        finally { elementNames.Dispose(); memberNames.Dispose(); }
    }
    private ValidationResult Finish() { CheckRevision(document, revision); return operation.Complete(); }

    private void ValidateElement(XmlNode node, ref QNameBuffer elementNames, ref QNameBuffer memberNames)
    {
        var name = elementNames.Make(node.Prefix, node.LocalName);
        if (!Text(name, token: true)) return;
        var element = model.FindElement(name, operation);
        if (!operation.IsComplete) return;
        if (element is null) Error("DTD.UndeclaredElement", "An element has no declaration.");
        int count = 0;
        foreach (var attribute in node.Attributes)
        {
            if (!Member(ref count)) return;
            ValidateAttribute(name, memberNames.Make(attribute.Prefix, attribute.LocalName), attribute.Value);
            if (!operation.IsComplete) return;
        }
        foreach (var declaration in node.NamespaceDeclarations)
        {
            if (!Member(ref count)) return;
            ValidateAttribute(name, memberNames.Make(declaration.Prefix.Length == 0 ? "" : "xmlns", declaration.Prefix.Length == 0 ? "xmlns" : declaration.Prefix), declaration.Value);
            if (!operation.IsComplete) return;
        }
        for (int attributeIndex = 0; attributeIndex < model.Attributes.Count; attributeIndex++)
        {
            var attribute = model.Attributes[attributeIndex];
            if (!Work()) return;
            if (!DtdLexical.Equal(name, attribute.Declaration.ElementName, operation)) { if (!operation.IsComplete) return; continue; }
            bool found = false;
            foreach (var actual in node.Attributes)
            {
                if (!Work()) return;
                if (DtdLexical.Equal(memberNames.Make(actual.Prefix, actual.LocalName), attribute.Declaration.Name, operation)) { found = true; break; }
                if (!operation.IsComplete) return;
            }
            if (!found)
                foreach (var actual in node.NamespaceDeclarations)
                {
                    if (!Work()) return;
                    if (DtdLexical.Equal(memberNames.Make(actual.Prefix.Length == 0 ? "" : "xmlns", actual.Prefix.Length == 0 ? "xmlns" : actual.Prefix), attribute.Declaration.Name, operation)) { found = true; break; }
                    if (!operation.IsComplete) return;
                }
            if (!found)
            {
                if (attribute.Declaration.DefaultKind == DtdDefaultKind.Required)
                    Error("DTD.RequiredAttribute", "A required attribute is missing.");
                if (document.ValidationStandalone == true && attribute.Declaration.IsExternal && attribute.NormalizedDefault is not null)
                    Error("DTD.Standalone", "Standalone input depends on an external attribute default.", attribute.Declaration.Location);
            }
        }
        int state = element?.Content.InitialState ?? 0;
        foreach (var child in node.Children)
        {
            if (!Work()) return;
            if (element?.Content.Kind == DtdParticleKind.Empty && (child.Kind != XmlNodeKind.Text || child.Value.Length != 0))
                Error("DTD.Content", "An EMPTY element must have no content nodes.");
            if (child.Kind == XmlNodeKind.Element)
            {
                if (element is not null && !element.Content.TryAdvance(ref state, memberNames.Make(child.Prefix, child.LocalName), operation))
                    Error("DTD.Content", "Element children do not match the content model.");
            }
            else if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
            {
                bool white = WhiteText(child.Value);
                if (element is null) continue;
                if (element.Content.Kind == DtdParticleKind.Empty && child.Value.Length != 0 ||
                    element.Content.Kind is DtdParticleKind.Sequence or DtdParticleKind.Choice or DtdParticleKind.Name && (child.Kind == XmlNodeKind.CData || !white))
                    Error("DTD.Content", "Character data is not permitted by the content model.");
                if (document.ValidationStandalone == true && element.Declaration.IsExternal &&
                    element.Content.Kind is DtdParticleKind.Sequence or DtdParticleKind.Choice or DtdParticleKind.Name &&
                    child.Kind == XmlNodeKind.Text && child.Value.Length != 0 && white)
                {
                    if (!HasValidatedWhitespace(name))
                        operation.Refuse("DTD.IncompleteInput", "Standalone whitespace source provenance is unknown.");
                }
            }
            if (!operation.IsComplete) return;
        }
        if (element is not null && !element.Content.IsAccepting(state)) Error("DTD.Content", "Element content is incomplete.");
    }
    private bool HasValidatedWhitespace(ReadOnlySpan<char> name)
    {
        if (!document.SourceInputComplete || document.ValidationSourceContentRevision is not long stamp ||
            stamp < 0 || stamp != revision || document.Dtd is not { } original) return false;
        if (!Work()) return false;
        if (ReferenceEquals(model.Source, original)) return true;
        for (int declarationIndex = 0; declarationIndex < original.Elements.Count; declarationIndex++)
        {
            var declaration = original.Elements[declarationIndex];
            if (!Work()) return false;
            if (!DtdLexical.Equal(name, declaration.Name, operation)) { if (!operation.IsComplete) return false; continue; }
            return declaration.IsExternal && declaration.Content.Kind is DtdParticleKind.Sequence or DtdParticleKind.Choice or DtdParticleKind.Name;
        }
        return false;
    }
    private bool Member(ref int count)
    {
        if (!Work()) return false;
        if (count == int.MaxValue || operation.Limits.AttributesPerElement is int cap && count >= cap)
        { operation.Refuse("VALIDATION.Limit", "Attribute count limit exceeded."); return false; }
        count++; return true;
    }
    private void ValidateAttribute(ReadOnlySpan<char> element, ReadOnlySpan<char> name, string value)
    {
        if (!Text(name, token: true) || !Text(value, token: true)) return;
        var attribute = model.FindAttribute(element, name, operation);
        if (!operation.IsComplete) return;
        if (attribute is null) { Error("DTD.UndeclaredAttribute", "An attribute has no declaration."); return; }
        if (document.ValidationStandalone == true && attribute.Declaration.IsExternal && attribute.Declaration.Type != DtdAttributeType.CData && NeedsNormalization(value))
            Error("DTD.Standalone", "Standalone input depends on external attribute normalization.", attribute.Declaration.Location);
        if (!attribute.IsValueValid(value, model, operation))
        { Error("DTD.AttributeType", "Attribute value does not satisfy its declared type."); return; }
        if (attribute.Declaration.DefaultKind == DtdDefaultKind.Fixed && !DtdLexical.Equal(value, attribute.NormalizedDefault, operation))
            Error("DTD.FixedAttribute", "Attribute value does not match its fixed default.");
        if (!operation.IsComplete) return;
        if (attribute.Declaration.Type == DtdAttributeType.Id)
        {
            if (!Work(value.Length + 1L) || !Retain()) return;
            ids ??= new(StringComparer.Ordinal);
            if (!ids.Add(value)) Error("DTD.DuplicateId", "An ID value is already used.");
        }
        else if (attribute.Declaration.Type is DtdAttributeType.IdRef or DtdAttributeType.IdRefs)
        {
            references ??= new(StringComparer.Ordinal);
            ReadOnlySpan<char> remaining = value;
            while (!remaining.IsEmpty)
            {
                int end = remaining.IndexOf(' '); if (end < 0) end = remaining.Length;
                var token = remaining[..end];
                if (!Work(token.Length + 1L)) return;
                if (!references.GetAlternateLookup<ReadOnlySpan<char>>().Contains(token))
                {
                    if (!Retain()) return;
                    references.Add(end == value.Length ? value : new string(token));
                }
                remaining = end == remaining.Length ? [] : remaining[(end + 1)..];
            }
        }
    }
    private bool Retain()
    {
        if (retained == 1_048_576) { operation.Refuse("VALIDATION.Limit", "ID bookkeeping capacity exceeded."); return false; }
        retained++; return true;
    }
    private bool NeedsNormalization(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty) return false;
        bool priorSpace = true;
        foreach (char c in value)
        {
            if (!Work()) return false;
            if (c is '\t' or '\r' or '\n' || c == ' ' && priorSpace) return true;
            priorSpace = c == ' ';
        }
        return priorSpace;
    }
    private bool WhiteText(string value)
    {
        if (!Text(value)) return false;
        foreach (char c in value)
        { if (!Work()) return false; if (c is not (' ' or '\t' or '\r' or '\n')) return false; }
        return true;
    }
    private bool Text(ReadOnlySpan<char> value, bool token = false)
    {
        int scalars = 0;
        while (!value.IsEmpty)
        {
            if (!Work()) return false;
            if (Rune.DecodeFromUtf16(value, out var rune, out int consumed) != OperationStatus.Done || !ValueSyntax.IsXmlScalar(rune.Value))
            { Error("DTD.InvalidCharacter", "Invalid XML character in the current tree."); return false; }
            if (characters == long.MaxValue || operation.Limits.DocumentCharacters is long cap && characters >= cap ||
                token && (scalars == int.MaxValue || operation.Limits.TokenCharacters is int tokenCap && scalars >= tokenCap))
            { operation.Refuse("VALIDATION.Limit", "Character/token limit exceeded."); return false; }
            characters++; scalars++; value = value[consumed..];
        }
        return true;
    }

    internal ref struct QNameBuffer(Span<char> initial, ValidationOperation operation)
    {
        private Span<char> buffer = initial;
        private char[]? rented;
        internal ReadOnlySpan<char> Make(string prefix, string local)
        {
            long length = prefix.Length == 0 ? local.Length : (long)prefix.Length + 1 + local.Length;
            // Check scalar token capacity before renting even for a giant existing name.
            int scalars = prefix.Length == 0 ? 0 : 1;
            for (int part = 0; part < 2; part++)
                foreach (var rune in (part == 0 ? prefix : local).EnumerateRunes())
                {
                    if (!operation.ChargeOperations()) return [];
                    if (scalars == int.MaxValue || operation.Limits.TokenCharacters is int cap && scalars >= cap)
                    { operation.Refuse("VALIDATION.Limit", "QName token limit exceeded."); return []; }
                    scalars++;
                }
            if (!operation.ChargeOperations(length + 1) || length > Array.MaxLength)
            { if (operation.IsComplete) operation.Refuse("VALIDATION.Limit", "QName capacity exceeded."); return []; }
            if (prefix.Length == 0) return local.AsSpan();
            if (length > buffer.Length)
            {
                var next = ArrayPool<char>.Shared.Rent((int)length);
                if (rented is not null) ArrayPool<char>.Shared.Return(rented);
                rented = next; buffer = next;
            }
            prefix.AsSpan().CopyTo(buffer); buffer[prefix.Length] = ':';
            local.AsSpan().CopyTo(buffer[(prefix.Length + 1)..]);
            return buffer[..(int)length];
        }
        internal void Dispose() { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }
}
