
namespace Xemele.XPath.Internal;

// Syntax is completely accepted before resolution. Drafts own only offsets and
// bounded lists; final AST constructors snapshot all persistence boundaries.
internal sealed class XPathCompiler : IXPathCompiler
{
    public XPathAst Compile(string expression, XPathStaticContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (expression.Length > int.MaxValue - context.ContextCharacters || context.Limits.MaxExpressionCharacters is int cap && expression.Length > cap - context.ContextCharacters)
        {
            throw Error("XPATH-COMPILE-LIMIT", new(0, expression.Length));
        }

        var parser = new Parser(expression, context, cancellationToken);
        return parser.Resolve(parser.Parse());
    }
    internal static XPathCompileException Error(string code, XPathSourceSpan span) => new(new(code,
        code == "XPATH-SYNTAX" ? XPathDiagnosticCategory.Syntax : code == "XPATH-COMPILE-LIMIT" ? XPathDiagnosticCategory.CompileLimit : XPathDiagnosticCategory.StaticContext,
        code switch
        {
            "XPATH-SYNTAX" => "Invalid XPath 1.0 syntax.",
            "XPATH-PREFIX" => "Unknown XPath namespace prefix.",
            "XPATH-FUNCTION" => "Unknown XPath function.",
            "XPATH-ARITY" => "Invalid XPath function argument count.",
            _ => "XPath compilation limit exceeded."
        }, span));

