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
        var context = XPathStaticContext.FromOptions(options, cancellationToken);
        return new(new XPathCompiler().Compile(expression, context, cancellationToken), expression.Length);
    }
    public XPathValue Evaluate(XmlNode context, XPathContext? options = null)
    {
        options?.CancellationToken.ThrowIfCancellationRequested();
        if (context.IsDefault || context.Kind == XmlNodeKind.Fragment) throw new ArgumentException("Require connected context.", nameof(context));
        var evaluator = Create(context.OwnerDocument, options);
        return evaluator.Evaluate(ast, evaluator.BindNode(context));
    }
    public XPathValue Evaluate(XPathItem context, XPathContext? options = null)
    {
        if (context.IsDefault) throw new ArgumentException("Default context item.", nameof(context));
        var evaluator = Create(context.Document, options);
        return evaluator.Evaluate(ast, evaluator.Bind(context));
    }
    private XPathEvaluator Create(XmlDocument document, XPathContext? options)
    {
        var budget = new XPathBudget(options?.Limits ?? new(), span, options?.CancellationToken ?? default);
        var dynamicContext = new XPathDynamicContext(options, budget);
        var navigator = new XPathNavigator(document, budget, span);
        return new(navigator, dynamicContext, budget, span);
    }
}
