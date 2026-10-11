using Xemele.XPath.Internal;
namespace Xemele.XPath;

/// <summary>An immutable XPath 1.0 program. Each evaluation owns a document snapshot.</summary>
public sealed class XPathExpression
{
    private readonly XPathAst ast;
    private readonly XPathSourceSpan span;
    private XPathExpression(XPathAst ast, int length) { this.ast = ast; span = new(0, length); }
    public static XPathExpression Compile(string expression, XPathCompileOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression); cancellationToken.ThrowIfCancellationRequested();
        var context = XPathStaticContext.FromOptions(options, cancellationToken, new XPathSourceSpan(0, expression.Length));
        return new(new XPathCompiler().Compile(expression, context, cancellationToken), expression.Length);
    }
    public XPathValue Evaluate(XmlNode context, XPathContext? options = null) => EvaluateWithAccounting(context, options, out _);
    public XPathValue Evaluate(XPathItem context, XPathContext? options = null) => EvaluateWithAccounting(context, options, out _);
    internal XPathValue EvaluateWithAccounting(XmlNode context, XPathContext? options, out long operations)
    {
        operations = 0;
        options?.CancellationToken.ThrowIfCancellationRequested();
        if (context.IsDefault || context.Kind == XmlNodeKind.Fragment) throw new ArgumentException("Require connected context.", nameof(context));
        var document = context.OwnerDocument;
        var budget = new XPathBudget(options?.Limits ?? new(), span, options?.CancellationToken ?? default);
        try
        {
            var evaluator = Create(document, options, budget, context);
            return evaluator.Evaluate(ast, evaluator.BindNode(context));
        }
        finally { operations = budget.Operations; }
    }
    internal XPathValue EvaluateWithAccounting(XPathItem context, XPathContext? options, out long operations)
    {
        operations = 0;
        if (context.IsDefault) throw new ArgumentException("Default context item.", nameof(context));
        var document = context.Document;
        var budget = new XPathBudget(options?.Limits ?? new(), span, options?.CancellationToken ?? default);
        try
        {
            var evaluator = Create(document, options, budget);
            return evaluator.Evaluate(ast, evaluator.Bind(context));
        }
        finally { operations = budget.Operations; }
    }
    private XPathEvaluator Create(XmlDocument document, XPathContext? options, XPathBudget budget, XmlNode? node = null)
    {
        if (node is XmlNode contextNode) XPathEvaluator.ValidateNode(contextNode, budget);
        var dynamicContext = new XPathDynamicContext(options, budget);
        var navigator = new XPathNavigator(document, budget, span);
        return new(navigator, dynamicContext, budget, span);
    }
}
