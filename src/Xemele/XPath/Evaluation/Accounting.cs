namespace Xemele.XPath.Internal;

// Schema callers cap each expression to their remaining allowance and debit
// this actual successful-charge count in finally, including failed evaluations.
internal static class XPathEvaluationAccounting
{
    internal static XPathValue EvaluateWithAccounting(XPathExpression expression, XmlNode context,
        XPathContext? options, out long operations) => expression.EvaluateWithAccounting(context, options, out operations);
    internal static XPathValue EvaluateWithAccounting(XPathExpression expression, XPathItem context,
        XPathContext? options, out long operations) => expression.EvaluateWithAccounting(context, options, out operations);
}
