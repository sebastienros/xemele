using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xemele.XPath;
using Xemele.XPath.Internal;

namespace Xemele.Validation;

// Stateless entry point; every source handle, resolver, budget and scratch table
// belongs to Run and is released after publication of the detached model.
internal sealed class SchematronCompiler : ISchematronCompiler
{
    public SchematronCompiledModel Compile(XmlDocument schema, SchematronCompileOptions options, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(operation);
        return new Run(SchemaSnapshots.Compile(options), operation).Compile(schema);
    }

    private sealed class Node(string name, XmlLocation location, Uri? baseUri)
    {
        internal readonly string Name = name;
        internal readonly XmlLocation Location = location;
        internal readonly Uri? BaseUri = baseUri;
        internal readonly Dictionary<string, string> Attributes = new(StringComparer.Ordinal);
        internal readonly List<Node> Children = [];
        internal string? Text;
        internal IReadOnlyDictionary<string, string>? Parameters;
        internal bool ForeignContent;
        internal string? Get(string name) => Attributes.GetValueOrDefault(name);
    }

    private sealed class Run
    {
        private const string Namespace = "http://purl.oclc.org/dsdl/schematron";
        private readonly SchematronCompileOptions options;
        private readonly ISchemaOperation operation;
        private readonly SchemaResourceLoader resources;
        private readonly List<SchemaDocumentScope> sources = [];
        private readonly HashSet<(Uri?, string)> inclusion = [];
        private readonly Dictionary<string, Node> ids = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> namespaces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Node> abstractPatterns = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Node> abstractRules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Node> diagnostics = new(StringComparer.Ordinal), properties = new(StringComparer.Ordinal);
        private XPathCompileOptions xpath = new();
        private int ruleKey;
        internal Run(SchematronCompileOptions options, ISchemaOperation operation)
        {
            this.options = options;
            this.operation = operation;
            resources = new(options.Schema, operation);
        }

        internal SchematronCompiledModel Compile(XmlDocument document)
        {
            var scope = Open(document);
            inclusion.Add((scope.SourceUri, ""));
            if (document.RootElement.IsDefault || document.RootElement.NamespaceUri != Namespace)
            {
                throw Error("SCH.SchemaRestriction", "A Schematron schema root is required.", document.RootElement.IsDefault ? default : scope.Location(document.RootElement));
            }

            var root = Snapshot(document.RootElement, scope, 0);
            if (root.Name != "schema")
            {
                Fail(root, "SCH.SchemaRestriction", "A Schematron schema root is required.");
            }

            Validate(root, 0);
            Index(root);
            if (root.Get("queryBinding") is string binding && binding != "xslt")
            {
                Fail(root, "SCH.QueryBinding", "Only the xslt XPath 1.0 binding is supported.");
            }

            foreach (var ns in Children(root, "ns"))
            {
                string prefix = Required(ns, "prefix"), uri = Required(ns, "uri");
                Name(prefix, ns, false);
                if (prefix == "xmlns" || uri.Length == 0 || prefix == "xml" && uri != TreeValidation.XmlUri || !namespaces.TryAdd(prefix, uri))
                {
                    Fail(ns, "SCH.SchemaRestriction", "Invalid or duplicate namespace binding.");
                }
            }
            xpath = new()
            {
                Namespaces = SchemaSnapshots.Map(namespaces),
                Limits = options.XPathLimits
            };
            foreach (var child in Children(root, "pattern"))
            {
                if (IsAbstract(child))
                {
                    abstractPatterns.Add(Required(child, "id"), child);
                }

                foreach (var rule in Children(child, "rule"))
                {
                    if (IsAbstract(rule))
                    {
                        abstractRules.Add(Required(rule, "id"), rule);
                    }
                }
            }
            foreach (var container in Children(root, "diagnostics"))
            {
                foreach (var node in Children(container, "diagnostic"))
                {
                    diagnostics.Add(Required(node, "id"), node);
                }
            }

            foreach (var container in Children(root, "properties"))
            {
                foreach (var node in Children(container, "property"))
                {
                    properties.Add(Required(node, "id"), node);
                }
            }

            CheckAuthoredReferences(root);
            foreach (var (id, rule) in abstractRules)
            {
                Extend(rule, [id], 0);
            }

            var schemaLets = Lets(root.Children, [], out var schemaNames);
            var phaseNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            var phaseLets = new Dictionary<string, IReadOnlyList<SchematronLet>>(StringComparer.Ordinal);
            var phaseNames = new Dictionary<string, HashSet<XPathExpandedName>>(StringComparer.Ordinal);
            foreach (var phase in Children(root, "phase"))
            {
                var id = Required(phase, "id");
                phaseNodes.Add(id, phase);
                phaseLets.Add(id, Lets(phase.Children, schemaNames, out var names));
                phaseNames.Add(id, names);
            }
            var patterns = new List<SchematronCompiledPattern>();
            var patternNames = new Dictionary<string, int>(StringComparer.Ordinal);
            var expanded = new List<Node>();
            foreach (var pattern in Children(root, "pattern"))
            {
                if (IsAbstract(pattern))
                {
                    continue;
                }

                var concrete = ExpandPattern(pattern);
                if (pattern.Get("id") is string id)
                {
                    patternNames.Add(id, patterns.Count);
                }

                expanded.Add(concrete);
                var available = schemaNames;
                if (pattern.Get("id") is string authoredId)
                {
                    foreach (var (phaseId, phaseNode) in phaseNodes)
                    {
                        if (Children(phaseNode, "active").Any(active => active.Get("pattern") == authoredId))
                        {
                            available = phaseNames[phaseId];
                            break;
                        }
                    }
                }

                patterns.Add(Pattern(concrete, patterns.Count, available));
            }
            var phases = new Dictionary<string, SchematronCompiledPhase>(StringComparer.Ordinal);
            foreach (var (id, phase) in phaseNodes)
            {
                var active = new List<int>();
                var distinct = new HashSet<int>();
                foreach (var node in Children(phase, "active"))
                {
                    Work(1, node);
                    var name = Required(node, "pattern");
                    if (!patternNames.TryGetValue(name, out var key) || !distinct.Add(key))
                    {
                        Fail(node, "SCH.Phase", "Unknown, abstract or duplicate active pattern.");
                    }
                    // Named phases must supply every referenced phase variable.
                    CheckPatternVariables(expanded[key], phaseNames[id]);
                    active.Add(key);
                }
                Components(1, phase);
                phases.Add(id, new(id, phaseLets[id], active));
            }
            var defaultPhase = root.Get("defaultPhase") ?? "#ALL";
            if (defaultPhase == "#DEFAULT" || defaultPhase != "#ALL" && !phases.ContainsKey(defaultPhase))
            {
                Fail(root, "SCH.Phase", "Unknown default phase.");
            }

            var compiledDiagnostics = TextMap(diagnostics, schemaNames);
            var compiledProperties = TextMap(properties, schemaNames);
            foreach (var source in sources)
            {
                source.CheckUnchanged();
            }

            Work(0, root);
            Components(1, root);
            return new(Children(root, "title").FirstOrDefault() is Node title ? PlainText(title) : null, defaultPhase,
                namespaces, schemaLets, patterns, phases, compiledDiagnostics, compiledProperties);
        }

