using System.Buffers;
using System.Text;
using Xemele.Parsing.Dtd;
namespace Xemele.XPath.Internal;

internal enum XPathTokenKind { End, Name, Number, Literal, Slash, DoubleSlash, Dot, Parent, Axis, At, Dollar, Open, Close, Bracket, EndBracket, Comma, Star, Plus, Minus, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, Union }
internal readonly record struct XPathToken(XPathTokenKind Kind, int Start, int Length)
{
    internal XPathSourceSpan Span => new(Start, Length);
}
// Tokens borrow the operation-owned source. No token string/array is materialized.
internal struct XPathLexer
{
    private readonly string source;
    private readonly CancellationToken cancellation;
    private readonly int? maximum;
    private int position, tokens;
    internal XPathLexer(string source, CancellationToken cancellation, int? maximum, int contextTokens)
    { this.source = source; this.cancellation = cancellation; this.maximum = maximum; position = 0; tokens = contextTokens; }
    internal ReadOnlySpan<char> Text(XPathToken token) => source.AsSpan(token.Start, token.Length);
    internal XPathToken Next()
    {
        cancellation.ThrowIfCancellationRequested();
        while (position < source.Length && source[position] is ' ' or '\t' or '\r' or '\n') Advance(1);
        int start = position;
        if (start == source.Length) return new(XPathTokenKind.End, start, 0);
        if (tokens == int.MaxValue || maximum is int cap && tokens >= cap) throw XPathCompiler.Error("XPATH-COMPILE-LIMIT", new(start, 1));
        tokens++;
        char c = source[position]; XPathTokenKind kind;
        if (c is '\'' or '"')
        {
            Advance(1);
            while (position < source.Length && source[position] != c) AdvanceScalar();
            if (position == source.Length) throw XPathCompiler.Error("XPATH-SYNTAX", new(start, position - start));
            Advance(1); kind = XPathTokenKind.Literal;
        }
        else if (c is >= '0' and <= '9' || c == '.' && position + 1 < source.Length && source[position + 1] is >= '0' and <= '9')
        {
            Digits(); if (position < source.Length && source[position] == '.') { Advance(1); Digits(); } kind = XPathTokenKind.Number;
        }
        else if (NameStart())
        {
            Name();
            if (position < source.Length && source[position] == ':' && !(position + 1 < source.Length && source[position + 1] == ':'))
            {
                Advance(1);
                if (position < source.Length && source[position] == '*') Advance(1);
                else { if (!NameStart()) throw XPathCompiler.Error("XPATH-SYNTAX", new(position, position == source.Length ? 0 : 1)); Name(); }
            }
            kind = XPathTokenKind.Name;
        }
        else
        {
            AdvanceScalar();
            kind = c switch {
                '/' => Pair('/', XPathTokenKind.DoubleSlash, XPathTokenKind.Slash),
                '.' => Pair('.', XPathTokenKind.Parent, XPathTokenKind.Dot),
                ':' => Pair(':', XPathTokenKind.Axis, null),
                '!' => Pair('=', XPathTokenKind.NotEqual, null),
                '<' => Pair('=', XPathTokenKind.LessEqual, XPathTokenKind.Less),
                '>' => Pair('=', XPathTokenKind.GreaterEqual, XPathTokenKind.Greater),
                '@' => XPathTokenKind.At, '$' => XPathTokenKind.Dollar, '(' => XPathTokenKind.Open, ')' => XPathTokenKind.Close,
                '[' => XPathTokenKind.Bracket, ']' => XPathTokenKind.EndBracket, ',' => XPathTokenKind.Comma, '*' => XPathTokenKind.Star,
                '+' => XPathTokenKind.Plus, '-' => XPathTokenKind.Minus, '=' => XPathTokenKind.Equal, '|' => XPathTokenKind.Union,
                _ => throw XPathCompiler.Error("XPATH-SYNTAX", new(start, position - start)) };
        }
        return new(kind, start, position - start);
    }
    private XPathTokenKind Pair(char next, XPathTokenKind pair, XPathTokenKind? single)
    {
        if (position < source.Length && source[position] == next) { Advance(1); return pair; }
        return single ?? throw XPathCompiler.Error("XPATH-SYNTAX", new(position - 1, 1));
    }
    private void Advance(int units)
    {
        int end = position + units;
        if ((position >> 8) != (end >> 8)) cancellation.ThrowIfCancellationRequested();
        position = end;
    }
    private int Scalar()
    {
        if (Rune.DecodeFromUtf16(source.AsSpan(position), out var rune, out _) != OperationStatus.Done || !ValueSyntax.IsXmlScalar(rune.Value))
            throw XPathCompiler.Error("XPATH-SYNTAX", new(position, 1));
        return rune.Value;
    }
    private void AdvanceScalar() { int scalar = Scalar(); Advance(scalar > 0xffff ? 2 : 1); }
    private bool NameStart() => position < source.Length && source[position] != ':' && ValueSyntax.IsNameStart(Scalar());
    private void Name()
    {
        AdvanceScalar();
        while (position < source.Length && source[position] != ':' && ValueSyntax.IsNameChar(Scalar())) AdvanceScalar();
    }
    private void Digits() { while (position < source.Length && source[position] is >= '0' and <= '9') Advance(1); }
}
