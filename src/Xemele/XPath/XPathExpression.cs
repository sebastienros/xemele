using Xemele.XPath.Internal;
namespace Xemele.XPath;

/// <summary>An immutable XPath 1.0 program. Each evaluation owns a document snapshot.</summary>
public sealed class XPathExpression
{
    private readonly XPathAst ast;
    private readonly XPathSourceSpan span;
    private XPathExpression(XPathAst ast, int length)
    {
        this.ast = ast;
        span = new(0, length);
    }
    /// <summary>Compiles an XPath 1.0 expression with a snapshot of its static context.</summary>
    /// <param name="expression">The XPath expression text.</param>
    /// <param name="options">The namespace bindings, extension signatures, and limits, or null for defaults.</param>
    /// <param name="cancellationToken">The token used to cancel compilation.</param>
    /// <returns>An immutable expression that can be evaluated repeatedly.</returns>
    /// <exception cref="ArgumentNullException">The expression is null.</exception>
    /// <exception cref="XPathCompileException">The expression, static context, or compilation limits prevent compilation.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XPathExpression Compile(string expression, XPathCompileOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        cancellationToken.ThrowIfCancellationRequested();
        var context = XPathStaticContext.FromOptions(options, cancellationToken, new XPathSourceSpan(0, expression.Length));
        return new(new XPathCompiler().Compile(expression, context, cancellationToken), expression.Length);
    }
    /// <summary>Evaluates the expression with a document-connected XML node as its context.</summary>
    /// <param name="context">The context node in the document to snapshot for this evaluation.</param>
    /// <param name="options">The dynamic context and evaluation limits, or null for defaults.</param>
    /// <returns>The resulting XPath value, with owned metadata for any returned node-set items.</returns>
    /// <exception cref="ArgumentException">The context is default, detached, or unsupported by the XPath data model.</exception>
    /// <exception cref="XPathEvaluationException">Evaluation fails or exceeds a configured limit.</exception>
    /// <exception cref="OperationCanceledException">The dynamic context requests cancellation.</exception>
    public XPathValue Evaluate(XmlNode context, XPathContext? options = null) => EvaluateWithAccounting(context, options, out _);
    /// <summary>Evaluates the expression with an XPath item as its context.</summary>
    /// <param name="context">The context item in the document to snapshot for this evaluation.</param>
    /// <param name="options">The dynamic context and evaluation limits, or null for defaults.</param>
    /// <returns>The resulting XPath value, with owned metadata for any returned node-set items.</returns>
    /// <exception cref="ArgumentException">The context item is default or cannot be bound to the evaluation snapshot.</exception>
    /// <exception cref="XPathEvaluationException">Evaluation fails or exceeds a configured limit.</exception>
    /// <exception cref="OperationCanceledException">The dynamic context requests cancellation.</exception>
    public XPathValue Evaluate(XPathItem context, XPathContext? options = null) => EvaluateWithAccounting(context, options, out _);
    internal XPathValue EvaluateWithAccounting(XmlNode context, XPathContext? options, out long operations)
    {
        operations = 0;
        options?.CancellationToken.ThrowIfCancellationRequested();
        if (context.IsDefault || context.Kind == XmlNodeKind.Fragment)
        {
            throw new ArgumentException("Require connected context.", nameof(context));
        }

        var document = context.OwnerDocument;
        var budget = new XPathBudget(options?.Limits ?? XPathEvaluationLimits.Default, span, options?.CancellationToken ?? default);
        try
        {
            var evaluator = Create(document, options, budget, context);
            return evaluator.Evaluate(ast, evaluator.BindNode(context)).Detached();
        }
        finally { operations = budget.Operations; }
    }
    internal XPathValue EvaluateWithAccounting(XPathItem context, XPathContext? options, out long operations)
    {
        operations = 0;
        if (context.IsDefault)
        {
            throw new ArgumentException("Default context item.", nameof(context));
        }

        var document = context.Document;
        var budget = new XPathBudget(options?.Limits ?? XPathEvaluationLimits.Default, span, options?.CancellationToken ?? default);
        try
        {
            var evaluator = Create(document, options, budget);
            return evaluator.Evaluate(ast, evaluator.Bind(context)).Detached();
        }
        finally { operations = budget.Operations; }
    }
    private XPathEvaluator Create(XmlDocument document, XPathContext? options, XPathBudget budget, XmlNode? node = null)
    {
        if (node is XmlNode contextNode)
        {
            XPathEvaluator.ValidateNode(contextNode, budget);
        }

        var dynamicContext = new XPathDynamicContext(options, budget);
        var navigator = new XPathNavigator(document, budget, span);
        return new(navigator, dynamicContext, budget, span);
    }
}