        private SchemaDocumentScope Open(XmlDocument document)
        {
            var scope = new SchemaDocumentScope(document, operation, resources.SourceUri(document));
            sources.Add(scope);
            return scope;
        }

        private Node Snapshot(XmlNode element, SchemaDocumentScope scope, int depth)
        {
            if (depth >= 256)
            {
                Limit(scope.Location(element), "Schema representation depth exceeded.");
            }

            var location = scope.Location(element);
            Work(1, location);
            Components(1, location);
            var context = scope.Context(element);
            var node = new Node(element.LocalName, location, context.BaseUri);
            foreach (var attribute in element.Attributes)
            {
                Work(1, location);
                Scan(attribute.Value, location);
                if (attribute.NamespaceUri.Length == 0)
                {
                    Components(1, location);
                    node.Attributes.Add(attribute.LocalName, attribute.Value);
                }
                else if (attribute.NamespaceUri == TreeValidation.XmlUri && attribute.LocalName is not ("base" or "lang" or "space" or "id"))
                {
                    Fail(node, "SCH.SchemaRestriction", "Unsupported XML attribute.");
                }
            }
            foreach (var child in element.Children)
            {
                Work(1, location);
                if (child.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                {
                    Scan(child.Value, location);
                    Components(1, location);
                    node.Children.Add(new("#text", scope.Location(child), context.BaseUri)
                    {
                        Text = child.Value
                    });
                }
                else if (child.Kind == XmlNodeKind.Element)
                {
                    if (child.NamespaceUri != Namespace)
                    {
                        node.ForeignContent = true;
                        CheckForeign(child, scope, depth + 1);
                        continue;
                    }
                    if (child.LocalName == "include" || child.LocalName == "extends" && child.GetAttribute("href") is not null)
                    {
                        Include(node, child, scope, depth + 1, child.LocalName == "extends");
                    }
                    else
                    {
                        node.Children.Add(Snapshot(child, scope, depth + 1));
                    }
                }
            }
            scope.CheckUnchanged();
            return node;
        }

        private void CheckForeign(XmlNode element, SchemaDocumentScope scope, int depth)
        {
            if (depth >= 256)
            {
                Limit(scope.Location(element), "Schema annotation depth exceeded.");
            }

            Work(1, scope.Location(element));
            if (element.NamespaceUri == "http://www.w3.org/1999/XSL/Transform")
            {
                throw Error("SCH.QueryBinding", "Foreign executable syntax is unsupported.", scope.Location(element));
            }

            foreach (var child in element.Children)
            {
                Work(1, scope.Location(child));
                if (child.Kind == XmlNodeKind.Element)
                {
                    CheckForeign(child, scope, depth + 1);
                }
            }
        }

        private void Include(Node parent, XmlNode include, SchemaDocumentScope source, int depth, bool extend)
        {
            var location = source.Location(include);
            if (extend ? parent.Name != "rule" : parent.Name is not ("schema" or "pattern" or "phase" or "rule" or "diagnostics"))
            {
                throw Error("SCH.SchemaRestriction", "Inclusion is not allowed in this position.", location);
            }

            foreach (var a in include.Attributes)
            {
                Work(1, source.Location(a));
                if (a.NamespaceUri.Length == 0 && a.LocalName != "href")
                {
                    throw Error("SCH.SchemaRestriction", "Invalid inclusion attribute.", location);
                }
            }
            foreach (var c in include.Children)
            {
                var childLocation = source.Location(c);
                Work(1, childLocation);
                if (c.Kind == XmlNodeKind.Element && c.NamespaceUri == Namespace || c.Kind is XmlNodeKind.Text or XmlNodeKind.CData && !White(c.Value, childLocation))
                {
                    throw Error("SCH.SchemaRestriction", "Inclusions must be empty.", childLocation);
                }

                if (c.Kind == XmlNodeKind.Element)
                {
                    CheckForeign(c, source, depth + 1);
                }
            }
            var href = include.GetAttribute("href") ?? throw Error("SCH.SchemaRestriction", "Missing inclusion href.", location);
            Scan(href, location);
            var separator = href.IndexOf('#');
            var fragment = separator < 0 ? "" : Uri.UnescapeDataString(href[(separator + 1)..]);
            var path = separator < 0 ? href : href[..separator];
            SchemaDocumentScope scope;
            if (path.Length == 0 && fragment.Length != 0)
            {
                scope = source;
            }
            else
            {
                var input = resources.Load(new(href, source.SourceUri, SchemaResourcePurpose.SchematronInclude, location), source.Context(include).BaseUri, operation);
                scope = Open(input);
            }
            var key = (scope.SourceUri, fragment);
            if (!inclusion.Add(key))
            {
                throw Error("SCHEMA.ResourceCycle", "Cyclic Schematron inclusion.", location with
                {
                    SourceUri = scope.SourceUri
                });
            }

            try
            {
                using var composition = resources.EnterComposition(location);
                var selected = scope.Document.RootElement;
                if (fragment.Length != 0)
                {
                    selected = default;
                    var stack = new Stack<XmlNode>();
                    stack.Push(scope.Document.RootElement);
                    while (stack.Count != 0)
                    {
                        var candidate = stack.Pop();
                        Work(1, location);
                        if (candidate.GetAttribute("id") == fragment || candidate.GetAttribute("id", TreeValidation.XmlUri) == fragment)
                        {
                            if (!selected.IsDefault)
                            {
                                throw Error("SCH.SchemaRestriction", "Ambiguous inclusion fragment.", location);
                            }

                            selected = candidate;
                        }
                        foreach (var c in candidate.Children)
                        {
                            Work(1, scope.Location(c));
                            if (c.Kind == XmlNodeKind.Element)
                            {
                                stack.Push(c);
                            }
                        }
                    }
                    if (selected.IsDefault)
                    {
                        throw Error("SCH.SchemaRestriction", "Missing inclusion fragment.", location);
                    }
                }
                if (selected.NamespaceUri != Namespace)
                {
                    throw Error("SCH.SchemaRestriction", "Inclusion must select Schematron syntax.", location);
                }

                var imported = Snapshot(selected, scope, depth);
                if (extend)
                {
                    if (imported.Name != "rule")
                    {
                        throw Error("SCH.SchemaRestriction", "An external extends must select a rule.", location);
                    }

                    parent.Children.AddRange(imported.Children);
                }
                else
                {
                    parent.Children.Add(imported);
                }

                source.CheckUnchanged();
            }
            finally { inclusion.Remove(key); }
        }

        private void Validate(Node node, int depth)
        {
            Work(1, node);
            var attributes = node.Name switch
            {
                "schema" => " id schemaVersion defaultPhase queryBinding ",
                "active" => " pattern ",
                "assert" or "report" => " test flag id diagnostics properties role subject ",
                "diagnostic" => " id role ",
                "property" => " id role scheme ",
                "dir" => " value ",
                "extends" => " rule ",
                "let" => " name value ",
                "name" => " path ",
                "ns" => " uri prefix ",
                "p" => " id class icon ",
                "param" => " name value ",
                "pattern" => " documents abstract id is-a ",
                "phase" => " id ",
                "rule" => " flag role subject abstract id context ",
                "span" => " class ",
                "diagnostics" or "properties" or "emph" or "title" or "value-of" => node.Name == "value-of" ? " select " : " ",
                _ => throw Error("SCH.SchemaRestriction", "Unknown Schematron element.", node.Location)
            };
            foreach (var name in node.Attributes.Keys)
            {
                if (!Word(attributes.AsSpan(), name.AsSpan()) && !Rich(node.Name, name))
                {
                    Fail(node, "SCH.SchemaRestriction", "Unknown Schematron attribute.");
                }
            }

            if (node.Get("abstract") is string abstractValue && abstractValue is not ("true" or "false"))
            {
                Fail(node, "SCH.SchemaRestriction", "Invalid abstract flag.");
            }

            if (node.Get("documents") is not null)
            {
                Fail(node, "SCH.QueryBinding", "Multi-document binding is unsupported.");
            }

            if (node.Get("id") is string id)
            {
                Name(id, node, false);
            }

            switch (node.Name)
            {
                case "assert":
                case "report":
                    Required(node, "test");
                    break;
                case "phase":
                    Required(node, "id");
                    break;
                case "active":
                    Required(node, "pattern");
                    break;
                case "diagnostic":
                case "property":
                    Required(node, "id");
                    break;
                case "let":
                    Required(node, "name");
                    if (node.ForeignContent)
                    {
                        Fail(node, "SCH.QueryBinding", "Content-valued lets require an unsupported result-tree binding.");
                    }

                    Required(node, "value");
                    break;
                case "param":
                    Required(node, "name");
                    Required(node, "value");
                    break;
                case "ns":
                    Required(node, "prefix");
                    Required(node, "uri");
                    break;
                case "extends":
                    Required(node, "rule");
                    break;
                case "value-of":
                    Required(node, "select");
                    break;
                case "span":
                    Required(node, "class");
                    break;
                case "dir":
                    if (node.Get("value") is string direction && direction is not ("ltr" or "rtl"))
                    {
                        Fail(node, "SCH.SchemaRestriction", "Invalid text direction.");
                    }

                    break;
                case "rule":
                    if (IsAbstract(node))
                    {
                        Required(node, "id");
                        if (node.Get("context") is not null)
                        {
                            Fail(node, "SCH.SchemaRestriction", "Abstract rules cannot have context.");
                        }
                    }
                    else
                    {
                        Required(node, "context");
                    }

                    break;
                case "pattern":
                    if (IsAbstract(node))
                    {
                        Required(node, "id");
                        if (node.Get("is-a") is not null)
                        {
                            Fail(node, "SCH.SchemaRestriction", "Abstract pattern cannot instantiate a pattern.");
                        }
                    }
                    break;
            }
            var stage = -1;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var child in node.Children)
            {
                Work(1, child);
                if (child.Name == "#text")
                {
                    if (!Message(node.Name) && !White(child.Text!, child.Location))
                    {
                        Fail(child, "SCH.SchemaRestriction", "Unexpected schema text.");
                    }

                    continue;
                }
                var next = node.Name == "schema" && child.Name == "p" && stage >= 5 ? 6 : Stage(node, child.Name);
                if (next < 0 || next < stage)
                {
                    Fail(child, "SCH.SchemaRestriction", "Invalid child or declaration order.");
                }

                stage = next;
                counts[child.Name] = counts.GetValueOrDefault(child.Name) + 1;
                Validate(child, depth + 1);
            }
            foreach (var singleton in new[] { "title", "diagnostics", "properties" })
            {
                if (counts.GetValueOrDefault(singleton) > 1)
                {
                    Fail(node, "SCH.SchemaRestriction", "Duplicate schema section.");
                }
            }

            if (node.Name == "schema" && counts.GetValueOrDefault("pattern") == 0)
            {
                Fail(node, "SCH.SchemaRestriction", "Schema requires patterns.");
            }

            if (node.Name == "rule" && counts.GetValueOrDefault("assert") + counts.GetValueOrDefault("report") + counts.GetValueOrDefault("extends") + counts.GetValueOrDefault("p") == 0)
            {
                Fail(node, "SCH.SchemaRestriction", "Rule requires a check or extension.");
            }
        }
        private static bool Rich(string element, string attribute) => element is "schema" or "assert" or "report" or "diagnostic" or "pattern" or "phase" or "rule"
            && attribute is "icon" or "see" or "fpi";
        private static bool Message(string name) => name is "assert" or "report" or "diagnostic" or "property" or "p" or "title" or "active" or "span" or "dir" or "emph";
        private static int Stage(Node node, string child) => node.Name switch
        {
            "schema" => child switch { "title" => 0, "ns" => 1, "p" => 2, "let" => 3, "phase" => 4, "pattern" => 5, "diagnostics" => 7, "properties" => 8, _ => -1 },
            "pattern" => child switch { "title" => 0, "p" => 1, "let" when node.Get("is-a") is null => 2, "rule" when node.Get("is-a") is null => 3, "param" when node.Get("is-a") is not null => 2, _ => -1 },
            "phase" => child switch { "p" => 0, "let" => 1, "active" => 2, _ => -1 },
            "rule" => child switch { "let" => 0, "assert" or "report" or "extends" or "p" => 1, _ => -1 },
            "diagnostics" => child == "diagnostic" ? 0 : -1,
            "properties" => child == "property" ? 0 : -1,
            "assert" or "report" or "property" => child is "name" or "value-of" or "emph" or "dir" or "span" ? 0 : -1,
            "diagnostic" => child is "value-of" or "emph" or "dir" or "span" ? 0 : -1,
            "p" or "active" => child is "emph" or "dir" or "span" ? 0 : -1,
            "title" => child == "dir" ? 0 : -1,
            _ => -1
        };

