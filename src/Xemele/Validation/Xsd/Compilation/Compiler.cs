using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Xemele.Validation;

// An invocation owns every mutable builder and borrowed source handle. Only the
// sealed definition graph crosses the publication boundary; no executable
// datatype library or public validation facade is installed here.
internal sealed class XmlSchemaCompiler : IXsdCompiler
{
    public XsdCompiledSchema Compile(XmlDocument schema, SchemaCompileOptions options, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(operation);
        return new XsdCompilation(SchemaSnapshots.Compile(options), operation).Compile(schema);
    }
}

internal sealed class XsdCompilation
{
    internal const string Namespace = "http://www.w3.org/2001/XMLSchema";
    private static readonly SchemaName AnyType = new(Namespace, "anyType"), AnySimple = new(Namespace, "anySimpleType");
    private readonly SchemaCompileOptions options;
    private readonly ISchemaOperation op;
    private readonly SchemaResourceLoader loader;
    private readonly Dictionary<(Uri, string), Source> sources = new();
    private readonly List<Source> allSources = new();
    private readonly Dictionary<(string, SchemaName), Declaration> declarations = new();
    private readonly Dictionary<XmlNode, SchemaName> anonymous = new();
    private readonly Dictionary<XmlNode, int> localElements = new(), localAttributes = new();
    private readonly Dictionary<SchemaName, XsdTypeDefinition> types = new();
    private readonly Dictionary<SchemaName, XmlDatatypeDefinition> simple = new();
    private readonly Dictionary<SchemaName, int> globalElements = new(), globalAttributes = new();
    private readonly List<XsdElementDeclaration> elements = new();
    private readonly List<XsdAttributeDeclaration> attributes = new();
    private readonly List<XsdIdentityConstraint> identities = new();
    private readonly HashSet<int> inferElementType = new();
    private readonly Dictionary<SchemaName, XsdNotationDeclaration> notations = new();
    private readonly HashSet<(string, SchemaName)> expanding = new();
    private int anonymousId, redefineId, depth;

    private sealed class Source(SchemaDocumentScope scope, string target, bool chameleon)
    {
        internal readonly SchemaDocumentScope Scope = scope;
        internal readonly string Target = target;
        internal readonly bool Chameleon = chameleon;
        internal readonly HashSet<string> Imports = new(StringComparer.Ordinal);
        internal readonly List<Source> Includes = new();
        internal readonly Dictionary<XmlNode, SchemaNamespaceContext> Contexts = new();
        internal bool Active = true;
        internal string ElementForm = "unqualified", AttributeForm = "unqualified", Block = "", Final = "";
    }
    private sealed record Declaration(Source Source, XmlNode Node, SchemaName Name, SchemaName? Previous = null, SchemaName? Original = null);
    internal XsdCompilation(SchemaCompileOptions options, ISchemaOperation operation)
    { this.options = options; op = operation; loader = new(options, operation); }

    internal XsdCompiledSchema Compile(XmlDocument document)
    {
        op.CancellationToken.ThrowIfCancellationRequested();
        var source = new SchemaDocumentScope(document, op, loader.SourceUri(document));
        Load(source, null, null);
        Builtins();
        foreach (var declaration in declarations.Values)
            if (declaration.Node.LocalName is "simpleType" or "complexType" && types.ContainsKey(declaration.Name)) Error("Built-in type cannot be redeclared.", At(declaration.Source, declaration.Node));
        // The enumeration snapshot is an owned builder boundary, not a token loop.
        foreach (var declaration in new List<Declaration>(declarations.Values))
        {
            if (declaration.Node.LocalName is "simpleType" or "complexType") Type(declaration.Name, declaration);
            else if (declaration.Node.LocalName == "element") Element(declaration, true);
            else if (declaration.Node.LocalName == "attribute") Attribute(declaration, true);
            else if (declaration.Node.LocalName == "notation") Notation(declaration);
            else if (declaration.Node.LocalName == "group") Group(declaration, declaration.Node);
            else if (declaration.Node.LocalName == "attributeGroup") AttributeGroup(declaration, new(), out _);
        }
        CheckGraphs();
        foreach (var s in allSources) s.Scope.CheckUnchanged();
        Tick(default, 0);
        var definitions = new ReadOnlyCollection<XmlDatatypeDefinition>(new List<XmlDatatypeDefinition>(simple.Values));
        return new(elements, attributes, new List<XsdTypeDefinition>(types.Values), definitions, identities, notations,
            globalElements, globalAttributes, new XsdDefinitionRegistry(definitions));
    }

    private Source Load(SchemaDocumentScope scope, string? effective, SchemaResourcePurpose? purpose)
    {
        var root = scope.Document.RootElement;
        if (root.IsDefault || root.NamespaceUri != Namespace || root.LocalName != "schema") Error("Expected an XSD schema document.", default);
        string declared = Collapse(root.GetAttribute("targetNamespace") ?? "");
        if (root.GetAttribute("targetNamespace") is not null && declared.Length == 0) Error("Empty targetNamespace is not allowed.", default);
        if (purpose is SchemaResourcePurpose.XsdInclude or SchemaResourcePurpose.XsdRedefine && declared.Length != 0 && declared != effective)
            Error("Included schema has a different target namespace.", scope.Location(root));
        if (purpose == SchemaResourcePurpose.XsdImport && declared != effective) Error("Imported schema target namespace does not match import namespace.", scope.Location(root));
        string target = effective ?? declared;
        if (scope.SourceUri is { } uri && sources.TryGetValue((uri, target), out var existing))
        {
            if (existing.Active && purpose != SchemaResourcePurpose.XsdImport)
                Fail("SCHEMA.ResourceCycle", "Cyclic include/redefine is not legal.", new(uri, 0, 0, null));
            return existing;
        }
        Components(default);
        var source = new Source(scope, target, declared.Length == 0 && target.Length != 0);
        allSources.Add(source);
        if (scope.SourceUri is { } identity) sources.Add((identity, target), source);
        Syntax(source, root);
        source.ElementForm = Form(root.GetAttribute("elementFormDefault"), root, source);
        source.AttributeForm = Form(root.GetAttribute("attributeFormDefault"), root, source);
        source.Block = root.GetAttribute("blockDefault") ?? ""; source.Final = root.GetAttribute("finalDefault") ?? "";
        Tokens(source.Block, "extension restriction substitution", source, root);
        Tokens(source.Final, "extension restriction list union", source, root);
        bool declarationsStarted = false;
        // Register this document before following imports, permitting legal mutual
        // imports to bind against the finite in-progress component table.
        foreach (var node in Children(source, root))
        {
            if (node.LocalName == "annotation") continue;
            if (node.LocalName is "include" or "import" or "redefine")
            { if (declarationsStarted) Error("Composition directives precede declarations.", At(source, node)); continue; }
            declarationsStarted = true;
            if (node.LocalName is not ("element" or "attribute" or "simpleType" or "complexType" or "group" or "attributeGroup" or "notation")) Error("Invalid global schema component.", At(source, node));
            var name = Named(source, node);
            if (node.GetAttribute("ref") is not null) Error("Global components cannot use ref.", At(source, node));
            Add(new(source, node, name), Space(node.LocalName));
        }
        foreach (var node in Children(source, root))
        {
            if (node.LocalName is not ("include" or "import" or "redefine")) continue;
            var kind = node.LocalName == "import" ? SchemaResourcePurpose.XsdImport : node.LocalName == "redefine" ? SchemaResourcePurpose.XsdRedefine : SchemaResourcePurpose.XsdInclude;
            string ns = kind == SchemaResourcePurpose.XsdImport ? Collapse(node.GetAttribute("namespace") ?? "") : target;
            if (kind == SchemaResourcePurpose.XsdImport)
            {
                if (node.GetAttribute("namespace") == "" || ns == target) Error("Import must name a different namespace (or omit it for no namespace).", At(source, node));
                source.Imports.Add(ns);
            }
            string? href = node.GetAttribute("schemaLocation");
            if (href is null && kind == SchemaResourcePurpose.XsdImport) continue; // The namespace may be supplied by another import.
            if (href is null) Error("schemaLocation is required.", At(source, node));
            var context = Context(source, node);
            Uri? requested = Resolve(href!, context.BaseUri);
            Source imported;
            if (requested is not null && sources.TryGetValue((requested, ns), out var cached))
            {
                if (cached.Active && kind != SchemaResourcePurpose.XsdImport) Fail("SCHEMA.ResourceCycle", "Cyclic include/redefine is not legal.", At(source, node));
                imported = cached;
            }
            else
            {
                var loaded = loader.Load(new(href!, source.Scope.SourceUri, kind, At(source, node)), context.BaseUri, op);
                using var traversal = loader.EnterComposition(At(source, node));
                imported = Load(new(loaded, op, loader.SourceUri(loaded)), ns, kind);
            }
            if (kind != SchemaResourcePurpose.XsdImport) source.Includes.Add(imported);
            if (kind == SchemaResourcePurpose.XsdRedefine) Redefine(source, node, imported);
        }
        source.Active = false; scope.CheckUnchanged(); return source;
    }

