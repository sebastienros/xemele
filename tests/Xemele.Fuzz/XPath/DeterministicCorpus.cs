using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xemele.XPath;
namespace Xemele.Fuzz.XPath;

// Owned callable lane; the existing shared bootstrap Program is deliberately unchanged.
public static class DeterministicCorpus
{
    public const uint Seed = 0x4D345850;
    public static string Run()
    {
        if (RuntimeInformation.FrameworkDescription != ".NET 10.0.11") throw new InvalidOperationException("Pinned reference runtime required.");
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        uint state = Seed;
        int Next(int maximum) { state = unchecked(state * 1664525 + 1013904223); return (int)(state % (uint)maximum); }
        string[] axes = ["child", "descendant", "parent", "ancestor", "following-sibling", "preceding-sibling", "following", "preceding", "attribute", "namespace", "self", "descendant-or-self", "ancestor-or-self"];
        string[] operators = ["+", "-", "*", "div", "mod", "=", "!=", "<", "<=", ">", ">="];
        int generated = 0, invalid = 0, reused = 0, mutation = 0, quota = 0, cancellation = 0;
        for (int tree = 0; tree < 32; tree++)
        {
            int length = 2 + Next(7);
            string xml = "<r xmlns:p='urn:p'>" + string.Concat(Enumerable.Range(0,length).Select(i => "<a n='" + i + "'>" + (1 + Next(20)) + "<b>3</b></a>")) + "<p:z/><!--end--><?go data?></r>";
            var document = XmlDocument.Parse(xml);
            var reference = new System.Xml.XPath.XPathDocument(new StringReader(xml)).CreateNavigator(); reference.MoveToChild("r", "");
            var resolver = new System.Xml.XmlNamespaceManager(new System.Xml.NameTable()); resolver.AddNamespace("p", "urn:p");
            var options = new XPathCompileOptions { Namespaces = new Dictionary<string,string> { ["p"] = "urn:p" } };
            for (int sample = 0; sample < 24; sample++)
            {
                int a = Next(9), b = 1 + Next(9);
                string expression = (sample % 12) switch
                {
                    0 => "(" + a + " " + operators[Next(operators.Length)] + " " + b + ")",
                    1 => "/r/a[" + (1 + Next(length)) + "]/" + axes[Next(axes.Length)] + "::node()",
                    2 => "/r/a[position() mod 2 = " + Next(2) + "]",
                    3 => "(/r/a)[last()]/@n",
                    4 => "/r/a[" + length + "]/preceding-sibling::*[" + (1 + Next(length)) + "]",
                    5 => "sum(/r/a/@n) + count(//b)",
                    6 => "substring(concat('ab','cd')," + a + "," + b + ")",
                    7 => "translate(normalize-space(' a b '),'ab','XY')",
                    8 => "(/r/a[1] | /r/a[last()] | /r/a[1])/b",
                    9 => "/r/a/@n " + operators[5 + Next(6)] + " " + a,
                    10 => "not(false()) and (true() or false())",
                    _ => "name(/r/p:*)"
                };
                var compiled = XPathExpression.Compile(expression, options);
                var bcl = reference.Compile(expression); bcl.SetContext(resolver);
                string expected = Canonical(reference.Evaluate(bcl));
                Equal(expected, Canonical(compiled.Evaluate(document.RootElement)), expression); generated++;
                Equal(expected, Canonical(compiled.Evaluate(document.RootElement)), "reuse:" + expression); reused++;
            }
            foreach (string malformed in new[] { "/r/a/", "count(/r/a", "1e" + Next(9), "if (true()) then 1 else 2" })
            {
                try { _ = XPathExpression.Compile(malformed); throw new InvalidOperationException("Invalid grammar accepted: " + malformed); }
                catch (XPathCompileException error) when (error.Diagnostic.Code == "XPATH-SYNTAX") { invalid++; }
            }
            var selection = XPathExpression.Compile("/r/a");
            var before = selection.Evaluate(document.RootElement).AsNodeSet();
            string frozen = Canonical(XPathValue.FromNodeSet(before));
            var text = document.RootElement.FirstChild.FirstChild; text.Value = "changed";
            document.RootElement.AppendChild(document.CreateElement("a"));
            Equal(frozen, Canonical(XPathValue.FromNodeSet(before)), "owned old metadata/membership");
            if (selection.Evaluate(document.RootElement).AsNodeSet().Count != length + 1) throw new InvalidOperationException("Reuse missed mutation.");
            mutation++;
            var scalar = XPathExpression.Compile("count(/r/a)");
            long low = 1, high = 1_000_000;
            while (low < high)
            {
                long mid = low + (high - low) / 2;
                try { _ = scalar.Evaluate(document.RootElement, new() { Limits = new() { MaxOperations = mid } }); high = mid; }
                catch (XPathEvaluationException error) when (error.Diagnostic.Code == "XPATH-EVALUATION-LIMIT") { low = mid + 1; }
            }
            Equal((length + 1).ToString(CultureInfo.InvariantCulture), scalar.Evaluate(document.RootElement, new() { Limits = new() { MaxOperations = low } }).AsNumber().ToString(CultureInfo.InvariantCulture), "exact quota");
            try { _ = scalar.Evaluate(document.RootElement, new() { Limits = new() { MaxOperations = low - 1 } }); throw new InvalidOperationException("One-over quota accepted."); }
            catch (XPathEvaluationException error) when (error.Diagnostic.Code == "XPATH-EVALUATION-LIMIT") { quota++; }
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { _ = XPathExpression.Compile("/r/a", cancellationToken: canceled.Token); throw new InvalidOperationException("Compile cancellation ignored."); }
            catch (OperationCanceledException error) when (error.CancellationToken == canceled.Token) { cancellation++; }
            try { _ = scalar.Evaluate(document.RootElement, new() { CancellationToken = canceled.Token }); throw new InvalidOperationException("Evaluation cancellation ignored."); }
            catch (OperationCanceledException error) when (error.CancellationToken == canceled.Token) { cancellation++; }
        }
        return JsonSerializer.Serialize(new { seed = Seed, runtime = RuntimeInformation.FrameworkDescription, generated, reused, invalid, mutation, quota, cancellation });
    }
    private static void Equal(string expected, string actual, string source)
    { if (!StringComparer.Ordinal.Equals(expected, actual)) throw new InvalidOperationException(source + " expected " + expected + " actual " + actual); }
    private static string Canonical(object value)
    {
        if (value is bool flag) return "b:" + flag;
        if (value is double number) return "n:" + number.ToString("R", CultureInfo.InvariantCulture);
        if (value is string text) return "s:" + text;
        if (value is System.Xml.XPath.XPathNodeIterator iterator)
        {
            var rows = new List<string>(); while (iterator.MoveNext()) { var n = iterator.Current!; rows.Add(string.Join("|", n.NodeType, n.Name, n.NamespaceURI, n.Value)); }
            // namespace order is unspecified; keep other generated node-set order exact.
            if (rows.Count > 0 && rows.All(r => r.StartsWith("Namespace|", StringComparison.Ordinal))) rows.Sort(StringComparer.Ordinal);
            return JsonSerializer.Serialize(rows);
        }
        throw new InvalidOperationException();
    }
    private static string Canonical(XPathValue value)
    {
        if (value.Kind == XPathValueKind.Boolean) return Canonical(value.AsBoolean());
        if (value.Kind == XPathValueKind.Number) return Canonical(value.AsNumber());
        if (value.Kind == XPathValueKind.String) return Canonical(value.AsString());
        var rows = value.AsNodeSet().Select(n => string.Join("|", n.Kind == XPathItemKind.Root ? "Root" : n.Kind.ToString(), n.Name, n.NamespaceUri, n.StringValue)).ToList();
        if (rows.Count > 0 && rows.All(r => r.StartsWith("Namespace|", StringComparison.Ordinal))) rows.Sort(StringComparer.Ordinal);
        return JsonSerializer.Serialize(rows);
    }
}