        private void CheckAuthoredReferences(Node node, Node? pattern = null)
        {
            Work(1, node);
            if (node.Name == "pattern")
            {
                pattern = node;
            }

            if (node.Name is "assert" or "report")
            {
                References(node, "diagnostics", diagnostics);
                References(node, "properties", properties);
            }
            if (node.Name == "extends")
            {
                // ISO 2016 5.4.12: ID uniqueness is schema-wide, but the referenced
                // abstract rule must belong to this effective containing pattern.
                // Check authored nodes before template copying so each valid
                // instantiation preserves its template's local rule references.
                var id = Required(node, "rule");
                var local = false;
                if (pattern is not null && abstractRules.TryGetValue(id, out var target))
                {
                    for (var i = 0; i < pattern.Children.Count; i++)
                    {
                        Work(1, node);
                        if (ReferenceEquals(pattern.Children[i], target))
                        {
                            local = true;
                            break;
                        }
                    }
                }

                if (!local)
                {
                    Fail(node, "SCH.SchemaRestriction", "An extends must reference an abstract rule in the same pattern.");
                }
            }
            foreach (var child in node.Children)
            {
                CheckAuthoredReferences(child, pattern);
            }
        }
        private void Index(Node node)
        {
            Work(1, node);
            if (node.Get("id") is string id)
            {
                if (id.StartsWith('#') || !ids.TryAdd(id, node))
                {
                    Fail(node, node.Name == "phase" ? "SCH.Phase" : "SCH.SchemaRestriction", "Duplicate or reserved schema ID.");
                }
            }
            foreach (var child in node.Children)
            {
                Index(child);
            }
        }
        private static bool IsAbstract(Node node) => node.Get("abstract") == "true";
        private IEnumerable<Node> Children(Node node, string name)
        {
            foreach (var child in node.Children)
            {
                Work(1, child);
                if (child.Name == name)
                {
                    yield return child;
                }
            }
        }

