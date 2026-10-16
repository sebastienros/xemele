using System.Buffers;
using System.Text;
namespace Xemele.Validation;

internal sealed class RegexParser(string pattern, XmlLocation at, ISchemaOperation operation)
{
    private int offset, depth, scalars;
    private readonly List<RegexState> states = [];
    // Nodes are temporary compile-owned objects; no AST or pattern string survives publication.
    private sealed record Node(RegexSet? Set, List<Node>? Children, bool Alternative = false, int Min = 1, int? Max = 1);
    private void Work(long count = 1)
    {
        if (!operation.Charge(count, at))
        {
            throw SchemaSnapshots.Failure(operation);
        }
    }
    private void Component()
    {
        Work();
        if (!operation.ChargeComponents(1, at))
        {
            throw SchemaSnapshots.Failure(operation);
        }
    }
    private void Limit(string message)
    {
        operation.Refuse("SCHEMA.Limit", message, at);
        throw SchemaSnapshots.Failure(operation);
    }
    private void Syntax()
    {
        var diagnostic = new XmlDiagnostic("REGEX.Syntax", XmlDiagnosticSeverity.Error, "Invalid XML Schema regular expression.", at);
        if (!operation.AddDiagnostic(diagnostic))
        {
            throw SchemaSnapshots.Failure(operation);
        }

        throw new SchemaCompileException([diagnostic]);
    }
    private char Peek => offset < pattern.Length ? pattern[offset] : '\0';
    private int Read()
    {
        Work();
        if (Rune.DecodeFromUtf16(pattern.AsSpan(offset), out var r, out var consumed) != OperationStatus.Done || !RegexCharacters.IsXml(r.Value))
        {
            Syntax();
        }

        offset += consumed;
        if (++scalars > (operation.Limits.TokenCharacters ?? int.MaxValue))
        {
            Limit("Regex pattern character limit exceeded.");
        }

        return r.Value;
    }
    private void Expect(char c)
    {
        if (Peek != c)
        {
            Syntax();
        }

        Read();
    }
    private void Enter()
    {
        Work();
        // The representation's recursive compiler has a finite stack bound even with unlimited policy.
        if (++depth > Math.Min(operation.Limits.Depth ?? 512, 512))
        {
            Limit("Regex nesting capacity exceeded.");
        }
    }
    internal XmlSchemaRegexProgram Compile()
    {
        var node = Expression();
        if (offset != pattern.Length)
        {
            Syntax();
        }

        Add(new(null, -1));
        var start = Emit(node, 0);
        Component(); // owned final program and storage boundary
        var program = new RegexAutomaton([.. states], start, at);
        Work(0);
        return program;
    }
    private Node Expression()
    {
        Enter();
        Component();
        var branches = new List<Node> { Branch() };
        while (Peek == '|')
        {
            Read();
            branches.Add(Branch());
        }
        depth--;
        return new(null, branches, true);
    }
    private Node Branch()
    {
        Component();
        var pieces = new List<Node>();
        while (offset < pattern.Length && Peek is not ('|' or ')'))
        {
            pieces.Add(Piece());
        }

        return new(null, pieces);
    }
    private Node Piece()
    {
        Node atom;
        if (Peek == '(')
        {
            Read();
            atom = Expression();
            Expect(')');
        }
        else
        {
            RegexSet set;
            if (Peek == '[')
            {
                set = Class();
            }
            else if (Peek == '\\')
            {
                var term = Escape(out _);
                Component();
                set = new([term]);
            }
            else if (Peek == '.')
            {
                Read();
                Component();
                set = new([new(10, 10), new(13, 13)], true);
            }
            else
            {
                if (Peek is '?' or '*' or '+' or '{' or '}' or ']' || offset == pattern.Length)
                {
                    Syntax();
                }

                var scalar = Read();
                Component();
                set = new([new(scalar, scalar)]);
            }
            Component();
            atom = new(set, null);
        }
        var min = 1;
        int? max = 1;
        switch (Peek)
        {
            case '?':
                Read();
                min = 0;
                break;
            case '*':
                Read();
                min = 0;
                max = null;
                break;
            case '+':
                Read();
                max = null;
                break;
            case '{':
                Read();
                min = Quantity();
                max = min;
                if (Peek == ',')
                {
                    Read();
                    max = Peek == '}' ? null : Quantity();
                }
                Expect('}');
                if (max < min)
                {
                    Syntax();
                }

                break;
            default:
                return atom;
        }
        Component();
        return new(null, [atom], false, min, max);
    }
    private int Quantity()
    {
        if (Peek is < '0' or > '9')
        {
            Syntax();
        }

        var value = 0;
        while (Peek is >= '0' and <= '9')
        {
            var digit = Read() - '0';
            if (value > (int.MaxValue - digit) / 10)
            {
                Limit("Regex repeat representation exceeded.");
            }

            value = value * 10 + digit;
        }
        return value;
    }
    private RegexSet Class()
    {
        Enter();
        Expect('[');
        var negative = Peek == '^';
        if (negative)
        {
            Read();
        }

        Component();
        var terms = new List<RegexTerm>();
        RegexSet? subtract = null;
        while (offset < pattern.Length && Peek != ']')
        {
            if (Peek == '-' && offset + 1 < pattern.Length && pattern[offset + 1] == '[' && terms.Count > 0)
            {
                Read();
                subtract = Class();
                if (Peek != ']')
                {
                    Syntax();
                }

                break;
            }
            var rawDash = Peek == '-';
            var first = ClassTerm(out var single);
            var beforeSubtraction = Peek == '-' && offset + 1 < pattern.Length && pattern[offset + 1] == '[';
            if (rawDash && terms.Count > 0 && Peek != ']' && !beforeSubtraction)
            {
                Syntax();
            }

            if (Peek == '-' && offset + 1 < pattern.Length && pattern[offset + 1] is not (']' or '[') &&
                !(pattern[offset + 1] == '-' && offset + 2 < pattern.Length && pattern[offset + 2] == '['))
            {
                if (!single || rawDash)
                {
                    Syntax();
                }

                Read();
                var last = ClassTerm(out var lastSingle, true);
                if (!lastSingle || first.First > last.First)
                {
                    Syntax();
                }

                first = new(first.First, last.First);
            }
            Component();
            terms.Add(first);
        }
        if (terms.Count == 0)
        {
            Syntax();
        }

        Expect(']');
        depth--;
        Component();
        return new([.. terms], negative, subtract);
    }
    private RegexTerm ClassTerm(out bool single, bool endpoint = false)
    {
        if (Peek == '\\')
        {
            return Escape(out single);
        }

        if (Peek is '[' or ']' || endpoint && Peek == '-')
        {
            Syntax();
        }

        var value = Read();
        single = true;
        return new(value, value);
    }
    private RegexTerm Escape(out bool single)
    {
        single = false;
        Expect('\\');
        var c = Read();
        if (c is 'n' or 'r' or 't')
        {
            single = true;
            var v = c == 'n' ? 10 : c == 'r' ? 13 : 9;
            return new(v, v);
        }
        if (c <= 127 && "\\|.?*+(){}-[]^".AsSpan().Contains((char)c))
        {
            single = true;
            return new(c, c);
        }
        var inverse = c is 'S' or 'I' or 'C' or 'D' or 'W' or 'P';
        switch (c)
        {
            case 'i':
            case 'I':
                return new(0, 0, Name: 1, Complement: inverse);
            case 'c':
            case 'C':
                return new(0, 0, Name: 2, Complement: inverse);
            case 'd':
            case 'D':
                return new(0, 0, RegexCharacters.Category("Nd"), Complement: inverse);
            case 's':
            case 'S':
                // XML whitespace is a compact mask over category-independent code points.
                return new(0, 0, Name: 3, Complement: inverse);
            case 'w':
            case 'W':
                return new(0, 0, RegexCharacters.Category("P") | RegexCharacters.Category("Z") | RegexCharacters.Category("C"), Complement: !inverse);
            case 'p':
            case 'P':
                Expect('{');
                var start = offset;
                while (offset < pattern.Length && Peek != '}')
                {
                    Read();
                }

                var name = pattern.AsSpan(start, offset - start);
                Expect('}');
                var mask = RegexCharacters.Category(name);
                if (name.SequenceEqual("IsSpecials"))
                {
                    return new(0, 0, Name: 4, Complement: inverse);
                }

                if (mask != 0)
                {
                    return new(0, 0, mask, Complement: inverse);
                }

                if (name.StartsWith("Is") && RegexCharacters.Block(name[2..], operation, at, out var lo, out var hi))
                {
                    return new(lo, hi, Complement: inverse);
                }

                Syntax();
                break;
        }
        Syntax();
        return default;
    }
    private int Add(RegexState state)
    {
        Component();
        var index = states.Count;
        if (index >= int.MaxValue / 5)
        {
            Limit("Regex state representation exceeded.");
        }

        states.Add(state);
        return index;
    }
    private int Emit(Node node, int next)
    {
        Work();
        if (node.Set is not null)
        {
            return Add(new(node.Set, next));
        }

        var children = node.Children!;
        if (node.Min != 1 || node.Max != 1)
        {
            var child = children[0];
            var start = next;
            if (node.Max is null)
            {
                var loop = Add(new(null, -1, next));
                var body = Emit(child, loop);
                states[loop] = new(null, body, next);
                start = loop;
            }
            else
            {
                for (var i = node.Max.Value; i > node.Min; i--)
                {
                    Work();
                    var body = Emit(child, start);
                    start = Add(new(null, body, start));
                }
            }

            for (var i = 0; i < node.Min; i++)
            {
                Work();
                start = Emit(child, start);
            }
            return start;
        }
        if (node.Alternative)
        {
            var start = Emit(children[0], next);
            for (var i = 1; i < children.Count; i++)
            {
                start = Add(new(null, start, Emit(children[i], next)));
            }

            return start;
        }
        for (var i = children.Count - 1; i >= 0; i--)
        {
            next = Emit(children[i], next);
        }

        return next;
    }
}
