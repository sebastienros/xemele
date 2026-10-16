using System.Text;
using System.Text.Json;
using Xemele.Html;
namespace Xemele.Tests.HtmlTreeConstruction;

internal static class TreeCorpus
{
    internal sealed record Result(int Originals, int Executions, int Mismatches, int BrowserExecutions, int NormativeConfigurations);
    internal static Result Run(string root, string report)
    {
        root = Path.GetFullPath(root);
        var failures = new List<object>();
        int count = 0, excluded = 0, originals = 0, normative = 0, documents = 0, fragments = 0;
        using var ledger = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "../../normative-discrepancies.json")));
        foreach (var file in Directory.GetFiles(root, "*.dat").Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            var sections = source.Split("#data\n", StringSplitOptions.None);
            for (var index = 1; index < sections.Length; index++)
            {
                var section = sections[index];
                var error = section.IndexOf("#errors", StringComparison.Ordinal);
                if (error < 0)
                {
                    throw new Exception("No errors section");
                }

                var input = error == 0 ? "" : section[..(error - 1)];
                var doc = section.IndexOf("#document\n", StringComparison.Ordinal);
                if (doc < 0)
                {
                    throw new Exception("No document section");
                }

                var expected = section[(doc + 10)..].TrimEnd('\n');
                var desc = expected.IndexOf("\n\n#description", StringComparison.Ordinal);
                if (desc >= 0)
                {
                    expected = expected[..desc];
                }

                string? context = null;
                var frag = section.IndexOf("#document-fragment\n", StringComparison.Ordinal);
                if (frag >= 0)
                {
                    var start = frag + 19;
                    var end = section.IndexOf('\n', start);
                    context = section[start..end];
                }
                bool[] scripting = section.Contains("#script-on\n", StringComparison.Ordinal) ? [true] : section.Contains("#script-off\n", StringComparison.Ordinal) ? [false] : [false, true];
                originals++;
                var browser = Find(ledger.RootElement.GetProperty("browser_execution"), Path.GetFileName(file), index - 1);
                if (browser is { } exclusion)
                {
                    if (exclusion.GetProperty("input").GetString() != input || exclusion.GetProperty("original_expected").GetString() != expected || !scripting.SequenceEqual([true]))
                    {
                        throw new InvalidOperationException("Exclusion does not match immutable original.");
                    }

                    excluded += scripting.Length;
                    continue;
                }
                var discrepancy = Find(ledger.RootElement.GetProperty("tree"), Path.GetFileName(file), index - 1);
                if (discrepancy is { } decision)
                {
                    if (decision.GetProperty("input").GetString() != input || decision.GetProperty("original_expected").GetString() != expected)
                    {
                        throw new InvalidOperationException("Normative decision does not match immutable original.");
                    }

                    expected = decision.GetProperty("current_expected").GetString()!;
                    normative += scripting.Length;
                }
                if (context is null)
                {
                    documents += scripting.Length;
                }
                else
                {
                    fragments += scripting.Length;
                }

                foreach (var script in scripting)
                {
                    foreach (var lane in context is null ? new[] { 0, 1, 2, 3 } : [0])
                    {
                        count++;
                        string actual;
                        try
                        {
                            var settings = new Html5ParseOptions { ScriptingEnabled = script };
                            HtmlNode node;
                            if (context is null)
                            {
                                byte[] utf8 = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(input)];
                                if (lane == 0)
                                {
                                    node = Html5Parser.Parse(input, settings).DocumentNode;
                                }
                                else if (lane == 1)
                                {
                                    node = Html5Parser.Parse(utf8, settings).DocumentNode;
                                }
                                else
                                {
                                    using var stream = new ChunkStream(utf8, lane == 2 ? 1 : 17);
                                    node = Html5Parser.Load(stream, settings).DocumentNode;
                                }
                            }
                            else
                            {
                                var ns = context.StartsWith("svg ", StringComparison.Ordinal) ? "http://www.w3.org/2000/svg" : context.StartsWith("math ", StringComparison.Ordinal) ? "http://www.w3.org/1998/Math/MathML" : "http://www.w3.org/1999/xhtml";
                                var name = context.Contains(' ') ? context[(context.IndexOf(' ') + 1)..] : context;
                                var owner = new HtmlDocument();
                                node = Html5Parser.ParseFragment(input, new(owner.CreateElement(name, ns)), settings);
                            }
                            actual = Dump(node).TrimEnd('\n');
                        }
                        catch (Exception ex) { actual = ex.ToString(); }
                        if (actual != expected)
                        {
                            failures.Add(new
                            {
                                File = Path.GetFileName(file),
                                Index = index - 1,
                                Script = script,
                                Lane = lane,
                                Context = context,
                                Input = input,
                                Expected = expected,
                                Actual = actual
                            });
                        }
                    }
                }
            }
        }
        File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(failures, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.ChangeExtension(report, ".inventory.json"), JsonSerializer.Serialize(new
        {
            OriginalFiles = 62,
            Originals = originals,
            Configurations = documents + fragments + excluded,
            BrowserExecutions = excluded,
            ApplicableConfigurations = documents + fragments,
            DocumentConfigurations = documents,
            FragmentConfigurations = fragments,
            Executions = count,
            NormativeConfigurations = normative,
            Mismatches = failures.Count
        }, new JsonSerializerOptions { WriteIndented = true }));
        return new(originals, count, failures.Count, excluded, normative);
    }
    private static JsonElement? Find(JsonElement entries, string file, int index)
    {
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.GetProperty("file").GetString() == file && entry.GetProperty("case").GetInt32() == index)
            {
                return entry;
            }
        }

        return null;
    }
    private sealed class ChunkStream(byte[] bytes, int chunk) : MemoryStream(bytes, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, chunk));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, chunk)]);
    }
    internal static string Dump(HtmlNode root)
    {
        var b = new StringBuilder();
        foreach (var child in root.Children)
        {
            Node(child, 0);
        }

        return b.ToString();
        void Line(int level, string value)
        {
            b.Append("| ");
            b.Append(' ', level * 2);
            b.Append(value);
            b.Append('\n');
        }
        void Node(HtmlNode n, int level)
        {
            switch (n.Kind)
            {
                case HtmlNodeKind.Element:
                    Line(level, "<" + (n.NamespaceUri == "http://www.w3.org/2000/svg" ? "svg " : n.NamespaceUri == "http://www.w3.org/1998/Math/MathML" ? "math " : "") + n.LocalName + ">");
                    foreach (var a in n.Attributes.OrderBy(a => AttributeName(a), StringComparer.Ordinal))
                    {
                        Line(level + 1, AttributeName(a) + "=\"" + a.Value + "\"");
                    }

                    foreach (var child in n.Children)
                    {
                        Node(child, level + 1);
                    }

                    if (n.TemplateContent is { } template)
                    {
                        Line(level + 1, "content");
                        foreach (var child in template.Children)
                        {
                            Node(child, level + 2);
                        }
                    }
                    break;
                case HtmlNodeKind.Text:
                    Line(level, "\"" + n.Value + "\"");
                    break;
                case HtmlNodeKind.Comment:
                    Line(level, "<!-- " + n.Value + " -->");
                    break;
                case HtmlNodeKind.ProcessingInstruction:
                    Line(level, "<?" + n.LocalName + " " + n.Value + "?>");
                    break;
                case HtmlNodeKind.DocumentType:
                    var d = n.Doctype!;
                    Line(level, "<!DOCTYPE " + d.Name + (!string.IsNullOrEmpty(d.PublicIdentifier) || !string.IsNullOrEmpty(d.SystemIdentifier) ? " \"" + d.PublicIdentifier + "\" \"" + d.SystemIdentifier + "\"" : "") + ">");
                    break;
            }
        }
        static string AttributeName(HtmlAttribute a) => (a.NamespaceUri switch
        {
            "http://www.w3.org/1999/xlink" => "xlink ",
            "http://www.w3.org/XML/1998/namespace" => "xml ",
            "http://www.w3.org/2000/xmlns/" => "xmlns ",
            _ => ""
        }) + a.LocalName;
    }

}
