using System.Text;
using Xemele.Html;

if (args.Length == 0) { Console.WriteLine(Dump(Html5Parser.Parse("<!doctype html><p>A<b>B<p>C").DocumentNode)); return; }
string root = Path.GetFullPath(args[0]); var failures = new List<object>(); int count = 0, excluded = 0;
foreach (string file in Directory.GetFiles(root, "*.dat").Order(StringComparer.Ordinal))
{
    string source = File.ReadAllText(file); var sections = source.Split("#data\n", StringSplitOptions.None);
    for (int index = 1; index < sections.Length; index++)
    {
        string section = sections[index]; int error = section.IndexOf("#errors", StringComparison.Ordinal); if (error < 0) throw new Exception("No errors section");
        string input = error == 0 ? "" : section[..(error - 1)]; int doc = section.IndexOf("#document\n", StringComparison.Ordinal); if (doc < 0) throw new Exception("No document section");
        string expected = section[(doc + 10)..].TrimEnd('\n'); int desc = expected.IndexOf("\n\n#description", StringComparison.Ordinal); if (desc >= 0) expected = expected[..desc];
        string? context = null; int frag = section.IndexOf("#document-fragment\n", StringComparison.Ordinal); if (frag >= 0) { int start = frag + 19; int end = section.IndexOf('\n', start); context = section[start..end]; }
        bool[] scripting = section.Contains("#script-on\n", StringComparison.Ordinal) ? [true] : section.Contains("#script-off\n", StringComparison.Ordinal) ? [false] : [false, true];
        if (Path.GetFileName(file).StartsWith("scripted_", StringComparison.Ordinal)) { excluded += scripting.Length; continue; }
        foreach (bool script in scripting)
        {
            count++; string actual;
            try
            {
                var settings = new Html5ParseOptions { ScriptingEnabled = script };
                HtmlNode node;
                if (context is null) node = Html5Parser.Parse(input, settings).DocumentNode;
                else { string ns = context.StartsWith("svg ", StringComparison.Ordinal) ? "http://www.w3.org/2000/svg" : context.StartsWith("math ", StringComparison.Ordinal) ? "http://www.w3.org/1998/Math/MathML" : "http://www.w3.org/1999/xhtml"; string name = context.Contains(' ') ? context[(context.IndexOf(' ') + 1)..] : context; var owner = new HtmlDocument(); node = Html5Parser.ParseFragment(input, new(owner.CreateElement(name, ns)), settings); }
                actual = Dump(node).TrimEnd('\n');
            }
            catch (Exception ex) { actual = ex.ToString(); }
            if (actual != expected) failures.Add(new { File = Path.GetFileName(file), Index = index - 1, Script = script, Context = context, Input = input, Expected = expected, Actual = actual });
        }
    }
}
string output = args.Length > 1 ? args[1] : "/private/tmp/xemele-builder-failures.json";
File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(failures, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Cases {count}; mismatches {failures.Count}; provisional scripted classification {excluded}; report {output}");
return;
static string Dump(HtmlNode root)
{
    var b = new StringBuilder(); foreach (var child in root.Children) Node(child, 0); return b.ToString();
    void Line(int level, string value) { b.Append("| "); b.Append(' ', level * 2); b.Append(value); b.Append('\n'); }
    void Node(HtmlNode n, int level)
    {
        switch (n.Kind)
        {
            case HtmlNodeKind.Element:
                Line(level, "<" + (n.NamespaceUri == "http://www.w3.org/2000/svg" ? "svg " : n.NamespaceUri == "http://www.w3.org/1998/Math/MathML" ? "math " : "") + n.LocalName + ">");
                foreach (var a in n.Attributes.OrderBy(a => AttributeName(a), StringComparer.Ordinal)) Line(level + 1, AttributeName(a) + "=\"" + a.Value + "\"");
                foreach (var child in n.Children) Node(child, level + 1);
                if (n.TemplateContent is { } template) { Line(level + 1, "content"); foreach (var child in template.Children) Node(child, level + 2); } break;
            case HtmlNodeKind.Text: Line(level, "\"" + n.Value + "\""); break;
            case HtmlNodeKind.Comment: Line(level, "<!-- " + n.Value + " -->"); break;
            case HtmlNodeKind.ProcessingInstruction: Line(level, "<?" + n.LocalName + " " + n.Value + "?>"); break;
            case HtmlNodeKind.DocumentType:
                var d = n.Doctype!; Line(level, "<!DOCTYPE " + d.Name + (!string.IsNullOrEmpty(d.PublicIdentifier) || !string.IsNullOrEmpty(d.SystemIdentifier) ? " \"" + d.PublicIdentifier + "\" \"" + d.SystemIdentifier + "\"" : "") + ">"); break;
        }
    }
    static string AttributeName(HtmlAttribute a) => (a.NamespaceUri switch { "http://www.w3.org/1999/xlink" => "xlink ", "http://www.w3.org/XML/1998/namespace" => "xml ", "http://www.w3.org/2000/xmlns/" => "xmlns ", _ => "" }) + a.LocalName;
}
