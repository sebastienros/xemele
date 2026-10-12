using System.Buffers;
using System.Text;

namespace Xemele.Validation;

internal interface IRelaxNgEvaluator
{
    ValidationResult Validate(RngCompiledSchema schema, XmlDocument document, ValidationOptions options);
}
internal sealed class RelaxNgEvaluator(RngProgram program) : IRelaxNgEvaluator
{
    public ValidationResult Validate(RngCompiledSchema schema, XmlDocument document, ValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(options);
        if (!ReferenceEquals(schema, program.Schema)) throw new ArgumentException("Evaluator requires its finalized schema.", nameof(schema));
        return Evaluate(program, document, new(options));
    }
    internal static ValidationResult Evaluate(RngProgram program, XmlDocument document, SchemaValidationOperation operation)
        { EvaluateCore(program, document, operation); return operation.Complete(); }
    internal static void EvaluateCore(RngProgram program, XmlDocument document, ISchemaOperation operation)
    {
        if (!operation.Charge(1, default) || !operation.ChargeComponents(6, default)) return;
        new Run(program, document, operation).Evaluate();
    }

    // A residual expression is identified by an integer. Frozen nodes stay in the
    // compiled graph; only newly derived choice/group/interleave/repetition nodes
    // are retained in this run. No input views or run caches escape validation.
    private readonly record struct Expression(RngPatternKind Kind, int Left, int Right);
    private sealed class Run
    {
        private const int Reject = -1, Empty = -2;
        private readonly RngProgram program;
        private readonly XmlDocument document;
        private readonly ISchemaOperation operation;
        private readonly RngOperation op;
        private readonly long revision;
        private readonly List<Expression> residuals = [];
        private readonly Dictionary<Expression, int> intern = [];
        private readonly Dictionary<int, bool> nullable = [];
        private readonly Dictionary<(int, XmlNode), bool> matched = [];
        private readonly Dictionary<XmlNode, SchemaNamespaceContext> contexts = [];
        private int frames;
        private XmlLocation at, mismatch;
        internal Run(RngProgram program, XmlDocument document, ISchemaOperation operation)
        { this.program = program; this.document = document; this.operation = operation; op = new(operation); revision = document.ContentRevision; }
        private void Check()
        {
            operation.CancellationToken.ThrowIfCancellationRequested();
            if (revision < 0 || document.ContentRevision != revision) throw new InvalidOperationException("Instance changed during RELAX NG validation.");
        }
        private void Work(long amount = 1) { Check(); op.Charge(amount, at); }
        private void Grow() { Work(); op.ChargeComponents(1, at); }
        private void Limit(string message) => op.Refuse("VALIDATION.Limit", message, at);
        private Frame Enter()
        { Work(); if (frames == 1024) Limit("Matcher stack representation exceeded."); frames++; return new(this); }
        private readonly struct Frame(Run run) : IDisposable { public void Dispose() => run.frames--; }
        internal void Evaluate()
        {
            Check();
            try
            {
                Work();
                if (!document.SourceInputComplete) op.Refuse("SCHEMA.IncompleteInput", "Instance input is incomplete.", default);
                Inspect();
                if (document.RootElement.IsDefault || !Nullable(Derive(program.Schema.Start, Event.Element, document.RootElement, default, "", null)))
                    operation.AddDiagnostic(new("RNG.Instance", XmlDiagnosticSeverity.Error, "Instance does not match the RELAX NG schema.", mismatch));
            }
            catch (RngOperation.Stopped) { }
            finally { Check(); }
        }
        // Account the live DOM before matching. This also checks quotas for callers
        // supplying a pre-parsed document under stricter validation limits.
        private void Inspect()
        {
            long characters = 0; int depth = 0;
            for (var node = document.DocumentNode; !node.IsDefault;)
            {
                at = document.OriginalLocation(node); Work();
                if (node.Kind == XmlNodeKind.Element)
                {
                    depth++;
                    if (operation.Limits.Depth is int cap && depth > cap) Limit("Instance depth limit exceeded.");
                    int count = 0;
                    foreach (var attribute in node.Attributes)
                    {
                        Work(); count++;
                        if (operation.Limits.AttributesPerElement is int maximum && count > maximum) Limit("Instance attribute limit exceeded.");
                        characters = AddCharacters(characters, Measure(attribute.Value));
                        Measure(attribute.LocalName); Measure(attribute.NamespaceUri);
                    }
                    foreach (var ns in node.NamespaceDeclarations) { Work(); Measure(ns.Prefix); Measure(ns.Value); }
                    Measure(node.LocalName); Measure(node.NamespaceUri);
                }
                else characters = AddCharacters(characters, Measure(node.Value));
                if (!node.FirstChild.IsDefault) { node = node.FirstChild; continue; }
                while (true)
                {
                    Work(); if (node.Kind == XmlNodeKind.Element) depth--;
                    if (!node.NextSibling.IsDefault) { node = node.NextSibling; break; }
                    if (node == document.DocumentNode) { node = default; break; }
                    node = node.Parent;
                }
            }
            mismatch = document.RootElement.IsDefault ? default : document.OriginalLocation(document.RootElement);
        }
        private long AddCharacters(long total, int count)
        {
            if (count > long.MaxValue - total || operation.Limits.DocumentCharacters is long max && count > max - total)
                Limit("Instance character limit exceeded.");
            return total + count;
        }
        private int Measure(ReadOnlySpan<char> value)
        {
            int count = 0;
            for (int i = 0; i < value.Length;)
            {
                Work();
                if (Rune.DecodeFromUtf16(value[i..], out _, out int consumed) != OperationStatus.Done)
                    op.Refuse("SCHEMA.Syntax", "Invalid instance Unicode.", at);
                i += consumed; count++;
                if (operation.Limits.TokenCharacters is int cap && count > cap) Limit("Instance token limit exceeded.");
            }
            return count;
        }
        private Expression Get(int id)
        {
            if (id == Reject) return new(RngPatternKind.NotAllowed, 0, 0);
            if (id == Empty) return new(RngPatternKind.Empty, 0, 0);
            if (id >= program.Schema.Patterns.Count) return residuals[id - program.Schema.Patterns.Count];
            var p = program.Schema.Patterns[id];
            return new(p.Kind, p.Children.Count == 0 ? Reject : p.Children[0], p.Children.Count < 2 ? Reject : p.Children[1]);
        }
        private int Make(RngPatternKind kind, int a, int b = Reject)
        {
            Work();
            if (kind == RngPatternKind.Choice)
            {
                if (a == Reject) return b; if (b == Reject || a == b) return a;
                if (a > b) (a, b) = (b, a);
            }
            else if (kind is RngPatternKind.Group or RngPatternKind.Interleave)
            {
                if (a == Reject || b == Reject) return Reject;
                if (a == Empty) return b; if (b == Empty) return a;
                if (kind == RngPatternKind.Interleave && a > b) (a, b) = (b, a);
            }
            if (kind == RngPatternKind.OneOrMore && a is Reject or Empty) return a;
            var expression = new Expression(kind, a, b);
            if (intern.TryGetValue(expression, out int known)) return known;
            Grow(); if (residuals.Count >= int.MaxValue - program.Schema.Patterns.Count) Limit("Residual graph representation exceeded.");
            int id = residuals.Count + program.Schema.Patterns.Count;
            residuals.Add(expression); intern.Add(expression, id); return id;
        }
        private bool Nullable(int id)
        {
            using var frame = Enter();
            if (nullable.TryGetValue(id, out bool known)) return known;
            var p = Get(id);
            bool result = p.Kind switch
            {
                RngPatternKind.Empty or RngPatternKind.Text => true,
                RngPatternKind.Choice => Nullable(p.Left) || Nullable(p.Right),
                RngPatternKind.Group or RngPatternKind.Interleave => Nullable(p.Left) && Nullable(p.Right),
                RngPatternKind.OneOrMore or RngPatternKind.Ref => Nullable(p.Left),
                _ => false
            };
            Grow(); nullable.Add(id, result); return result;
        }
        private bool Name(RngNameClass name, string uri, string local)
        {
            using var frame = Enter();
            return name switch
            {
                RngName n => Equal(n.Name.NamespaceUri, uri) && Equal(n.Name.LocalName, local),
                RngAnyName n => n.Except is null || !Name(n.Except, uri, local),
                RngNsName n => Equal(n.NamespaceUri, uri) && (n.Except is null || !Name(n.Except, uri, local)),
                RngNameChoice n => Name(n.Left, uri, local) || Name(n.Right, uri, local),
                _ => false
            };
        }
        private bool Equal(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            Work(); if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) { Work(); if (a[i] != b[i]) return false; } return true;
        }
        private enum Event { Element, Attribute, Text }
        private int Derive(int id, Event kind, XmlNode element, XmlAttribute attribute, ReadOnlySpan<char> text, SchemaNamespaceContext? context)
        {
            using var frame = Enter(); var p = Get(id);
            switch (p.Kind)
            {
                case RngPatternKind.Choice:
                    return Make(p.Kind, Derive(p.Left, kind, element, attribute, text, context), Derive(p.Right, kind, element, attribute, text, context));
                case RngPatternKind.Group:
                {
                    int first = Make(p.Kind, Derive(p.Left, kind, element, attribute, text, context), p.Right);
                    return kind == Event.Attribute || Nullable(p.Left)
                        ? Make(RngPatternKind.Choice, first, (kind == Event.Attribute ? Make(p.Kind, p.Left, Derive(p.Right, kind, element, attribute, text, context)) : Derive(p.Right, kind, element, attribute, text, context))) : first;
                }
                case RngPatternKind.Interleave:
                    return Make(RngPatternKind.Choice,
                        Make(p.Kind, Derive(p.Left, kind, element, attribute, text, context), p.Right),
                        Make(p.Kind, p.Left, Derive(p.Right, kind, element, attribute, text, context)));
                case RngPatternKind.OneOrMore:
                    return Make(RngPatternKind.Group,
                        Derive(p.Left, kind, element, attribute, text, context), Make(RngPatternKind.Choice, Empty, id));
                case RngPatternKind.Ref: return Derive(p.Left, kind, element, attribute, text, context);
                case RngPatternKind.Element:
                    if (kind == Event.Text) return Whitespace(text) ? id : Reject;
                    return kind == Event.Element && Matches(id, element) ? Empty : Reject;
                case RngPatternKind.Attribute:
                    if (kind != Event.Attribute) return Reject;
                    var authored = program.Schema.Patterns[id];
                    return Name(authored.NameClass!, attribute.NamespaceUri, attribute.LocalName) &&
                        (Nullable(Derive(p.Left, Event.Text, default, default, text, context)) || Whitespace(text) && Nullable(p.Left)) ? Empty : Reject;
                case RngPatternKind.Text: return kind == Event.Text ? id : Reject;
                case RngPatternKind.Data:
                case RngPatternKind.Value:
                    if (kind != Event.Text) return Reject;
                    var pattern = program.Schema.Patterns[id];
                    var datatype = pattern.Datatype!.Value; var library = program.Schema.Datatypes.Find(datatype.NamespaceUri)!;
                    bool valid = program.Definition(id) is XmlDatatypeDefinition definition
                        ? program.Facets.TryValidate(definition, text, context!, program.Schema.Datatypes, op, out var value, out _)
                        : library.TryParse(datatype, text, context!, op, out value, out _);
                    if (!valid) return Reject;
                    if (p.Kind == RngPatternKind.Value) return library.ValueEquals(value!, program.Value(id)!, op) ? Empty : Reject;
                    return p.Left == Reject || !Nullable(Derive(p.Left, kind, default, default, text, context)) ? Empty : Reject;
                case RngPatternKind.List:
                    if (kind != Event.Text) return Reject;
                    int state = p.Left;
                    for (int i = 0; i < text.Length;)
                    {
                        while (i < text.Length && IsWhitespace(text[i])) { Work(); i++; }
                        int start = i;
                        while (i < text.Length && !IsWhitespace(text[i])) { Work(); i++; }
                        if (start != i) state = Derive(state, Event.Text, default, default, text[start..i], context);
                    }
                    return Nullable(state) ? Empty : Reject;
                default: return Reject;
            }
        }
        private int Close(int id)
        {
            using var frame = Enter(); var p = Get(id);
            return p.Kind switch
            {
                RngPatternKind.Attribute => Reject,
                RngPatternKind.Choice or RngPatternKind.Group or RngPatternKind.Interleave => Make(p.Kind, Close(p.Left), Close(p.Right)),
                RngPatternKind.OneOrMore => Make(p.Kind, Close(p.Left)),
                RngPatternKind.Ref => Close(p.Left),
                _ => id
            };
        }
        private bool Matches(int id, XmlNode element)
        {
            using var frame = Enter(); at = document.OriginalLocation(element);
            if (matched.TryGetValue((id, element), out bool known)) return known;
            var pattern = program.Schema.Patterns[id];
            bool valid = Name(pattern.NameClass!, element.NamespaceUri, element.LocalName) && Content(pattern.Children[0], element);
            at = document.OriginalLocation(element); if (!valid) mismatch = at;
            Grow(); matched.Add((id, element), valid); return valid;
        }
        private bool Content(int state, XmlNode element)
        {
            if (!contexts.TryGetValue(element, out var context))
            { Grow(); context = SchemaSnapshots.Context(element, op, document.ParsedSourceUri); Check(); contexts.Add(element, context); }
            foreach (var attribute in element.Attributes)
            {
                at = document.OriginalLocation(attribute); Work();
                state = Derive(state, Event.Attribute, default, attribute, attribute.Value, context);
            }
            state = Close(state);
            bool child = false; string? text = null; StringBuilder? combined = null; int textScalars = 0;
            foreach (var node in element.Children)
            {
                at = document.OriginalLocation(node); Work();
                if (node.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
                {
                    string value = node.Value; if (value.Length == 0) continue;
                    int count = Measure(value);
                    if (count > int.MaxValue - textScalars || operation.Limits.TokenCharacters is int cap && count > cap - textScalars)
                        Limit("Coalesced instance text limit exceeded.");
                    textScalars += count;
                    if (text is null) text = value;
                    else
                    {
                        int length = combined?.Length ?? text.Length;
                        if (value.Length > int.MaxValue - length) Limit("Coalesced text representation exceeded.");
                        Grow();
                        if (combined is null) { Work(text.Length); combined = new StringBuilder(text); }
                        Work(value.Length); combined.Append(value);
                    }
                }
                else if (node.Kind == XmlNodeKind.Element)
                {
                    if (text is not null) { state = Text(state, combined is null ? text : OwnedText(combined), context); text = null; combined = null; textScalars = 0; }
                    child = true; state = Derive(state, Event.Element, node, default, "", context);
                }
            }
            at = document.OriginalLocation(element);
            if (text is not null) state = Text(state, combined is null ? text : OwnedText(combined), context);
            else if (!child) state = Make(RngPatternKind.Choice, state, Derive(state, Event.Text, default, default, "", context));
            return Nullable(state);
        }
        private string OwnedText(StringBuilder builder) { Grow(); Work(builder.Length); return builder.ToString(); }
        private int Text(int state, ReadOnlySpan<char> text, SchemaNamespaceContext context)
        {
            int derived = Derive(state, Event.Text, default, default, text, context);
            return Whitespace(text) ? Make(RngPatternKind.Choice, state, derived) : derived;
        }
        private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';
        private bool Whitespace(ReadOnlySpan<char> value)
        { foreach (char c in value) { Work(); if (!IsWhitespace(c)) return false; } return true; }
    }
}
