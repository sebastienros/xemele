using System.Text;
using AngleSharp.Html.Parser;

namespace HtmlComparison;

public static class Fixtures
{
    public static IEnumerable<string> Names => ["Small", "Article", "RecoveryForeign"];
    public static HtmlParser Parser() => new(new HtmlParserOptions { IsScripting = false });
    public static string Document(string name) => name switch
    {
        "Small" => "<!doctype html><html><head><meta charset=utf-8><title>Example</title></head><body><main><article class=entry data-id=0><h2>Title</h2><p>Hello &amp; welcome 日本語 😀.</p><a href='/one'>Read</a></article></main></body></html>",
        "Article" => Article(),
        "RecoveryForeign" => "<!doctype html><meta charset=utf-8><title>Recovery</title><main><p><b>one<i>two</b>three</i><table>outside<tr><td>cell</table><ul><li>one<li>two</ul><svg viewBox='0 0 10 10'><foreignObject><p>HTML</p></foreignObject><circle cx=5 /></svg><math><mi>x</mi><mo>+</mo><mn>1</mn></math><template><table><tr><td>inert</table></template><article class=entry data-id=0><h2>尾😀</h2><p>&NotEqualTilde; &copy; &amp;</article></main>",
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
    private static string Article()
    {
        var s = new StringBuilder("<!doctype html><html><head><meta charset=utf-8><title>Articles</title><style>p { color: red }</style></head><body><main>");
        for (int i = 0; i < 128; i++)
            s.Append("<article class='entry featured' data-id='").Append(i).Append("'><h2>Article ").Append(i).Append("</h2><p>Content &amp; entities 日本語 😀 with <em>emphasis</em>.</p><a href='/read/").Append(i).Append("'>Read more</a></article>");
        return s.Append("</main><script>if (a < b) { x = '&'; }</script></body></html>").ToString();
    }
    public const string Selector = "main > article.entry[data-id]:nth-child(2n+1) > h2";
    public static (string Input, string Context) Fragment(string name) => name switch
    {
        "Table" => ("<tr><td>A&amp;B<td>日本語 😀</tr>", "table"),
        "Textarea" => ("a&amp;b<b>text</b>", "textarea"),
        "Template" => ("<p>one<b>two</p>three</b><template><span>inert</span></template>", "template"),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