    private enum Kind
    {
        Literal, Variable, Unary, Binary, Call, Filter, Path
    }
    private sealed class Draft(Kind kind, XPathSourceSpan span)
    {
        internal Kind Kind = kind;
        internal XPathSourceSpan Span = span;
        internal XPathToken Token;
        internal XPathBinaryOperator Operator;
        internal Draft? Left, Right;
        internal List<Draft> Children = [];
        internal List<Step> Steps = [];
        internal bool Absolute;
        internal XPathAst? Result;
    }
    private sealed class Step(XPathAxis axis, XPathNodeTestKind kind, XPathToken token, XPathSourceSpan span)
    {
        internal XPathAxis Axis = axis;
        internal XPathNodeTestKind Kind = kind;
        internal XPathToken Token = token;
        internal XPathSourceSpan Span = span;
        internal XPathToken? Target;
        internal List<Draft> Predicates = [];
    }
    private enum State
    {
        Start, Unary, Group, Arguments, Argument, Primary, Filter, FilterEnd, Step, Predicates, PredicateEnd, Operand, Binary
    }
    private sealed class Frame(int minimum, int depth)
    {
        internal int Minimum = minimum, Depth = depth;
        internal State State;
        internal XPathToken Start;
        internal Draft? Value, Child;
        internal Step? Step;
        internal XPathBinaryOperator Operator;
    }
    private sealed class Parser
    {
        private readonly string source;
        private readonly XPathStaticContext context;
        private readonly CancellationToken cancellation;
        private XPathLexer lexer;
        private XPathToken current, lookahead, previous;
        private int nodes;
        private readonly Stack<Frame> frames = new();
        internal Parser(string source, XPathStaticContext context, CancellationToken cancellation)
        {
            this.source = source;
            this.context = context;
            this.cancellation = cancellation;
            lexer = new(source, cancellation, context.Limits.MaxTokens, context.ContextTokens);
            current = lexer.Next();
            lookahead = lexer.Next();
        }
        private ReadOnlySpan<char> Text(XPathToken token) => source.AsSpan(token.Start, token.Length);
        private void Next()
        {
            previous = current;
            current = lookahead;
            lookahead = lexer.Next();
        }
        private bool Take(XPathTokenKind kind)
        {
            if (current.Kind != kind)
            {
                return false;
            }

            Next();
            return true;
        }
        private void Require(XPathTokenKind kind)
        {
            if (!Take(kind))
            {
                throw Error("XPATH-SYNTAX", current.Span);
            }
        }
        private XPathSourceSpan Through(int start) => new(start, previous.Start + previous.Length - start);
        private void Charge(XPathSourceSpan span)
        {
            cancellation.ThrowIfCancellationRequested();
            if (nodes == int.MaxValue || context.Limits.MaxAstNodes is int cap && nodes >= cap)
            {
                throw Error("XPATH-COMPILE-LIMIT", span);
            }

            nodes++;
        }
        private Draft Make(Kind kind, XPathSourceSpan span)
        {
            Charge(span);
            return new(kind, span);
        }
        private void Push(int min, int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            if (context.Limits.MaxDepth is int cap && depth > cap)
            {
                throw Error("XPATH-COMPILE-LIMIT", current.Span);
            }

            frames.Push(new(min, depth));
        }
        private void Depth(int depth)
        {
            if (context.Limits.MaxDepth is int cap && depth > cap)
            {
                throw Error("XPATH-COMPILE-LIMIT", current.Span);
            }
        }
        internal Draft Parse()
        {
            Push(1, 1);
            Draft? result = null;
            while (frames.Count != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var f = frames.Peek();
                switch (f.State)
                {
                    case State.Start:
                        f.Start = current;
                        if (Take(XPathTokenKind.Minus))
                        {
                            f.State = State.Unary;
                            Push(7, checked(f.Depth + 1));
                            break;
                        }
                        if (current.Kind is XPathTokenKind.Slash or XPathTokenKind.DoubleSlash || IsStep())
                        {
                            f.Value = Make(Kind.Path, current.Span);
                            if (current.Kind is XPathTokenKind.Slash or XPathTokenKind.DoubleSlash)
                            {
                                f.Value.Absolute = true;
                                var slash = current;
                                Next();
                                if (slash.Kind == XPathTokenKind.DoubleSlash)
                                {
                                    Descendant(f.Value, slash.Span, f.Depth);
                                }
                                else if (!IsStep())
                                {
                                    f.Value.Span = slash.Span;
                                    f.State = State.Operand;
                                    break;
                                }
                            }
                            f.State = State.Step;
                            break;
                        }
                        if (current.Kind is XPathTokenKind.Literal or XPathTokenKind.Number)
                        {
                            f.Value = Make(Kind.Literal, current.Span);
                            f.Value.Token = current;
                            Next();
                            f.State = State.Primary;
                            break;
                        }
                        if (Take(XPathTokenKind.Dollar))
                        {
                            if (current.Kind != XPathTokenKind.Name || Text(current).EndsWith(":*", StringComparison.Ordinal))
                            {
                                throw Error("XPATH-SYNTAX", current.Span);
                            }

                            var name = current;
                            Next();
                            f.Value = Make(Kind.Variable, Through(f.Start.Start));
                            f.Value.Token = name;
                            f.State = State.Primary;
                            break;
                        }
                        if (Take(XPathTokenKind.Open))
                        {
                            f.State = State.Group;
                            Push(1, checked(f.Depth + 1));
                            break;
                        }
                        if (current.Kind == XPathTokenKind.Name && lookahead.Kind == XPathTokenKind.Open && !Text(current).EndsWith(":*", StringComparison.Ordinal))
                        {
                            f.Value = Make(Kind.Call, current.Span);
                            f.Value.Token = current;
                            Next();
                            Require(XPathTokenKind.Open);
                            f.State = State.Arguments;
                            break;
                        }
                        throw Error("XPATH-SYNTAX", current.Span);
                    case State.Unary:
                        f.Value = Make(Kind.Unary, Through(f.Start.Start));
                        f.Value.Left = f.Child;
                        f.State = State.Operand;
                        break;
                    case State.Group:
                        Require(XPathTokenKind.Close);
                        f.Value = f.Child;
                        f.State = State.Primary;
                        break;
                    case State.Arguments:
                        if (Take(XPathTokenKind.Close))
                        {
                            f.Value!.Span = Through(f.Start.Start);
                            f.State = State.Primary;
                        }
                        else
                        {
                            f.State = State.Argument;
                            Push(1, checked(f.Depth + 1));
                        }
                        break;
                    case State.Argument:
                        f.Value!.Children.Add(f.Child!);
                        if (Take(XPathTokenKind.Comma))
                        {
                            Push(1, checked(f.Depth + 1));
                        }
                        else
                        {
                            Require(XPathTokenKind.Close);
                            f.Value.Span = Through(f.Start.Start);
                            f.State = State.Primary;
                        }
                        break;
                    case State.Primary:
                        if (current.Kind == XPathTokenKind.Bracket)
                        {
                            var filter = Make(Kind.Filter, Through(f.Start.Start));
                            filter.Left = f.Value;
                            f.Value = filter;
                            f.State = State.Filter;
                        }
                        else
                        {
                            FinishPrimary(f);
                        }

                        break;
                    case State.Filter:
                        if (Take(XPathTokenKind.Bracket))
                        {
                            f.State = State.FilterEnd;
                            Depth(checked(f.Depth + 1));
                            Push(1, checked(f.Depth + 2));
                        }
                        else
                        {
                            f.Value!.Span = Through(f.Start.Start);
                            FinishPrimary(f);
                        }
                        break;
                    case State.FilterEnd:
                        Require(XPathTokenKind.EndBracket);
                        f.Value!.Children.Add(f.Child!);
                        f.State = State.Filter;
                        break;
                    case State.Step:
                        Depth(checked(f.Depth + 1));
                        f.Step = ReadStep();
                        f.State = State.Predicates;
                        break;
                    case State.Predicates:
                        if (current.Kind == XPathTokenKind.Bracket && f.Step!.Token.Kind is not (XPathTokenKind.Dot or XPathTokenKind.Parent))
                        {
                            Next();
                            f.State = State.PredicateEnd;
                            Depth(checked(f.Depth + 2));
                            Push(1, checked(f.Depth + 3));
                        }
                        else
                        {
                            f.Step!.Span = Through(f.Step.Span.Start);
                            f.Value!.Steps.Add(f.Step);
                            f.Value.Span = Through(f.Start.Start);
                            if (current.Kind is XPathTokenKind.Slash or XPathTokenKind.DoubleSlash)
                            {
                                var slash = current;
                                Next();
                                if (slash.Kind == XPathTokenKind.DoubleSlash)
                                {
                                    Descendant(f.Value, slash.Span, f.Depth);
                                }

                                f.State = State.Step;
                            }
                            else
                            {
                                f.State = State.Operand;
                            }
                        }
                        break;
                    case State.PredicateEnd:
                        Require(XPathTokenKind.EndBracket);
                        f.Step!.Predicates.Add(f.Child!);
                        f.State = State.Predicates;
                        break;
                    case State.Operand:
                        if (Binary(out var op, out var precedence) && precedence >= f.Minimum)
                        {
                            f.Operator = op;
                            Next();
                            f.State = State.Binary;
                            Push(precedence + 1, f.Depth);
                        }
                        else
                        {
                            result = f.Value!;
                            frames.Pop();
                            if (frames.TryPeek(out var parent))
                            {
                                parent.Child = result;
                            }
                        }
                        break;
                    case State.Binary:
                        var binary = Make(Kind.Binary, Through(f.Start.Start));
                        binary.Left = f.Value;
                        binary.Right = f.Child;
                        binary.Operator = f.Operator;
                        f.Value = binary;
                        f.State = State.Operand;
                        break;
                }
            }
            if (current.Kind != XPathTokenKind.End)
            {
                throw Error("XPATH-SYNTAX", current.Span);
            }

            return result!;
        }
        private void FinishPrimary(Frame f)
        {
            if (current.Kind is XPathTokenKind.Slash or XPathTokenKind.DoubleSlash)
            {
                var path = Make(Kind.Path, Through(f.Start.Start));
                path.Left = f.Value;
                f.Value = path;
                var slash = current;
                Next();
                if (slash.Kind == XPathTokenKind.DoubleSlash)
                {
                    Descendant(path, slash.Span, f.Depth);
                }

                f.State = State.Step;
            }
            else
            {
                f.State = State.Operand;
            }
        }
        private bool IsStep() => current.Kind is XPathTokenKind.Dot or XPathTokenKind.Parent or XPathTokenKind.At or XPathTokenKind.Star || current.Kind == XPathTokenKind.Name &&
            (lookahead.Kind != XPathTokenKind.Open || NodeKind(Text(current)) is not null);
        private void Descendant(Draft path, XPathSourceSpan span, int depth)
        {
            Depth(checked(depth + 1));
            Charge(span);
            Charge(span);
            path.Steps.Add(new(XPathAxis.DescendantOrSelf, XPathNodeTestKind.Node, default, span));
        }
        private Step ReadStep()
        {
            var start = current;
            var axis = XPathAxis.Child;
            XPathNodeTestKind kind;
            XPathToken? target = null;
            if (current.Kind is XPathTokenKind.Dot or XPathTokenKind.Parent)
            {
                axis = current.Kind == XPathTokenKind.Dot ? XPathAxis.Self : XPathAxis.Parent;
                Next();
                kind = XPathNodeTestKind.Node;
            }
            else
            {
                if (Take(XPathTokenKind.At))
                {
                    axis = XPathAxis.Attribute;
                }
                else if (current.Kind == XPathTokenKind.Name && lookahead.Kind == XPathTokenKind.Axis)
                {
                    axis = Axis(Text(current)) ?? throw Error("XPATH-SYNTAX", current.Span);
                    Next();
                    Require(XPathTokenKind.Axis);
                }
                var test = current;
                if (Take(XPathTokenKind.Star))
                {
                    kind = XPathNodeTestKind.AnyPrincipal;
                }
                else if (current.Kind == XPathTokenKind.Name)
                {
                    Next();
                    if (current.Kind == XPathTokenKind.Open)
                    {
                        kind = NodeKind(Text(test)) ?? throw Error("XPATH-SYNTAX", test.Span);
                        Next();
                        if (kind == XPathNodeTestKind.ProcessingInstruction && current.Kind == XPathTokenKind.Literal)
                        {
                            target = current;
                            Next();
                        }
                        Require(XPathTokenKind.Close);
                    }
                    else
                    {
                        kind = Text(test).EndsWith(":*", StringComparison.Ordinal) ? XPathNodeTestKind.NamespaceWildcard : XPathNodeTestKind.Name;
                    }
                }
                else
                {
                    throw Error("XPATH-SYNTAX", current.Span);
                }

                Charge(test.Span);
                Charge(start.Span);
                return new(axis, kind, test, Through(start.Start))
                {
                    Target = target
                };
            }
            Charge(start.Span);
            Charge(start.Span);
            return new(axis, kind, start, Through(start.Start));
        }
        private bool Binary(out XPathBinaryOperator op, out int precedence)
        {
            (XPathBinaryOperator, int)? value = current.Kind switch
            {
                XPathTokenKind.Equal => (XPathBinaryOperator.Equal, 3),
                XPathTokenKind.NotEqual => (XPathBinaryOperator.NotEqual, 3),
                XPathTokenKind.Less => (XPathBinaryOperator.Less, 4),
                XPathTokenKind.LessEqual => (XPathBinaryOperator.LessOrEqual, 4),
                XPathTokenKind.Greater => (XPathBinaryOperator.Greater, 4),
                XPathTokenKind.GreaterEqual => (XPathBinaryOperator.GreaterOrEqual, 4),
                XPathTokenKind.Plus => (XPathBinaryOperator.Add, 5),
                XPathTokenKind.Minus => (XPathBinaryOperator.Subtract, 5),
                XPathTokenKind.Star => (XPathBinaryOperator.Multiply, 6),
                XPathTokenKind.Union => (XPathBinaryOperator.Union, 8),
                XPathTokenKind.Name => Text(current) switch { "or" => (XPathBinaryOperator.Or, 1), "and" => (XPathBinaryOperator.And, 2), "div" => (XPathBinaryOperator.Divide, 6), "mod" => (XPathBinaryOperator.Modulo, 6), _ => null },
                _ => null
            };
            (op, precedence) = value ?? default;
            return value is not null;
        }
        internal XPathAst Resolve(Draft root)
        {
            var pending = new Stack<(Draft Draft, bool Ready)>();
            pending.Push((root, false));
            while (pending.TryPop(out var item))
            {
                cancellation.ThrowIfCancellationRequested();
                var d = item.Draft;
                if (!item.Ready)
                {
                    pending.Push((d, true));
                    // Reverse pushes preserve original left-to-right resolution order.
                    for (var s = d.Steps.Count - 1; s >= 0; s--)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        for (var p = d.Steps[s].Predicates.Count - 1; p >= 0; p--)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            pending.Push((d.Steps[s].Predicates[p], false));
                        }
                    }
                    for (var i = d.Children.Count - 1; i >= 0; i--)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        pending.Push((d.Children[i], false));
                    }
                    if (d.Right is not null)
                    {
                        pending.Push((d.Right, false));
                    }