    private void Redefine(Source source, XmlNode node, Source imported)
    {
        foreach (var child in Children(source, node))
        {
            if (child.LocalName == "annotation") continue;
            if (child.LocalName is not ("simpleType" or "complexType" or "group" or "attributeGroup")) Error("Invalid redefinition component.", At(source, child));
            var name = Named(source, child); string space = Space(child.LocalName);
            if (!declarations.TryGetValue((space, name), out var old) || !Contains(imported, old.Source, new())) Error("Redefinition has no original component.", At(source, child));
            if (old.Node.LocalName != child.LocalName) Error("Redefinition changes component kind.", At(source, child));
            var previous = new SchemaName(source.Target, "#redefined" + (++redefineId).ToString(CultureInfo.InvariantCulture));
            declarations.Remove((space, name));
            Add(old with { Name = previous, Original = name }, space);
            Add(new(source, child, name, previous), space);
        }
    }
    private bool Contains(Source outer, Source wanted, HashSet<Source> visited)
    {
        Tick(default); if (ReferenceEquals(outer, wanted)) return true; if (!visited.Add(outer)) return false;
        foreach (var include in outer.Includes) if (Contains(include, wanted, visited)) return true;
        return false;
    }
    private static Uri? Resolve(string href, Uri? basis) => Uri.TryCreate(href, UriKind.Absolute, out var uri) ? uri : basis is not null && Uri.TryCreate(basis, href, out uri) ? uri : null;
    private static string Space(string kind) => kind is "simpleType" or "complexType" ? "type" : kind;
    private void Add(Declaration declaration, string space)
    {
        Components(At(declaration.Source, declaration.Node));
        if (!declarations.TryAdd((space, declaration.Name), declaration)) Error("Duplicate component in one symbol space.", At(declaration.Source, declaration.Node));
    }
    private SchemaNamespaceContext Context(Source source, XmlNode node)
    {
        source.Scope.CheckUnchanged();
        if (!source.Contexts.TryGetValue(node, out var context))
        { Components(At(source, node)); context = source.Scope.Context(node); source.Contexts.Add(node, context); }
        return context;
    }
    private static XmlLocation At(Source source, XmlNode node) => source.Scope.Location(node);
    private static XmlLocation AttributeAt(Source source, XmlNode node, string name)
    {
        foreach (var attribute in node.Attributes)
            if (attribute.NamespaceUri.Length == 0 && attribute.LocalName == name) return source.Scope.Location(attribute);
        return At(source,node);
    }
    private static XmlLocation ConstraintAt(Source source, XmlNode node) =>
        node.GetAttribute("default") is not null ? AttributeAt(source,node,"default") :
        node.GetAttribute("fixed") is not null ? AttributeAt(source,node,"fixed") : At(source,node);
    private void Tick(XmlLocation at, long amount = 1) { if (!op.Charge(amount, at)) throw SchemaSnapshots.Failure(op); }
    private void Components(XmlLocation at) { if (!op.ChargeComponents(1, at)) throw SchemaSnapshots.Failure(op); }
    [DoesNotReturn] private void Error(string message, XmlLocation at)
    {
        foreach (var source in allSources) source.Scope.CheckUnchanged();
        var diagnostic = new XmlDiagnostic("XSD.SchemaRestriction", XmlDiagnosticSeverity.Error, message, at);
        op.AddDiagnostic(diagnostic);
        if (!op.IsComplete) throw SchemaSnapshots.Failure(op);
        if (op is SchemaCompileOperation compile) throw compile.Exception();
        throw new SchemaCompileException([diagnostic]);
    }
    [DoesNotReturn] private void Fail(string code, string message, XmlLocation at)
    { op.Refuse(code, message, at); throw SchemaSnapshots.Failure(op); }
    private IDisposable Descend(Source source, XmlNode node)
    {
        Tick(At(source, node));
        if (depth >= (op.Limits.Depth ?? int.MaxValue)) Fail("SCHEMA.Limit", "XSD expansion depth exceeded.", At(source, node));
        depth++; return new DepthScope(this);
    }
    private sealed class DepthScope(XsdCompilation compiler) : IDisposable { public void Dispose() => compiler.depth--; }

