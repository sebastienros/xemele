using System.Text;
namespace Xemele.Validation;

// The evaluator owns no schema or run state. Prepared content programs are supplied
// by the composition owner; every cursor, effective value and hook session is local.
internal sealed class XmlSchemaValidator : IXsdEvaluator
{
    public ValidationResult Validate(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> contentPrograms, IXsdContentMatcher matcher,
        IXmlFacetEvaluator facets, IXsdIdentityFactory? identityFactory, XsdIdentityProgram? identityProgram)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(contentPrograms);
        ArgumentNullException.ThrowIfNull(matcher); ArgumentNullException.ThrowIfNull(facets);
        if ((identityFactory is null) != (identityProgram is null))
            throw new ArgumentException("Identity factory and prepared program must be supplied together.");
        var run = new XsdValidationRun(schema, document, options, contentPrograms, matcher, facets);
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
    private readonly Dictionary<SchemaName, XsdTypeDefinition> types = new();
    private IXsdIdentitySession? identity;
    private XmlLocation current;
    private long characters;
    private HashSet<string>? ids;
    private Dictionary<string, XmlLocation>? references;
    private sealed class Stopped : Exception { }
    private sealed class Frame(XmlNode node, XsdElementDeclaration? declaration, XsdTypeDefinition? type,
        SchemaNamespaceContext context, bool skip, bool nil, bool valid, IXsdContentCursor? cursor)
    {
        internal readonly XmlNode Node = node;
        internal readonly XsdElementDeclaration? Declaration = declaration;
        internal readonly XsdTypeDefinition? Type = type;
        internal readonly SchemaNamespaceContext Context = context;
        internal readonly bool Skip = skip, Nil = nil;
        internal bool Valid = valid, HasChild;
        internal readonly IXsdContentCursor? Cursor = cursor;
        internal XmlNode Next = node.FirstChild;
        internal string? Text;
        internal StringBuilder? Joined;
    }
    internal XsdValidationRun(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> programs, IXsdContentMatcher matcher, IXmlFacetEvaluator facets)
    {
        this.schema = schema; this.document = document; this.programs = programs; this.matcher = matcher; this.facets = facets;
        operation = new(options); revision = document.ContentRevision;
    }
    public XmlResourceLimits Limits => operation.Limits;
    public CancellationToken CancellationToken => operation.CancellationToken;
    public bool IsComplete => operation.IsComplete;
    public long? RemainingWork => operation.RemainingWork;
    private void Revision()
    { if (revision < 0 || document.ContentRevision != revision) throw new InvalidOperationException("Document changed during XSD validation."); }
    public bool Charge(long work, XmlLocation location)
    { Revision(); return operation.Charge(work, location == default ? current : location); }
    public bool ChargeComponents(int count, XmlLocation location)
    { Revision(); return operation.ChargeComponents(count, location == default ? current : location); }
    public bool AddDiagnostic(XmlDiagnostic diagnostic)
    { Revision(); return operation.AddDiagnostic(diagnostic); }
    public void Refuse(string code, string message, XmlLocation location)
    { Revision(); operation.Refuse(code, message, location == default ? current : location); }
    private void Work(long amount = 1) { if (!Charge(amount, current)) throw new Stopped(); }
    private void Retain(int count = 1) { if (!ChargeComponents(count, current)) throw new Stopped(); }
    private void Poll() => Work(0);
    private bool Error(string code, string message, XmlLocation? location = null)
    { AddDiagnostic(new(code, XmlDiagnosticSeverity.Error, message, location ?? current)); Poll(); return false; }
    private void Limit(string message) { Refuse("VALIDATION.Limit", message, current); throw new Stopped(); }
    internal ValidationResult Run(IXsdIdentityFactory? factory, XsdIdentityProgram? program)
    {
        try
        {
            Work();
            if (!document.SourceInputComplete || document.HasIncompleteAttributes || document.UnresolvedReferences.Count != 0 || document.Dtd?.UnreadParameterLocation is not null)
            { Refuse("XSD.IncompleteInput", "Instance input contains unresolved or incomplete source content.", default); return Finish(); }
            foreach (var type in schema.Types) { Work(); Retain(); types.Add(type.Name, type); }
            if (factory is not null) { identity = factory.Create(schema, program!, document); Poll(); }
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
            stack.Add(Enter(root, declaration, empty, false, 1));
            while (stack.Count != 0)
            {
                Work(); var frame = stack[^1]; current = document.OriginalLocation(frame.Node);
                if (frame.Next.IsDefault) { Leave(frame); stack.RemoveAt(stack.Count - 1); continue; }
                var child = frame.Next; frame.Next = child.NextSibling; current = document.OriginalLocation(child);
                if (child.Kind != XmlNodeKind.Element)
                {
                    Text(child.Value, false);
                    if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                    {
                        if (!frame.Skip && frame.Type is { } t && (frame.Nil && child.Value.Length != 0 ||
                            t.ContentKind == XsdContentKind.Empty && child.Value.Length != 0 || t.ContentKind == XsdContentKind.ElementOnly && !White(child.Value)))
                            frame.Valid = Error("XSD.Content", "Character content is not permitted here.");
                        if (frame.Type?.ContentKind is XsdContentKind.Simple or XsdContentKind.Mixed)
                        {
                            Work(child.Value.Length);
                            if (frame.Text is null) frame.Text = child.Value;
                            else { if (frame.Joined is null) { Retain(); frame.Joined = new(frame.Text); } frame.Joined.Append(child.Value); }
                        }
                    }
                    continue;
                }
                frame.HasChild = true; XsdElementDeclaration? matched = null; bool skip = frame.Skip;
                if (!skip)
                {
                    if (frame.Nil) frame.Valid = Error("XSD.Nil", "A nilled element cannot have element content.");
                    if (frame.Cursor is not null)
                    {
                        bool accepted = frame.Cursor.Accept(Name(child), this, out int? id, out var policy); Poll();
                        if (!accepted) frame.Valid = Error("XSD.Content", "Child does not match the prepared content model.");
                        if (id is int known) matched = schema.Elements[known];
                        else if (policy == XsdProcessContents.Skip) skip = true;
                        else if (schema.GlobalElements.TryGetValue(Name(child), out int global)) matched = schema.Elements[global];
                        else if (policy == XsdProcessContents.Strict) frame.Valid = Error("XSD.UndeclaredElement", "Strict wildcard requires an element declaration.");
                    }
                    else frame.Valid = Error("XSD.Content", "Element children are not permitted here.");
                }
                // Unknown lax and skip content remain untyped but still open every hook scope.
                stack.Add(Enter(child, matched, frame.Context, skip || matched is null, stack.Count + 1));
            }
            if (references is not null) foreach (var reference in references)
            { Work(reference.Key.Length + 1L); if (ids?.Contains(reference.Key) != true) Error("XSD.IdRef", "IDREF has no matching ID.", reference.Value); }
            identity?.Complete(this); Poll();
        }
        catch (Stopped) { }
        return Finish();
    }
    private ValidationResult Finish() { Revision(); return operation.Complete(); }
    private static SchemaName Name(XmlNode node) => new(node.NamespaceUri, node.LocalName);
    private static SchemaName Name(XmlAttribute attribute) => new(attribute.NamespaceUri, attribute.LocalName);
    private Frame Enter(XmlNode node, XsdElementDeclaration? declaration, SchemaNamespaceContext parent, bool skip, int depth)
    {
        current = document.OriginalLocation(node); Work();
        if (Limits.Depth is int cap && depth > cap) Limit("Element depth limit exceeded.");
        Text(node.LocalName, true); Text(node.NamespaceUri, true); Text(node.Prefix, true);
        var context = Context(node, parent); bool valid = true, nil = false;
        XsdTypeDefinition? type = null;
        if (!skip)
        {
            if (declaration is null) valid = Error("XSD.UndeclaredElement", "Element has no declaration.");
            else
            {
                if (declaration.Abstract) valid = Error("XSD.Abstract", "An abstract element cannot be instantiated.");
                if (!types.TryGetValue(declaration.Type, out type)) throw new ArgumentException("Declared type is missing from the sealed schema.");
                string? xsiType = node.GetAttribute("type", Xsi);
                if (xsiType is not null)
                {
                    Text(xsiType, true);
                    if (!QName(xsiType.AsSpan().Trim(), context, out var name) || !types.TryGetValue(name, out var replacement) || replacement.Anonymous)
                        valid = Error("XSD.Type", "xsi:type does not name a global type.");
                    else if (!Derived(replacement.Name, type.Name, declaration.Block, type.Block)) valid = Error("XSD.Type", "xsi:type derivation is prohibited.");
                    else type = replacement;
                }
                if (type.Abstract) valid = Error("XSD.Abstract", "An abstract type cannot be instantiated.");
                if (node.GetAttribute("nil", Xsi) is string lexical)
                {
                    Text(lexical, true); var collapsed = lexical.AsSpan().Trim();
                    if (collapsed.SequenceEqual("true") || collapsed.SequenceEqual("1")) nil = true;
                    else if (!collapsed.SequenceEqual("false") && !collapsed.SequenceEqual("0")) valid = Error("XSD.Nil", "xsi:nil must be an XML Schema boolean.");
                    if (nil && (!declaration.Nillable || declaration.FixedValue is not null)) valid = Error("XSD.Nil", "Element cannot be nilled with this declaration.");
                }
            }
        }
        IXsdContentCursor? cursor = null;
        if (type is not null)
        {
            if (!programs.TryGetValue(type.Name, out var prepared)) throw new ArgumentException("Prepared content program is missing.", "contentPrograms");
            Retain(); cursor = matcher.CreateCursor(prepared); Poll();
        }
        Retain(); var frame = new Frame(node, declaration, type, context, skip, nil, valid, cursor);
        identity?.EnterElement(node, declaration, this); Poll();
        Attributes(frame); return frame;
    }
    private SchemaNamespaceContext Context(XmlNode node, SchemaNamespaceContext parent)
    {
        Retain(2); var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in parent.Prefixes) { Work(); Retain(); prefixes.Add(entry.Key, entry.Value); }
        int count = node.Attributes.Count;
        if (Limits.AttributesPerElement is int cap && (long)count + node.NamespaceDeclarations.Count > cap) Limit("Attribute count limit exceeded.");
        foreach (var ns in node.NamespaceDeclarations) { Work(); Text(ns.Prefix, true); Text(ns.Value, true); Retain(); prefixes[ns.Prefix] = ns.Value; }
        Uri? baseUri = parent.BaseUri;
        if (node.GetAttribute("base", "http://www.w3.org/XML/1998/namespace") is string lexical)
        {
            Work(lexical.Length + 1L);
            if (Uri.TryCreate(lexical, UriKind.Absolute, out var absolute) || baseUri is not null && Uri.TryCreate(baseUri, lexical, out absolute)) baseUri = absolute;
        }
        return new(prefixes, prefixes.GetValueOrDefault("", ""), baseUri);
    }
    private void Leave(Frame frame)
    {
        current = document.OriginalLocation(frame.Node); Work();
        if (frame.Cursor is not null && !frame.Nil) { bool complete = frame.Cursor.Complete(this); Poll(); if (!complete) frame.Valid = Error("XSD.Content", "Element content is incomplete."); }
        var typeName = frame.Type?.Name ?? new SchemaName(Xs, "anyType"); XmlDatatypeValue? value = null;
        if (!frame.Skip && !frame.Nil && frame.Type is { } type)
        {
            string lexical = frame.Joined?.ToString() ?? frame.Text ?? "";
            SchemaName? simple = type.SimpleType;
            if (simple is not null)
            {
                if (frame.HasChild) frame.Valid = Error("XSD.Content", "Simple content cannot have element children.");
                var context = frame.Context;
                string? effective = frame.Declaration?.FixedValue ?? frame.Declaration?.DefaultValue;
                if (lexical.Length == 0 && !frame.HasChild && effective is not null) { lexical = effective; context = frame.Declaration!.Context; }
                if (!Value(simple.Value, lexical, context, out value)) frame.Valid = false;
                if (value is not null && frame.Declaration?.FixedValue is string fixedLexical && !Fixed(simple.Value, value, fixedLexical, frame.Declaration.Context)) frame.Valid = false;
            }
            else if (frame.Declaration?.FixedValue is string fixedText)
            {
                if (frame.HasChild || lexical != fixedText && lexical.Length != 0) frame.Valid = Error("XSD.Fixed", "Element content differs from its fixed value.");
            }
        }
        identity?.LeaveElement(frame.Node, new(frame.Valid, typeName, value), this); Poll();
    }
    private static bool White(ReadOnlySpan<char> text)
    { foreach (char c in text) if (c is not (' ' or '\t' or '\r' or '\n')) return false; return true; }
    private void Text(ReadOnlySpan<char> text, bool token)
    {
        int scalars = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            Work(); if (characters == long.MaxValue || Limits.DocumentCharacters is long cap && characters >= cap || token && Limits.TokenCharacters is int tokenCap && scalars >= tokenCap)
                Limit("Character/token limit exceeded.");
            characters++; scalars++;
            if (!ValueSyntax.IsXmlScalar(rune.Value)) Error("XSD.Character", "Invalid XML character in instance.");
        }
    }
}