                    if (d.Left is not null)
                    {
                        pending.Push((d.Left, false));
                    }

                    continue;
                }
                switch (d.Kind)
                {
                    case Kind.Literal:
                        d.Result = new XPathLiteral(d.Token.Kind == XPathTokenKind.Literal ? XPathValue.FromString(Own(d.Token, true)) : XPathValue.FromNumber(XPathCompileNumber.Parse(Text(d.Token), cancellation)), d.Span);
                        break;
                    case Kind.Variable:
                        d.Result = new XPathVariable(Name(d.Token, d.Span), d.Span, cancellation);
                        break;
                    case Kind.Unary:
                        d.Result = new XPathUnaryMinus(d.Left!.Result!, d.Span);
                        break;
                    case Kind.Binary:
                        d.Result = new XPathBinary(d.Operator, d.Left!.Result!, d.Right!.Result!, d.Span);
                        break;
                    case Kind.Call:
                        var name = Name(d.Token, d.Span);
                        var signature = XPathResolution.FindFunction(context.Functions, name, cancellation) ?? throw Error("XPATH-FUNCTION", d.Span);
                        if (d.Children.Count < signature.MinArguments || signature.MaxArguments is int max && d.Children.Count > max)
                        {
                            throw Error("XPATH-ARITY", d.Span);
                        }

                        d.Result = new XPathCall(signature, Results(d.Children), d.Span, cancellation);
                        break;
                    case Kind.Filter:
                        d.Result = new XPathFilter(d.Left!.Result!, Results(d.Children), d.Span, cancellation);
                        break;
                    case Kind.Path:
                        var steps = new XPathStep[d.Steps.Count];
                        for (var i = 0; i < steps.Length; i++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            var s = d.Steps[i];
                            var test = new XPathNodeTest(s.Kind, s.Kind is XPathNodeTestKind.Name or XPathNodeTestKind.NamespaceWildcard ? Name(s.Token, s.Token.Span) : default, s.Target is XPathToken target ? Own(target, true) : null, cancellation);
                            steps[i] = new(s.Axis, test, Results(s.Predicates), s.Span, cancellation);
                        }
                        d.Result = new XPathPath(d.Absolute, d.Left?.Result, steps, d.Span, cancellation);
                        break;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            return root.Result!;
        }
        private XPathAst[] Results(List<Draft> drafts)
        {
            var result = new XPathAst[drafts.Count];
            for (var i = 0; i < result.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                result[i] = drafts[i].Result!;
            }
            return result;
        }
        private string Own(XPathToken token, bool quoted = false)
        {
            return Own(token.Start + (quoted ? 1 : 0), token.Length - (quoted ? 2 : 0));
        }
        private string Own(int start, int length) => string.Create(length, (source, start, cancellation), static (destination, state) =>
        {
            for (var i = 0; i < destination.Length;)
            {
                state.cancellation.ThrowIfCancellationRequested();
                var count = Math.Min(256, destination.Length - i);
                state.source.AsSpan(state.start + i, count).CopyTo(destination.Slice(i, count));
                i += count;
            }
            state.cancellation.ThrowIfCancellationRequested();
        });
        private XPathExpandedName Name(XPathToken token, XPathSourceSpan diagnosticSpan)
        {
            var text = Text(token);
            var colon = XPathResolution.FindColon(text, cancellation);
            if (colon < 0)
            {
                return new("", Own(token.Start, token.Length), cancellation);
            }

            var prefix = text[..colon];
            string? uri = null;
            // Borrowed ordinal probe: avoid a transient prefix string per reference.
            foreach (var binding in context.Namespaces)
            {
                cancellation.ThrowIfCancellationRequested();
                if (XPathResolution.Equal(prefix, binding.Key, cancellation))
                {
                    uri = binding.Value;
                    break;
                }
            }
            if (uri is null)
            {
                throw Error("XPATH-PREFIX", diagnosticSpan);
            }

            return text[(colon + 1)..].SequenceEqual("*") ? XPathExpandedName.NamespaceWildcard(uri) : new(uri, Own(token.Start + colon + 1, token.Length - colon - 1), cancellation);
        }
    }
    internal static XPathNodeTestKind? NodeKind(ReadOnlySpan<char> text) => text switch { "node" => XPathNodeTestKind.Node, "text" => XPathNodeTestKind.Text, "comment" => XPathNodeTestKind.Comment, "processing-instruction" => XPathNodeTestKind.ProcessingInstruction, _ => null };
    internal static XPathAxis? Axis(ReadOnlySpan<char> text) => text switch
    {
        "child" => XPathAxis.Child,
        "descendant" => XPathAxis.Descendant,
        "parent" => XPathAxis.Parent,
        "ancestor" => XPathAxis.Ancestor,
        "following-sibling" => XPathAxis.FollowingSibling,
        "preceding-sibling" => XPathAxis.PrecedingSibling,
        "following" => XPathAxis.Following,
        "preceding" => XPathAxis.Preceding,
        "attribute" => XPathAxis.Attribute,
        "namespace" => XPathAxis.Namespace,
        "self" => XPathAxis.Self,
        "descendant-or-self" => XPathAxis.DescendantOrSelf,
        "ancestor-or-self" => XPathAxis.AncestorOrSelf,
        _ => null
    };
}