    private List<XmlNode> Children(Source source, XmlNode node)
    {
        source.Scope.CheckUnchanged(); var result = new List<XmlNode>();
        foreach (var child in node.Children)
        {
            Tick(At(source, child));
            if (child.Kind == XmlNodeKind.Element)
            { if (child.NamespaceUri != Namespace) Error("Foreign element outside annotation.", At(source, child)); result.Add(child); }
            else if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData && !Whitespace(child.Value.AsSpan())) Error("Schema content must not contain character data.", At(source, child));
        }
        return result;
    }
    private static bool Whitespace(ReadOnlySpan<char> text) { foreach (char c in text) if (c is not (' ' or '\t' or '\r' or '\n')) return false; return true; }
    private static string Collapse(string text)
    {
        // Persist normalized lexical values once; this is an owned graph boundary.
        bool canonical = text.Length == 0 || text[0] is not (' ' or '\t' or '\r' or '\n') && text[^1] is not (' ' or '\t' or '\r' or '\n');
        for (int i = 0; canonical && i < text.Length; i++)
            if (text[i] is '\t' or '\r' or '\n' || text[i] == ' ' && i > 0 && text[i-1] == ' ') canonical = false;
        if (canonical) return text;
        var builder = new StringBuilder(); bool pending = false;
        foreach (char c in text)
            if (c is ' ' or '\t' or '\r' or '\n') pending = builder.Length != 0;
            else { if (pending) builder.Append(' '); builder.Append(c); pending = false; }
        return builder.ToString();
    }
    private string Required(Source source, XmlNode node, string attribute)
    { var value = node.GetAttribute(attribute); if (value is null) Error("Required schema attribute is missing.", At(source, node)); return value!; }
    private string NCName(string text, Source source, XmlNode node)
    {
        text = Collapse(text); Tick(At(source, node), text.Length);
        try { TreeValidation.Name(text); } catch (ArgumentException) { Error("Invalid NCName.", At(source, node)); }
        return text;
    }
    private SchemaName Named(Source source, XmlNode node) => new(source.Target, NCName(Required(source, node, "name"), source, node));
    private SchemaName QName(Declaration owner, XmlNode node, string lexical, string? previousSpace = null)
    {
        var source = owner.Source; lexical = Collapse(lexical); Tick(At(source, node), lexical.Length);
        int colon = lexical.IndexOf(':'); string prefix = colon < 0 ? "" : lexical[..colon]; string local = colon < 0 ? lexical : lexical[(colon + 1)..];
        NCName(local, source, node); if (colon >= 0) NCName(prefix, source, node);
        var context = Context(source, node);
        string ns = prefix.Length == 0 ? context.DefaultNamespace : context.Prefixes.TryGetValue(prefix, out var uri) ? uri : "#unbound";
        if (ns == "#unbound") Error("Unbound QName prefix.", At(source, node));
        if (ns.Length == 0 && source.Chameleon) ns = source.Target;
        if (ns != source.Target && ns != Namespace && !Imported(source, ns, new())) Error("QName references a namespace without an import.", At(source, node));
        var result = new SchemaName(ns, local);
        // Name is the internal version identity; Original preserves the authored
        // name after renaming. Only the designated redefine self-reference in
        // that component's own symbol space binds to the preceding version.
        if (previousSpace == Space(owner.Node.LocalName) && owner.Previous is { } old && result == (owner.Original ?? owner.Name)) return old;
        return result;
    }
    private bool Imported(Source source, string ns, HashSet<Source> visited)
    {
        Tick(default); if (!visited.Add(source)) return false;
        if (source.Imports.Contains(ns)) return true;
        foreach (var include in source.Includes) if (Imported(include, ns, visited)) return true;
        return false;
    }
    private SchemaName ResolveType(Declaration owner, XmlNode node, string value, bool previous = false)
    {
        var name = QName(owner, node, value, previous ? "type" : null);
        if (!types.ContainsKey(name) && !simple.ContainsKey(name) && !declarations.ContainsKey(("type", name))) Error("Unknown type definition.", At(owner.Source, node));
        return name;
    }
    private SchemaName Anonymous(Declaration owner, XmlNode node)
    {
        if (anonymous.TryGetValue(node, out var name)) return name;
        Components(At(owner.Source, node));
        name = new(owner.Source.Target, "#anonymous" + (++anonymousId).ToString(CultureInfo.InvariantCulture)); anonymous.Add(node, name);
        var declaration = new Declaration(owner.Source, node, name); Add(declaration, "type"); Type(name, declaration); return name;
    }
    private static string Form(string? value, XmlNode node, Source source) => value is null ? "unqualified" : Collapse(value);
    private bool Boolean(Source source, XmlNode node, string name)
    {
        string? value = node.GetAttribute(name); if (value is null) return false;
        value = Collapse(value); if (value is "true" or "1") return true; if (value is "false" or "0") return false;
        Error("Invalid boolean schema attribute.", At(source, node)); return false;
    }
    private List<string> Tokens(string lexical, string allowed, Source source, XmlNode node)
    {
        lexical = Collapse(lexical); var result = new List<string>();
        if (lexical == "#all") { foreach (var token in allowed.AsSpan().Split(' ')) result.Add(allowed.AsSpan()[token].ToString()); return result; }
        foreach (var range in lexical.AsSpan().Split(' '))
        {
            var token = lexical.AsSpan()[range]; if (token.IsEmpty) continue;
            Tick(At(source, node), token.Length);
            bool found = false; foreach (var a in allowed.AsSpan().Split(' ')) if (token.SequenceEqual(allowed.AsSpan()[a])) { found = true; break; }
            if (!found) Error("Invalid block/final token.", At(source, node));
            var owned = token.ToString(); if (!result.Contains(owned)) result.Add(owned);
        }
        return result;
    }
    private List<XsdDerivation> Final(Source source, XmlNode node, string allowed)
    {
        var result = new List<XsdDerivation>();
        foreach (string token in Tokens(node.GetAttribute("final") ?? source.Final, node.GetAttribute("final") is null ? "restriction extension list union" : allowed, source, node))
        {
            if (!allowed.AsSpan().Contains(token.AsSpan(), StringComparison.Ordinal)) continue;
            result.Add(token switch { "restriction" => XsdDerivation.Restriction, "extension" => XsdDerivation.Extension, "list" => XsdDerivation.List, _ => XsdDerivation.Union });
        }
        return result;
    }
    private List<XsdElementBlock> ElementBlock(Source source, XmlNode node)
    {
        var result = new List<XsdElementBlock>();
        foreach (string token in Tokens(node.GetAttribute("block") ?? source.Block, "restriction extension substitution", source, node))
            result.Add(token switch { "restriction" => XsdElementBlock.Restriction, "extension" => XsdElementBlock.Extension, _ => XsdElementBlock.Substitution });
        return result;
    }

    private void Builtins()
    {
        Components(default);
        var open = new XsdWildcard(true, Array.Empty<string>(), XsdProcessContents.Lax);
        Components(default); Components(default); Components(default);
        types.Add(AnyType, new(AnyType, null, XsdDerivation.None, false, false, [], [], XsdContentKind.Mixed, null,
            new(new XsdWildcardTerm(open), BigInteger.Zero, null, default), [], open, default));
        Builtin("anySimpleType", null);
        foreach (var name in new[] { "string", "boolean", "decimal", "float", "double", "duration", "dateTime", "time", "date", "gYearMonth", "gYear", "gMonthDay", "gDay", "gMonth", "hexBinary", "base64Binary", "anyURI", "QName", "NOTATION" }) Builtin(name, "anySimpleType");
        foreach (var pair in new (string, string)[] { ("normalizedString","string"),("token","normalizedString"),("language","token"),("Name","token"),("NCName","Name"),("ID","NCName"),("IDREF","NCName"),("ENTITY","NCName"),("NMTOKEN","token"),("integer","decimal"),("nonPositiveInteger","integer"),("negativeInteger","nonPositiveInteger"),("long","integer"),("int","long"),("short","int"),("byte","short"),("nonNegativeInteger","integer"),("unsignedLong","nonNegativeInteger"),("unsignedInt","unsignedLong"),("unsignedShort","unsignedInt"),("unsignedByte","unsignedShort"),("positiveInteger","nonNegativeInteger") }) Builtin(pair.Item1, pair.Item2);
        foreach (var pair in new (string, string)[] { ("NMTOKENS","NMTOKEN"),("IDREFS","IDREF"),("ENTITIES","ENTITY") })
        {
            var name = new SchemaName(Namespace, pair.Item1); Components(default);
            simple.Add(name, new(name, XmlDatatypeVariety.List, AnySimple, new(Namespace, pair.Item2), [], [], [], false, default));
            SimpleWrapper(simple[name], XsdDerivation.List);
        }
    }
    private void Builtin(string local, string? baseLocal)
    {
        var name = new SchemaName(Namespace, local); Components(default);
        var definition = new XmlDatatypeDefinition(name, XmlDatatypeVariety.Atomic, baseLocal is null ? null : new(Namespace, baseLocal), null, [], [], [], false, default);
        simple.Add(name, definition); SimpleWrapper(definition, baseLocal is null ? XsdDerivation.None : XsdDerivation.Restriction);
    }
    private void SimpleWrapper(XmlDatatypeDefinition definition, XsdDerivation derivation)
    {
        Components(definition.Location);
        var final = new List<XsdDerivation>(); foreach (var item in definition.Final) final.Add(item switch { XmlDatatypeDerivation.Restriction => XsdDerivation.Restriction, XmlDatatypeDerivation.List => XsdDerivation.List, _ => XsdDerivation.Union });
        types.Add(definition.Name, new(definition.Name, definition.BaseType ?? AnyType, derivation, false, definition.Anonymous, final, [], XsdContentKind.Simple, definition.Name, null, [], null, definition.Location));
    }
    private XsdTypeDefinition Type(SchemaName name, Declaration? declaration = null)
    {
        Tick(default);
        if (types.TryGetValue(name, out var value)) return value;
        declaration ??= Find("type", name, null);
        var key = ("type", name);
        if (!expanding.Add(key)) Error("Circular type derivation.", At(declaration.Source, declaration.Node));
        using var scope = Descend(declaration.Source, declaration.Node);
        try
        {
            if (declaration.Node.LocalName == "simpleType") Simple(declaration);
            else Complex(declaration);
            return types[name];
        }
        finally { expanding.Remove(key); }
    }
    private Declaration Find(string space, SchemaName name, Declaration? owner)
    {
        Tick(owner is null ? default : At(owner.Source, owner.Node));
        if (!declarations.TryGetValue((space, name), out var declaration)) Error("Undefined schema component reference.", owner is null ? default : At(owner.Source, owner.Node));
        return declaration!;
    }
    private XmlNode SingleBody(Declaration owner)
    {
        XmlNode body = default;
        foreach (var child in Children(owner.Source, owner.Node))
        {
            if (child.LocalName == "annotation") continue;
            if (!body.IsDefault) Error("Expected one component body.", At(owner.Source, child)); body = child;
        }
        if (body.IsDefault) Error("Missing component body.", At(owner.Source, owner.Node)); return body;
    }
    private void Simple(Declaration owner)
    {
        var s = owner.Source; var node = owner.Node; var body = SingleBody(owner);
        if (body.LocalName is not ("restriction" or "list" or "union")) Error("Invalid simple type derivation.", At(s, body));
        SchemaName? basis = null, item = null; var members = new List<SchemaName>(); var facets = new List<XmlFacet>();
        var children = Children(s, body);
        int inline = 0;
        if (body.LocalName == "restriction")
        {
            if (body.GetAttribute("base") is { } baseValue) basis = ResolveType(owner, body, baseValue, true);
            foreach (var child in children)
            {
                if (child.LocalName == "annotation") continue;
                if (child.LocalName == "simpleType")
                { if (basis is not null || inline++ != 0 || facets.Count != 0) Error("Restriction has incompatible base declarations.", At(s, child)); basis = Anonymous(owner, child); }
                else facets.Add(Facet(owner, child));
            }
            if (basis is null) Error("Simple restriction requires a base.", At(s, body));
            var baseType = Type(basis!.Value);
            if (!simple.ContainsKey(basis!.Value)) Error("Simple restriction base must be a simple type definition.", At(s, body));
            if (baseType.Final.Contains(XsdDerivation.Restriction)) Error("Base type forbids restriction.", At(s, body));
            if (owner.Previous is { } old && basis != old) Error("Redefined simple type must restrict its original.", At(s, body));
            var inherited = simple[basis.Value];
            item = inherited.ItemType; members.AddRange(inherited.MemberTypes);
            AddSimple(new(owner.Name, inherited.Variety, basis, item, members, facets, DatatypeFinal(s, node), IsAnonymous(owner.Name), At(s, node)), XsdDerivation.Restriction);
            return;
        }
        if (owner.Previous is not null) Error("A simple type can only be redefined by restriction.", At(s, body));
        if (body.LocalName == "list")
        {
            basis = AnySimple;
            if (body.GetAttribute("itemType") is { } lexical) item = ResolveType(owner, body, lexical);
            foreach (var child in children)
            {
                if (child.LocalName == "annotation") continue;
                if (child.LocalName != "simpleType" || item is not null) Error("List requires exactly one item type.", At(s, child)); item = Anonymous(owner, child);
            }
            if (item is null) Error("List item type missing.", At(s, body));
            var type = Type(item!.Value);
            if (!simple.ContainsKey(item.Value) || simple[item.Value].Variety == XmlDatatypeVariety.List || HasListMember(item.Value, new())) Error("List item type cannot contain a list.", At(s, body));
            if (type.Final.Contains(XsdDerivation.List)) Error("Item type forbids list derivation.", At(s, body));
        }
        else
        {
            basis = AnySimple;
            if (body.GetAttribute("memberTypes") is { } lexical)
            {
                string normalized = Collapse(lexical);
                foreach (var range in normalized.AsSpan().Split(' '))
                { var token = normalized.AsSpan()[range]; if (!token.IsEmpty) members.Add(ResolveType(owner, body, token.ToString())); }
            }
            foreach (var child in children)
            {
                if (child.LocalName == "annotation") continue;
                if (child.LocalName != "simpleType") Error("Union allows only simple type members.", At(s, child)); members.Add(Anonymous(owner, child));
            }
            if (members.Count == 0) Error("Union requires a member type.", At(s, body));
            foreach (var member in members)
            { var type = Type(member); if (!simple.ContainsKey(member) || type.Final.Contains(XsdDerivation.Union)) Error("Invalid or final union member type.", At(s, body)); }
        }
        AddSimple(new(owner.Name, body.LocalName == "list" ? XmlDatatypeVariety.List : XmlDatatypeVariety.Union, basis, item, members, facets,
            DatatypeFinal(s, node), IsAnonymous(owner.Name), At(s, node)), body.LocalName == "list" ? XsdDerivation.List : XsdDerivation.Union);
    }
    private bool HasListMember(SchemaName name, HashSet<SchemaName> seen)
    {
        Tick(default); if (!seen.Add(name)) return false;
        var definition = simple[name]; if (definition.Variety == XmlDatatypeVariety.List) return true;
        foreach (var member in definition.MemberTypes) if (HasListMember(member, seen)) return true;
        return false;
    }
    private List<XmlDatatypeDerivation> DatatypeFinal(Source source, XmlNode node)
    {
        var result = new List<XmlDatatypeDerivation>();
        foreach (var value in Final(source, node, "restriction list union")) result.Add(value switch { XsdDerivation.Restriction => XmlDatatypeDerivation.Restriction, XsdDerivation.List => XmlDatatypeDerivation.List, _ => XmlDatatypeDerivation.Union });
        return result;
    }
    private static bool IsAnonymous(SchemaName name) => name.LocalName.StartsWith("#", StringComparison.Ordinal);
    private void AddSimple(XmlDatatypeDefinition definition, XsdDerivation derivation)
    { Components(definition.Location); simple.Add(definition.Name, definition); SimpleWrapper(definition, derivation); }
    private XmlFacet Facet(Declaration owner, XmlNode node)
    {
        var kind = node.LocalName switch
        {
            "length" => XmlFacetKind.Length, "minLength" => XmlFacetKind.MinLength, "maxLength" => XmlFacetKind.MaxLength,
            "pattern" => XmlFacetKind.Pattern, "enumeration" => XmlFacetKind.Enumeration, "whiteSpace" => XmlFacetKind.WhiteSpace,
            "maxInclusive" => XmlFacetKind.MaxInclusive, "maxExclusive" => XmlFacetKind.MaxExclusive,
            "minInclusive" => XmlFacetKind.MinInclusive, "minExclusive" => XmlFacetKind.MinExclusive,
            "totalDigits" => XmlFacetKind.TotalDigits, "fractionDigits" => XmlFacetKind.FractionDigits, _ => (XmlFacetKind)(-1)
        };
        if ((int)kind < 0) Error("Unknown facet.", At(owner.Source, node));
        Components(At(owner.Source, node));
        return new(kind, Required(owner.Source, node, "value"), Boolean(owner.Source, node, "fixed"), Context(owner.Source, node), AttributeAt(owner.Source, node,"value"));
    }
    private void Complex(Declaration owner)
    {
        var s = owner.Source; var node = owner.Node; bool mixed = Boolean(s, node, "mixed");
        var children = Children(s, node); XmlNode content = default;
        foreach (var child in children) if (child.LocalName is "simpleContent" or "complexContent") { if (!content.IsDefault) Error("Multiple complex type content bodies.", At(s, child)); content = child; }
        SchemaName basis = AnyType; XsdDerivation derivation = XsdDerivation.Restriction;
        SchemaName? simpleType = null; XsdParticle? particle = null; XsdWildcard? wildcard;
        var bindings = new List<XsdAttributeBinding>();
        XmlNode body = node; XsdTypeDefinition? baseType = null;
        if (!content.IsDefault)
        {
            foreach (var child in children) if (child != content && child.LocalName != "annotation") Error("Content wrapper excludes direct content and attributes.", At(s, child));
            body = SingleBody(owner with { Node = content });
            if (body.LocalName is not ("restriction" or "extension")) Error("Invalid complex content derivation.", At(s, body));
            derivation = body.LocalName == "extension" ? XsdDerivation.Extension : XsdDerivation.Restriction;
            basis = ResolveType(owner, body, Required(s, body, "base"), true); baseType = Type(basis);
            if (baseType.Final.Contains(derivation)) Error("Base type final forbids derivation.", At(s, body));
            if (owner.Previous is { } old && basis != old) Error("Redefined complex type must derive from original.", At(s, body));
            if (content.LocalName == "complexContent")
            {
                if (baseType.SimpleType is not null) Error("Complex content requires a complex base.", At(s, body));
                if (content.GetAttribute("mixed") is not null) mixed = Boolean(s, content, "mixed");
            }
            else
            {
                if (derivation == XsdDerivation.Restriction && simple.ContainsKey(basis)) Error("Simple content restriction requires a complex base.", At(s, body));
                if (baseType.SimpleType is null && !(derivation == XsdDerivation.Restriction && baseType.ContentKind == XsdContentKind.Mixed && Emptiable(baseType.Particle)))
                    Error("Simple content requires a simple or emptiable mixed base.", At(s, body));
                simpleType = baseType.SimpleType;
                XmlNode inline = default; var facets = new List<XmlFacet>();
                foreach (var child in Children(s, body))
                {
                    if (child.LocalName == "simpleType") { if (!inline.IsDefault || derivation != XsdDerivation.Restriction) Error("Invalid inline simple content type.", At(s, child)); inline = child; }
                    else if (IsFacet(child.LocalName)) { if (derivation != XsdDerivation.Restriction) Error("Extension cannot have facets.", At(s, child)); facets.Add(Facet(owner, child)); }
                }
                if (!inline.IsDefault) simpleType = Anonymous(owner, inline);
                if (simpleType is null) Error("Mixed-base simple restriction requires an inline simple type.", At(s, body));
                if (derivation == XsdDerivation.Restriction)
                {
                    Components(At(s, body)); var synthetic = new SchemaName(s.Target, "#anonymous" + (++anonymousId).ToString(CultureInfo.InvariantCulture));
                    var parent = simple[simpleType!.Value];
                    AddSimple(new(synthetic, parent.Variety, simpleType, parent.ItemType, parent.MemberTypes, facets, [], true, At(s, body)), XsdDerivation.Restriction);
                    simpleType = synthetic;
                }
                mixed = false;
            }
        }
        else if (owner.Previous is not null) Error("Redefined complex type requires explicit derivation.", At(s, node));
        bool attributeSection = false;
        foreach (var child in Children(s, body))
        {
            if (child.LocalName is "attribute" or "attributeGroup" or "anyAttribute") attributeSection = true;
            else if (attributeSection && child.LocalName != "annotation") Error("Content particles and facets precede attribute uses.", At(s, child));
            if (child.LocalName is "sequence" or "choice" or "all" or "group")
            { if (particle is not null || simpleType is not null) Error("Invalid duplicate particle.", At(s, child)); particle = Particle(owner, child, true); }
            else if (child.LocalName is not ("annotation" or "attribute" or "attributeGroup" or "anyAttribute" or "simpleType") && !IsFacet(child.LocalName)) Error("Invalid complex type body.", At(s, child));
            else if ((child.LocalName == "simpleType" || IsFacet(child.LocalName)) && (content.IsDefault || content.LocalName != "simpleContent")) Error("Simple facets outside simple content.", At(s, child));
        }
        AttributeBody(owner, body, bindings, out wildcard);
        if (baseType is not null)
        {
            if (derivation == XsdDerivation.Extension)
            {
                if (baseType.Particle is not null)
                {
                    if (particle is not null)
                    {
                        if (IsAll(baseType.Particle) || IsAll(particle)) Error("All content cannot be extended with another particle.", At(s, body));
                        Components(At(s, body)); Components(At(s, body));
                        particle = new(new XsdGroupTerm(XsdCompositor.Sequence, [baseType.Particle, particle]), BigInteger.One, BigInteger.One, At(s, body));
                    }
                    else { particle = baseType.Particle; if (content.LocalName == "complexContent" && node.GetAttribute("mixed") is null && content.GetAttribute("mixed") is null) mixed = baseType.ContentKind == XsdContentKind.Mixed; }
                }
                foreach (var inherited in baseType.Attributes)
                {
                    bool present = false;
                    foreach (var local in bindings) if (attributes[local.DeclarationId].Name == attributes[inherited.DeclarationId].Name)
                    {
                        present = true;
                        if (local.Use == XsdAttributeUse.Prohibited || attributes[local.DeclarationId].Type != attributes[inherited.DeclarationId].Type || inherited.Use == XsdAttributeUse.Required && local.Use != XsdAttributeUse.Required)
                            Error("Incompatible extension of an inherited attribute use.", local.Location);
                    }
                    if (!present) bindings.Add(inherited);
                }
                if (baseType.AnyAttribute is not null) wildcard = wildcard is null ? baseType.AnyAttribute : Union(baseType.AnyAttribute, wildcard, At(s, body));
            }
        }
        var block = new List<XsdTypeBlock>();
        foreach (string token in Tokens(node.GetAttribute("block") ?? s.Block, node.GetAttribute("block") is null ? "restriction extension substitution" : "restriction extension", s, node))
            if (token != "substitution") block.Add(token == "extension" ? XsdTypeBlock.Extension : XsdTypeBlock.Restriction);
        Components(At(s, node));
        types.Add(owner.Name, new(owner.Name, basis, derivation, Boolean(s, node, "abstract"), IsAnonymous(owner.Name), Final(s, node, "restriction extension"), block,
            simpleType is not null ? XsdContentKind.Simple : mixed ? XsdContentKind.Mixed : particle is null ? XsdContentKind.Empty : XsdContentKind.ElementOnly,
            simpleType, particle, bindings, wildcard, At(s, node)));
    }
    private static bool IsFacet(string name) => name is "length" or "minLength" or "maxLength" or "pattern" or "enumeration" or "whiteSpace" or "maxInclusive" or "maxExclusive" or "minInclusive" or "minExclusive" or "totalDigits" or "fractionDigits";
    private static bool IsAll(XsdParticle particle) => particle.Term is XsdGroupTerm { Compositor: XsdCompositor.All };
    private bool Emptiable(XsdParticle? particle)
    {
        Tick(default); if (particle is null || particle.MinOccurs == 0) return true;
        if (particle.Term is not XsdGroupTerm group) return false;
        if (group.Compositor == XsdCompositor.Choice) { foreach (var child in group.Particles) if (Emptiable(child)) return true; return false; }
        foreach (var child in group.Particles) if (!Emptiable(child)) return false; return true;
    }
    private XsdWildcard Union(XsdWildcard first, XsdWildcard second, XmlLocation at) =>
        ComposeWildcard(first, second, true, second.ProcessContents, at);

    private XsdWildcard ComposeWildcard(XsdWildcard first, XsdWildcard second, bool union, XsdProcessContents process, XmlLocation at)
    {
        Tick(at);
        Components(at); // Owned result and its namespace snapshot; entries reserve below.
        var names = new List<string>();
        bool negated = union ? first.Negated || second.Negated : first.Negated && second.Negated;
        if (first.Negated == second.Negated)
        {
            bool combine = union != first.Negated;
            for (int i = 0; i < first.Namespaces.Count; i++)
            {
                Tick(at); string name = first.Namespaces[i];
                if (combine || ContainsNamespace(second.Namespaces, name, at)) Add(name);
            }
            if (combine)
                for (int i = 0; i < second.Namespaces.Count; i++)
                {
                    Tick(at); string name = second.Namespaces[i];
                    if (!ContainsNamespace(first.Namespaces, name, at)) Add(name);
                }
        }
        else
        {
            // Union subtracts the positive set from the excluded set; intersection
            // subtracts the excluded set from the positive set.
            var source = first.Negated == union ? first.Namespaces : second.Namespaces;
            var remove = first.Negated == union ? second.Namespaces : first.Namespaces;
            for (int i = 0; i < source.Count; i++)
            {
                Tick(at); string name = source[i];
                if (!ContainsNamespace(remove, name, at)) Add(name);
            }
        }
        // XSD 1.0 represents any, not(absent), or not(one namespace and absent).
        // Reject NOW: a subsequent finite intersection could erase the evidence.
        if (negated && names.Count != 0 && (names.Count > 2 || !ContainsNamespace(names, "", at)))
            Error("Wildcard namespace composition is not expressible in XSD 1.0.", at);
        Tick(at, names.Count); // Bounded owned snapshot copy in XsdWildcard.
        return new(negated, names, process);

        void Add(string name) { Components(at); names.Add(name); }
    }

    private bool ContainsNamespace(IReadOnlyList<string> names, string value, XmlLocation at)
    {
        for (int i = 0; i < names.Count; i++)
        {
            Tick(at); string candidate = names[i];
            if (candidate.Length != value.Length) continue;
            int j = 0;
            for (; j < value.Length; j++) { Tick(at); if (candidate[j] != value[j]) break; }
            if (j == value.Length) return true;
        }
        return false;
    }

    private int Element(Declaration owner, bool global)
    {
        var s = owner.Source; var node = owner.Node;
        if (global && globalElements.TryGetValue(owner.Name, out int existing)) return existing;
        if (!global && localElements.TryGetValue(node, out existing)) return existing;
        using var scope = Descend(s, node);
        if (node.GetAttribute("ref") is { } reference)
        {
            if (global) Error("Global element cannot have ref.", At(s, node));
            Exclusive(s, node, "ref", "name type form default fixed nillable abstract block final substitutionGroup");
            foreach (var child in Children(s, node)) if (child.LocalName != "annotation") Error("Element reference cannot have a body.", At(s, child));
            var referenced = QName(owner, node, reference); return Element(Find("element", referenced, owner), true);
        }
        string local = NCName(Required(s, node, "name"), s, node);
        string form = node.GetAttribute("form") is { } localForm ? Collapse(localForm) : s.ElementForm;
        if (form is not ("qualified" or "unqualified")) Error("Invalid element form.", At(s, node));
        var name = global ? owner.Name : new SchemaName(form == "qualified" ? s.Target : "", local);
        if (global && (node.GetAttribute("minOccurs") is not null || node.GetAttribute("maxOccurs") is not null || node.GetAttribute("form") is not null)) Error("Global element has local-only attributes.", At(s, node));
        if (!global && (node.GetAttribute("abstract") is not null || node.GetAttribute("substitutionGroup") is not null || node.GetAttribute("final") is not null)) Error("Local element has global-only attributes.", At(s, node));
        Components(At(s, node)); int id = elements.Count; elements.Add(null!);
        if (global) globalElements.Add(name, id); else localElements.Add(node, id);
        SchemaName type = node.GetAttribute("type") is { } lexical ? ResolveType(owner, node, lexical) : AnyType;
        bool inline = false; var constraintIds = new List<int>();
        foreach (var child in Children(s, node))
        {
            if (child.LocalName == "annotation") continue;
            if (child.LocalName is "simpleType" or "complexType")
            {
                if (inline || node.GetAttribute("type") is not null || constraintIds.Count != 0) Error("Element has incompatible inline type.", At(s, child));
                inline = true; type = Anonymous(owner, child);
            }
            else if (child.LocalName is "unique" or "key" or "keyref") constraintIds.Add(Identity(owner, child));
            else Error("Invalid element declaration child.", At(s, child));
        }
        if (node.GetAttribute("default") is not null && node.GetAttribute("fixed") is not null) Error("Element cannot have both default and fixed.", At(s, node));
        SchemaName? head = node.GetAttribute("substitutionGroup") is { } substitution ? QName(owner, node, substitution) : null;
        if (head is not null && !inline && node.GetAttribute("type") is null) inferElementType.Add(id);
        elements[id] = new(id, name, type, Boolean(s, node, "abstract"), Boolean(s, node, "nillable"), node.GetAttribute("default"), node.GetAttribute("fixed"), head,
            ElementBlock(s, node), global ? Final(s, node, "restriction extension") : [], constraintIds, Context(s, node), ConstraintAt(s, node));
        return id;
    }
    private int Attribute(Declaration owner, bool global)
    {
        var s = owner.Source; var node = owner.Node;
        if (global && globalAttributes.TryGetValue(owner.Name, out int existing)) return existing;
        if (!global && localAttributes.TryGetValue(node, out existing)) return existing;
        if (global && (node.GetAttribute("ref") is not null || node.GetAttribute("form") is not null || node.GetAttribute("use") is not null)) Error("Global attribute has local-only attributes.", At(s, node));
        if (node.GetAttribute("ref") is { } reference)
        {
            Exclusive(s, node, "ref", "name type form");
            foreach (var child in Children(s, node)) if (child.LocalName != "annotation") Error("Attribute reference cannot have inline type.", At(s, child));
            return Attribute(Find("attribute", QName(owner, node, reference), owner), true);
        }
        string local = NCName(Required(s, node, "name"), s, node);
        if (local == "xmlns") Error("Attribute name xmlns is reserved.", At(s, node));
        string form = node.GetAttribute("form") is { } localForm ? Collapse(localForm) : s.AttributeForm;
        if (form is not ("qualified" or "unqualified")) Error("Invalid attribute form.", At(s, node));
        var name = global ? owner.Name : new SchemaName(form == "qualified" ? s.Target : "", local);
        if (name.NamespaceUri == "http://www.w3.org/2001/XMLSchema-instance") Error("Attribute declarations in the xsi namespace are forbidden.", At(s, node));
        SchemaName type = node.GetAttribute("type") is { } lexical ? ResolveType(owner, node, lexical) : AnySimple;
        bool inline = false;
        foreach (var child in Children(s, node))
        {
            if (child.LocalName == "annotation") continue;
            if (child.LocalName != "simpleType" || inline || node.GetAttribute("type") is not null) Error("Invalid attribute type definition.", At(s, child));
            inline = true; type = Anonymous(owner, child);
        }
        if (node.GetAttribute("default") is not null && node.GetAttribute("fixed") is not null) Error("Attribute cannot have both default and fixed.", At(s, node));
        Components(At(s, node)); int id = attributes.Count;
        attributes.Add(new(id, name, type, node.GetAttribute("default"), node.GetAttribute("fixed"), Context(s, node), ConstraintAt(s, node)));
        if (global) globalAttributes.Add(name, id); else localAttributes.Add(node, id);
        return id;
    }
    private XsdAttributeBinding Binding(Declaration owner, XmlNode node)
    {
        var s = owner.Source;
        var use = (node.GetAttribute("use") is { } lexical ? Collapse(lexical) : "optional") switch
        { "optional" => XsdAttributeUse.Optional, "required" => XsdAttributeUse.Required, "prohibited" => XsdAttributeUse.Prohibited, _ => (XsdAttributeUse)(-1) };
        if ((int)use < 0) Error("Invalid attribute use.", At(s, node));
        if (node.GetAttribute("default") is not null && (use != XsdAttributeUse.Optional || node.GetAttribute("fixed") is not null)) Error("Attribute default requires optional use and no fixed constraint.", At(s, node));
        int id = Attribute(owner with { Node = node }, false);
        Components(At(s, node));
        return new(id, use, node.GetAttribute("default"), node.GetAttribute("fixed"), Context(s, node), ConstraintAt(s, node));
    }
    private void AttributeBody(Declaration owner, XmlNode body, List<XsdAttributeBinding> bindings, out XsdWildcard? wildcard)
    {
        wildcard = null; bool explicitWildcard = false;
        var names = new HashSet<SchemaName>(); foreach (var binding in bindings) names.Add(attributes[binding.DeclarationId].Name);
        foreach (var node in Children(owner.Source, body))
        {
            if (node.LocalName is not ("attribute" or "attributeGroup" or "anyAttribute")) continue;
            if (explicitWildcard) Error("anyAttribute must be last.", At(owner.Source, node));
            if (node.LocalName == "anyAttribute")
            { var authored = Wildcard(owner, node); wildcard = wildcard is null ? authored : Intersection(authored, wildcard, At(owner.Source, node)); explicitWildcard = true; continue; }
            var added = new List<XsdAttributeBinding>();
            if (node.LocalName == "attribute") added.Add(Binding(owner, node));
            else
            {
                if (node.GetAttribute("name") is not null) Error("Attribute group reference cannot have name.", At(owner.Source, node));
                foreach (var child in Children(owner.Source, node)) if (child.LocalName != "annotation") Error("Attribute group reference cannot have a body.", At(owner.Source, child));
                var name = QName(owner, node, Required(owner.Source, node, "ref"), "attributeGroup");
                AttributeGroup(Find("attributeGroup", name, owner), added, out var includedWildcard);
                if (includedWildcard is not null) wildcard = wildcard is null ? includedWildcard : Intersection(wildcard, includedWildcard, At(owner.Source, node));
            }
            foreach (var binding in added)
            {
                var name = attributes[binding.DeclarationId].Name;
                if (!names.Add(name)) Error("Duplicate attribute use after group expansion.", binding.Location);
                bindings.Add(binding);
            }
        }
    }
    private void AttributeGroup(Declaration owner, List<XsdAttributeBinding> bindings, out XsdWildcard? wildcard)
    {
        var key = ("attributeGroup", owner.Name);
        if (!expanding.Add(key)) Error("Circular attribute group.", At(owner.Source, owner.Node));
        using var scope = Descend(owner.Source, owner.Node);
        try
        {
            foreach (var child in Children(owner.Source, owner.Node)) if (child.LocalName is not ("annotation" or "attribute" or "attributeGroup" or "anyAttribute")) Error("Invalid attribute group child.", At(owner.Source, child));
            int selfReferences = owner.Previous is null ? -1 : CheckRedefinedGroup(owner, "attributeGroup");
            AttributeBody(owner, owner.Node, bindings, out wildcard);
            if (selfReferences == 0)
            {
                var original = Find("attributeGroup", owner.Previous!.Value, owner); var originalBindings = new List<XsdAttributeBinding>();
                AttributeGroup(original, originalBindings, out var originalWildcard);
                RetainGroupRestriction(owner, original, "attribute-group", null, null, bindings, originalBindings, wildcard, originalWildcard);
            }
        }
        finally { expanding.Remove(key); }
    }
    private XsdWildcard Intersection(XsdWildcard first, XsdWildcard second, XmlLocation at) =>
        ComposeWildcard(first, second, false, first.ProcessContents, at);
    private XsdWildcard Wildcard(Declaration owner, XmlNode node)
    {
        var s = owner.Source; string lexical = Collapse(node.GetAttribute("namespace") ?? "##any");
        var namespaces = new List<string>(); bool negated = false;
        if (lexical == "##any") negated = true;
        else if (lexical == "##other") { negated = true; namespaces.Add(s.Target); if (s.Target.Length != 0) namespaces.Add(""); }
        else
        {
            foreach (var range in lexical.AsSpan().Split(' '))
            {
                var token = lexical.AsSpan()[range]; if (token.IsEmpty) Error("Empty wildcard namespace list.", At(s, node));
                string ns = token.SequenceEqual("##targetNamespace") ? s.Target : token.SequenceEqual("##local") ? "" : token.ToString();
                if (ns.StartsWith("##", StringComparison.Ordinal)) Error("Invalid wildcard namespace token.", At(s, node));
                if (!namespaces.Contains(ns)) namespaces.Add(ns);
            }
        }
        var process = (node.GetAttribute("processContents") is { } value ? Collapse(value) : "strict") switch
        { "strict" => XsdProcessContents.Strict, "lax" => XsdProcessContents.Lax, "skip" => XsdProcessContents.Skip, _ => (XsdProcessContents)(-1) };
        if ((int)process < 0) Error("Invalid wildcard processContents.", At(s, node));
        Components(At(s, node)); return new(negated, namespaces, process);
    }
    private XsdParticle Particle(Declaration owner, XmlNode node, bool top = false)
    {
        var s = owner.Source; using var scope = Descend(s, node);
        BigInteger minimum = Occurs(s, node, "minOccurs");
        BigInteger? maximum = node.GetAttribute("maxOccurs") is { } value && Collapse(value) == "unbounded" ? null : Occurs(s, node, "maxOccurs");
        if (maximum is { } max && minimum > max) Error("minOccurs exceeds maxOccurs.", At(s, node));
        XsdTerm term;
        if (node.LocalName == "element") { Components(At(s, node)); term = new XsdElementTerm(Element(owner with { Node = node }, false)); }
        else if (node.LocalName == "any") { Components(At(s, node)); term = new XsdWildcardTerm(Wildcard(owner, node)); }
        else if (node.LocalName == "group")
        {
            if (node.GetAttribute("name") is not null) Error("Local group must use ref.", At(s, node));
            var name = QName(owner, node, Required(s, node, "ref"), "group");
            foreach (var child in Children(s, node)) if (child.LocalName != "annotation") Error("Group ref cannot have a body.", At(s, child));
            term = Group(Find("group", name, owner), node);
        }
        else if (node.LocalName is "sequence" or "choice" or "all")
        {
            var compositor = node.LocalName == "sequence" ? XsdCompositor.Sequence : node.LocalName == "choice" ? XsdCompositor.Choice : XsdCompositor.All;
            var particles = new List<XsdParticle>();
            foreach (var child in Children(s, node))
            {
                if (child.LocalName == "annotation") continue;
                if (compositor == XsdCompositor.All && child.LocalName != "element") Error("All compositor allows only elements.", At(s, child));
                if (child.LocalName is not ("element" or "group" or "sequence" or "choice" or "any")) Error("Invalid particle child.", At(s, child));
                var particle = Particle(owner, child);
                if (compositor == XsdCompositor.All && (particle.MinOccurs > 1 || particle.MaxOccurs is null || particle.MaxOccurs > 1)) Error("All member occurrence exceeds one.", At(s, child));
                particles.Add(particle);
            }
            Components(At(s, node)); term = new XsdGroupTerm(compositor, particles);
        }
        else { Error("Invalid particle.", At(s, node)); throw new InvalidOperationException(); }
        if (term is XsdGroupTerm { Compositor: XsdCompositor.All } && (!top || minimum > 1 || maximum != BigInteger.One)) Error("All group requires top-level placement and maxOccurs=1.", At(s, node));
        Components(At(s, node)); return new(term, minimum, maximum, At(s, node));
    }
    private BigInteger Occurs(Source source, XmlNode node, string name)
    {
        string? value = node.GetAttribute(name); if (value is null) return BigInteger.One;
        value = Collapse(value); Tick(At(source, node), value.Length);
        // XML Schema nonNegativeInteger accepts an optional sign and leading zeros.
        if (!BigInteger.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) || parsed < 0) Error("Invalid occurrence count.", At(source, node));
        return parsed;
    }
    private XsdGroupTerm Group(Declaration owner, XmlNode site)
    {
        var key = ("group", owner.Name);
        if (!expanding.Add(key)) Error("Circular model group.", At(owner.Source, owner.Node));
        using var scope = Descend(owner.Source, owner.Node);
        try
        {
            int selfReferences = owner.Previous is null ? -1 : CheckRedefinedGroup(owner, "group");
            var body = SingleBody(owner);
            if (body.LocalName is not ("sequence" or "choice" or "all") || body.GetAttribute("minOccurs") is not null || body.GetAttribute("maxOccurs") is not null) Error("Global group requires one compositor without occurrence attributes.", At(owner.Source, body));
            var particle = Particle(owner, body, true);
            if (selfReferences == 0)
            {
                var original = Find("group", owner.Previous!.Value, owner);
                var originalTerm = Group(original, site); Components(At(original.Source, original.Node));
                var originalParticle = new XsdParticle(originalTerm, BigInteger.One, BigInteger.One, At(original.Source, original.Node));
                RetainGroupRestriction(owner, original, "model-group", particle, originalParticle, [], [], null, null);
            }
            return (XsdGroupTerm)particle.Term;
        }
        finally { expanding.Remove(key); }
    }
    private int CheckRedefinedGroup(Declaration owner, string kind)
    {
        int count = 0;
        void Visit(XmlNode node)
        {
            using var scope = Descend(owner.Source, node);
            foreach (var child in Children(owner.Source, node))
            {
                if (child.LocalName == "annotation") continue;
                if (child.LocalName == kind && child.GetAttribute("ref") is { } lexical && QName(owner, child, lexical, kind) == owner.Previous)
                {
                    count++;
                    if (kind == "group" && (Occurs(owner.Source, child, "minOccurs") != 1 || Occurs(owner.Source, child, "maxOccurs") != 1 || child.GetAttribute("maxOccurs") == "unbounded")) Error("Redefinition self-reference requires occurrences of one.", At(owner.Source, child));
                }
                Visit(child);
            }
        }
        Visit(owner.Node);
        if (count > 1) Error("Multiple redefinition self-references.", At(owner.Source, owner.Node));
        return count;
    }
    private void RetainGroupRestriction(Declaration owner, Declaration original, string kind,
        XsdParticle? currentParticle, XsdParticle? originalParticle,
        IReadOnlyList<XsdAttributeBinding> currentAttributes, IReadOnlyList<XsdAttributeBinding> originalAttributes,
        XsdWildcard? currentWildcard, XsdWildcard? originalWildcard)
    {
        // Zero-self-reference redefine expresses a restriction. The frozen graph
        // has no group table, so retain both as anonymous type programs linked by
        // Restriction. Content/facet joins can then check the real old/new programs
        // before publishing a facade; this obligation must not disappear at seal.
        var oldName = new SchemaName(owner.Source.Target, "#" + kind + ":" + original.Name.LocalName);
        var newName = new SchemaName(owner.Source.Target, "#" + kind + ":" + owner.Name.LocalName);
        if (!types.ContainsKey(oldName))
        {
            Components(At(original.Source, original.Node));
            types.Add(oldName, new(oldName, AnyType, XsdDerivation.Restriction, false, true, [], [],
                originalParticle is null ? XsdContentKind.Empty : XsdContentKind.ElementOnly, null, originalParticle, originalAttributes, originalWildcard, At(original.Source, original.Node)));
        }
        if (!types.ContainsKey(newName))
        {
            Components(At(owner.Source, owner.Node));
            types.Add(newName, new(newName, oldName, XsdDerivation.Restriction, false, true, [], [],
                currentParticle is null ? XsdContentKind.Empty : XsdContentKind.ElementOnly, null, currentParticle, currentAttributes, currentWildcard, At(owner.Source, owner.Node)));
        }
    }
    private int Identity(Declaration owner, XmlNode node)
    {
        var s = owner.Source; var name = Named(s, node);
        foreach (var identity in identities) if (identity.Name == name) Error("Duplicate identity constraint name.", At(s, node));
        string? selector = null; var fields = new List<string>();
        var originalContext = Context(s, node);
        var prefixes = new Dictionary<string,string>(originalContext.Prefixes, StringComparer.Ordinal);
        var aliases = new Dictionary<(string,string),string>(); int aliasId = 0;
        string Expression(XmlNode authored)
        {
            var lexical = Required(s, authored, "xpath"); var local = Context(s, authored); var rewritten = new StringBuilder();
            for (int index = 0; index < lexical.Length;)
            {
                Tick(At(s, authored)); char current = lexical[index];
                if (current is '\'' or '"')
                {
                    int end = index + 1; while (end < lexical.Length && lexical[end] != current) { Tick(At(s, authored)); end++; }
                    if (end < lexical.Length) end++; rewritten.Append(lexical.AsSpan(index,end-index)); index = end; continue;
                }
                if (!(char.IsLetter(current) || current == '_' || current >= 0x80)) { rewritten.Append(current); index++; continue; }
                int stop = index + 1;
                while (stop < lexical.Length && XPathNameChar(lexical[stop])) { Tick(At(s, authored)); stop++; }
                if (stop < lexical.Length && lexical[stop] == ':' && (stop + 1 == lexical.Length || lexical[stop+1] != ':'))
                {
                    string prefix = NCName(lexical[index..stop], s, authored);
                    if (!local.Prefixes.TryGetValue(prefix,out var ns)) Error("Unbound identity XPath prefix.", At(s, authored));
                    if (!aliases.TryGetValue((prefix,ns!),out var alias))
                    {
                        do { alias = "_xsd" + (++aliasId).ToString(CultureInfo.InvariantCulture); } while (prefixes.ContainsKey(alias));
                        aliases.Add((prefix,ns!),alias); prefixes.Add(alias,ns!);
                    }
                    rewritten.Append(alias); rewritten.Append(':'); index = stop+1;
                }
                else { rewritten.Append(lexical.AsSpan(index,stop-index)); index = stop; }
            }
            return rewritten.ToString();
        }
        foreach (var child in Children(s, node))
        {
            if (child.LocalName == "annotation") continue;
            if (child.LocalName == "selector" && selector is null && fields.Count == 0) selector = Expression(child);
            else if (child.LocalName == "field" && selector is not null) fields.Add(Expression(child));
            else Error("Invalid identity selector/field order.", At(s, child));
        }
        if (selector is null || fields.Count == 0) Error("Identity constraint requires a selector and fields.", At(s, node));
        var kind = node.LocalName == "key" ? XsdIdentityKind.Key : node.LocalName == "unique" ? XsdIdentityKind.Unique : XsdIdentityKind.KeyRef;
        SchemaName? refer = kind == XsdIdentityKind.KeyRef ? QName(owner, node, Required(s, node, "refer")) : null;
        if (kind != XsdIdentityKind.KeyRef && node.GetAttribute("refer") is not null) Error("Only keyref may have refer.", At(s, node));
        Components(At(s, node)); int id = identities.Count;
        identities.Add(new(id, name, kind, selector!, fields, refer, new(prefixes, originalContext.DefaultNamespace, originalContext.BaseUri), At(s, node))); return id;
    }
    private static bool XPathNameChar(char value) => char.IsLetterOrDigit(value) || value is '_' or '-' or '.' || value >= 0x80;
    private void Notation(Declaration owner)
    {
        var s = owner.Source; var node = owner.Node;
        if (node.GetAttribute("public") is null && node.GetAttribute("system") is null) Error("Notation requires public or system identifier.", At(s, node));
        notations.Add(owner.Name, new(owner.Name, node.GetAttribute("public"), node.GetAttribute("system"), Context(s, node), At(s, node)));
    }
    private void Exclusive(Source source, XmlNode node, string name, string forbidden)
    {
        foreach (var range in forbidden.AsSpan().Split(' ')) if (node.GetAttribute(forbidden.AsSpan()[range].ToString()) is not null) Error("Incompatible attributes on a component reference.", At(source, node));
    }

    private void CheckGraphs()
    {
        SchemaName Infer(int id, HashSet<int> seen)
        {
            Tick(elements[id].Location);
            if (!inferElementType.Contains(id)) return elements[id].Type;
            if (!seen.Add(id)) Error("Circular substitution affiliation.", elements[id].Location);
            if (!globalElements.TryGetValue(elements[id].SubstitutionGroup!.Value, out int head)) Error("Undefined substitution head.", elements[id].Location);
            var type = Infer(head, seen); elements[id] = elements[id] with { Type = type }; inferElementType.Remove(id); return type;
        }
        foreach (int id in new List<int>(inferElementType)) Infer(id, new());
        foreach (var element in elements)
        {
            Tick(element.Location); var type = Type(element.Type);
            if ((element.DefaultValue is not null || element.FixedValue is not null) && type.SimpleType is null && !(type.ContentKind == XsdContentKind.Mixed && Emptiable(type.Particle))) Error("Element value constraint requires simple or emptiable mixed content.", element.Location);
            if (element.SubstitutionGroup is not null)
            {
                var visited = new HashSet<int> { element.Id }; var member = element;
                while (member.SubstitutionGroup is { } headName)
                {
                    Tick(member.Location);
                    if (!globalElements.TryGetValue(headName, out int headId)) Error("Undefined substitution group head.", member.Location);
                    if (!visited.Add(headId)) Error("Circular substitution group affiliation.", member.Location);
                    var head = elements[headId]; var derivations = DerivationPath(element.Type, head.Type, element.Location);
                    foreach (var derivation in derivations) if (head.Final.Contains(derivation)) Error("Substitution member derivation is prohibited by head final.", element.Location);
                    member = head;
                }
            }
        }
        foreach (var attribute in attributes)
        { Tick(attribute.Location); if (!simple.ContainsKey(Type(attribute.Type).Name)) Error("Attribute requires a simple type definition.", attribute.Location); }
        foreach (var identity in identities)
        {
            Tick(identity.Location); if (identity.Kind != XsdIdentityKind.KeyRef) continue;
            XsdIdentityConstraint? target = null; foreach (var possible in identities) { Tick(identity.Location); if (possible.Name == identity.Refer) { target = possible; break; } }
            if (target is null || target.Kind == XsdIdentityKind.KeyRef || target.Fields.Count != identity.Fields.Count) Error("Invalid keyref target or field count.", identity.Location);
        }
        foreach (var type in types.Values)
        {
            Tick(type.Location); if (type.BaseType is { } basis && type.Name != AnyType && type.Name != AnySimple)
            {
                var parent = Type(basis);
                if (parent.Final.Contains(type.Derivation)) Error("Type derivation violates base final.", type.Location);
                DerivationPath(type.Name, AnyType, type.Location);
            }
            foreach (var binding in type.Attributes)
            {
                Tick(binding.Location); var attribute = attributes[binding.DeclarationId];
                if (binding.DefaultValue is not null && attribute.FixedValue is not null) Error("A default cannot replace a referenced fixed constraint.", binding.Location);
                // Comparing typed fixed values (QName, numeric, lists...) is a
                // datatype/facet finalization obligation, never string equality.
            }
        }
    }
    private List<XsdDerivation> DerivationPath(SchemaName derived, SchemaName ancestor, XmlLocation at)
    {
        var path = new List<XsdDerivation>(); var seen = new HashSet<SchemaName>();
        while (derived != ancestor)
        {
            Tick(at); if (!seen.Add(derived)) Error("Circular type derivation.", at);
            var type = Type(derived); if (type.BaseType is not { } parent) Error("Substitution type does not derive from the head type.", at);
            path.Add(type.Derivation is XsdDerivation.List or XsdDerivation.Union ? XsdDerivation.Restriction : type.Derivation); derived = type.BaseType!.Value;
        }
        return path;
    }

    private void Syntax(Source source, XmlNode root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Visit(XmlNode node)
        {
            using var scope = Descend(source, node);
            string allowed = node.LocalName switch
            {
                "schema" => "id targetNamespace version finalDefault blockDefault attributeFormDefault elementFormDefault",
                "include" or "redefine" => "id schemaLocation", "import" => "id namespace schemaLocation",
                "annotation" => "id", "appinfo" => "source", "documentation" => "source",
                "simpleType" => "id name final", "complexType" => "id name abstract mixed final block",
                "simpleContent" => "id", "complexContent" => "id mixed",
                "restriction" or "extension" => "id base", "list" => "id itemType", "union" => "id memberTypes",
                "element" => "id name ref type substitutionGroup abstract nillable default fixed form block final minOccurs maxOccurs",
                "attribute" => "id name ref type default fixed form use", "group" => "id name ref minOccurs maxOccurs",
                "attributeGroup" => "id name ref", "sequence" or "choice" or "all" => "id minOccurs maxOccurs",
                "any" => "id namespace processContents minOccurs maxOccurs", "anyAttribute" => "id namespace processContents",
                "key" or "unique" => "id name", "keyref" => "id name refer", "selector" or "field" => "id xpath",
                "notation" => "id name public system", _ when IsFacet(node.LocalName) => "id value fixed", _ => "#invalid"
            };
            if (allowed == "#invalid") Error("Unknown XSD construct (XSD 1.0 required).", At(source, node));
            bool globalType = !node.Parent.IsDefault && node.Parent.NamespaceUri == Namespace && node.Parent.LocalName is "schema" or "redefine";
            if (!globalType && node.LocalName == "simpleType") allowed = "id";
            if (!globalType && node.LocalName == "complexType") allowed = "id mixed";
            foreach (var attribute in node.Attributes)
            {
                Tick(At(source, node));
                if (attribute.NamespaceUri.Length != 0)
                { if (attribute.NamespaceUri == Namespace) Error("Schema attributes are unqualified.", At(source, node)); continue; }
                bool found = false;
                foreach (var range in allowed.AsSpan().Split(' ')) if (attribute.LocalName.AsSpan().SequenceEqual(allowed.AsSpan()[range])) { found = true; break; }
                if (!found) Error("Unknown or misplaced schema attribute.", At(source, node));
                if (attribute.LocalName == "id" && !ids.Add(NCName(attribute.Value, source, node))) Error("Duplicate schema ID.", At(source, node));
                if (attribute.LocalName is "form" or "elementFormDefault" or "attributeFormDefault" && Collapse(attribute.Value) is not ("qualified" or "unqualified")) Error("Invalid form value.", At(source, node));
            }
            if (node.LocalName == "annotation")
            {
                foreach (var child in node.Children)
                {
                    Tick(At(source, child));
                    if (child.Kind != XmlNodeKind.Element) continue;
                    if (child.NamespaceUri != Namespace || child.LocalName is not ("appinfo" or "documentation")) Error("Invalid annotation body.", At(source, child));
                    // Annotation content is opaque and cannot execute or add schema components.
                }
                return;
            }
            bool annotation = false, bodySeen = false;
            foreach (var child in Children(source, node))
            {
                if (node.LocalName != "schema" && node.LocalName != "redefine")
                {
                    if (child.LocalName == "annotation") { if (annotation || bodySeen) Error("Annotation must occur once, first.", At(source, child)); annotation = true; }
                    else bodySeen = true;
                }
                string permitted = node.LocalName switch
                {
                    "schema" => "annotation include import redefine simpleType complexType element attribute group attributeGroup notation",
                    "include" or "import" => "annotation", "redefine" => "annotation simpleType complexType group attributeGroup",
                    "simpleType" => "annotation restriction list union", "complexType" => "annotation simpleContent complexContent group all choice sequence attribute attributeGroup anyAttribute",
                    "simpleContent" or "complexContent" => "annotation restriction extension",
                    "restriction" => "annotation simpleType group all choice sequence length minLength maxLength pattern enumeration whiteSpace maxInclusive maxExclusive minInclusive minExclusive totalDigits fractionDigits attribute attributeGroup anyAttribute",
                    "extension" => "annotation group all choice sequence attribute attributeGroup anyAttribute", "list" or "union" => "annotation simpleType",
                    "element" => "annotation simpleType complexType unique key keyref", "attribute" => "annotation simpleType",
                    "group" => "annotation all choice sequence", "attributeGroup" => "annotation attribute attributeGroup anyAttribute",
                    "sequence" or "choice" => "annotation element group choice sequence any", "all" => "annotation element",
                    "key" or "unique" or "keyref" => "annotation selector field", _ => "annotation"
                };
                bool permittedChild = false; foreach (var range in permitted.AsSpan().Split(' ')) if (child.LocalName.AsSpan().SequenceEqual(permitted.AsSpan()[range])) { permittedChild = true; break; }
                if (!permittedChild) Error("Invalid child for XSD construct.", At(source, child));
                Visit(child);
            }
        }
        Visit(root);
    }
}

