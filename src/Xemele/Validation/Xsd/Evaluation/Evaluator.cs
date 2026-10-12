using System.Text;
using Xemele.Parsing.Dtd;
namespace Xemele.Validation;

// The evaluator owns no schema or run state. Prepared content programs are supplied
// by the composition owner; every cursor, effective value and hook session is local.
internal sealed class XmlSchemaValidator : IXsdEvaluator
{
    public ValidationResult Validate(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> contentPrograms, IXsdContentMatcher matcher,
        IXmlFacetEvaluator facets, IXsdIdentityFactory? identityFactory, XsdIdentityProgram? identityProgram)
    {
        return Validate(schema, document, options, contentPrograms, matcher, facets, identityFactory, identityProgram, null);
    }
    internal ValidationResult Validate(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> contentPrograms, IXsdContentMatcher matcher,
        IXmlFacetEvaluator facets, IXsdIdentityFactory? identityFactory, XsdIdentityProgram? identityProgram, ComponentAvailability? availability)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(contentPrograms);
        ArgumentNullException.ThrowIfNull(matcher); ArgumentNullException.ThrowIfNull(facets);
        if ((identityFactory is null) != (identityProgram is null))
            throw new ArgumentException("Identity factory and prepared program must be supplied together.");
        var run = new XsdValidationRun(schema, document, options, contentPrograms, matcher, facets, availability);
        return run.Run(identityFactory, identityProgram);
    }
}

