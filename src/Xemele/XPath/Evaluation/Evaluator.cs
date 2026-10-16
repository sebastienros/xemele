namespace Xemele.XPath.Internal;

// Iterator continuations run on an explicit linked frame chain. Every child
// request suspends its parent; no caller-sized AST/predicate recursion occurs.
internal sealed class XPathEvaluator(XPathNavigator navigator, XPathDynamicContext context, XPathBudget shared, XPathSourceSpan entrySpan)
{
    private static readonly XPathCoreFunctions Core = new();
    private Binding? variables;
    private sealed class Binding(XPathExpandedName name, XPathValue value, Binding? next)
    {
        internal readonly XPathExpandedName Name = name;
        internal readonly XPathValue Value = value;
        internal readonly Binding? Next = next;
    }
    private sealed class Request(XPathAst ast, XPathFocus focus)
    {
        internal readonly XPathAst Ast = ast;
        internal readonly XPathFocus Focus = focus;
        internal XPathValue Result;
    }
    private sealed class Frame(IEnumerator<Request> continuation, Frame? parent)
    {
        internal readonly IEnumerator<Request> Continuation = continuation;
        internal readonly Frame? Parent = parent;
    }
    internal XPathItem Bind(XPathItem item) => navigator.Rebind(item, shared, true);
    internal static void ValidateNode(XmlNode node, IXPathBudget budget)
    {
        if (node.IsDefault || node.Kind == XmlNodeKind.Fragment)
        {
            throw new ArgumentException("Require connected context.", nameof(node));
        }

        var top = node;
        while (true)
        {
            budget.ChargeOperations(1);
            var parent = top.Parent;
            if (parent.IsDefault)
            {
                break;
            }

            top = parent;
        }
        if (top.Kind != XmlNodeKind.Document)
        {
            throw new ArgumentException("Detached context.", nameof(node));
        }
    }
    internal XPathItem BindNode(XmlNode node)
    {
        var kind = node.Kind switch
        {
            XmlNodeKind.Document => XPathItemKind.Root,
            XmlNodeKind.Element => XPathItemKind.Element,
            XmlNodeKind.Text or XmlNodeKind.CData => XPathItemKind.Text,
            XmlNodeKind.Comment => XPathItemKind.Comment,
            XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
            _ => throw new ArgumentException("Unsupported context.", nameof(node))
        };
        if (kind == XPathItemKind.Text)
        {
            while (XPathItem.IsText(node.PreviousSibling))
            {
                shared.ChargeOperations(1);
                node = node.PreviousSibling;
            }
        }

        return Bind(new XPathItem(new(kind, node, default, null), ""));
    }
    internal XPathValue Evaluate(XPathAst ast, XPathItem item)
    {
        if (context.Variables.Count != 0)
        {
            var entryBudget = new LocatedBudget(shared, entrySpan);
            foreach (var binding in context.Variables)
            {
                entryBudget.ChargeOperations(1);
                variables = new(binding.Key, RebindValue(binding.Value, entryBudget), variables);
            }
        }
        var root = new Request(ast, new(item, 1, 1));
        Frame? frame = new(Run(root).GetEnumerator(), null);
        try
        {
            while (frame is not null)
            {
                shared.ChargeOperations(0);
                if (frame.Continuation.MoveNext())
                {
                    frame = new(Run(frame.Continuation.Current).GetEnumerator(), frame);
                }
                else
                {
                    frame.Continuation.Dispose();
                    frame = frame.Parent;
                }
            }
        }
        finally
        {
            while (frame is not null)
            {
                frame.Continuation.Dispose();
                frame = frame.Parent;
            }
        }
        return root.Result;
    }
    private IEnumerable<Request> Run(Request request)
    {
        var ast = request.Ast;
        var focus = request.Focus;
        var budget = new LocatedBudget(shared, ast.Span);
        budget.ChargeOperations(1);
        switch (ast)
        {
            case XPathLiteral literal:
                request.Result = RebindValue(literal.Value, budget);
                break;
            case XPathVariable variable:
                var found = variables;
                while (found is not null)
                {
                    budget.ChargeOperations(1);
                    if (SameName(found.Name, variable.Name, budget))
                    {
                        break;
                    }

                    found = found.Next;
                }
                if (found is null)
                {
                    throw Error("XPATH-VARIABLE", ast.Span);
                }

                request.Result = found.Value;
                break;
            case XPathUnaryMinus unary:
                var operand = new Request(unary.Operand, focus);
                yield return operand;
                request.Result = XPathValue.FromNumber(-Number(operand.Result, focus, budget));
                break;
            case XPathBinary binary:
                var left = new Request(binary.Left, focus);
                yield return left;
                if (binary.Operator == XPathBinaryOperator.Or && left.Result.AsBoolean())
                {
                    request.Result = XPathValue.FromBoolean(true);
                    break;
                }
                if (binary.Operator == XPathBinaryOperator.And && !left.Result.AsBoolean())
                {
                    request.Result = XPathValue.FromBoolean(false);
                    break;
                }
                var right = new Request(binary.Right, focus);
                yield return right;
                request.Result = Binary(binary.Operator, left.Result, right.Result, focus, budget, ast.Span);
                break;
            case XPathCall call:
                budget.ChargeOperations(call.Arguments.Count);
                var arguments = new XPathValue[call.Arguments.Count];
                for (var i = 0; i < arguments.Length; i++)
                {
                    budget.ChargeOperations(1);
                    var argument = new Request(call.Arguments[i], focus);
                    yield return argument;
                    budget.ChargeOperations(1);
                    arguments[i] = argument.Result;
                }
                if (call.Signature.Name.NamespaceUri.Length == 0)
                {
                    request.Result = CallCore(call.Signature, focus, arguments, budget);
                }
                else
                {
                    XPathExtensionFunction? function = null;
                    foreach (var binding in context.Functions)
                    {
                        budget.ChargeOperations(1);
                        if (SameName(binding.Key, call.Signature.Name, budget))
                        {
                            function = binding.Value;
                            break;
                        }
                    }
                    if (function is null)
                    {
                        throw Error("XPATH-EXTENSION", ast.Span);
                    }

                    budget.ChargeOperations(1);
                    // Keep dispatch entirely outside all engine exception handlers.
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        arguments[i] = arguments[i].Detached();
                    }

                    var returned = function(new XPathFunctionContext(focus, budget), Array.AsReadOnly(arguments));
                    budget.ChargeOperations(0);
                    request.Result = RebindValue(returned, budget);
                }
                break;
            case XPathFilter filter:
                var input = new Request(filter.Input, focus);
                yield return input;
                var filtered = new XPathNodeSelection();
                filtered.Adopt(AsNodeList(Nodes(input.Result, ast.Span)), budget);
                var scratchSelection = new XPathNodeSelection();
                foreach (var predicate in filter.Predicates)
                {
                    budget.ChargeOperations(1);
                    var next = scratchSelection;
                    next.Reset();
                    var size = filtered.Count;
                    for (var i = 0; i < size; i++)
                    {
                        budget.ChargeOperations(1);
                        var candidate = new Request(predicate, new(navigator.ItemAt(filtered[i]), i + 1, size, filtered[i]));
                        yield return candidate;
                        if (Accept(candidate.Result, i + 1))
                        {
                            next.Add(filtered[i], budget);
                        }
                    }
                    next.Flush(budget);
                    scratchSelection = filtered;
                    filtered = next;
                }
                request.Result = Materialize(filtered, budget);
                break;
            case XPathPath path:
                XPathNodeList current;
                if (path.Input is not null)
                {
                    var start = new Request(path.Input, focus);
                    yield return start;
                    current = AsNodeList(Nodes(start.Result, ast.Span));
                    budget.CheckNodeSetSize(current.Count);
                    budget.ChargeOperations(2L * current.Count);
                }
                else
                {
                    var contextIndex = path.Absolute ? 0 : FocusIndex(focus);
                    budget.CheckNodeSetSize(1);
                    budget.ChargeOperations(1);
                    current = new(navigator, [contextIndex], 1);
                }
                XPathNodeSelection? selected = null, spare = null;
                foreach (var step in path.Steps)
                {
                    if (step.Predicates.Count == 0)
                    {
                        current = EvaluateStep(current, step);
                        continue;
                    }

                    BeginStep(step);
                    var merged = new XPathNodeSelection();
                    merged.Presize(current.Count);
                    selected ??= new();
                    LocatedBudget? stepBudget = step.Predicates.Count == 0 ? null : new LocatedBudget(shared, step.Span);
                    for (var j = 0; j < current.Count; j++)
                    {
                        var at = current.IndexAt(j);
                        selected.Reset();
                        SelectStep(at, step, selected);
                        foreach (var predicate in step.Predicates)
                        {
                            stepBudget!.ChargeOperations(1);
                            spare ??= new();
                            var next = spare;
                            next.Reset();
                            var size = selected.Count;
                            for (var i = 0; i < size; i++)
                            {
                                stepBudget.ChargeOperations(1);
                                var candidate = new Request(predicate, new(navigator.ItemAt(selected[i]), i + 1, size, selected[i]));
                                yield return candidate;
                                if (Accept(candidate.Result, i + 1))
                                {
                                    next.Add(selected[i], stepBudget);
                                }
                            }
                            next.Flush(stepBudget);
                            spare = selected;
                            selected = next;
                        }
                        MergeSelected(step, selected, merged);
                    }
                    current = FinishStep(step, merged);
                }
                request.Result = Materialize(current, budget);
                break;
            default:
                throw new InvalidOperationException("Unknown XPath AST.");
        }
    }
    private static bool Accept(XPathValue value, int position) => value.Kind == XPathValueKind.Number ? value.AsNumber() == position : value.AsBoolean();
    private XPathNodeList Normalize(IReadOnlyList<XPathItem> input, IXPathBudget budget)
    {
        try
        {
            return navigator.Normalize(input, budget);
        }
        catch (XPathEvaluationException error) { throw Locate(error, budget is LocatedBudget located ? located.Span : entrySpan); }
    }
    private XPathValue Materialize(XPathNodeSelection nodes, IXPathBudget budget)
    {
        try
        {
            return XPathValue.FromSnapshotNodeSet(navigator.NormalizeSelection(nodes, budget), budget);
        }
        catch (XPathEvaluationException error) { throw Locate(error, budget is LocatedBudget located ? located.Span : entrySpan); }
    }
    private XPathValue Materialize(IReadOnlyList<XPathItem> nodes, IXPathBudget budget) => XPathValue.FromSnapshotNodeSet(Normalize(nodes, budget), budget);
    // Evaluator node-sets are normalized lists over this navigator's snapshot; other
    // sequences are resolved to snapshot indexes without charging.
    private XPathNodeList AsNodeList(IReadOnlyList<XPathItem> nodes)
    {
        if (nodes is XPathNodeList list && ReferenceEquals(list.Navigator, navigator))
        {
            return list;
        }

        var indexes = new int[nodes.Count];
        for (var i = 0; i < indexes.Length; i++)
        {
            indexes[i] = navigator.IndexOfUnmetered(nodes[i]);
        }

        return new(navigator, indexes, indexes.Length);
    }
    private int FocusIndex(XPathFocus focus) => focus.Index >= 0 ? focus.Index : navigator.IndexOfUnmetered(focus.Item);
    private XPathNodeList EvaluateStep(XPathNodeList current, XPathStep step)
    {
        try
        {
            return navigator.EvaluateStep(current, step, shared);
        }
        catch (XPathEvaluationException error) { throw Locate(error, step.Span); }
    }
    private void BeginStep(XPathStep step)
    {
        try
        {
            shared.ChargeOperations(1);
        }
        catch (XPathEvaluationException error) { throw Locate(error, step.Span); }
    }
    private void SelectStep(int at, XPathStep step, XPathNodeSelection selected)
    {
        try
        {
            navigator.SelectStep(at, step, shared, selected);
            selected.Flush(shared);
        }
        catch (XPathEvaluationException error) { throw Locate(error, step.Span); }
    }
    private void MergeSelected(XPathStep step, XPathNodeSelection selected, XPathNodeSelection merged)
    {
        try
        {
            if (step.Axis is not (XPathAxis.Ancestor or XPathAxis.AncestorOrSelf or XPathAxis.Preceding or XPathAxis.PrecedingSibling) &&
                merged.TakeIncreasing(selected, navigator, shared))
            {
                return;
            }

            for (var i = 0; i < selected.Count; i++)
            {
                merged.AddUnique(selected[i], navigator, shared);
            }
        }
        catch (XPathEvaluationException error) { throw Locate(error, step.Span); }
    }
    private XPathNodeList FinishStep(XPathStep step, XPathNodeSelection merged)
    {
        try
        {
            merged.Flush(shared);
            var normalized = navigator.NormalizeSelection(merged, shared);
            shared.CheckNodeSetSize(normalized.Count);
            shared.ChargeOperations(2L * normalized.Count);
            return normalized;
        }
        catch (XPathEvaluationException error) { throw Locate(error, step.Span); }
    }
    private XPathValue RebindValue(XPathValue value, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (value.Kind == XPathValueKind.String)
        {
            Scan(value.AsString(), budget);
        }

        if (value.Kind != XPathValueKind.NodeSet)
        {
            return value;
        }

        var nodes = value.AsNodeSet();
        budget.CheckNodeSetSize(nodes.Count);
        return XPathValue.FromSnapshotNodeSet(Normalize(nodes, budget), budget);
    }
    private static IReadOnlyList<XPathItem> Nodes(XPathValue value, XPathSourceSpan span)
    {
        if (value.Kind != XPathValueKind.NodeSet)
        {
            throw Error("XPATH-TYPE", span);
        }

        return value.AsNodeSet();
    }
    private static void Scan(string text, IXPathBudget budget)
    {
        budget.CheckStringSize(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            budget.ChargeOperations(1);
        }
    }
    private static bool SameName(XPathExpandedName a, XPathExpandedName b, IXPathBudget budget) => Equal(a.NamespaceUri, b.NamespaceUri, budget) && Equal(a.LocalName, b.LocalName, budget);
    private static bool Equal(string a, string b, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            budget.ChargeOperations(2);
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }
    private double Number(XPathValue value, XPathFocus focus, IXPathBudget budget) => Convert("number", value, focus, budget).AsNumber();
    private string Text(XPathValue value, XPathFocus focus, IXPathBudget budget) => Convert("string", value, focus, budget).AsString();
    private XPathValue Convert(string name, XPathValue value, XPathFocus focus, IXPathBudget budget) => CallCore(XPathCoreSignatures.ByName[new("", name)], focus, new SingleArgument(value), budget);
    private XPathValue CallCore(XPathFunctionSignature signature, XPathFocus focus, IReadOnlyList<XPathValue> arguments, IXPathBudget budget)
    {
        try
        {
            return Core.Invoke(signature, focus, arguments, navigator, budget);
        }
        catch (XPathEvaluationException error) { throw Locate(error, budget is LocatedBudget located ? located.Span : entrySpan); }
    }
    private XPathValue Binary(XPathBinaryOperator op, XPathValue left, XPathValue right, XPathFocus focus, IXPathBudget budget, XPathSourceSpan span)
    {
        if (op is XPathBinaryOperator.And or XPathBinaryOperator.Or)
        {
            return XPathValue.FromBoolean(right.AsBoolean());
        }

        if (op == XPathBinaryOperator.Union)
        {
            var result = new XPathNodeSelection();
            result.Adopt(AsNodeList(Nodes(left, span)), budget);
            var other = AsNodeList(Nodes(right, span));
            for (var i = 0; i < other.Count; i++)
            {
                result.AddUnique(other.IndexAt(i), navigator, budget);
            }

            result.Flush(budget);
            return Materialize(result, budget);
        }
        if (op is XPathBinaryOperator.Equal or XPathBinaryOperator.NotEqual or XPathBinaryOperator.Less or XPathBinaryOperator.LessOrEqual or XPathBinaryOperator.Greater or XPathBinaryOperator.GreaterOrEqual)
        {
            return XPathValue.FromBoolean(Compare(op, left, right, focus, budget));
        }

        double a = Number(left, focus, budget), b = Number(right, focus, budget);
        return XPathValue.FromNumber(op switch
        {
            XPathBinaryOperator.Add => a + b,
            XPathBinaryOperator.Subtract => a - b,
            XPathBinaryOperator.Multiply => a * b,
            XPathBinaryOperator.Divide => a / b,
            XPathBinaryOperator.Modulo => a % b,
            _ => throw new InvalidOperationException("Unknown operator.")
        });
    }
    private bool Compare(XPathBinaryOperator op, XPathValue left, XPathValue right, XPathFocus focus, IXPathBudget budget)
    {
        var equality = op is XPathBinaryOperator.Equal or XPathBinaryOperator.NotEqual;
        bool leftSet = left.Kind == XPathValueKind.NodeSet, rightSet = right.Kind == XPathValueKind.NodeSet;
        var booleanPair = left.Kind == XPathValueKind.Boolean || right.Kind == XPathValueKind.Boolean;
        if (booleanPair && (equality || leftSet || rightSet))
        {
            budget.ChargeOperations(2);
            return Numeric(op, left.AsBoolean() ? 1 : 0, right.AsBoolean() ? 1 : 0);
        }
        if (leftSet || rightSet)
        {
            int leftCount = leftSet ? left.AsNodeSet().Count : 1, rightCount = rightSet ? right.AsNodeSet().Count : 1;
            var numeric = !equality || left.Kind == XPathValueKind.Number || right.Kind == XPathValueKind.Number;
            for (var i = 0; i < leftCount; i++)
            {
                budget.ChargeOperations(1);
                var a = leftSet ? XPathValue.FromString(left.AsNodeSet()[i].StringValue) : left;
                for (var j = 0; j < rightCount; j++)
                {
                    budget.ChargeOperations(1);
                    var b = rightSet ? XPathValue.FromString(right.AsNodeSet()[j].StringValue) : right;
                    if (numeric ? Numeric(op, Number(a, focus, budget), Number(b, focus, budget)) : StringCompare(op, Text(a, focus, budget), Text(b, focus, budget), budget))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        if (!equality || left.Kind == XPathValueKind.Number || right.Kind == XPathValueKind.Number)
        {
            return Numeric(op, Number(left, focus, budget), Number(right, focus, budget));
        }

        return StringCompare(op, Text(left, focus, budget), Text(right, focus, budget), budget);
    }
    private static bool StringCompare(XPathBinaryOperator op, string a, string b, IXPathBudget budget)
    {
        var same = Equal(a, b, budget);
        return op == XPathBinaryOperator.Equal ? same : !same;
    }
    private static bool Numeric(XPathBinaryOperator op, double a, double b) => op switch
    {
        XPathBinaryOperator.Equal => a == b,
        XPathBinaryOperator.NotEqual => a != b,
        XPathBinaryOperator.Less => a < b,
        XPathBinaryOperator.LessOrEqual => a <= b,
        XPathBinaryOperator.Greater => a > b,
        XPathBinaryOperator.GreaterOrEqual => a >= b,
        _ => throw new InvalidOperationException("Unknown comparison.")
    };
    private static XPathEvaluationException Error(string code, XPathSourceSpan span) => new(new(code, XPathDiagnosticCategory.Evaluation, "Invalid XPath evaluation value or binding.", span));
    private static XPathEvaluationException Locate(XPathEvaluationException error, XPathSourceSpan span) => new(error.Diagnostic with { Span = span });
    private sealed class LocatedBudget(IXPathBudget inner, XPathSourceSpan span) : IXPathBudget
    {
        internal XPathSourceSpan Span => span;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public void ChargeOperations(long amount)
        {
            try
            {
                inner.ChargeOperations(amount);
            }
            catch (XPathEvaluationException error) { throw Locate(error, span); }
        }
        public void CheckNodeSetSize(int count)
        {
            try
            {
                inner.CheckNodeSetSize(count);
            }
            catch (XPathEvaluationException error) { throw Locate(error, span); }
        }
        public void CheckStringSize(int count)
        {
            try
            {
                inner.CheckStringSize(count);
            }
            catch (XPathEvaluationException error) { throw Locate(error, span); }
        }
    }
    private sealed class SingleArgument(XPathValue value) : IReadOnlyList<XPathValue>
    {
        public int Count => 1;
        public XPathValue this[int index] => index == 0 ? value : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<XPathValue> GetEnumerator()
        {
            yield return value;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
