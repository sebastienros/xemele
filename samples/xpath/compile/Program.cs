using Xemele.XPath;
using Xemele.XPath.Internal;
IXPathCompiler compiler = new XPathCompiler();
var context = XPathStaticContext.FromOptions(new() { Namespaces = new Dictionary<string, string> { { "p", "urn:catalog" } } });
var ast = (XPathPath)compiler.Compile("//p:book[@id='b1']/title", context, default);
if (!ast.Absolute || ast.Steps.Count != 3 || ast.Steps[1].Test.Name.NamespaceUri != "urn:catalog") throw new Exception("Unexpected compiler result.");
Console.WriteLine($"XPathPath absolute={ast.Absolute} steps={ast.Steps.Count} span={ast.Span.Start}:{ast.Span.Length}");
foreach (var step in ast.Steps) Console.WriteLine($"{step.Axis} {step.Test.Kind} predicates={step.Predicates.Count} span={step.Span.Start}:{step.Span.Length}");
try
{
    compiler.Compile("count()", context, default);
    throw new Exception("Expected rejection.");
}
catch (XPathCompileException error) { Console.WriteLine($"{error.Diagnostic.Code} {error.Diagnostic.Span.Start}:{error.Diagnostic.Span.Length}"); }