internal sealed partial class XsdValidationRun : ISchemaOperation
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private readonly XsdCompiledSchema schema;
    private readonly XmlDocument document;
    private readonly long revision;
    private readonly SchemaValidationOperation operation;
    private readonly IReadOnlyDictionary<SchemaName, XsdContentProgram> programs;
    private readonly IXsdContentMatcher matcher;
    private readonly IXmlFacetEvaluator facets;
    private readonly ComponentAvailability? availability;
    private readonly Dictionary<SchemaName, XsdTypeDefinition> types = new();
    private IXsdIdentitySession? identity;
    private XmlLocation current;
    private long characters;
    private HashSet<string>? ids;
    private Dictionary<string, XmlLocation>? references;
    private sealed class Frame(XmlNode node, XsdElementDeclaration? declaration, XsdTypeDefinition? type,
        SchemaNamespaceContext context, bool skip, bool nil, bool valid, IXsdContentCursor? cursor)
    {
        internal readonly XmlNode Node = node;
        internal readonly XsdElementDeclaration? Declaration = declaration;
        internal readonly XsdTypeDefinition? Type = type;
        internal readonly SchemaNamespaceContext Context = context;
        internal readonly bool Skip = skip, Nil = nil;
        internal bool Valid = valid, ContentFaulted, HasChild, HasCharacters;
        internal readonly IXsdContentCursor? Cursor = cursor;
        internal XmlNode Next = node.FirstChild;
        internal readonly XsdTextBuffer Text = new();
        internal long TextScalars;
    }
    internal XsdValidationRun(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> programs, IXsdContentMatcher matcher, IXmlFacetEvaluator facets)
        : this(schema, document, options, programs, matcher, facets, null) { }
    internal XsdValidationRun(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> programs, IXsdContentMatcher matcher, IXmlFacetEvaluator facets, ComponentAvailability? availability)
    {
        this.schema = schema; this.document = document; this.programs = programs; this.matcher = matcher; this.facets = facets; this.availability = availability;
        operation = new(options); revision = document.ContentRevision;
    }
    public XmlResourceLimits Limits => operation.Limits;
    public CancellationToken CancellationToken => operation.CancellationToken;
    public bool IsComplete => operation.IsComplete;
    public long? RemainingWork => operation.RemainingWork;
    private void Revision()
    { if (revision < 0 || document.ContentRevision != revision) throw new InvalidOperationException("Document changed during XSD validation."); }
    public bool Charge(long work, XmlLocation location)
    { Revision(); if (!operation.Charge(work, location == default ? current : location)) throw new XsdEvaluationStopped(); return true; }
    public bool ChargeComponents(int count, XmlLocation location)
    { Revision(); if (!operation.ChargeComponents(count, location == default ? current : location)) throw new XsdEvaluationStopped(); return true; }
    public bool AddDiagnostic(XmlDiagnostic diagnostic)
    { Revision(); if (!operation.AddDiagnostic(diagnostic)) throw new XsdEvaluationStopped(); return true; }
    public void Refuse(string code, string message, XmlLocation location)
    { Revision(); operation.Refuse(code, message, location == default ? current : location); throw new XsdEvaluationStopped(); }
    private void Work(long amount = 1) { if (!Charge(amount, current)) throw new XsdEvaluationStopped(); }
    private void Retain(int count = 1) { if (!ChargeComponents(count, current)) throw new XsdEvaluationStopped(); }
    private void Poll() => Work(0);
    private bool Error(string code, string message, XmlLocation? location = null)
    { AddDiagnostic(new(code, XmlDiagnosticSeverity.Error, message, location ?? current)); Poll(); return false; }
    private void Limit(string message) { Refuse("VALIDATION.Limit", message, current); throw new XsdEvaluationStopped(); }
    internal ValidationResult Run(IXsdIdentityFactory? factory, XsdIdentityProgram? program)
    {
        try
        {
            Work();
            if (!document.SourceInputComplete || document.HasIncompleteAttributes || document.UnresolvedReferences.Count != 0 || document.Dtd?.UnreadParameterLocation is not null)
            { Refuse("XSD.IncompleteInput", "Instance input contains unresolved or incomplete source content.", default); return Finish(); }
            foreach (var type in schema.Types) { Work(); Retain(); types.Add(type.Name, type); }
            if (availability is null) ComponentAvailability.RequireComplete(schema, this);
            if (factory is not null) { identity = factory.Create(schema, program!, document) ?? throw new InvalidOperationException("Identity factory returned no session."); Poll(); }
            var root = document.RootElement;
            if (root.IsDefault) { Error("XSD.Root", "A document element is required."); return Finish(); }
            // Also inspect top-level text and detect multiple elements in a mutable DOM.
            int roots = 0;
            foreach (var node in document.DocumentNode.Children)
            {
                Work(); current = document.OriginalLocation(node);
                if (node.Kind == XmlNodeKind.Element) roots++;
                else { Text(node.Value, false); if (node.Kind is XmlNodeKind.Text or XmlNodeKind.CData && !White(node.Value)) Error("XSD.Root", "Character content outside the document element."); }
            }
            if (roots != 1) Error("XSD.Root", "Exactly one document element is required.");
            schema.GlobalElements.TryGetValue(Name(root), out int rootId);
            XsdElementDeclaration? declaration = schema.GlobalElements.ContainsKey(Name(root)) ? schema.Elements[rootId] : null;
            var stack = new List<Frame>();
            var empty = new SchemaNamespaceContext(new Dictionary<string, string> { ["xml"] = "http://www.w3.org/XML/1998/namespace" }, "", document.OriginalLocation(root).SourceUri);
            stack.Add(Enter(root, declaration, empty, false, 1, true));
            while (stack.Count != 0)
            {
                Work(); var frame = stack[^1]; current = document.OriginalLocation(frame.Node);
                if (frame.Next.IsDefault) { Leave(frame); stack.RemoveAt(stack.Count - 1); continue; }
                var child = frame.Next; frame.Next = child.NextSibling; current = document.OriginalLocation(child);
                if (child.Kind != XmlNodeKind.Element)
                {
                    int scalars = Text(child.Value, false);
                    if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                    {
                        frame.HasCharacters |= child.Value.Length != 0;
                        if (!frame.Skip && frame.Type is { } t && (frame.Nil && child.Value.Length != 0 ||
                            t.ContentKind == XsdContentKind.Empty && child.Value.Length != 0 || t.ContentKind == XsdContentKind.ElementOnly && !White(child.Value)))
                            frame.Valid = Error("XSD.Content", "Character content is not permitted here.");
                        if (frame.Type?.ContentKind == XsdContentKind.Simple || frame.Type?.ContentKind == XsdContentKind.Mixed && (frame.Declaration?.FixedValue ?? frame.Declaration?.DefaultValue) is not null)
                        {
                            if (scalars > long.MaxValue - frame.TextScalars || Limits.TokenCharacters is int tokenCap && scalars > tokenCap - frame.TextScalars)
                                Limit("Accumulated simple content token limit exceeded.");
                            frame.TextScalars += scalars;
                            frame.Text.Append(child.Value, this, current);
                        }
                    }
                    continue;
                }
                frame.HasChild = true; XsdElementDeclaration? matched = null; bool skip = frame.Skip, requireAssessment = false;
                if (!skip)
                {
                    if (frame.Nil) frame.Valid = Error("XSD.Nil", "A nilled element cannot have element content.");
                    if (frame.ContentFaulted)
                    {
                        if (schema.GlobalElements.TryGetValue(Name(child), out int global)) matched = schema.Elements[global];
                    }
                    else if (frame.Cursor is not null)
                    {
                        bool accepted = frame.Cursor.Accept(Name(child), this, out int? id, out var policy); Poll();
                        if (!accepted)
                        {
                            // A rejected cursor has no particle attribution. Its failure
                            // outputs cannot establish a declaration or wildcard policy.
                            frame.ContentFaulted = true;
                            frame.Valid = Error("XSD.Content", "Child does not match the prepared content model.");
                            if (schema.GlobalElements.TryGetValue(Name(child), out int global)) matched = schema.Elements[global];
                        }
                        else if (id is int known) matched = schema.Elements[known];
                        else if (policy == XsdProcessContents.Skip) skip = true;
                        else if (schema.GlobalElements.TryGetValue(Name(child), out int global)) matched = schema.Elements[global];
                        else if (policy == XsdProcessContents.Strict) requireAssessment = true;
                    }
                    else frame.Valid = Error("XSD.Content", "Element children are not permitted here.");
                }
                // Lax undeclared nodes use open assessment; actual skip remains subtree skip.
                stack.Add(Enter(child, matched, frame.Context, skip, stack.Count + 1, requireAssessment));
            }
            if (references is not null) foreach (var reference in references)
            { Work(reference.Key.Length + 1L); if (ids?.Contains(reference.Key) != true) Error("XSD.IdRef", "IDREF has no matching ID.", reference.Value); }
            identity?.Complete(this); Poll();
        }
        catch (XsdEvaluationStopped) { }
        return Finish();
    }
    private ValidationResult Finish() { Revision(); return operation.Complete(); }
    private static SchemaName Name(XmlNode node) => new(node.NamespaceUri, node.LocalName);
    private static SchemaName Name(XmlAttribute attribute) => new(attribute.NamespaceUri, attribute.LocalName);
    private Frame Enter(XmlNode node, XsdElementDeclaration? declaration, SchemaNamespaceContext parent, bool skip, int depth, bool requireAssessment = false)
    {
        current = document.OriginalLocation(node); Work();
        if (Limits.Depth is int cap && depth > cap) Limit("Element depth limit exceeded.");
        Text(node.LocalName, true); Text(node.NamespaceUri, true); Text(node.Prefix, true);
        var context = Context(node, parent); bool valid = SchemaHints(node), nil = false, globalTypeResolved = false;
        XsdTypeDefinition? type = null;
        if (!skip)
        {
            if (declaration is null)
            {
                type = types[new(Xs, "anyType")];
            }
            else
            {
                Require(ComponentAvailability.Element(declaration));
                if (declaration.Abstract) valid = Error("XSD.Abstract", "An abstract element cannot be instantiated.");
                if (declaration.Type is not { } declaredType || !types.TryGetValue(declaredType, out type)) throw new ArgumentException("Declared type is missing from the sealed schema.");
            }
            string? xsiType = Attribute(node, "type", Xsi);
            if (xsiType is not null)
            {
                Text(xsiType, true);
                if (!QName(XsdEvaluationText.TrimXml(xsiType, this, current), context, out var name) || !types.TryGetValue(name, out var replacement) || replacement.Anonymous)
                    valid = Error("XSD.Type", "xsi:type does not name a global type.");
                else
                {
                    Require(ComponentAvailability.Type(replacement.Name));
                    if (!Derived(replacement.Name, type.Name, declaration?.Block ?? [], type.Block)) valid = Error("XSD.Type", "xsi:type derivation is prohibited.");
                    else { type = replacement; globalTypeResolved = true; }
                }
            }
            if (requireAssessment && declaration is null && !globalTypeResolved)
                valid = Error("XSD.UndeclaredElement", "Assessment requires an element declaration or globally resolved xsi:type.");
            Require(ComponentAvailability.Type(type.Name));
            if (type.Abstract) valid = Error("XSD.Abstract", "An abstract type cannot be instantiated.");
            if (Attribute(node, "nil", Xsi) is string lexical)
            {
                Text(lexical, true); var collapsed = XsdEvaluationText.TrimXml(lexical, this, current);
                if (declaration is { Nillable: false }) valid = Error("XSD.Nil", "xsi:nil is prohibited on a non-nillable element.");
                if (collapsed.SequenceEqual("true") || collapsed.SequenceEqual("1")) nil = declaration is { Nillable: true };
                else if (!collapsed.SequenceEqual("false") && !collapsed.SequenceEqual("0")) valid = Error("XSD.Nil", "xsi:nil must be an XML Schema boolean.");
                if (nil && declaration?.FixedValue is not null) valid = Error("XSD.Nil", "A fixed element cannot be nilled.");
            }
        }
        if (!skip && type is not null) RequireAttributeTypes(node, type);
        IXsdContentCursor? cursor = null;
        if (type is not null)
        {
            if (!programs.TryGetValue(type.Name, out var prepared)) throw new ArgumentException("Prepared content program is missing.", "contentPrograms");
            Retain(); cursor = matcher.CreateCursor(prepared) ?? throw new InvalidOperationException("Content matcher returned no cursor."); Poll();
        }
        Retain(); var frame = new Frame(node, declaration, type, context, skip, nil, valid, cursor);
        identity?.EnterElement(node, declaration, this); Poll();
        Attributes(frame); return frame;
    }
    private void Require(ComponentIdentity owner)
    {
        Poll();
        var missing = availability?.Missing(owner, DependencyUse.TypeAssessment, this, current);
        Poll();
        if (missing is not null)
            Refuse("XSD.MissingComponent", MissingMessage(missing), current);
    }
    private static string MissingMessage(AvailabilityEntry entry)
    {
        // Diagnostic ownership is the only formatting allocation on this path.
        // Bound retained explanatory names/URI independently of input lengths.
        static ReadOnlySpan<char> Bounded(string? text) => text.AsSpan(0, Math.Min(text?.Length ?? 0, 192));
        var at = entry.ReferenceLocation;
        return $"Required {entry.Kind} '{{{Bounded(entry.AuthoredName.NamespaceUri)}}}{Bounded(entry.AuthoredName.LocalName)}' is unavailable; schema source '{Bounded(at.SourceUri?.OriginalString)}', line {at.LineNumber}, column {at.LinePosition}, byte {at.ByteOffset}.";
    }
    private void RequireAttributeTypes(XmlNode node, XsdTypeDefinition type)
    {
        foreach (var binding in type.Attributes)
        {
            Work(); if (binding.Use == XsdAttributeUse.Prohibited) continue;
            var declaration = schema.Attributes[binding.DeclarationId]; bool supplied = false;
            foreach (var attribute in node.Attributes) { Work(); if (Name(attribute) == declaration.Name) { supplied = true; current = document.OriginalLocation(attribute); break; } }
            if (supplied || binding.Use != XsdAttributeUse.Required && (binding.FixedValue ?? binding.DefaultValue ?? declaration.FixedValue ?? declaration.DefaultValue) is not null)
                Require(ComponentAvailability.Simple(declaration.Type));
            current = document.OriginalLocation(node);
        }
        if (type.AnyAttribute is not { ProcessContents: not XsdProcessContents.Skip } wildcard) return;
        foreach (var attribute in node.Attributes)
        {
            Work(); var name = Name(attribute); if (name.NamespaceUri == Xsi || !Allows(wildcard, name.NamespaceUri)) continue;
            bool bound = false;
            foreach (var binding in type.Attributes)
            { Work(); if (binding.Use != XsdAttributeUse.Prohibited && schema.Attributes[binding.DeclarationId].Name == name) { bound = true; break; } }
            if (!bound && schema.GlobalAttributes.TryGetValue(name, out int id))
            { current = document.OriginalLocation(attribute); Require(ComponentAvailability.Simple(schema.Attributes[id].Type)); }
            current = document.OriginalLocation(node);
        }
    }
    private SchemaNamespaceContext Context(XmlNode node, SchemaNamespaceContext parent)
    {
        Retain(2); var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in parent.Prefixes) { Work(); Retain(); prefixes.Add(entry.Key, entry.Value); }
        int count = node.Attributes.Count;
        if (Limits.AttributesPerElement is int cap && (long)count + node.NamespaceDeclarations.Count > cap) Limit("Attribute count limit exceeded.");
        foreach (var ns in node.NamespaceDeclarations) { Work(); Text(ns.Prefix, true); Text(ns.Value, true); Retain(); prefixes[ns.Prefix] = ns.Value; }
        Uri? baseUri = parent.BaseUri;
        if (Attribute(node, "base", "http://www.w3.org/XML/1998/namespace") is string lexical)
        {
            Work(lexical.Length + 1L);
            if (Uri.TryCreate(lexical, UriKind.Absolute, out var absolute) || baseUri is not null && Uri.TryCreate(baseUri, lexical, out absolute)) baseUri = absolute;
        }
        return new(prefixes, prefixes.GetValueOrDefault("", ""), baseUri);
    }
    private void Leave(Frame frame)
    {
        current = document.OriginalLocation(frame.Node); Work();
        if (frame.Cursor is not null && !frame.Nil && !frame.ContentFaulted) { bool complete = frame.Cursor.Complete(this); Poll(); if (!complete) frame.Valid = Error("XSD.Content", "Element content is incomplete."); }
        var typeName = frame.Type?.Name ?? new SchemaName(Xs, "anyType"); XmlDatatypeValue? value = null;
        if (!frame.Skip && !frame.Nil && frame.Type is { } type)
        {
            string lexical = frame.Text.Materialize(this, current);
            SchemaName? simple = type.SimpleType;
            if (simple is not null)
            {
                if (frame.HasChild) frame.Valid = Error("XSD.Content", "Simple content cannot have element children.");
                var context = frame.Context;
                bool assessValue = true;
                string? effective = frame.Declaration?.FixedValue ?? frame.Declaration?.DefaultValue;
                if (!frame.HasCharacters && !frame.HasChild && effective is not null)
                {
                    var declaredSimple = types[frame.Declaration!.Type!.Value].SimpleType ?? new SchemaName(Xs, "anySimpleType");
                    if (Constraint(declaredSimple, effective, frame.Declaration.Context, out var prepared))
                    { lexical = prepared!.Lexical; context = prepared.Context; }
                    else { frame.Valid = false; assessValue = false; }
                }
                if (assessValue && !Value(simple.Value, lexical, context, out value)) frame.Valid = false;
                if (value is not null && frame.Declaration?.FixedValue is string fixedLexical && !Fixed(types[frame.Declaration.Type!.Value].SimpleType ?? new(Xs, "anySimpleType"), value, fixedLexical, frame.Declaration.Context)) frame.Valid = false;
            }
            else if ((frame.Declaration?.FixedValue ?? frame.Declaration?.DefaultValue) is string constraint)
            {
                bool constraintUsed = frame.Declaration!.FixedValue is not null || !frame.HasChild && !frame.HasCharacters;
                if (constraintUsed && type.ContentKind is XsdContentKind.Empty or XsdContentKind.ElementOnly) frame.Valid = Error("XSD.Default", "Selected type cannot accept a value constraint.");
                else if (frame.HasChild && frame.Declaration!.FixedValue is not null) frame.Valid = Error("XSD.Fixed", "A mixed-content fixed constraint cannot have element children.");
                else if (!frame.HasChild && type.ContentKind == XsdContentKind.Mixed)
                {
                    var stringType = new SchemaName(Xs, "anySimpleType");
                    if (!Value(stringType, lexical.Length == 0 ? constraint : lexical, frame.Declaration!.Context, out value)) frame.Valid = false;
                    if (value is not null && frame.Declaration!.FixedValue is string fixedLexical && !Fixed(stringType, value, fixedLexical, frame.Declaration.Context)) frame.Valid = false;
                }
            }
        }
        identity?.LeaveElement(frame.Node, new(frame.Valid, ActualType(value, typeName), value), this); Poll();
    }
    private string? Attribute(XmlNode node, string local, string uri)
    { foreach (var attribute in node.Attributes) { Work(); if (attribute.LocalName == local && attribute.NamespaceUri == uri) return attribute.Value; } return null; }
    private bool White(ReadOnlySpan<char> text)
    { foreach (char c in text) { Work(); if (c is not (' ' or '\t' or '\r' or '\n')) return false; } return true; }
    private int Text(ReadOnlySpan<char> text, bool token)
    {
        int scalars = 0;
        while (!text.IsEmpty)
        {
            Work(); if (characters == long.MaxValue || Limits.DocumentCharacters is long cap && characters >= cap || token && Limits.TokenCharacters is int tokenCap && scalars >= tokenCap)
                Limit("Character/token limit exceeded.");
            characters++; scalars++;
            var status = Rune.DecodeFromUtf16(text, out var rune, out int consumed);
            if (status != System.Buffers.OperationStatus.Done || !ValueSyntax.IsXmlScalar(rune.Value)) Error("XSD.Character", "Invalid XML character in instance.");
            text = text[Math.Max(1, consumed)..];
        }
        return scalars;
    }
}