        private Node ExpandPattern(Node node)
        {
            if (node.Get("is-a") is not string target)
            {
                return node;
            }

            Work(1, node);
            if (!abstractPatterns.TryGetValue(target, out var template))
            {
                Fail(node, "SCH.SchemaRestriction", "Unknown abstract pattern.");
            }

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in Children(node, "param"))
            {
                var name = Required(parameter, "name");
                Name(name, parameter, false);
                if (!parameters.TryAdd(name, Required(parameter, "value")))
                {
                    Fail(parameter, "SCH.SchemaRestriction", "Duplicate abstract parameter.");
                }
            }
            var result = Copy(template!, parameters, 0);
            result.Attributes.Remove("abstract");
            result.Attributes.Remove("id");
            foreach (var (key, value) in node.Attributes)
            {
                if (key != "is-a")
                {
                    result.Attributes[key] = value;
                }
            }

            var title = Children(node, "title").FirstOrDefault();
            if (title is not null)
            {
                result.Children.RemoveAll(c => c.Name == "title");
                result.Children.Insert(0, title);
            }
            return result;
        }
        private Node Copy(Node source, IReadOnlyDictionary<string, string> parameters, int depth)
        {
            if (depth >= 256)
            {
                Limit(source.Location, "Abstract expansion depth exceeded.");
            }

            Work(1, source);
            Components(1, source);
            var result = new Node(source.Name, source.Location, source.BaseUri) { Text = source.Text, Parameters = parameters };
            foreach (var (name, value) in source.Attributes)
            {
                Work(1, source);
                Components(1, source);
                result.Attributes.Add(name, name is "context" or "test" or "select" or "path" or "value" or "subject" ? Substitute(value, parameters, source) : value);
            }
            foreach (var child in source.Children)
            {
                result.Children.Add(Copy(child, parameters, depth + 1));
            }

            return result;
        }
        private string Substitute(string text, IReadOnlyDictionary<string, string> parameters, Node source)
        {
            // Lexer tokens borrow text. A $parameter inside a lexical literal never
            // participates, and a longer variable name never matches a prefix.
            Scan(text, source.Location);
            var lexer = new XPathLexer(text, operation.CancellationToken, options.XPathLimits.MaxTokens, 0);
            StringBuilder? output = null;
            var copied = 0;
            try
            {
                while (true)
                {
                    var token = lexer.Next();
                    Work(1, source);
                    if (token.Kind == XPathTokenKind.End)
                    {
                        break;
                    }

                    if (token.Kind != XPathTokenKind.Dollar)
                    {
                        continue;
                    }

                    var name = lexer.Next();
                    Work(1, source);
                    if (name.Kind != XPathTokenKind.Name)
                    {
                        Fail(source, "SCH.XPath", "Invalid abstract parameter reference.");
                    }
                    // Dictionary string keys persist; lookup uses borrowed ordinal comparisons.
                    foreach (var parameter in parameters)
                    {
                        Work(1, source);
                        if (!lexer.Text(name).SequenceEqual(parameter.Key))
                        {
                            continue;
                        }

                        output ??= new();
                        Append(output, text.AsSpan(copied, token.Start - copied), source);
                        Scan(parameter.Value, source.Location);
                        Append(output, parameter.Value, source);
                        copied = name.Start + name.Length;
                        break;
                    }
                }
            }
            catch (XPathCompileException error) { if (error.Diagnostic.Category == XPathDiagnosticCategory.CompileLimit) { Limit(source.Location, "XPath compilation limit exceeded."); } Fail(source, "SCH.XPath", "Invalid abstract XPath expression."); }
            if (output is null)
            {
                return text;
            }

            Append(output, text.AsSpan(copied), source);
            return output.ToString();
        }

