# HTML and CSS contracts — M7

Decision owner: `M7-contracts`. Engine prerequisites are M3 tree/streaming infrastructure; implementation can overlap M4–M6. The cumulative M7 gate still follows M6.

```csharp
namespace Xemele.Html;
public static class HtmlParser
{
    public static Document Parse(string html, HtmlParseOptions? options = null);
}
public static class Html5Parser
{
    public static Document Parse(string html, Html5ParseOptions? options = null);
    public static Node ParseFragment(string html, HtmlFragmentContext context,
        Html5ParseOptions? options = null);
}
public sealed record HtmlFragmentContext(string LocalName, string NamespaceUri);
public static class HtmlSerializer
{
    public static string Serialize(Document document, HtmlSaveOptions? options = null);
}

namespace Xemele.Css;
public sealed class CssSelector
{
    public static CssSelector Compile(string selector, CssSelectorOptions? options = null);
    public bool Matches(Node element);
    public IReadOnlyList<Node> Select(Node root);
}
```

Contract review fixes byte/stream overloads, encoding precedence, document/fragment ownership, fragment return kind, scripting flag, quirks mode metadata, serializer modes, and selector namespace/case behavior. Fragment context needs more than a tag name for all HTML cases; the contract must cover form/template/ancestor context or declare exact unsupported profiles before implementation. No JavaScript execution.

Separate classes/paths implement legacy libxml2-compatible HTML and WHATWG HTML5. Share immutable character/entity tables where measured useful, never XML grammar assumptions. `M7-html-model` freezes token types, tokenizer/tree-builder interface, foreign namespace/name handling, and named character references. Tokens must carry all information the tree builder needs, including self-closing state and source locations.

After the model, legacy parsing, HTML5 tokenizer, and CSS can run independently. HTML5 tree builder follows tokenizer, and covers insertion modes, adoption agency, foster parenting, implied elements, foreign content, and templates. HTML serialization follows the engines and defines mode-specific escaping/void/raw-text rules. HTML5 token callbacks are a tokenizer API: they cannot claim SAX-equivalent events for the corrected DOM. The callback adapter is a separate additive task.

CSS contract task fixes an explicit feature matrix (selector levels, combinators, attributes, supported pseudo-classes, namespace behavior, errors). Start with universal/type/ID/class/attribute selectors, descendant/child/sibling combinators, grouping, and basic structural/negation selectors; unsupported syntax errors instead of partially matching. The milestone gate requires the declared matrix and pinned xmloxide overlap to pass, not an unspecified claim of all CSS.

Own test corpora separately: legacy differentials, tokenizer vectors, WPT tree/fragment vectors, serializer vectors, and selectors. HTML5 reports classify scripting/browser cases explicitly. XML behavior cannot regress. CLI HTML commands are separate command files added after the XML CLI exists, without coupling parser engines to the tool.
