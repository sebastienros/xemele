using System.Buffers;
using System.Text;
using Xemele.Parsing.Dtd;
namespace Xemele.Validation;

// Restricted XSD 1.0 paths, not an M4 expression. Compilation owns names once;
// event matching uses borrowed node handles and immutable tests without scratch.
internal sealed class IdentityXPath(IdentityPath[] paths, bool selectsNonElements)
{
    internal bool SelectsNonElements { get; } = selectsNonElements;
    internal bool Matches(XmlNode context, XmlNode node, SchemaName? attribute, ISchemaOperation op, XmlLocation at)
    {
        foreach (var path in paths)
        {
            IdentityWork.Tick(op, at);
            if ((attribute is null) != (path.Attribute is null)) continue;
            if (attribute is { } name && !path.Attribute!.Value.Matches(name.NamespaceUri, name.LocalName, op, at)) continue;
            var cursor = node; bool matched = true;
            for (int i = path.Steps.Length - 1; i >= 0; i--)
            {
                IdentityWork.Tick(op, at);
                if (cursor == context || cursor.IsDefault || !path.Steps[i].Matches(cursor.NamespaceUri, cursor.LocalName, op, at))
                { matched = false; break; }
                cursor = cursor.Parent;
            }
            if (!matched) continue;
            if (!path.Descendants) { if (cursor == context) return true; continue; }
            while (!cursor.IsDefault)
            {
                IdentityWork.Tick(op, at);
                if (cursor == context) return true;
                cursor = cursor.Parent;
            }
        }
        return false;
    }
    internal static IdentityXPath Compile(string expression, bool field, SchemaNamespaceContext context, ISchemaOperation op, XmlLocation at)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new Parser(expression, field, context, op, at).Compile();
    }
    private sealed class Parser(string source, bool field, SchemaNamespaceContext context, ISchemaOperation op, XmlLocation at)
    {
        private int position;
        internal IdentityXPath Compile()
        {
            IdentityWork.Retain(op, at);
            var paths = new List<IdentityPath>();
            bool nonElements = false;
            do
            {
                bool descendant = false;
                var steps = new List<IdentityNameTest>(); IdentityNameTest? attribute = null;
                if (Axis("descendant-or-self"))
                { NodeTest(); if (!Take('/')) Bad(); descendant = true; }
                else if (Take('.'))
                {
                    if (Take('/'))
                    {
                        // '//' is a single longest token: whitespace can surround,
                        // but cannot split, its two slashes.
                        if (position < source.Length && source[position] == '/') { Consume(1); descendant = true; }
                        else { ParseStep(steps, ref attribute); }
                    }
                    else if (Peek() != '|' && Peek() != '\0') Bad();
                }
                else ParseStep(steps, ref attribute);
                if (descendant) ParseStep(steps, ref attribute);
                while (Take('/'))
                {
                    if (attribute is not null || Peek() == '/') Bad();
                    ParseStep(steps, ref attribute);
                }
                if (Peek() != '|' && Peek() != '\0') Bad();
                IdentityWork.Retain(op, at, checked(1 + steps.Count));
                nonElements |= descendant && steps.Count == 0 && attribute is null;
                paths.Add(new(descendant, steps.ToArray(), attribute));
            } while (Take('|'));
            if (position != source.Length || paths.Count == 0) Bad();
            IdentityWork.Tick(op, at, 0);
            return new(paths.ToArray(), nonElements);
        }
        private void ParseStep(List<IdentityNameTest> steps, ref IdentityNameTest? attribute)
        {
            if (Take('.')) return;
            bool attr = Take('@');
            if (Axis("self")) { if (attr) Bad(); NodeTest(); return; }
            if (Axis("child")) { if (attr) Bad(); }
            else if (Axis("attribute")) { if (attr) Bad(); attr = true; }
            if (attr && !field) Bad();
            Skip(); int start = position;
            while (position < source.Length && source[position] is not ('/' or '|' or '@' or ' ' or '\t' or '\r' or '\n'))
            { IdentityWork.Tick(op, at); position++; }
            var lexical = source.AsSpan(start, position - start);
            if (lexical.IsEmpty) Bad();
            IdentityNameTest test;
            if (lexical.SequenceEqual("*")) test = new(null, null);
            else
            {
                int colon = lexical.IndexOf(':');
                if (colon < 0) { Name(lexical); IdentityWork.Retain(op, at); test = new("", lexical.ToString()); }
                else
                {
                    var prefix = lexical[..colon]; var local = lexical[(colon + 1)..];
                    Name(prefix); if (!local.SequenceEqual("*")) Name(local);
                    IdentityWork.Retain(op, at, 2);
                    if (!context.Prefixes.TryGetValue(prefix.ToString(), out var ns) || ns.Length == 0) Bad();
                    test = new(ns, local.SequenceEqual("*") ? null : local.ToString());
                }
            }
            if (attr) attribute = test;
            else { IdentityWork.Retain(op, at); steps.Add(test); }
        }
        private void Name(ReadOnlySpan<char> name)
        {
            if (name.IsEmpty) Bad();
            bool first = true;
            while (!name.IsEmpty)
            {
                IdentityWork.Tick(op, at);
                if (Rune.DecodeFromUtf16(name, out var scalar, out int width) != OperationStatus.Done ||
                    scalar.Value == ':' || (first ? !ValueSyntax.IsNameStart(scalar.Value) : !ValueSyntax.IsNameChar(scalar.Value))) Bad();
                first = false; name = name[width..];
            }
        }
        private char Peek() { Skip(); return position < source.Length ? source[position] : '\0'; }
        private bool Take(char c)
        { if (Peek() != c) return false; IdentityWork.Tick(op, at); position++; return true; }
        private void Skip()
        { while (position < source.Length && source[position] is ' ' or '\t' or '\r' or '\n') { IdentityWork.Tick(op, at); position++; } }
        private void Consume(int count) { IdentityWork.Tick(op, at, count); position += count; }
        private bool Axis(string name)
        {
            Skip(); var remaining = source.AsSpan(position);
            IdentityWork.Tick(op, at, name.Length);
            if (!remaining.StartsWith(name, StringComparison.Ordinal)) return false;
            int stop = name.Length;
            while (stop < remaining.Length && remaining[stop] is ' ' or '\t' or '\r' or '\n')
            { IdentityWork.Tick(op, at); stop++; }
            IdentityWork.Tick(op, at, 2);
            if (!remaining[stop..].StartsWith("::", StringComparison.Ordinal)) return false;
            position += stop + 2; return true;
        }
        private void NodeTest()
        {
            Skip(); if (!source.AsSpan(position).StartsWith("node", StringComparison.Ordinal)) Bad();
            Consume(4); if (!Take('(') || !Take(')')) Bad();
        }
        private void Bad() => IdentityWork.Invalid(op, at, "Invalid XSD identity restricted XPath.");
    }
}
internal readonly record struct IdentityNameTest(string? Namespace, string? Local)
{
    internal bool Matches(string ns, string local, ISchemaOperation op, XmlLocation at)
    { IdentityWork.Tick(op, at, 1L + ns.Length + local.Length); return (Namespace is null || Namespace == ns) && (Local is null || Local == local); }
}
internal sealed record IdentityPath(bool Descendants, IdentityNameTest[] Steps, IdentityNameTest? Attribute);