        private void Append(StringBuilder builder, ReadOnlySpan<char> text, Node source)
        {
            var maximum = options.XPathLimits.MaxExpressionCharacters ?? int.MaxValue;
            if (text.Length > maximum - builder.Length)
            {
                Limit(source.Location, "Expanded XPath expression limit exceeded.");
            }

            builder.Append(text);
        }

        private List<Node> Extend(Node rule, HashSet<string> visiting, int depth)
        {
            if (depth >= 256)
            {
                Limit(rule.Location, "Rule extension depth exceeded.");
            }

            var result = new List<Node>();
            foreach (var child in rule.Children)
            {
                Work(1, child);
                if (child.Name != "extends")
                {
                    result.Add(child);
                    continue;
                }
                var id = Required(child, "rule");
                if (!abstractRules.TryGetValue(id, out var target))
                {
                    Fail(child, "SCH.SchemaRestriction", "An extends must reference an abstract rule.");
                }

                if (!visiting.Add(id))
                {
                    Fail(child, "SCH.SchemaRestriction", "Cyclic abstract rule extension.");
                }

                result.AddRange(Extend(rule.Parameters is null ? target! : Copy(target!, rule.Parameters, 0), visiting, depth + 1));
                visiting.Remove(id);
            }
            return result;
        }

        private SchematronCompiledPattern Pattern(Node pattern, int key, HashSet<XPathExpandedName> outer)
        {
            var lets = Lets(pattern.Children, outer, out var names);
            var rules = new List<SchematronCompiledRule>();
            foreach (var rule in Children(pattern, "rule"))
            {
                if (IsAbstract(rule))
                {
                    continue;
                }

                var body = Extend(rule, [], 0);
                var locals = Lets(body, names, out var scope);
                var context = Required(rule, "context");
                var selector = Expression(MatchSelector(context, rule), scope, rule);
                var subjectSource = rule.Get("subject");
                var subject = subjectSource is null ? null : Expression(subjectSource, scope, rule);
                var checks = new List<SchematronCompiledCheck>();
                foreach (var check in body)
                {
                    if (check.Name is not ("assert" or "report"))
                    {
                        continue;
                    }

                    var testSource = Required(check, "test");
                    var checkSubject = check.Get("subject");
                    var refs = References(check, "diagnostics", diagnostics);
                    var props = References(check, "properties", properties);
                    Components(1, check);
                    checks.Add(new(check.Name == "assert" ? SchematronFindingKind.FailedAssert : SchematronFindingKind.SuccessfulReport,
                        check.Get("id"), check.Get("role"), check.Get("flag"), testSource, Expression(testSource, scope, check),
                        checkSubject, checkSubject is null ? null : Expression(checkSubject, scope, check), Text(check, scope), refs, props, check.Location));
                    // Dynamic references execute in this check's scope, not schema scope.
                    foreach (var id in refs)
                    {
                        Text(diagnostics[id], scope);
                    }

                    foreach (var id in props)
                    {
                        Text(properties[id], scope);
                    }
                }
                Components(1, rule);
                if (ruleKey == int.MaxValue)
                {
                    Limit(rule.Location, "Rule key capacity exceeded.");
                }

                rules.Add(new(ruleKey++, context, selector, subjectSource, subject, locals, checks, rule.Location));
            }
            Components(1, pattern);
            return new(key, pattern.Get("id"), Children(pattern, "title").FirstOrDefault() is Node title ? PlainText(title) : null, lets, rules);
        }
        private void CheckPatternVariables(Node pattern, HashSet<XPathExpandedName> outer)
        {
            Lets(pattern.Children, outer, out var names);
            foreach (var rule in Children(pattern, "rule"))
            {
                if (IsAbstract(rule))
                {
                    continue;
                }

                var body = Extend(rule, [], 0);
                Lets(body, names, out var scope);
                Check(rule, scope);
                foreach (var check in body)
                {
                    if (check.Name is not ("assert" or "report"))
                    {
                        continue;
                    }

                    Check(check, scope);
                    foreach (var id in References(check, "diagnostics", diagnostics))
                    {
                        Check(diagnostics[id], scope);
                    }

                    foreach (var id in References(check, "properties", properties))
                    {
                        Check(properties[id], scope);
                    }
                }
            }
            void Check(Node node, HashSet<XPathExpandedName> names)
            {
                foreach (var (attribute, value) in node.Attributes)
                {
                    if (attribute is not ("context" or "test" or "select" or "path" or "subject"))
                    {
                        continue;
                    }

                    foreach (var variable in Variables(value, node))
                    {
                        if (!names.Contains(variable))
                        {
                            Fail(node, "SCH.XPath", "Phase does not supply a referenced variable.");
                        }
                    }
                }
                foreach (var child in node.Children)
                {
                    Check(child, names);
                }
            }
        }

