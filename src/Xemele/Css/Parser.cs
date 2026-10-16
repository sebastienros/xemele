using System.Text;
using System.Numerics;
using Xemele.Html;
using Xemele.Internal;

namespace Xemele.Css;

public sealed partial class CssSelector
{
    // Compilation owns decoded identifiers and the immutable matching program.
    private sealed class Parser(string source, Dictionary<string, string>? namespaces)
    {
        private int position;
        private char Current => position < source.Length ? source[position] : '\0';
        private CssSelectorException Error(int? offset = null) => new("Invalid or unsupported CSS selector.", offset ?? position);
        public Complex[] Parse()
        {
            var list = new List<Complex>();
            Skip();
            while (true)
            {
                var parts = new List<Compound> { ParseCompound(false) };
                var links = new List<char>();
                while (true)
                {
                    var space = Skip();
                    if (position == source.Length || Current == ',')
                    {
                        break;
                    }

                    char link;
                    if (Current is '>' or '+' or '~')
                    {
                        link = Current;
                        position++;
                        Skip();
                    }
                    else
                    {
                        link = space ? ' ' : throw Error();
                    }

                    links.Add(link);
                    parts.Add(ParseCompound(false));
                }
                list.Add(new([.. parts], [.. links]));
                if (position == source.Length)
                {
                    break;
                }

                position++;
                Skip();
            }
            return [.. list];
        }
        private Compound ParseCompound(bool inNot)
        {
            Comments();
            var tests = new List<Test>();
            NameTest? type = null;
            if (Current is '*' or '|' || IdentifierStart(position))
            {
                type = Name(false);
            }

            while (true)
            {
                Comments();
                if (Current is '#' or '.')
                {
                    var symbol = Current;
                    position++;
                    var value = Identifier();
                    var attrName = symbol == '#' ? "id" : "class";
                    tests.Add(new(new(new(attrName, attrName, ""), symbol == '#' ? AttrOp.Equals : AttrOp.Token, value)));
                }
                else if (Current == '[')
                {
                    tests.Add(new(ParseAttribute()));
                }
                else if (Current == ':')
                {
                    tests.Add(ParsePseudo(inNot));
                }
                else
                {
                    break;
                }
            }
            if (type is null && tests.Count == 0)
            {
                throw Error();
            }

            return new(type, [.. tests]);
        }
        private NameTest Name(bool attribute)
        {
            var start = position;
            string first;
            if (Current == '|')
            {
                first = "";
            }
            else if (Current == '*')
            {
                first = "*";
                position++;
            }
            else
            {
                first = Identifier();
            }

            Comments();
            if (Current == '|' && position + 1 < source.Length && source[position + 1] == '|')
            {
                throw Error();
            }

            if (Current == '|' && !(position + 1 < source.Length && source[position + 1] == '='))
            {
                position++;
                Comments();
                string local;
                if (Current == '*' && !attribute)
                {
                    local = "*";
                    position++;
                }
                else
                {
                    local = Identifier();
                }

                string? uri;
                if (first == "*")
                {
                    uri = null;
                }
                else if (first == "")
                {
                    uri = "";
                }
                else if (namespaces is null || !namespaces.TryGetValue(first, out uri))
                {
                    throw Error(start);
                }

                return new(local, HtmlNames.Fold(local), uri);
            }
            if (first.Length == 0 || attribute && first == "*")
            {
                throw Error(start);
            }

            return new(first, HtmlNames.Fold(first), attribute ? "" : namespaces?.GetValueOrDefault(""));
        }
        private AttributeTest ParseAttribute()
        {
            position++;
            Skip();
            var name = Name(true);
            Skip();
            if (Current == ']')
            {
                position++;
                return new(name, AttrOp.Present, "");
            }
            AttrOp op;
            if (Current == '=')
            {
                op = AttrOp.Equals;
                position++;
            }
            else if (Current is '~' or '|' or '^' or '$' or '*')
            {
                var first = Current;
                position++;
                if (Current != '=')
                {
                    throw Error();
                }

                position++;
                op = first switch
                {
                    '~' => AttrOp.Token,
                    '|' => AttrOp.DashPrefix,
                    '^' => AttrOp.Prefix,
                    '$' => AttrOp.Suffix,
                    _ => AttrOp.Substring,
                };
            }
            else
            {
                throw Error();
            }

            Skip();
            var value = Current is '\'' or '"' ? String() : Identifier();
            Skip();
            if (Current != ']')
            {
                throw Error();
            }

            position++;
            return new(name, op, value);
        }
        private Test ParsePseudo(bool inNot)
        {
            var start = position++;
            if (Current == ':')
            {
                throw Error(start);
            }

            var pseudo = HtmlNames.Fold(Identifier());
            Comments();
            switch (pseudo)
            {
                case "root":
                case "scope":
                case "empty":
                case "first-child":
                case "last-child":
                case "only-child":
                case "first-of-type":
                case "last-of-type":
                case "only-of-type":
                    if (Current == '(')
                    {
                        throw Error();
                    }

                    return new(Pseudo: pseudo switch
                    {
                        "root" => PseudoKind.Root,
                        "scope" => PseudoKind.Scope,
                        "empty" => PseudoKind.Empty,
                        "first-child" => PseudoKind.FirstChild,
                        "last-child" => PseudoKind.LastChild,
                        "only-child" => PseudoKind.OnlyChild,
                        "first-of-type" => PseudoKind.FirstOfType,
                        "last-of-type" => PseudoKind.LastOfType,
                        _ => PseudoKind.OnlyOfType,
                    });
                case "not":
                    if (inNot)
                    {
                        throw Error(start);
                    }

                    if (Current != '(')
                    {
                        throw Error();
                    }

                    position++;
                    Skip();
                    var compounds = new List<Compound>();
                    while (true)
                    {
                        compounds.Add(ParseCompound(true));
                        Skip();
                        if (Current == ')')
                        {
                            position++;
                            break;
                        }
                        if (Current != ',')
                        {
                            throw Error();
                        }

                        position++;
                        Skip();
                    }
                    return new(Not: [.. compounds]);
                case "nth-child":
                case "nth-last-child":
                case "nth-of-type":
                case "nth-last-of-type":
                    if (Current != '(')
                    {
                        throw Error();
                    }

                    position++;
                    Skip();
                    var (a, b) = Normalize(Nth());
                    Skip();
                    if (Current != ')')
                    {
                        throw Error();
                    }

                    position++;
                    return new(Pseudo: pseudo switch
                    {
                        "nth-child" => PseudoKind.NthChild,
                        "nth-last-child" => PseudoKind.NthLastChild,
                        "nth-of-type" => PseudoKind.NthOfType,
                        _ => PseudoKind.NthLastOfType,
                    }, A: a, B: b);
                default:
                    throw Error(start);
            }
        }
        private (BigInteger, BigInteger) Nth()
        {
            var start = position;
            long sign = 1;
            var signed = Current is '+' or '-';
            if (Current is '+' or '-')
            {
                if (Current == '-')
                {
                    sign = -1;
                }

                position++;
            }
            var hasDigits = char.IsAsciiDigit(Current);
            var number = hasDigits ? Unsigned() : 1;
            if (!IdentifierStart(position))
            {
                if (!hasDigits)
                {
                    throw Error(start);
                }

                return (0, sign * number);
            }
            // A dimension unit or ident token carries decoded CSS escapes. Read it
            // as one token so whitespace can never turn "2 n" into a dimension.
            if (hasDigits && White(Current))
            {
                return (0, sign * number);
            }

            var unitStart = position;
            var unit = Identifier();
            if (!hasDigits && !signed)
            {
                if (Equal(unit, "odd", true))
                {
                    return (2, 1);
                }

                if (Equal(unit, "even", true))
                {
                    return (2, 0);
                }
            }
            if (!hasDigits && !signed && unit.StartsWith("-n", StringComparison.OrdinalIgnoreCase))
            {
                sign = -1;
                unit = unit[1..];
            }
            if (unit.Length == 0 || Lower(unit[0]) != 'n')
            {
                throw Error(unitStart);
            }

            var a = sign * number;
            if (unit.Length > 1)
            {
                if (unit[1] != '-')
                {
                    throw Error(unitStart + 1);
                }

                if (unit.Length > 2)
                {
                    BigInteger b = 0;
                    for (var i = 2; i < unit.Length; i++)
                    {
                        if (!char.IsAsciiDigit(unit[i]))
                        {
                            throw Error(unitStart + i);
                        }

                        b = b * 10 + unit[i] - '0';
                    }
                    return (a, -b);
                }
                Skip();
                if (!char.IsAsciiDigit(Current))
                {
                    throw Error();
                }

                return (a, -Unsigned());
            }
            Skip();
            if (Current == ')')
            {
                return (a, 0);
            }

            if (Current is not ('+' or '-'))
            {
                throw Error();
            }

            long bsign = Current == '-' ? -1 : 1;
            position++;
            Skip();
            if (!char.IsAsciiDigit(Current))
            {
                throw Error();
            }

            return (a, bsign * Unsigned());
        }
        // Trees use Int32 slots/counts. Normalize arbitrary CSS integers at the owned
        // compile boundary to the same progression restricted to possible positions.
        // Matching then needs neither BigInteger allocations nor overflowing subtraction.
        private static (long, long) Normalize((BigInteger A, BigInteger B) formula)
        {
            var (a, b) = formula;
            BigInteger maximum = int.MaxValue;
            if (a.IsZero)
            {
                return b >= 1 && b <= maximum ? (0, (long)b) : (0, 0);
            }

            if (a.Sign > 0)
            {
                if (b > maximum)
                {
                    return (0, 0);
                }

                if (b < 1)
                {
                    b += ((1 - b + a - 1) / a) * a;
                }

                if (b > maximum)
                {
                    return (0, 0);
                }
            }
            else
            {
                if (b < 1)
                {
                    return (0, 0);
                }

                var step = -a;
                if (b > maximum)
                {
                    b -= ((b - maximum + step - 1) / step) * step;
                }

                if (b < 1)
                {
                    return (0, 0);
                }
            }
            return BigInteger.Abs(a) > maximum ? (0, (long)b) : ((long)a, (long)b);
        }
        private BigInteger Unsigned()
        {
            BigInteger number = 0;
            while (char.IsAsciiDigit(Current))
            {
                var digit = Current - '0';
                number = number * 10 + digit;
                position++;
            }
            return number;
        }
        private static bool NameStart(char c) => c == '\0' || c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_' || c >= 0x80;
        private static bool NameChar(char c) => NameStart(c) || c is >= '0' and <= '9' or '-';
        private bool EscapeStart(int index) => index + 1 < source.Length && source[index] == '\\' && source[index + 1] is not ('\r' or '\n' or '\f');
        private bool IdentifierStart(int index)
        {
            if (index >= source.Length)
            {
                return false;
            }

            var c = source[index];
            if (NameStart(c) || EscapeStart(index))
            {
                return true;
            }

            return c == '-' && index + 1 < source.Length && (NameStart(source[index + 1]) || source[index + 1] == '-' || EscapeStart(index + 1));
        }
        private string Identifier()
        {
            if (!IdentifierStart(position))
            {
                throw Error();
            }

            var start = position;
            var decoded = new ValueStringBuilder();
            var hasDecoded = false;
            try
            {
                while (position < source.Length)
                {
                    if (NameChar(Current))
                    {
                        if (InvalidScalar())
                        {
                            if (!hasDecoded)
                            {
                                decoded.Append(source.AsSpan(start, position - start));
                                hasDecoded = true;
                            }
                            decoded.Append('\uFFFD');
                        }
                        else if (hasDecoded)
                        {
                            decoded.Append(Current);
                        }

                        position++;
                    }
                    else if (Current == '\\')
                    {
                        if (!EscapeStart(position))
                        {
                            throw Error();
                        }

                        if (!hasDecoded)
                        {
                            decoded.Append(source.AsSpan(start, position - start));
                            hasDecoded = true;
                        }
                        Escape(ref decoded, false);
                    }
                    else
                    {
                        break;
                    }
                }
                return !hasDecoded ? source.AsSpan(start, position - start).ToString() : decoded.ToString();
            }
            finally
            {
                decoded.Dispose();
            }
        }
        private string String()
        {
            var quote = Current;
            position++;
            var value = new ValueStringBuilder();
            try
            {
                while (position < source.Length)
                {
                    if (Current == quote)
                    {
                        position++;
                        return value.ToString();
                    }
                    if (Current is '\r' or '\n' or '\f')
                    {
                        throw Error();
                    }

                    if (Current == '\\')
                    {
                        Escape(ref value, true);
                    }
                    else
                    {
                        value.Append(InvalidScalar() ? '\uFFFD' : Current);
                        position++;
                    }
                }
                throw Error();
            }
            finally
            {
                value.Dispose();
            }
        }
        private bool InvalidScalar() => Current == '\0' ||
            char.IsHighSurrogate(Current) && (position + 1 == source.Length || !char.IsLowSurrogate(source[position + 1])) ||
            char.IsLowSurrogate(Current) && (position == 0 || !char.IsHighSurrogate(source[position - 1]));
        private void Escape(ref ValueStringBuilder value, bool inString)
        {
            position++;
            if (position == source.Length)
            {
                throw Error(position - 1);
            }

            if (Current is '\r' or '\n' or '\f')
            {
                if (!inString)
                {
                    throw Error(position - 1);
                }

                var first = Current;
                position++;
                if (first == '\r' && Current == '\n')
                {
                    position++;
                }

                return;
            }
            int scalar = 0, count = 0;
            while (count < 6 && Hex(Current) >= 0)
            {
                scalar = scalar * 16 + Hex(Current);
                position++;
                count++;
            }
            if (count == 0)
            {
                value.Append(InvalidScalar() ? '\uFFFD' : Current);
                position++;
                return;
            }
            if (White(Current))
            {
                var first = Current;
                position++;
                if (first == '\r' && Current == '\n')
                {
                    position++;
                }
            }
            if (scalar == 0 || scalar > 0x10FFFF || scalar is >= 0xD800 and <= 0xDFFF)
            {
                scalar = 0xFFFD;
            }

            value.AppendScalar(scalar);
        }
        private static int Hex(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
        private void Comments()
        {
            while (Current == '/' && position + 1 < source.Length && source[position + 1] == '*')
            {
                var start = position;
                position += 2;
                while (position + 1 < source.Length && !(source[position] == '*' && source[position + 1] == '/'))
                {
                    position++;
                }

                if (position + 1 >= source.Length)
                {
                    throw Error(start);
                }

                position += 2;
            }
        }
        private bool Skip()
        {
            var whitespace = false;
            while (true)
            {
                Comments();
                if (!White(Current))
                {
                    return whitespace;
                }

                whitespace = true;
                position++;
            }
        }
    }
}
