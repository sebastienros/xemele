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
        if (node.IsDefault || node.Kind == XmlNodeKind.Fragment) throw new ArgumentException("Require connected context.", nameof(node));
        XmlNode top = node;
        while (true)
        {
            budget.ChargeOperations(1); var parent = top.Parent;
            if (parent.IsDefault) break;
            top = parent;
        }
        if (top.Kind != XmlNodeKind.Document) throw new ArgumentException("Detached context.", nameof(node));
    }
    internal XPathItem BindNode(XmlNode node)
    {
        var kind = node.Kind switch
        {
            XmlNodeKind.Document => XPathItemKind.Root, XmlNodeKind.Element => XPathItemKind.Element,
            XmlNodeKind.Text or XmlNodeKind.CData => XPathItemKind.Text, XmlNodeKind.Comment => XPathItemKind.Comment,
            XmlNodeKind.ProcessingInstruction => XPathItemKind.ProcessingInstruction,
            _ => throw new ArgumentException("Unsupported context.", nameof(node))
        };
        if (kind == XPathItemKind.Text)
            while (XPathItem.IsText(node.PreviousSibling)) { shared.ChargeOperations(1); node = node.PreviousSibling; }
        return Bind(new XPathItem(new(kind, node, default, null), ""));
    }
    internal XPathValue Evaluate(XPathAst ast, XPathItem item)
    {
        var entryBudget = new LocatedBudget(shared, entrySpan);
        foreach (var binding in context.Variables)
        {
            entryBudget.ChargeOperations(1);
            variables = new(binding.Key, RebindValue(binding.Value, entryBudget), variables);
        }
        var root = new Request(ast, new(item, 1, 1));
        Frame? frame = new(Run(root).GetEnumerator(), null);
        try
        {
            while (frame is not null)
            {
                shared.ChargeOperations(0);
                if (frame.Continuation.MoveNext())
                    frame = new(Run(frame.Continuation.Current).GetEnumerator(), frame);
                else { frame.Continuation.Dispose(); frame = frame.Parent; }
            }
        }
        finally
        { while (frame is not null) { frame.Continuation.Dispose(); frame = frame.Parent; } }
        return root.Result;
    }
    private IEnumerable<Request> Run(Request request)
    {
        var ast = request.Ast; var focus = request.Focus;
        var budget = new LocatedBudget(shared, ast.Span);
        budget.ChargeOperations(1);
        switch (ast)
        {
            case XPathLiteral literal:
                request.Result = RebindValue(literal.Value, budget); break;
            case XPathVariable variable:
                Binding? found = variables;
                while (found is not null)
                {
                    budget.ChargeOperations(1);
                    if (SameName(found.Name, variable.Name, budget)) break;
                    found = found.Next;
                }
                if (found is null) throw Error("XPATH-VARIABLE", ast.Span);
                request.Result = found.Value;
                break;
            case XPathUnaryMinus unary:
                var operand = new Request(unary.Operand, focus); yield return operand;
                request.Result = XPathValue.FromNumber(-Number(operand.Result, focus, budget)); break;
            case XPathBinary binary:
                var left = new Request(binary.Left, focus); yield return left;
                if (binary.Operator == XPathBinaryOperator.Or && left.Result.AsBoolean())
                { request.Result = XPathValue.FromBoolean(true); break; }
                if (binary.Operator == XPathBinaryOperator.And && !left.Result.AsBoolean())
                { request.Result = XPathValue.FromBoolean(false); break; }
                var right = new Request(binary.Right, focus); yield return right;
                request.Result = Binary(binary.Operator, left.Result, right.Result, focus, budget, ast.Span); break;
            case XPathCall call:
                budget.ChargeOperations(call.Arguments.Count);
                var arguments = new XPathValue[call.Arguments.Count];
                for (int i = 0; i < arguments.Length; i++)
                {
                    budget.ChargeOperations(1);
                    var argument = new Request(call.Arguments[i], focus); yield return argument;
                    budget.ChargeOperations(1); arguments[i] = argument.Result;
                }
                if (call.Signature.Name.NamespaceUri.Length == 0)
                    request.Result = CallCore(call.Signature, focus, arguments, budget);
                else
                {
                    XPathExtensionFunction? function = null;
                    foreach (var binding in context.Functions)
                    {
                        budget.ChargeOperations(1);
                        if (SameName(binding.Key, call.Signature.Name, budget)) { function = binding.Value; break; }
                    }
                    if (function is null) throw Error("XPATH-EXTENSION", ast.Span);
                    budget.ChargeOperations(1);
                    // Keep dispatch entirely outside all engine exception handlers.
                    var returned = function(new XPathFunctionContext(focus, budget), Array.AsReadOnly(arguments));
                    budget.ChargeOperations(0);
                    request.Result = RebindValue(returned, budget);
                }
                break;
            case XPathFilter filter:
                var input = new Request(filter.Input, focus); yield return input;
                var filtered = new Selection(Nodes(input.Result, ast.Span), budget);
                foreach (var predicate in filter.Predicates)
                {
                    budget.ChargeOperations(1);
                    var next = new Selection();
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        budget.ChargeOperations(1);
                        var candidate = new Request(predicate, new(filtered[i], i + 1, filtered.Count)); yield return candidate;
                        if (Accept(candidate.Result, i + 1)) next.Add(filtered[i], budget);
                    }
                    filtered = next;
                }
                request.Result = Materialize(filtered, budget); break;
            case XPathPath path:
                Selection current;
                if (path.Input is not null)
                {
                    var start = new Request(path.Input, focus); yield return start;
                    current = new(Nodes(start.Result, ast.Span), budget);
                }
                else { current = new(); current.Add(path.Absolute ? navigator.Root : focus.Item, budget); }
                foreach (var step in path.Steps)
                {
                    var stepBudget = new LocatedBudget(shared, step.Span);
                    stepBudget.ChargeOperations(1);
                    var merged = new Selection();
                    for (int j = 0; j < current.Count; j++)
                    {
                        stepBudget.ChargeOperations(1);
                        var axis = navigator.SelectAxis(current[j], step.Axis, stepBudget);
                        var selected = new Selection();
                        for (int i = 0; i < axis.Count; i++)
                        {
                            stepBudget.ChargeOperations(1);
                            if (Matches(axis[i], step, stepBudget)) selected.Add(axis[i], stepBudget);
                        }
                        foreach (var predicate in step.Predicates)
                        {
                            stepBudget.ChargeOperations(1);
                            var next = new Selection();
                            for (int i = 0; i < selected.Count; i++)
                            {
                                stepBudget.ChargeOperations(1);
                                var candidate = new Request(predicate, new(selected[i], i + 1, selected.Count)); yield return candidate;
                                if (Accept(candidate.Result, i + 1)) next.Add(selected[i], stepBudget);
                            }
                            selected = next;
                        }
                        for (int i = 0; i < selected.Count; i++) merged.AddUnique(selected[i], navigator, stepBudget);
                    }
                    current = new(Normalize(merged, stepBudget), stepBudget);
                }
                request.Result = Materialize(current, budget); break;
            default: throw new InvalidOperationException("Unknown XPath AST.");
        }
    }
    private static bool Accept(XPathValue value, int position) => value.Kind == XPathValueKind.Number ? value.AsNumber() == position : value.AsBoolean();
    private IReadOnlyList<XPathItem> Normalize(IReadOnlyList<XPathItem> input, IXPathBudget budget)
    {
        try { return navigator.Normalize(input, budget); }
        catch (XPathEvaluationException error) { throw Locate(error, budget is LocatedBudget located ? located.Span : entrySpan); }
    }
    private XPathValue Materialize(IReadOnlyList<XPathItem> nodes, IXPathBudget budget) => XPathValue.FromSnapshotNodeSet(Normalize(nodes, budget), budget);
    private XPathValue RebindValue(XPathValue value, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (value.Kind == XPathValueKind.String) Scan(value.AsString(), budget);
        if (value.Kind != XPathValueKind.NodeSet) return value;
        var nodes = value.AsNodeSet(); budget.CheckNodeSetSize(nodes.Count);
        return XPathValue.FromSnapshotNodeSet(Normalize(nodes, budget), budget);
    }
    private static IReadOnlyList<XPathItem> Nodes(XPathValue value, XPathSourceSpan span)
    { if (value.Kind != XPathValueKind.NodeSet) throw Error("XPATH-TYPE", span); return value.AsNodeSet(); }
    private static bool Matches(XPathItem item, XPathStep step, IXPathBudget budget)
    {
        var test = step.Test;
        var principal = step.Axis switch { XPathAxis.Attribute => XPathItemKind.Attribute, XPathAxis.Namespace => XPathItemKind.Namespace, _ => XPathItemKind.Element };
        return test.Kind switch
        {
            XPathNodeTestKind.Node => true,
            XPathNodeTestKind.Text => item.Kind == XPathItemKind.Text,
            XPathNodeTestKind.Comment => item.Kind == XPathItemKind.Comment,
            XPathNodeTestKind.ProcessingInstruction => item.Kind == XPathItemKind.ProcessingInstruction && (test.ProcessingInstructionTarget is null || Equal(item.LocalName, test.ProcessingInstructionTarget, budget)),
            XPathNodeTestKind.AnyPrincipal => item.Kind == principal,
            XPathNodeTestKind.NamespaceWildcard => item.Kind == principal && Equal(item.NamespaceUri, test.Name.NamespaceUri, budget),
            XPathNodeTestKind.Name => item.Kind == principal && Equal(item.NamespaceUri, test.Name.NamespaceUri, budget) && Equal(item.LocalName, test.Name.LocalName, budget),
            _ => throw new InvalidOperationException("Unknown node test.")
        };
    }
    private static void Scan(string text, IXPathBudget budget)
    { budget.CheckStringSize(text.Length); for (int i = 0; i < text.Length; i++) budget.ChargeOperations(1); }
    private static bool SameName(XPathExpandedName a, XPathExpandedName b, IXPathBudget budget) => Equal(a.NamespaceUri, b.NamespaceUri, budget) && Equal(a.LocalName, b.LocalName, budget);
    private static bool Equal(string a, string b, IXPathBudget budget)
    {
        budget.ChargeOperations(1);
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) { budget.ChargeOperations(2); if (a[i] != b[i]) return false; }
        return true;
    }
    private double Number(XPathValue value, XPathFocus focus, IXPathBudget budget) => Convert("number", value, focus, budget).AsNumber();
    private string Text(XPathValue value, XPathFocus focus, IXPathBudget budget) => Convert("string", value, focus, budget).AsString();
    private XPathValue Convert(string name, XPathValue value, XPathFocus focus, IXPathBudget budget) => CallCore(XPathCoreSignatures.ByName[new("", name)], focus, new SingleArgument(value), budget);
    private XPathValue CallCore(XPathFunctionSignature signature, XPathFocus focus, IReadOnlyList<XPathValue> arguments, IXPathBudget budget)
    {
        try { return Core.Invoke(signature, focus, arguments, navigator, budget); }
        catch (XPathEvaluationException error) { throw Locate(error, budget is LocatedBudget located ? located.Span : entrySpan); }
    }
    private XPathValue Binary(XPathBinaryOperator op, XPathValue left, XPathValue right, XPathFocus focus, IXPathBudget budget, XPathSourceSpan span)
    {
        if (op is XPathBinaryOperator.And or XPathBinaryOperator.Or) return XPathValue.FromBoolean(right.AsBoolean());
        if (op == XPathBinaryOperator.Union)
        {
            var result = new Selection(Nodes(left, span), budget);
            var other = Nodes(right, span);
            for (int i = 0; i < other.Count; i++) result.AddUnique(other[i], navigator, budget);
            return Materialize(result, budget);
        }
        if (op is XPathBinaryOperator.Equal or XPathBinaryOperator.NotEqual or XPathBinaryOperator.Less or XPathBinaryOperator.LessOrEqual or XPathBinaryOperator.Greater or XPathBinaryOperator.GreaterOrEqual)
            return XPathValue.FromBoolean(Compare(op, left, right, focus, budget));
        double a = Number(left, focus, budget), b = Number(right, focus, budget);
        return XPathValue.FromNumber(op switch
        {
            XPathBinaryOperator.Add => a + b, XPathBinaryOperator.Subtract => a - b,
            XPathBinaryOperator.Multiply => a * b, XPathBinaryOperator.Divide => a / b,
            XPathBinaryOperator.Modulo => a % b, _ => throw new InvalidOperationException("Unknown operator.")
        });
    }
    private bool Compare(XPathBinaryOperator op, XPathValue left, XPathValue right, XPathFocus focus, IXPathBudget budget)
    {
        bool equality = op is XPathBinaryOperator.Equal or XPathBinaryOperator.NotEqual;
        bool leftSet = left.Kind == XPathValueKind.NodeSet, rightSet = right.Kind == XPathValueKind.NodeSet;
        bool booleanPair = left.Kind == XPathValueKind.Boolean || right.Kind == XPathValueKind.Boolean;
        if (booleanPair && (equality || leftSet || rightSet))
        {
            budget.ChargeOperations(2);
            return Numeric(op, left.AsBoolean() ? 1 : 0, right.AsBoolean() ? 1 : 0);
        }
        if (leftSet || rightSet)
        {
            int leftCount = leftSet ? left.AsNodeSet().Count : 1, rightCount = rightSet ? right.AsNodeSet().Count : 1;
            bool numeric = !equality || left.Kind == XPathValueKind.Number || right.Kind == XPathValueKind.Number;
            for (int i = 0; i < leftCount; i++)
            {
                budget.ChargeOperations(1);
                var a = leftSet ? XPathValue.FromString(left.AsNodeSet()[i].StringValue) : left;
                for (int j = 0; j < rightCount; j++)
                {
                    budget.ChargeOperations(1);
                    var b = rightSet ? XPathValue.FromString(right.AsNodeSet()[j].StringValue) : right;
                    if (numeric ? Numeric(op, Number(a, focus, budget), Number(b, focus, budget)) : StringCompare(op, Text(a, focus, budget), Text(b, focus, budget), budget)) return true;
                }
            }
            return false;
        }
        if (!equality || left.Kind == XPathValueKind.Number || right.Kind == XPathValueKind.Number)
            return Numeric(op, Number(left, focus, budget), Number(right, focus, budget));
        return StringCompare(op, Text(left, focus, budget), Text(right, focus, budget), budget);
    }
    private static bool StringCompare(XPathBinaryOperator op, string a, string b, IXPathBudget budget)
    { bool same = Equal(a, b, budget); return op == XPathBinaryOperator.Equal ? same : !same; }
    private static bool Numeric(XPathBinaryOperator op, double a, double b) => op switch
    {
        XPathBinaryOperator.Equal => a == b, XPathBinaryOperator.NotEqual => a != b,
        XPathBinaryOperator.Less => a < b, XPathBinaryOperator.LessOrEqual => a <= b,
        XPathBinaryOperator.Greater => a > b, XPathBinaryOperator.GreaterOrEqual => a >= b,
        _ => throw new InvalidOperationException("Unknown comparison.")
    };
    private static XPathEvaluationException Error(string code, XPathSourceSpan span) => new(new(code, XPathDiagnosticCategory.Evaluation, "Invalid XPath evaluation value or binding.", span));
    private static XPathEvaluationException Locate(XPathEvaluationException error, XPathSourceSpan span) => new(error.Diagnostic with { Span = span });
    private sealed class LocatedBudget(IXPathBudget inner, XPathSourceSpan span) : IXPathBudget
    {
        internal XPathSourceSpan Span => span;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public void ChargeOperations(long amount) { try { inner.ChargeOperations(amount); } catch (XPathEvaluationException error) { throw Locate(error, span); } }
        public void CheckNodeSetSize(int count) { try { inner.CheckNodeSetSize(count); } catch (XPathEvaluationException error) { throw Locate(error, span); } }
        public void CheckStringSize(int count) { try { inner.CheckStringSize(count); } catch (XPathEvaluationException error) { throw Locate(error, span); } }
    }
    private sealed class SingleArgument(XPathValue value) : IReadOnlyList<XPathValue>
    {
        public int Count => 1;
        public XPathValue this[int index] => index == 0 ? value : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<XPathValue> GetEnumerator() { yield return value; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    // Geometric capacity growth copies through explicit polling. Logical size
    // is capped before every append; retained snapshots use exact owned arrays.
    private sealed class Selection : IReadOnlyList<XPathItem>
    {
        private XPathItem[] items = [];
        public int Count { get; private set; }
        public XPathItem this[int index] => index < Count ? items[index] : throw new ArgumentOutOfRangeException(nameof(index));
        internal Selection() { }
        internal Selection(IReadOnlyList<XPathItem> input, IXPathBudget budget)
        {
            budget.CheckNodeSetSize(input.Count);
            items = new XPathItem[input.Count];
            for (int i = 0; i < input.Count; i++) { budget.ChargeOperations(2); items[i] = input[i]; }
            Count = input.Count;
        }
        internal void Add(XPathItem item, IXPathBudget budget)
        {
            if (Count == Array.MaxLength) throw new XPathEvaluationException(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath node-set capacity exceeded.", default));
            budget.CheckNodeSetSize(Count + 1);
            if (Count == items.Length)
            {
                int capacity = (int)Math.Min(Array.MaxLength, Math.Max(4L, (long)Count * 2));
                var grown = new XPathItem[capacity];
                for (int i = 0; i < Count; i++) { budget.ChargeOperations(2); grown[i] = items[i]; }
                items = grown;
            }
            budget.ChargeOperations(1); items[Count++] = item;
        }
        internal void AddUnique(XPathItem item, XPathNavigator navigator, IXPathBudget budget)
        {
            for (int i = 0; i < Count; i++)
            { budget.ChargeOperations(1); if (navigator.CompareDocumentOrder(item, items[i], budget) == 0) return; }
            Add(item, budget);
        }
        public IEnumerator<XPathItem> GetEnumerator() { for (int i = 0; i < Count; i++) yield return items[i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
