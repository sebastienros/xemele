using Xemele;
using Xemele.XPath;
var document = XmlDocument.Parse("<r><a n='2'>two</a><a n='3'>three</a></r>");
var expression = XPathExpression.Compile("sum(/r/a/@n) + $offset");
var context = new XPathContext { Variables = new Dictionary<XPathExpandedName, XPathValue> { [new("", "offset")] = XPathValue.FromNumber(1) } };
if (expression.Evaluate(document.RootElement, context).AsNumber() != 6) throw new Exception("Sum mismatch.");
var nodes = XPathExpression.Compile("/r/a[2]/preceding-sibling::*[1]").Evaluate(document.RootElement).AsNodeSet();
if (nodes.Count != 1 || nodes[0].StringValue != "two") throw new Exception("Predicate mismatch.");
Console.WriteLine("PASS: public XPathExpression, sum=6; reverse-axis first=two; four facade declarations");
