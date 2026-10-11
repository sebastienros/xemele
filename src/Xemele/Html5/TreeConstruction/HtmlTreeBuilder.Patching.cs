using System.Buffers;
using System.Text;
namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private sealed record Patch(HtmlNode Parent, HtmlNode Start, HtmlNode? End);
    private readonly Dictionary<HtmlNode, Patch> patches = new();
    private void InsertTemplate(HtmlToken token)
    {
        FlushText(); var location = Location(); var scope = location.Parent;
        if (Html(scope, "body")) scope = scope.Parent!;
        var template = Insert(token); string? name = template.GetAttribute("for"); if (string.IsNullOrEmpty(name)) return;
        HtmlNode? start = null, end = null;
        foreach (var node in Descendants(scope))
        {
            if (node.Kind != HtmlNodeKind.ProcessingInstruction || PiNameAttribute(node.Value) != name) continue;
            if (node.LocalName == "marker") { start = end = node; break; }
            if (node.LocalName != "start") continue;
            start = node; int nesting = 0;
            for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
            {
                if (sibling.Kind != HtmlNodeKind.ProcessingInstruction) continue;
                if (sibling.LocalName == "start") nesting++;
                if (sibling.LocalName == "end") { if (nesting == 0) { end = sibling; break; } nesting--; }
            }
            break;
        }
        if (start is null) return;
        template.Remove(); patches.Add(template, new(start.Parent!, start, end));
        if (start == end) return;
        for (var current = start.NextSibling; current is not null && current != end; ) { var next = current.NextSibling; current.Remove(); current = next; }
    }
    private void ClosePatch(HtmlNode template)
    {
        FlushText(); if (!patches.Remove(template, out var patch)) return; patch.Start.Remove(); patch.End?.Remove();
    }
    // DOM's PI attribute map uses XML-stylesheet pseudo-attributes, not HTML tag
    // attributes. Any malformed or duplicate attribute invalidates the whole map.
    private static string? PiNameAttribute(string data)
    {
        var input = data.AsSpan(); int p = 0; var names = new HashSet<string>(StringComparer.Ordinal); string? result = null;
        while (true)
        {
            while (p < input.Length && XmlSpace(input[p])) p++; if (p == input.Length) return result;
            int start = p; bool first = true;
            while (p < input.Length && Rune.DecodeFromUtf16(input[p..], out var rune, out int consumed) == OperationStatus.Done &&
                (first ? Xemele.Parsing.Dtd.ValueSyntax.IsNameStart(rune.Value) : Xemele.Parsing.Dtd.ValueSyntax.IsNameChar(rune.Value))) { first = false; p += consumed; }
            if (first) return null; string name = input[start..p].ToString(); if (!names.Add(name)) return null;
            while (p < input.Length && XmlSpace(input[p])) p++; if (p == input.Length || input[p++] != '=') return null;
            while (p < input.Length && XmlSpace(input[p])) p++; if (p == input.Length || input[p] is not ('\'' or '"')) return null;
            char quote = input[p++]; var value = new StringBuilder();
            while (p < input.Length && input[p] != quote)
            {
                char c = input[p++]; if (c == '<') return null;
                if (c != '&') { value.Append(c); continue; }
                int semicolon = input[p..].IndexOf(';'); if (semicolon < 0) return null;
                var reference = input.Slice(p, semicolon); p += semicolon + 1;
                int scalar = reference.SequenceEqual("amp") ? '&' : reference.SequenceEqual("lt") ? '<' : reference.SequenceEqual("gt") ? '>' : reference.SequenceEqual("quot") ? '"' : reference.SequenceEqual("apos") ? '\'' : -1;
                if (scalar < 0 && reference.StartsWith("#"))
                {
                    int baseValue = 10, digit = 1; if (digit < reference.Length && reference[digit] == 'x') { baseValue = 16; digit++; }
                    int digitStart = digit; uint number = 0;
                    while (digit < reference.Length) { char ch = reference[digit++]; int d = ch is >= '0' and <= '9' ? ch - '0' : baseValue == 16 && ch is >= 'a' and <= 'f' ? ch - 'a' + 10 : baseValue == 16 && ch is >= 'A' and <= 'F' ? ch - 'A' + 10 : -1; if (d < 0 || d >= baseValue || number > 0x10FFFF / baseValue) return null; number = number * (uint)baseValue + (uint)d; }
                    if (digit == digitStart || !(number is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF)) return null; scalar = (int)number;
                }
                if (scalar < 0) return null; value.Append(char.ConvertFromUtf32(scalar));
            }
            if (p == input.Length) return null; p++; if (p < input.Length && !XmlSpace(input[p])) return null;
            if (name == "name") result = value.ToString();
        }
    }
    private static bool XmlSpace(char c) => c is ' ' or '\t' or '\n' or '\r';
}
