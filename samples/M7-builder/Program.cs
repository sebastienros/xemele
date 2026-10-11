using Xemele.Html;
using Xemele.Tests.HtmlTreeConstruction;
if (args.Length == 0) { Console.WriteLine(TreeCorpus.Dump(Html5Parser.Parse("<!doctype html><p>A<b>B<p>C").DocumentNode)); return 0; }
var result = TreeCorpus.Run(args[0], args.Length > 1 ? args[1] : "/private/tmp/xemele-builder-failures.json");
Console.WriteLine(result); return result.Mismatches == 0 ? 0 : 1;
