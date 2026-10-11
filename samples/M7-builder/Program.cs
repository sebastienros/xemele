using Xemele.Html;
using Xemele.Tests.HtmlTreeConstruction;
if (args.Length == 0)
{
    const string input = "<!doctype html><p>A<b>B<p>C";
    var document = Html5Parser.Parse(input);
    Console.WriteLine(TreeCorpus.Dump(document.DocumentNode));
    var bytes = System.Text.Encoding.UTF8.GetBytes(input);
    if (TreeCorpus.Dump(Html5Parser.Parse(bytes).DocumentNode) != TreeCorpus.Dump(document.DocumentNode)) return 1;
    using var stream = new MemoryStream(bytes, false);
    if (TreeCorpus.Dump(Html5Parser.Load(stream).DocumentNode) != TreeCorpus.Dump(document.DocumentNode) || !stream.CanRead) return 1;
    var context = new HtmlFragmentContext(document.CreateElement("table"));
    var fragment = Html5Parser.ParseFragment("<tr><td>fragment", context);
    if (fragment.FirstChild?.LocalName != "tbody" || fragment.GetTextContent() != "fragment") return 1;
    Console.WriteLine("Four public entry points; contextual tbody fragment; caller stream remains open."); return 0;
}
var result = TreeCorpus.Run(args[0], args.Length > 1 ? args[1] : "/private/tmp/xemele-builder-failures.json");
Console.WriteLine(result); return result.Mismatches == 0 ? 0 : 1;
