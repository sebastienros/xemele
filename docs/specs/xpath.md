# XPath contract — M4

Decision owner: `M4-contracts`. Depends on the tree and XML foundation, not schema validation or canonicalization.

```csharp
namespace Xemele.XPath;
public sealed class XPathExpression
{
    public static XPathExpression Compile(string expression, XPathCompileOptions? options = null);
    public XPathValue Evaluate(Node context, XPathContext? options = null);
}
public sealed record XPathContext
{
    public IReadOnlyDictionary<string, string> Namespaces { get; init; }
    public IReadOnlyDictionary<string, XPathValue> Variables { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public long MaxOperations { get; init; }
}
public readonly struct XPathValue
{
    public XPathValueKind Kind { get; }
    public double AsNumber();
    public string AsString();
    public bool AsBoolean();
    public IReadOnlyList<XPathItem> AsNodeSet();
}
public readonly struct XPathItem
{
    public XPathItemKind Kind { get; }
    public string StringValue { get; }
    public Node? Node { get; }
    public Node? OwnerElement { get; }
}
```

Contract task defines exact factories for scalar/node-set values, item name/namespace access, expanded variable/function names, compile options, and extension registration. Attribute/namespace items have distinct identities including owner and expanded name/prefix; they never masquerade as element nodes. Compiled syntax and static namespace bindings are immutable; runtime context/variables are per evaluation. Decide and document static versus dynamic bindings before API freeze.

Internal seams: immutable AST records owned by `M4-model`; `XPathCompiler` produces AST; `XPathNavigator` exposes axes/order/identity; evaluator consumes AST/navigation and an `IXPathFunctionLibrary`. Both interfaces and built-in function metadata are frozen by the model task. Public `XPathExpression` facade is owned by composition, not compiler/evaluator lanes.

Parallel lanes after model: grammar compilation, node/axis adapter, and core function library. Evaluation joins them, then conformance verifies the public facade. Implement XPath 1.0 conversions, all axes and operators, context size/position, predicates on reverse axes, document order/deduplication, numeric edge cases, namespace rules, variables, and core functions. Function implementation can use test navigation fakes but production dependencies must be integrated.

Dynamic evaluation budgets/cancellation include extension dispatch, with documented limits for user code. Query result collections are snapshots of identities; mutation effects follow the tree contract. Compilation errors and evaluation errors have separate structured diagnostic categories. No implicit XPath 2.0 functions.

Acceptance: exact semantic tests for each grammar/operator/axis/function, attribute/namespace identities, compiled-expression reuse, variables, error positions, and pinned differential cases. Performance measures compile and evaluate separately. Public API fragments and fixtures live under XPath-owned paths.