        private IReadOnlyList<SchematronLet> Lets(IEnumerable<Node> children, HashSet<XPathExpandedName> outer, out HashSet<XPathExpandedName> scope)
        {
            scope = [.. outer];
            var pending = new Dictionary<XPathExpandedName, Node>();
            foreach (var node in children)
            {
                Work(1, node);
                if (node.Name != "let")
                {
                    continue;
                }

                var name = Name(Required(node, "name"), node, true);
                if (!pending.TryAdd(name, node))
                {
                    Fail(node, "SCH.SchemaRestriction", "Duplicate let name in one scope.");
                }
            }
            var localNames = new HashSet<XPathExpandedName>(pending.Keys);
            var result = new List<SchematronLet>();
            var done = new HashSet<XPathExpandedName>();
            // Iterative dependency ordering avoids recursion on long variable chains.
            while (pending.Count != 0)
            {
                var progress = false;
                foreach (var (name, node) in pending.ToArray())
                {
                    Work(1, node);
                    var refs = Variables(Required(node, "value"), node);
                    var ready = true;
                    foreach (var reference in refs)
                    {
                        Work(1, node);
                        if (reference == name && outer.Contains(reference))
                        {
                            continue;
                        }

                        if (localNames.Contains(reference))
                        {
                            if (!done.Contains(reference))
                            {
                                ready = false;
                            }
                        }
                        else if (!outer.Contains(reference))
                        {
                            Fail(node, "SCH.XPath", "Unresolved let variable.");
                        }
                    }
                    if (!ready)
                    {
                        continue;
                    }

                    Components(1, node);
                    result.Add(new(name, Expression(Required(node, "value"), scope, node)));
                    pending.Remove(name);
                    done.Add(name);
                    scope.Add(name);
                    progress = true;
                }
                if (!progress)
                {
                    Fail(pending.First().Value, "SCH.XPath", "Circular let dependency.");
                }
            }
            return result.AsReadOnly();
        }

        private HashSet<XPathExpandedName> Variables(string text, Node node)
        {
            Scan(text, node.Location);
            var result = new HashSet<XPathExpandedName>();
            var lexer = new XPathLexer(text, operation.CancellationToken, options.XPathLimits.MaxTokens, 0);
            try
            {
                while (true)
                {
                    var token = lexer.Next();
                    Work(1, node);
                    if (token.Kind == XPathTokenKind.End)
                    {
                        break;
                    }

                    if (token.Kind == XPathTokenKind.Dollar)
                    {
                        token = lexer.Next();
                        Work(1, node);
                        if (token.Kind != XPathTokenKind.Name)
                        {
                            Fail(node, "SCH.XPath", "Invalid variable reference.");
                        }

                        result.Add(Name(text.Substring(token.Start, token.Length), node, true));
                    }
                }
            }
            catch (XPathCompileException error) { if (error.Diagnostic.Category == XPathDiagnosticCategory.CompileLimit) { Limit(node.Location, "XPath compilation limit exceeded."); } Fail(node, "SCH.XPath", "Invalid XPath expression."); }
            return result;
        }
        private XPathExpression Expression(string text, HashSet<XPathExpandedName> scope, Node node)
        {
            foreach (var name in Variables(text, node))
            {
                Work(1, node);
                if (!scope.Contains(name))
                {
                    Fail(node, "SCH.XPath", "Unresolved XPath variable.");
                }
            }
            Work(1, node);
            Components(1, node);
            try
            {
                return XPathExpression.Compile(text, xpath, operation.CancellationToken);
            }
            catch (XPathCompileException error)
            {
                if (error.Diagnostic.Category == XPathDiagnosticCategory.CompileLimit)
                {
                    Limit(node.Location, "XPath compilation limit exceeded.");
                }

                Fail(node, "SCH.XPath", "Invalid XPath 1.0 expression.");
                throw;
            }
        }