internal sealed class XsdDefinitionRegistry : IXmlDatatypeRegistry
{
    private readonly IReadOnlyDictionary<SchemaName, XmlDatatypeDefinition> definitions;
    internal XsdDefinitionRegistry(IReadOnlyList<XmlDatatypeDefinition> types)
    { var owned = new Dictionary<SchemaName, XmlDatatypeDefinition>(); foreach (var type in types) owned.Add(type.Name, type); definitions = new ReadOnlyDictionary<SchemaName, XmlDatatypeDefinition>(owned); }
    public IXmlDatatypeLibrary? Find(string namespaceUri) { ArgumentNullException.ThrowIfNull(namespaceUri); return null; }
    public XmlDatatypeDefinition? FindType(SchemaName name) => definitions.TryGetValue(name, out var value) ? value : null;
}

// A graph consumer for the later content join. Closure contains transitive legal
// affiliations; element/type block affects instance substitution, not whether a
// valid affiliation declaration can exist in the compiled component set.
internal static class XsdSubstitutionGroups
{
    internal static IReadOnlyList<int> Members(XsdCompiledSchema schema, int headId, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(operation);
        if ((uint)headId >= (uint)schema.Elements.Count) throw new ArgumentOutOfRangeException(nameof(headId));
        var result = new List<int>(); var head = schema.Elements[headId];
        var typeMap = new Dictionary<SchemaName, XsdTypeDefinition>();
        foreach (var definition in schema.Types)
        { if (!operation.Charge(1, definition.Location)) throw SchemaSnapshots.Failure(operation); typeMap.Add(definition.Name, definition); }
        var headType = typeMap[head.Type];
        if (head.Block.Contains(XsdElementBlock.Substitution))
        { if (!head.Abstract) result.Add(headId); return result.AsReadOnly(); }
        foreach (var candidate in schema.GlobalElements.Values)
        {
            var element = schema.Elements[candidate]; if (element.Abstract) continue;
            var seen = new HashSet<int>(); int current = candidate; bool affiliated = current == headId;
            while (!affiliated && schema.Elements[current].SubstitutionGroup is { } parent && schema.GlobalElements.TryGetValue(parent, out current))
            {
                if (!operation.Charge(1, element.Location)) throw SchemaSnapshots.Failure(operation);
                if (!seen.Add(current)) break; affiliated = current == headId;
            }
            if (!affiliated) continue;
            SchemaName type = element.Type; bool blocked = false;
            var typeSeen = new HashSet<SchemaName>(); var path = new List<XsdTypeDefinition>();
            while (type != head.Type)
            {
                if (!operation.Charge(1, element.Location)) throw SchemaSnapshots.Failure(operation);
                if (!typeSeen.Add(type) || !typeMap.TryGetValue(type,out var definition) || definition.BaseType is not { } basis) { blocked = true; break; }
                path.Add(definition); type = basis;
            }
            bool restriction = false, extension = false;
            foreach (var definition in path)
            { restriction |= definition.Derivation is XsdDerivation.Restriction or XsdDerivation.List or XsdDerivation.Union; extension |= definition.Derivation == XsdDerivation.Extension; }
            if (restriction && head.Block.Contains(XsdElementBlock.Restriction) || extension && head.Block.Contains(XsdElementBlock.Extension)) blocked = true;
            path.Add(headType);
            // Blocks prohibit descendants of a type from substituting that
            // type. The candidate's own block has no bearing on its substitution
            // for an ancestor; head and intermediate definitions contribute.
            for (int blockIndex = 1; blockIndex < path.Count; blockIndex++)
            {
                var definition = path[blockIndex];
                if (!operation.Charge(1, element.Location)) throw SchemaSnapshots.Failure(operation);
                if (restriction && definition.Block.Contains(XsdTypeBlock.Restriction) || extension && definition.Block.Contains(XsdTypeBlock.Extension)) blocked = true;
            }
            if (!blocked) result.Add(candidate);
        }
        return result.AsReadOnly();
    }
}
