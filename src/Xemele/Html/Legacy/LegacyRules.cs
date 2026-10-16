// Derived from libxml2 HTMLparser.c at 96498992efa48d52b0e8b83058bd88dbdaf153c1 (MIT).
namespace Xemele.Html;

internal static class LegacyRules
{
    internal static bool AutoClose(string oldName, string newName) => oldName switch
    {
        "a" => newName is "a" or "fieldset" or "table" or "td" or "th",
        "address" => newName is "dd" or "dl" or "dt" or "form" or "li" or "ul",
        "b" => newName is "center" or "p" or "td" or "th",
        "big" => newName is "p",
        "caption" => newName is "col" or "colgroup" or "tbody" or "tfoot" or "thead" or "tr",
        "col" => newName is "col" or "colgroup" or "tbody" or "tfoot" or "thead" or "tr",
        "colgroup" => newName is "colgroup" or "tbody" or "tfoot" or "thead" or "tr",
        "dd" => newName is "dt",
        "dir" => newName is "dd" or "dl" or "dt" or "form" or "ul",
        "dl" => newName is "form" or "li",
        "dt" => newName is "dd" or "dl",
        "font" => newName is "center" or "td" or "th",
        "form" => newName is "form",
        "h1" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "h2" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "h3" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "h4" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "h5" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "h6" => newName is "fieldset" or "form" or "li" or "p" or "table",
        "head" => newName is "a" or "abbr" or "acronym" or "address" or "b" or "bdo" or "big" or "blockquote" or "body" or "br" or "center" or "cite" or "code" or "dd" or "dfn" or "dir" or "div" or "dl" or "dt" or "em" or "fieldset" or "font" or "form" or "frameset" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "hr" or "i" or "iframe" or "img" or "kbd" or "li" or "listing" or "map" or "menu" or "ol" or "p" or "pre" or "q" or "s" or "samp" or "small" or "span" or "strike" or "strong" or "sub" or "sup" or "table" or "tt" or "u" or "ul" or "var" or "xmp",
        "hr" => newName is "form",
        "i" => newName is "center" or "p" or "td" or "th",
        "legend" => newName is "fieldset",
        "li" => newName is "li",
        "link" => newName is "body" or "frameset",
        "listing" => newName is "dd" or "dl" or "dt" or "fieldset" or "form" or "li" or "table" or "ul",
        "menu" => newName is "dd" or "dl" or "dt" or "form" or "ul",
        "ol" => newName is "form",
        "option" => newName is "optgroup" or "option",
        "p" => newName is "address" or "blockquote" or "body" or "caption" or "center" or "col" or "colgroup" or "dd" or "dir" or "div" or "dl" or "dt" or "fieldset" or "form" or "frameset" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "head" or "hr" or "li" or "listing" or "menu" or "ol" or "p" or "pre" or "table" or "tbody" or "td" or "tfoot" or "th" or "title" or "tr" or "ul" or "xmp",
        "pre" => newName is "dd" or "dl" or "dt" or "fieldset" or "form" or "li" or "table" or "ul",
        "s" => newName is "p",
        "script" => newName is "noscript",
        "small" => newName is "p",
        "span" => newName is "td" or "th",
        "strike" => newName is "p",
        "style" => newName is "body" or "frameset",
        "tbody" => newName is "tbody" or "tfoot",
        "td" => newName is "tbody" or "td" or "tfoot" or "th" or "tr",
        "tfoot" => newName is "tbody",
        "th" => newName is "tbody" or "td" or "tfoot" or "th" or "tr",
        "thead" => newName is "tbody" or "tfoot",
        "title" => newName is "body" or "frameset",
        "tr" => newName is "tbody" or "tfoot" or "tr",
        "tt" => newName is "p",
        "u" => newName is "p" or "td" or "th",
        "ul" => newName is "address" or "form" or "menu" or "pre",
        "xmp" => newName is "dd" or "dl" or "dt" or "fieldset" or "form" or "li" or "table" or "ul",
        _ => false
    };
    internal static int Priority(string name) => name switch { "div" => 150, "td" or "th" => 160, "tr" => 170, "thead" or "tbody" or "tfoot" => 180, "table" => 190, "head" or "body" => 200, "html" => 220, _ => 100 };
    internal static bool Void(string name) => name is "area" or "base" or "basefont" or "br" or "col" or "frame" or "hr" or "img" or "input" or "isindex" or "link" or "meta" or "param";
    internal static bool Boolean(string name) => name is "checked" or "compact" or "declare" or "defer" or "disabled" or "ismap" or "multiple" or "nohref" or "noresize" or "noshade" or "nowrap" or "readonly" or "selected";
    internal static int TextMode(string name) => name switch { "title" or "textarea" => 1, "script" => 3, "plaintext" => 4, "style" or "xmp" or "iframe" or "noembed" or "noframes" => 2, _ => 0 };
}