        private string MatchSelector(string text, Node node)
        {
            // Validate the XSLT 1.0 match grammar using the M4 lexer. Predicates are
            // arbitrary XPath; only the outer steps restrict axes/functions.
            Scan(text, node.Location);
            var tokens = new List<XPathToken>();
            var lexer = new XPathLexer(text, operation.CancellationToken, options.XPathLimits.MaxTokens, 0);
            try
            {
                while (true)
                {
                    var t = lexer.Next();
                    Work(1, node);
                    if (t.Kind == XPathTokenKind.End)
                    {
                        break;
                    }

                    tokens.Add(t);
                }
            }
            catch (XPathCompileException error) { if (error.Diagnostic.Category == XPathDiagnosticCategory.CompileLimit) { Limit(node.Location, "XPath compilation limit exceeded."); } Fail(node, "SCH.XPath", "Invalid rule match pattern."); }
            var result = new StringBuilder();
            int start = 0, brackets = 0;
            for (var i = 0; i <= tokens.Count; i++)
            {
                Work(1, node);
                if (i < tokens.Count && tokens[i].Kind == XPathTokenKind.Bracket)
                {
                    brackets++;
                }

                if (i < tokens.Count && tokens[i].Kind == XPathTokenKind.EndBracket)
                {
                    brackets--;
                }

                if (brackets < 0)
                {
                    Fail(node, "SCH.XPath", "Invalid rule predicate.");
                }

                if (i != tokens.Count && (tokens[i].Kind != XPathTokenKind.Union || brackets != 0))
                {
                    continue;
                }

                if (start == i)
                {
                    Fail(node, "SCH.XPath", "Empty match-pattern branch.");
                }

                ValidateMatch(tokens, start, i, text, node);
                if (result.Length != 0)
                {
                    result.Append(" | ");
                }

                var first = tokens[start];
                var anchored = first.Kind is XPathTokenKind.Slash or XPathTokenKind.DoubleSlash || first.Kind == XPathTokenKind.Name && start + 1 < i && tokens[start + 1].Kind == XPathTokenKind.Open && lexer.Text(first).SequenceEqual("id");
                if (!anchored)
                {
                    result.Append("//");
                }

                var end = tokens[i - 1].Start + tokens[i - 1].Length;
                result.Append(text.AsSpan(first.Start, end - first.Start));
                start = i + 1;
            }
            if (brackets != 0)
            {
                Fail(node, "SCH.XPath", "Unclosed match predicate.");
            }

            return result.ToString();
        }
        private void ValidateMatch(List<XPathToken> tokens, int start, int end, string source, Node node)
        {
            var i = start;
            bool Is(XPathTokenKind kind) => i < end && tokens[i].Kind == kind;
            ReadOnlySpan<char> Token() => source.AsSpan(tokens[i].Start, tokens[i].Length);
            if (Is(XPathTokenKind.Slash))
            {
                i++;
                if (i == end)
                {
                    return;
                }
            }
            else if (Is(XPathTokenKind.DoubleSlash))
            {
                i++;
            }
            else if (Is(XPathTokenKind.Name) && Token().SequenceEqual("id") && i + 1 < end && tokens[i + 1].Kind == XPathTokenKind.Open)
            {
                i += 2;
                if (!Is(XPathTokenKind.Literal))
                {
                    Fail(node, "SCH.XPath", "id() match needs a literal.");
                }

                i++;
                if (!Is(XPathTokenKind.Close))
                {
                    Fail(node, "SCH.XPath", "Invalid id() match.");
                }

                i++;
                if (i == end)
                {
                    return;
                }

                if (!Is(XPathTokenKind.Slash) && !Is(XPathTokenKind.DoubleSlash))
                {
                    Fail(node, "SCH.XPath", "Invalid id() match path.");
                }

                i++;
            }
            while (i < end)
            {
                Work(1, node);
                if (Is(XPathTokenKind.At))
                {
                    i++;
                }
                else if (Is(XPathTokenKind.Name) && i + 1 < end && tokens[i + 1].Kind == XPathTokenKind.Axis)
                {
                    if (!Token().SequenceEqual("child") && !Token().SequenceEqual("attribute"))
                    {
                        Fail(node, "SCH.XPath", "Only child and attribute match axes are allowed.");
                    }

                    i += 2;
                }
                if (Is(XPathTokenKind.Star))
                {
                    i++;
                }
                else if (Is(XPathTokenKind.Name))
                {
                    var kind = Token() switch
                    {
                        "node" => "node",
                        "text" => "text",
                        "comment" => "comment",
                        "processing-instruction" => "processing-instruction",
                        _ => null
                    };
                    i++;
                    if (Is(XPathTokenKind.Open))
                    {
                        if (kind is null)
                        {
                            Fail(node, "SCH.XPath", "Function is not a match node test.");
                        }

                        i++;
                        if (kind == "processing-instruction" && Is(XPathTokenKind.Literal))
                        {
                            i++;
                        }

                        if (!Is(XPathTokenKind.Close))
                        {
                            Fail(node, "SCH.XPath", "Invalid match node test.");
                        }

                        i++;
                    }
                }
                else
                {
                    Fail(node, "SCH.XPath", "Invalid match step.");
                }

                while (Is(XPathTokenKind.Bracket))
                {
                    var depth = 1;
                    i++;
                    while (i < end && depth != 0)
                    {
                        Work(1, node);
                        if (tokens[i].Kind == XPathTokenKind.Bracket)
                        {
                            depth++;
                        }

                        if (tokens[i].Kind == XPathTokenKind.EndBracket)
                        {
                            depth--;
                        }

                        i++;
                    }
                    if (depth != 0)
                    {
                        Fail(node, "SCH.XPath", "Unclosed match predicate.");
                    }
                }
                if (i == end)
                {
                    return;
                }

                if (!Is(XPathTokenKind.Slash) && !Is(XPathTokenKind.DoubleSlash))
                {
                    Fail(node, "SCH.XPath", "Invalid match separator.");
                }

                i++;
                if (i == end)
                {
                    Fail(node, "SCH.XPath", "Missing match step.");
                }
            }
            Fail(node, "SCH.XPath", "Missing match step.");
        }

        private IReadOnlyList<string> References(Node node, string attribute, Dictionary<string, Node> declarations)
        {
            var result = new List<string>();
            if (node.Get(attribute) is not string source)
            {
                return result.AsReadOnly();
            }

            Scan(source, node.Location);
            var remaining = source.AsSpan();
            while (!remaining.IsEmpty)
            {
                var start = 0;
                while (start < remaining.Length && Space(remaining[start]))
                {
                    start++;
                }

                remaining = remaining[start..];
                if (remaining.IsEmpty)
                {
                    break;
                }

                var end = 0;
                while (end < remaining.Length && !Space(remaining[end]))
                {
                    end++;
                }

                var id = remaining[..end].ToString();
                Work(1, node);
                if (!declarations.ContainsKey(id))
                {
                    Fail(node, "SCH.SchemaRestriction", "Undefined diagnostic or property reference.");
                }

                result.Add(id);
                remaining = remaining[end..];
            }
            if (result.Count == 0)
            {
                Fail(node, "SCH.SchemaRestriction", "Empty reference list.");
            }

            return result.AsReadOnly();
        }
        private Dictionary<string, IReadOnlyList<SchematronTextPart>> TextMap(Dictionary<string, Node> input, HashSet<XPathExpandedName> scope)
        {
            var result = new Dictionary<string, IReadOnlyList<SchematronTextPart>>(StringComparer.Ordinal);
            foreach (var (id, node) in input)
            {
                result.Add(id, Text(node, scope, false));
            }

            return result;
        }
        private IReadOnlyList<SchematronTextPart> Text(Node node, HashSet<XPathExpandedName> scope, bool requireVariables = true)
        {
            var result = new List<SchematronTextPart>();
            var stack = new Stack<Node>();
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }

            while (stack.Count != 0)
            {
                var child = stack.Pop();
                Work(1, child);
                Components(1, child);
                if (child.Name == "#text")
                {
                    result.Add(new SchematronLiteral(child.Text!));
                }
                else if (child.Name == "value-of")
                {
                    result.Add(new SchematronValueOf(TextExpression(Required(child, "select"), child)));
                }
                else if (child.Name == "name")
                {
                    result.Add(new SchematronNameOf(child.Get("path") is string path ? TextExpression(path, child) : null));
                }
                else
                {
                    for (var i = child.Children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(child.Children[i]);
                    }
                }
            }
            return result.AsReadOnly();
            XPathExpression TextExpression(string source, Node origin)
            {
                if (requireVariables)
                {
                    return Expression(source, scope, origin);
                }
                // References may legally use a check-local let; lexical compilation
                // here is detached, and each actual use above resolves its scope.
                Scan(source, origin.Location);
                Work(1, origin);
                Components(1, origin);
                try
                {
                    return XPathExpression.Compile(source, xpath, operation.CancellationToken);
                }
                catch (XPathCompileException error)
                {
                    if (error.Diagnostic.Category == XPathDiagnosticCategory.CompileLimit)
                    {
                        Limit(origin.Location, "XPath compilation limit exceeded.");
                    }

                    Fail(origin, "SCH.XPath", "Invalid dynamic message XPath.");
                    throw;
                }
            }
        }
        private string PlainText(Node node)
        {
            var output = new StringBuilder();
            var stack = new Stack<Node>();
            stack.Push(node);
            while (stack.Count != 0)
            {
                var n = stack.Pop();
                Work(1, n);
                if (n.Text is string text)
                {
                    output.Append(text);
                }

                for (var i = n.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push(n.Children[i]);
                }
            }
            return output.ToString();
        }
        private XPathExpandedName Name(string lexical, Node node, bool qname)
        {
            Scan(lexical, node.Location);
            var colon = lexical.IndexOf(':');
            var local = lexical;
            var uri = "";
            if (colon >= 0)
            {
                if (!qname || colon == 0 || colon == lexical.Length - 1 || lexical.AsSpan(colon + 1).Contains(':'))
                {
                    Fail(node, "SCH.SchemaRestriction", "Invalid schema name.");
                }

                var prefix = lexical[..colon];
                local = lexical[(colon + 1)..];
                if (prefix == "xml")
                {
                    uri = TreeValidation.XmlUri;
                }
                else if (!namespaces.TryGetValue(prefix, out uri!))
                {
                    Fail(node, "SCH.XPath", "Unbound variable prefix.");
                }
            }
            try
            {
                ContextScan.Name(local, operation.CancellationToken);
            }
            catch (ArgumentException) { Fail(node, "SCH.SchemaRestriction", "Invalid schema name."); }
            return new(uri, local);
        }
        private string Required(Node node, string name)
        {
            var value = node.Get(name);
            if (value is null || value.Length == 0)
            {
                Fail(node, "SCH.SchemaRestriction", "Missing required schema attribute.");
            }

            return value!;
        }
        private static bool Word(ReadOnlySpan<char> vocabulary, ReadOnlySpan<char> word)
        {
            while (!vocabulary.IsEmpty)
            {
                var position = vocabulary.IndexOf(word, StringComparison.Ordinal);
                if (position < 0)
                {
                    return false;
                }

                var end = position + word.Length;
                if (position > 0 && vocabulary[position - 1] == ' ' && end < vocabulary.Length && vocabulary[end] == ' ')
                {
                    return true;
                }

                vocabulary = vocabulary[(position + 1)..];
            }
            return false;
        }
        private static bool Space(char value) => value is ' ' or '\t' or '\r' or '\n';
        private bool White(string value, XmlLocation location)
        {
            foreach (var c in value)
            {
                Work(1, location);
                if (!Space(c))
                {
                    return false;
                }
            }
            return true;
        }
        private void Scan(string text, XmlLocation location)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                Work(1, location);
            }
        }
        private void Work(long amount, Node node) => Work(amount, node.Location);
        private void Work(long amount, XmlLocation location)
        {
            if (!operation.Charge(amount, location))
            {
                throw SchemaSnapshots.Failure(operation);
            }
        }
        private void Components(int count, Node node) => Components(count, node.Location);
        private void Components(int count, XmlLocation location)
        {
            if (!operation.ChargeComponents(count, location))
            {
                throw SchemaSnapshots.Failure(operation);
            }
        }
        private SchemaCompileException Error(string code, string message, XmlLocation location)
        {
            if (code == "SCHEMA.ResourceCycle")
            {
                operation.Refuse(code, message, location);
                return SchemaSnapshots.Failure(operation);
            }
            var diagnostic = new XmlDiagnostic(code, XmlDiagnosticSeverity.Error, message, location);
            operation.AddDiagnostic(diagnostic);
            return operation.IsComplete ? new([diagnostic]) : SchemaSnapshots.Failure(operation);
        }
        [DoesNotReturn] private void Fail(Node node, string code, string message) => throw Error(code, message, node.Location);
        [DoesNotReturn]
        private void Limit(XmlLocation location, string message)
        {
            operation.Refuse("SCHEMA.Limit", message, location);
            throw SchemaSnapshots.Failure(operation);
        }
    }
}
