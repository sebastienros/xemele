using System.Diagnostics.CodeAnalysis;
using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

// A compiler instance has no run state and may be shared. Build owns mutable
// source handles only until publication; the result owns strings, contexts and
// graph snapshots, never source documents or resolver streams.
internal sealed class RelaxNgCompiler : IRelaxNgCompiler
{
    public RngCompiledSchema Compile(XmlDocument schema, SchemaCompileOptions options, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(operation);
        return new Build(SchemaSnapshots.Compile(options), operation).Compile(schema);
    }

    private sealed class Build
    {
        private const string Rng = "http://relaxng.org/ns/structure/1.0";
        private const string XsdLibrary = "http://www.w3.org/2001/XMLSchema-datatypes";
        private readonly SchemaCompileOptions options;
        private readonly ISchemaOperation op;
        private readonly SchemaResourceLoader loader;
        private readonly List<Pattern> patterns = new();
        private readonly List<Pending> pending = new();
        private readonly List<SchemaDocumentScope> sources = new();
        private readonly HashSet<Uri> active = new();
        private readonly Dictionary<XmlNode, XmlDocument> loaded = new();
        private int depth;
        internal Build(SchemaCompileOptions options, ISchemaOperation op)
        { this.options = options; this.op = op; loader = new(options, op); }

        private sealed class Pattern(RngPatternKind kind, Env env)
        {
            internal RngPatternKind Kind = kind;
            internal List<int> Children = new();
            internal RngNameClass? Name;
            internal SchemaName? Datatype;
            internal List<XmlFacet> Parameters = new();
            internal string? Value;
            internal SchemaNamespaceContext Context = env.Scope.Context(env.Node);
            internal XmlLocation At = env.Scope.Location(env.Node);
        }
        private sealed record Env(XmlNode Node, SchemaDocumentScope Scope, string Ns, string Library);
        private sealed class Grammar(Grammar? parent)
        {
            internal Grammar? Parent = parent;
            internal Dictionary<string, int> Definitions = new(StringComparer.Ordinal);
        }
        private sealed record Part(string Key, string? Combine, Env Env);
        private sealed record Pending(int Id, Grammar Grammar, string Name);

        internal RngCompiledSchema Compile(XmlDocument document)
        {
            var scope = Open(document);
            if (scope.SourceUri is not null) active.Add(scope.SourceUri);
            var root = document.RootElement;
            if (root.IsDefault) Fail("Missing schema root.", default);
            Syntax(new(root, scope, "", ""), "pattern");
            int start = Parse(new(root, scope, "", ""), null);
            foreach (var item in pending)
            {
                Work(1, patterns[item.Id].At);
                if (!item.Grammar.Definitions.TryGetValue(item.Name, out int target)) Fail("Undefined grammar reference.", patterns[item.Id].At);
                patterns[item.Id].Children.Add(target);
            }
            var normalized = Normalize(start);
            RngRestrictions.Check(normalized.Start, normalized.Patterns, op);
            foreach (var source in sources) source.CheckUnchanged();
            Work(0, normalized.Patterns[normalized.Start].Location);
            return normalized;
        }
        private SchemaDocumentScope Open(XmlDocument document)
        { var scope = new SchemaDocumentScope(document, op, loader.SourceUri(document)); sources.Add(scope); return scope; }
        private void Work(long count, XmlLocation at) { if (!op.Charge(count, at)) throw SchemaSnapshots.Failure(op); }
        private void Component(XmlLocation at) { if (!op.ChargeComponents(1, at)) throw SchemaSnapshots.Failure(op); }
        [DoesNotReturn] private void Fail(string message, XmlLocation at, string code = "RNG.SchemaRestriction")
        { op.AddDiagnostic(new(code, XmlDiagnosticSeverity.Error, message, at)); throw SchemaSnapshots.Failure(op); }
        private int Add(RngPatternKind kind, Env env, params int[] children)
        {
            Component(env.Scope.Location(env.Node));
            var pattern = new Pattern(kind, env); pattern.Children.AddRange(children);
            int id = patterns.Count; patterns.Add(pattern); return id;
        }
        private Env Read(Env env, ReadOnlySpan<string> allowed)
        {
            env.Scope.CheckUnchanged(); var node = env.Node; var at = env.Scope.Location(node);
            Work(1, at);
            if (node.NamespaceUri != Rng) Fail("Expected a RELAX NG element.", at);
            string ns = node.GetAttribute("ns") ?? env.Ns, library = node.GetAttribute("datatypeLibrary") ?? env.Library;
            foreach (var attribute in node.Attributes)
            {
                Scan(attribute.Value.AsSpan(), at);
                if (attribute.NamespaceUri.Length != 0 && attribute.NamespaceUri != Rng) continue;
                bool found = attribute.NamespaceUri.Length == 0 && (attribute.LocalName is "ns" or "datatypeLibrary");
                foreach (string name in allowed) found |= attribute.NamespaceUri.Length == 0 && attribute.LocalName == name;
                if (!found) Fail("Unexpected schema attribute.", env.Scope.Location(attribute));
            }
            if (library.Length != 0 && (!Uri.TryCreate(library, UriKind.Absolute, out var uri) || uri.Fragment.Length != 0))
                Fail("datatypeLibrary must be an absolute fragment-free URI or empty.", at);
            return env with { Ns = ns, Library = library };
        }
        private void Scan(ReadOnlySpan<char> value, XmlLocation at)
        { foreach (var rune in value.EnumerateRunes()) Work(1, at); }
        private List<Env> Children(Env env, bool text = false)
        {
            var result = new List<Env>();
            foreach (var child in env.Node.Children)
            {
                Work(1, env.Scope.Location(child));
                if (child.Kind == XmlNodeKind.Element)
                {
                    if (child.NamespaceUri == Rng) result.Add(env with { Node = child });
                    else if (text) Fail("Foreign children are not allowed in name/value/param.", env.Scope.Location(child));
                }
                else if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                {
                    Scan(child.Value.AsSpan(), env.Scope.Location(child));
                    if (!text && !Trim(child.Value.AsSpan()).IsEmpty) Fail("Unexpected schema text.", env.Scope.Location(child));
                }
            }
            return result;
        }
        private string Required(Env env, string attribute, bool ncname = false)
        {
            string? value = env.Node.GetAttribute(attribute);
            if (value is null) Fail("Required schema attribute is missing.", env.Scope.Location(env.Node));
            if (ncname)
            {
                var span = Trim(value.AsSpan()); CheckName(span, env);
                return span.Length == value.Length ? value : span.ToString(); // owned symbol boundary
            }
            return value;
        }
        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text) => text.Trim(" \t\r\n");
        // Full XML grammar is checked before include overrides and reachability
        // discard components. This also expands resources in discarded branches:
        // externalRef/include expansion precedes the later simplifications.
        private void Syntax(Env source, string category)
        {
            if (++depth > (op.Limits.Depth ?? 1024))
            { op.Refuse("SCHEMA.Limit", "Schema syntax depth exceeded.", source.Scope.Location(source.Node)); throw SchemaSnapshots.Failure(op); }
            try
            {
                string tag = source.Node.LocalName;
                bool allowed = category switch
                {
                    "pattern" => tag is "element" or "attribute" or "group" or "interleave" or "choice" or "optional" or "zeroOrMore" or "oneOrMore" or "list" or "mixed" or "ref" or "parentRef" or "empty" or "text" or "value" or "data" or "notAllowed" or "externalRef" or "grammar",
                    "nameClass" => tag is "name" or "anyName" or "nsName" or "choice",
                    "grammar" => tag is "start" or "define" or "div" or "include",
                    "include" => tag is "start" or "define" or "div",
                    _ => false
                };
                if (!allowed) Fail("Unexpected element in schema grammar.", source.Scope.Location(source.Node));
                ReadOnlySpan<string> attributes = tag switch
                {
                    "element" or "attribute" or "ref" or "parentRef" => ["name"],
                    "value" or "data" => ["type"], "externalRef" or "include" => ["href"],
                    "start" => ["combine"], "define" => ["name", "combine"], _ => []
                };
                var env = Read(source, attributes); var at = env.Scope.Location(env.Node);
                var children = Children(env, tag is "name" or "value");
                if (tag is "ref" or "parentRef" or "define") Required(env, "name", true);
                if (tag == "data") Required(env, "type", true);
                if (tag == "value" && env.Node.GetAttribute("type") is not null) Required(env, "type", true);
                if (tag is "define" or "start" && env.Node.GetAttribute("combine") is string combine && Trim(combine) is var method && !method.SequenceEqual("choice") && !method.SequenceEqual("interleave"))
                    Fail("Unknown combine method.", at);
                if (tag == "name" || tag is "element" or "attribute" && env.Node.GetAttribute("name") is not null)
                {
                    var qname = Trim((tag == "name" ? env.Node.GetTextContent() : Required(env, "name")).AsSpan()); int colon = qname.IndexOf(':');
                    if (colon < 0) CheckName(qname, env); else { CheckName(qname[..colon], env); CheckName(qname[(colon + 1)..], env); }
                }
                if (tag is "ref" or "parentRef" or "empty" or "text" or "notAllowed" or "name" or "value" or "externalRef")
                {
                    if (children.Count != 0) Fail("Leaf grammar element has children.", at);
                    if (tag == "externalRef") Resource(env, SchemaResourcePurpose.RelaxNgExternalRef, target => { Syntax(target, "pattern"); return 0; });
                    return;
                }
                if (tag is "grammar" or "div" or "include")
                {
                    if (tag == "include") Resource(env, SchemaResourcePurpose.RelaxNgInclude, target =>
                    { if (target.Node.LocalName != "grammar") Fail("include target is not a grammar.", target.Scope.Location(target.Node)); Syntax(target, "pattern"); return 0; });
                    foreach (var child in children) Syntax(child, tag == "include" || category == "include" ? "include" : "grammar");
                    return;
                }
                if (category == "nameClass" && tag is "anyName" or "nsName")
                {
                    if (children.Count > 1 || children.Count == 1 && children[0].Node.LocalName != "except") Fail("Invalid name-class children.", at);
                    if (children.Count == 1) ExceptSyntax(children[0], "nameClass");
                    return;
                }
                if (tag == "data")
                {
                    bool except = false;
                    foreach (var child in children)
                    {
                        if (child.Node.LocalName == "param" && !except)
                        { var param = Read(child, ["name"]); Required(param, "name", true); if (Children(param, true).Count != 0) Fail("param has children.", param.Scope.Location(param.Node)); }
                        else if (child.Node.LocalName == "except" && !except) { except = true; ExceptSyntax(child, "pattern"); }
                        else Fail("Invalid data children.", child.Scope.Location(child.Node));
                    }
                    return;
                }
                int offset = 0;
                if (tag is "element" or "attribute" && env.Node.GetAttribute("name") is null)
                { if (children.Count == 0) Fail("Missing name class.", at); Syntax(children[0], "nameClass"); offset = 1; }
                int count = children.Count - offset;
                if (tag == "attribute" ? count > 1 : count == 0 || tag == "start" && count != 1) Fail("Invalid schema child count.", at);
                for (int i = offset; i < children.Count; i++) Syntax(children[i], category == "nameClass" ? "nameClass" : "pattern");
            }
            finally { depth--; }
        }
        private void ExceptSyntax(Env source, string category)
        {
            var env = Read(source, []); var children = Children(env);
            if (children.Count == 0) Fail("Empty except.", env.Scope.Location(env.Node));
            foreach (var child in children) Syntax(child, category);
        }
        private void CheckName(ReadOnlySpan<char> value, Env env)
        {
            bool first = true;
            foreach (var rune in value.EnumerateRunes())
            {
                Work(1, env.Scope.Location(env.Node));
                if (rune.Value == ':' || (first ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value)))
                    Fail("Invalid NCName.", env.Scope.Location(env.Node));
                first = false;
            }
            if (first) Fail("Empty NCName.", env.Scope.Location(env.Node));
        }
        private SchemaName QName(ReadOnlySpan<char> value, Env env, string ns)
        {
            value = Trim(value); int colon = value.IndexOf(':');
            if (colon < 0) { CheckName(value, env); return new(ns, value.ToString()); }
            CheckName(value[..colon], env); CheckName(value[(colon + 1)..], env);
            var context = env.Scope.Context(env.Node);
            // Only persist prefix/local names at this namespace resolution boundary.
            if (!context.Prefixes.TryGetValue(value[..colon].ToString(), out var uri)) Fail("Undeclared QName prefix.", env.Scope.Location(env.Node));
            return new(uri, value[(colon + 1)..].ToString());
        }
        private int Parse(Env source, Grammar? grammar)
        {
            // Parser recursion is bounded by the same document nesting policy, and
            // a finite representation guard still applies to unlimited policies.
            if (++depth > (op.Limits.Depth ?? 1024))
            { op.Refuse("SCHEMA.Limit", "Schema pattern depth exceeded.", source.Scope.Location(source.Node)); throw SchemaSnapshots.Failure(op); }
            try { return ParseCore(source, grammar); } finally { depth--; }
        }
        private int ParseCore(Env source, Grammar? grammar)
        {
            string tag = source.Node.LocalName;
            ReadOnlySpan<string> attrs = tag switch
            {
                "element" or "attribute" => ["name"], "ref" or "parentRef" => ["name"],
                "data" or "value" => ["type"], "externalRef" => ["href"], _ => []
            };
            var env = Read(source, attrs); var children = Children(env, tag == "value"); var at = env.Scope.Location(env.Node);
            switch (tag)
            {
                case "empty": case "text": case "notAllowed":
                    if (children.Count != 0) Fail("Leaf pattern has children.", at);
                    return Add(tag == "empty" ? RngPatternKind.Empty : tag == "text" ? RngPatternKind.Text : RngPatternKind.NotAllowed, env);
                case "ref": case "parentRef":
                    if (children.Count != 0) Fail("Reference has children.", at);
                    var target = tag == "parentRef" ? grammar?.Parent : grammar;
                    if (target is null) Fail("Reference has no enclosing grammar scope.", at);
                    int reference = Add(RngPatternKind.Ref, env); pending.Add(new(reference, target, Required(env, "name", true))); return reference;
                case "grammar": return ParseGrammar(env, grammar);
                case "externalRef":
                    if (children.Count != 0) Fail("externalRef has children.", at);
                    return Resource(env, SchemaResourcePurpose.RelaxNgExternalRef, (loaded) => Parse(loaded, grammar));
                case "element": case "attribute":
                    bool attribute = tag == "attribute";
                    RngNameClass name;
                    if (env.Node.GetAttribute("name") is string lexical)
                    {
                        Component(at);
                        name = new RngName(QName(lexical, env, attribute && env.Node.GetAttribute("ns") is null ? "" : env.Ns));
                        CheckAttributeName(name, env, attribute);
                    }
                    else
                    {
                        if (children.Count == 0) Fail("Missing name class.", at);
                        name = Name(children[0], attribute, 0); children.RemoveAt(0);
                    }
                    if (attribute && children.Count > 1) Fail("Attribute has more than one content pattern.", at);
                    int content = children.Count == 0 && attribute ? Add(RngPatternKind.Text, env) : Fold(children, RngPatternKind.Group, grammar, env);
                    int named = Add(attribute ? RngPatternKind.Attribute : RngPatternKind.Element, env, content); patterns[named].Name = name; return named;
                case "group": case "interleave": case "choice":
                    return Fold(children, tag == "group" ? RngPatternKind.Group : tag == "choice" ? RngPatternKind.Choice : RngPatternKind.Interleave, grammar, env);
                case "optional": case "zeroOrMore": case "oneOrMore": case "list": case "mixed":
                    int body = Fold(children, RngPatternKind.Group, grammar, env);
                    return tag switch
                    {
                        "optional" => Add(RngPatternKind.Choice, env, body, Add(RngPatternKind.Empty, env)),
                        "zeroOrMore" => Add(RngPatternKind.Choice, env, Add(RngPatternKind.OneOrMore, env, body), Add(RngPatternKind.Empty, env)),
                        "oneOrMore" => Add(RngPatternKind.OneOrMore, env, body),
                        "list" => Add(RngPatternKind.List, env, body),
                        _ => Add(RngPatternKind.Interleave, env, body, Add(RngPatternKind.Text, env))
                    };
                case "value": case "data": return Datatype(env, children, grammar, tag == "value");
                default: Fail("Unknown RELAX NG pattern element.", at); return -1;
            }
        }
        private int Fold(List<Env> children, RngPatternKind kind, Grammar? grammar, Env env)
        {
            if (children.Count == 0) Fail("Pattern requires at least one child.", env.Scope.Location(env.Node));
            int id = Parse(children[0], grammar);
            for (int i = 1; i < children.Count; i++) id = Add(kind, env, id, Parse(children[i], grammar));
            return id;
        }

        private RngNameClass Name(Env source, bool attribute, int except)
        {
            if (++depth > (op.Limits.Depth ?? 1024))
            { op.Refuse("SCHEMA.Limit", "Schema name class depth exceeded.", source.Scope.Location(source.Node)); throw SchemaSnapshots.Failure(op); }
            try
            {
                var env = Read(source, []); string tag = env.Node.LocalName; var at = env.Scope.Location(env.Node);
                var children = Children(env, tag == "name"); Component(at);
                RngNameClass result;
                switch (tag)
                {
                    case "name":
                        if (children.Count != 0) Fail("Name has element children.", at);
                        result = new RngName(QName(env.Node.GetTextContent(), env, env.Ns)); break;
                    case "choice":
                        if (children.Count == 0) Fail("Empty name-class choice.", at);
                        result = Name(children[0], attribute, except);
                        for (int i = 1; i < children.Count; i++) { if (i > 1) Component(at); result = new RngNameChoice(result, Name(children[i], attribute, except)); }
                        break;
                    case "anyName": case "nsName":
                        if (tag == "anyName" && except != 0 || tag == "nsName" && except == 2) Fail("Illegal name-class exclusion.", at);
                        RngNameClass? exclusion = null;
                        if (children.Count != 0)
                        {
                            if (children.Count != 1 || children[0].Node.LocalName != "except") Fail("Name class requires one except child.", at);
                            var excluded = Read(children[0], []); var classes = Children(excluded);
                            if (classes.Count == 0) Fail("Empty name-class except.", at);
                            int nesting = tag == "nsName" ? 2 : Math.Max(except, 1);
                            exclusion = Name(classes[0], attribute, nesting);
                            for (int i = 1; i < classes.Count; i++) { Component(at); exclusion = new RngNameChoice(exclusion, Name(classes[i], attribute, nesting)); }
                        }
                        result = tag == "anyName" ? new RngAnyName(exclusion) : new RngNsName(env.Ns, exclusion); break;
                    default: Fail("Unknown name-class element.", at); return null!;
                }
                CheckAttributeName(result, env, attribute); return result;
            }
            finally { depth--; }
        }
        private void CheckAttributeName(RngNameClass name, Env env, bool attribute)
        {
            if (!attribute) return;
            if (name is RngName n && (n.Name.NamespaceUri == "" && n.Name.LocalName == "xmlns" || n.Name.NamespaceUri == "http://www.w3.org/2000/xmlns") ||
                name is RngNsName ns && ns.NamespaceUri == "http://www.w3.org/2000/xmlns")
                Fail("Namespace declarations cannot be attribute names.", env.Scope.Location(env.Node));
        }

        private int ParseGrammar(Env env, Grammar? parent)
        {
            var grammar = new Grammar(parent); var parts = Parts(env, false);
            var groups = new Dictionary<string, List<Part>>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                Component(part.Env.Scope.Location(part.Env.Node));
                if (!groups.TryGetValue(part.Key, out var list)) { list = new(); groups.Add(part.Key, list); }
                list.Add(part);
            }
            if (!groups.ContainsKey("")) Fail("Grammar has no start.", env.Scope.Location(env.Node));
            foreach (var (key, list) in groups)
            {
                if (key.Length == 0) continue;
                grammar.Definitions.Add(key, Add(RngPatternKind.Ref, list[0].Env));
            }
            int start = -1;
            foreach (var (key, list) in groups)
            {
                string? combine = null; int missing = 0;
                foreach (var part in list)
                {
                    if (part.Combine is null) missing++;
                    else if (combine is not null && combine != part.Combine) Fail("Conflicting combine methods.", part.Env.Scope.Location(part.Env.Node));
                    else combine = part.Combine;
                }
                if (missing > 1) Fail("Duplicate uncombined declaration.", list[0].Env.Scope.Location(list[0].Env.Node));
                int body = -1;
                foreach (var part in list)
                {
                    var content = Children(part.Env);
                    if (key.Length == 0 && content.Count != 1) Fail("start requires exactly one pattern.", part.Env.Scope.Location(part.Env.Node));
                    int current = Fold(content, RngPatternKind.Group, grammar, part.Env);
                    body = body < 0 ? current : Add(combine == "choice" ? RngPatternKind.Choice : RngPatternKind.Interleave, part.Env, body, current);
                }
                if (key.Length == 0) start = body; else patterns[grammar.Definitions[key]].Children.Add(body);
            }
            return start;
        }
        private List<Part> Parts(Env env, bool overrides)
        {
            if (++depth > (op.Limits.Depth ?? 1024))
            { op.Refuse("SCHEMA.Limit", "Schema component depth exceeded.", env.Scope.Location(env.Node)); throw SchemaSnapshots.Failure(op); }
            try { return PartsCore(env, overrides); } finally { depth--; }
        }
        private List<Part> PartsCore(Env env, bool overrides)
        {
            var result = new List<Part>();
            foreach (var source in Children(env))
            {
                string tag = source.Node.LocalName;
                if (tag is "start" or "define")
                {
                    var part = Read(source, tag == "start" ? ["combine"] : ["name", "combine"]);
                    string key = tag == "start" ? "" : Required(part, "name", true);
                    string? combine = part.Node.GetAttribute("combine");
                    if (combine is not null) { combine = Trim(combine.AsSpan()).ToString(); if (combine is not ("choice" or "interleave")) Fail("Unknown combine method.", part.Scope.Location(part.Node)); }
                    result.Add(new(key, combine, part));
                }
                else if (tag == "div") result.AddRange(Parts(Read(source, []), overrides));
                else if (tag == "include" && !overrides)
                {
                    var include = Read(source, ["href"]); var replacements = Parts(include, true);
                    var imported = Resource(include, SchemaResourcePurpose.RelaxNgInclude, loaded =>
                    {
                        if (loaded.Node.LocalName != "grammar") Fail("include target is not a grammar.", loaded.Scope.Location(loaded.Node));
                        return Parts(Read(loaded, []), false);
                    });
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var replacement in replacements) keys.Add(replacement.Key);
                    foreach (string key in keys)
                    {
                        bool found = false; foreach (var part in imported) { Work(1, part.Env.Scope.Location(part.Env.Node)); if (part.Key == key) found = true; }
                        if (!found) Fail("include override has no matching imported declaration.", include.Scope.Location(include.Node));
                    }
                    foreach (var part in imported) if (!keys.Contains(part.Key)) result.Add(part);
                    result.AddRange(replacements);
                }
                else Fail("Invalid grammar component.", source.Scope.Location(source.Node));
            }
            return result;
        }
        private T Resource<T>(Env env, SchemaResourcePurpose purpose, Func<Env, T> compile)
        {
            string href = Required(env, "href"); var context = env.Scope.Context(env.Node); var at = env.Scope.Location(env.Node);
            if (href.AsSpan().Contains('#')) Fail("Schema references cannot have fragments.", at);
            Uri? request = null;
            if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)) request = absolute;
            else if (context.BaseUri is not null) Uri.TryCreate(context.BaseUri, href, out request);
            if (request is not null && active.Contains(request)) Cycle(at);
            if (!loaded.TryGetValue(env.Node, out var document))
            { document = loader.Load(new(href, context.BaseUri, purpose, at), op); loaded.Add(env.Node, document); }
            var scope = Open(document);
            Uri final = scope.SourceUri!;
            if (!active.Add(final)) Cycle(new(final, at.LineNumber, at.LinePosition, at.ByteOffset));
            using var composition = loader.EnterComposition(at);
            try
            {
                // Only ns transfers across a resource boundary. Library and the
                // original resource's namespace/base maps are independent.
                return compile(new(document.RootElement, scope, env.Ns, ""));
            }
            finally { active.Remove(final); }
        }
        [DoesNotReturn] private void Cycle(XmlLocation at)
        { op.Refuse("SCHEMA.ResourceCycle", "RELAX NG resource cycle.", at); throw SchemaSnapshots.Failure(op); }

        private int Datatype(Env env, List<Env> children, Grammar? grammar, bool value)
        {
            var at = env.Scope.Location(env.Node); string? type = env.Node.GetAttribute("type");
            string library = env.Library;
            if (type is null && value) { type = "token"; library = ""; }
            else type = Required(env, "type", true);
            if (library is not ("" or XsdLibrary)) Fail("Unsupported datatype library.", at, "RNG.Datatype");
            int id = Add(value ? RngPatternKind.Value : RngPatternKind.Data, env); var pattern = patterns[id];
            pattern.Datatype = new(library, type);
            if (value)
            {
                if (children.Count != 0) Fail("value cannot contain elements.", at);
                pattern.Value = env.Node.GetTextContent(); pattern.Context = pattern.Context with { DefaultNamespace = env.Ns };
                return id;
            }
            bool seenExcept = false;
            foreach (var child in children)
            {
                if (child.Node.LocalName == "param" && !seenExcept)
                {
                    var param = Read(child, ["name"]); string name = Required(param, "name", true);
                    if (Children(param, true).Count != 0) Fail("param cannot contain elements.", param.Scope.Location(param.Node));
                    if (library.Length == 0) Fail("Built-in datatypes do not accept parameters.", param.Scope.Location(param.Node), "RNG.Datatype");
                    XmlFacetKind kind = name switch
                    {
                        "length" => XmlFacetKind.Length, "minLength" => XmlFacetKind.MinLength, "maxLength" => XmlFacetKind.MaxLength,
                        "pattern" => XmlFacetKind.Pattern,
                        "maxInclusive" => XmlFacetKind.MaxInclusive, "maxExclusive" => XmlFacetKind.MaxExclusive,
                        "minInclusive" => XmlFacetKind.MinInclusive, "minExclusive" => XmlFacetKind.MinExclusive,
                        "totalDigits" => XmlFacetKind.TotalDigits, "fractionDigits" => XmlFacetKind.FractionDigits,
                        _ => InvalidParameter(param)
                    };
                    Component(param.Scope.Location(param.Node));
                    pattern.Parameters.Add(new(kind, param.Node.GetTextContent(), false, param.Scope.Context(param.Node), param.Scope.Location(param.Node)));
                }
                else if (child.Node.LocalName == "except" && !seenExcept)
                {
                    seenExcept = true; var excluded = Read(child, []);
                    pattern.Children.Add(Fold(Children(excluded), RngPatternKind.Choice, grammar, excluded));
                }
                else Fail("Invalid data pattern child order.", child.Scope.Location(child.Node));
            }
            return id;
        }
        private XmlFacetKind InvalidParameter(Env env)
        { Fail("Unsupported RELAX NG datatype parameter.", env.Scope.Location(env.Node), "RNG.Datatype"); return default; }

        private RngCompiledSchema Normalize(int start)
        {
            // Expand non-element references, rejecting only reachable unguarded
            // recursion; preserve element recursion as edges in the final graph.
            var expanded = new Dictionary<int, int>(); var visiting = new HashSet<int>();
            int Expand(int root)
            {
                var stack = new Stack<(int Id, int Edge)>(); stack.Push((root, -1));
                while (stack.Count != 0)
                {
                    var (id, edge) = stack.Pop(); var p = patterns[id]; Work(1, p.At);
                    if (p.Kind == RngPatternKind.Element || expanded.ContainsKey(id)) continue;
                    if (edge < 0) { if (!visiting.Add(id)) Fail("Reference recursion is not guarded by an element.", p.At); edge = 0; }
                    if (edge < p.Children.Count)
                    {
                        int child = p.Children[edge];
                        if (patterns[child].Kind != RngPatternKind.Element && !expanded.ContainsKey(child))
                        {
                            if (visiting.Contains(child)) Fail("Reference recursion is not guarded by an element.", p.At);
                            stack.Push((id, edge)); stack.Push((child, -1)); continue;
                        }
                        p.Children[edge] = expanded.TryGetValue(child, out int value) ? value : child;
                        stack.Push((id, edge + 1));
                    }
                    else { visiting.Remove(id); expanded.Add(id, p.Kind == RngPatternKind.Ref ? p.Children[0] : id); }
                }
                return expanded.TryGetValue(root, out int result) ? result : root;
            }
            start = Expand(start);
            var reachable = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count != 0)
            {
                int id = queue.Dequeue(); if (!reachable.Add(id)) continue; var p = patterns[id]; Work(1, p.At);
                if (p.Kind == RngPatternKind.Element) p.Children[0] = Expand(p.Children[0]);
                foreach (int child in p.Children) queue.Enqueue(child);
            }
            // Fixed point for empty/notAllowed reductions after reference expansion.
            var aliases = new Dictionary<int, int>();
            int Resolve(int id) { while (aliases.TryGetValue(id, out int next)) { Work(1, patterns[id].At); id = next; } return id; }
            bool changed;
            do
            {
                changed = false;
                foreach (int original in reachable)
                {
                    int id = Resolve(original); if (id != original) continue; var p = patterns[id]; Work(1, p.At);
                    for (int i = 0; i < p.Children.Count; i++) p.Children[i] = Resolve(p.Children[i]);
                    if (p.Kind is RngPatternKind.Element or RngPatternKind.Ref) continue;
                    bool leftNA = p.Children.Count > 0 && patterns[p.Children[0]].Kind == RngPatternKind.NotAllowed;
                    bool rightNA = p.Children.Count > 1 && patterns[p.Children[1]].Kind == RngPatternKind.NotAllowed;
                    bool leftEmpty = p.Children.Count > 0 && patterns[p.Children[0]].Kind == RngPatternKind.Empty;
                    bool rightEmpty = p.Children.Count > 1 && patterns[p.Children[1]].Kind == RngPatternKind.Empty;
                    int replacement = -1;
                    if (p.Kind == RngPatternKind.Choice)
                    {
                        if (leftNA) replacement = p.Children[1]; else if (rightNA) replacement = p.Children[0];
                        else if (leftEmpty && rightEmpty) replacement = p.Children[0];
                        else if (rightEmpty) { (p.Children[0], p.Children[1]) = (p.Children[1], p.Children[0]); }
                    }
                    else if (p.Kind is RngPatternKind.Group or RngPatternKind.Interleave or RngPatternKind.OneOrMore or RngPatternKind.List or RngPatternKind.Attribute)
                    {
                        if (leftNA || rightNA) replacement = leftNA ? p.Children[0] : p.Children[1];
                        else if (p.Kind is RngPatternKind.Group or RngPatternKind.Interleave)
                        { if (leftEmpty) replacement = p.Children[1]; else if (rightEmpty) replacement = p.Children[0]; }
                        else if (p.Kind == RngPatternKind.OneOrMore && leftEmpty) replacement = p.Children[0];
                    }
                    else if (p.Kind == RngPatternKind.Data && leftNA) { p.Children.Clear(); changed = true; }
                    if (replacement >= 0) { aliases.Add(id, replacement); changed = true; }
                }
            } while (changed);
            start = Resolve(start); var dense = new Dictionary<int, int>(); queue.Enqueue(start);
            var order = new List<int>();
            while (queue.Count != 0)
            {
                int id = Resolve(queue.Dequeue()); if (dense.ContainsKey(id)) continue;
                Work(1, patterns[id].At); dense.Add(id, order.Count); order.Add(id);
                foreach (int child in patterns[id].Children) queue.Enqueue(Resolve(child));
            }
            var output = new List<RngPattern>(order.Count);
            var elementRefs = new Dictionary<int, int>();
            foreach (int id in order) if (patterns[id].Kind == RngPatternKind.Element) elementRefs.Add(id, order.Count + elementRefs.Count);
            int Edge(int id) { id = Resolve(id); return elementRefs.TryGetValue(id, out int reference) ? reference : dense[id]; }
            foreach (int id in order)
            {
                var p = patterns[id]; var edges = new List<int>(p.Children.Count);
                foreach (int child in p.Children) edges.Add(Edge(child));
                output.Add(new(dense[id], p.Kind, edges, p.Name, p.Datatype, p.Parameters, p.Value, p.Context, p.At));
            }
            foreach (var (id, reference) in elementRefs)
            {
                var p = patterns[id]; Component(p.At);
                output.Add(new(reference, RngPatternKind.Ref, [dense[id]], null, null, [], null, p.Context, p.At));
            }
            // Datatype correctness precedes reachability/notAllowed simplification
            // (4.16). Detached checkpoints preserve every removed data/value
            // lexical context for the verified facade's finalization pass.
            for (int id = 0; id < patterns.Count; id++)
            {
                var p = patterns[id]; Work(1, p.At);
                if (p.Kind is RngPatternKind.Data or RngPatternKind.Value && !dense.ContainsKey(id))
                    output.Add(new(output.Count, p.Kind, [], null, p.Datatype, p.Parameters, p.Value, p.Context, p.At));
            }
            return new(Edge(start), output, new DefinitionsOnly());
        }
        private sealed class DefinitionsOnly : IXmlDatatypeRegistry
        {
            public IXmlDatatypeLibrary? Find(string namespaceUri) => null;
            public XmlDatatypeDefinition? FindType(SchemaName name) => null;
        }
    }
}
