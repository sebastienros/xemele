using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xemele.Html;

namespace Xemele.Fuzz.Html;

// Callable owned lane, like XPath/DeterministicCorpus; shared bootstrap Program stays unchanged.
public static class DeterministicCorpus
{
    public const uint Seed = 0x4D374854;
    public const int Iterations = 96;
    public static string Run()
    {
        Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription, "runtime");
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        int byteDocuments = 0, byteStreams = 0;
        int documents = 0, streams = 0, fragments = 0, ownership = 0, quotas = 0, cancellations = 0, faults = 0, tokens = 0;
        string[] atoms = ["<p>", "</p>", "<b><i>", "</b></i>", "<table>", "<tr><td>", "</table>", "<select><option>", "</select>", "<template>", "</template>", "<svg><foreignObject>", "</foreignObject></svg>", "<math><mi>", "</mi></math>", "<!--x-->", "<!--broken", "<?Pi 😀?>", "<textarea>", "</textarea>", "<noscript>", "</noscript>", "<script><!--", "</script>", "<a ID=I class='a b' data='&amp;'>", "</a>", "&notin;&amp;&#0;&#x1F600;", "\0", "é😀𝒜", "\r\n", "\uD800", "\uDC00", "</", "<!", "<input a=1 A=2/>"];
        string[] contexts = ["div", "table", "tbody", "tr", "td", "select", "textarea", "script", "noscript", "template", "form", "foreignObject", "mi", "annotation-xml"];
        try
        {
            foreach (string culture in new[] { "", "tr-TR", "fr-FR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                uint state = Seed;
                int Next(int max) { state = unchecked(state * 1664525 + 1013904223); return (int)(state % (uint)max); }
                for (int iteration = 0; iteration < Iterations; iteration++)
                {
                    string raw = "<!doctype html><body>" + string.Concat(Enumerable.Range(0, 5 + Next(20)).Select(_ => atoms[Next(atoms.Length)]));
                    // Explicit invalid UTF16 replacement is independent Encoding.UTF8 semantics;
                    // do not assert string parsing inherits byte-decoder behavior on unpaired surrogates.
                    string source = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(raw));
                    byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(source)];
                    for (int profile = 0; profile < 3; profile++)
                    {
                        string label = $"{culture}/seed={Seed}/iteration={iteration}/profile={profile}/input={JsonSerializer.Serialize(source)}";
                        HtmlDocument ParseText() => profile == 0 ? HtmlParser.Parse(source) : Html5Parser.Parse(source, new() { ScriptingEnabled = profile == 2 });
                        HtmlDocument ParseBytes(byte[] b) => profile == 0 ? HtmlParser.Parse(b) : Html5Parser.Parse(b, new() { ScriptingEnabled = profile == 2 });
                        HtmlDocument Load(Stream s, CancellationToken ct = default) => profile == 0 ? HtmlParser.Load(s, cancellationToken: ct) : Html5Parser.Load(s, new() { ScriptingEnabled = profile == 2 }, ct);
                        var doc = ParseText(); CheckGraph(doc.DocumentNode); string expected = Snapshot(doc); documents++;
                        byte[] borrowed = bytes.ToArray(); var fromBytes = ParseBytes(borrowed); Array.Fill(borrowed, (byte)'x');
                        Equal(expected, Snapshot(fromBytes), "borrowed bytes:" + label); CheckGraph(fromBytes.DocumentNode); ownership++;
                        foreach (int chunk in new[] { 1, 2, 7, 31 })
                        {
                            using var stream = new CallerStream(bytes, chunk);
                            var loaded = Load(stream); Equal(expected, Snapshot(loaded), "short-read:" + chunk + ":" + label); CheckGraph(loaded.DocumentNode);
                            Require(!stream.Disposed && stream.Position == bytes.Length, "stream ownership/EOF:" + label); streams++;
                        }
                        using var canceled = new CancellationTokenSource(); canceled.Cancel();
                        using var untouched = new CallerStream(bytes, 1);
                        Expect<OperationCanceledException>(() => Load(untouched, canceled.Token), e => e.CancellationToken == canceled.Token);
                        Require(untouched.Reads == 0 && !untouched.Disposed, "entry cancellation"); cancellations++;
                        using var during = new CancellationTokenSource();
                        using var cancelStream = new CallerStream(Encoding.UTF8.GetBytes("<p>" + new string('x', 12000)), 17, onRead: () => during.Cancel());
                        Expect<OperationCanceledException>(() => Load(cancelStream, during.Token), e => e.CancellationToken == during.Token);
                        Require(!cancelStream.Disposed, "mid-read cancellation ownership"); cancellations++;
                        var sentinel = new IOException("M7 fuzz read sentinel"); using var broken = new CallerStream(bytes, 1, failure: sentinel);
                        Expect<IOException>(() => Load(broken), e => ReferenceEquals(e, sentinel)); Require(!broken.Disposed, "I/O fault ownership"); faults++;
                        foreach (var mode in new[] { HtmlSerializationMode.Html5, HtmlSerializationMode.Legacy })
                        {
                            var settings = new HtmlSaveOptions { Mode = mode }; using var output = new MemoryStream();
                            HtmlSerializer.Save(doc, output, settings); Require(output.CanWrite, "save ownership");
                            Equal(HtmlSerializer.Serialize(doc, settings), Encoding.UTF8.GetString(output.ToArray()), "save/serialize:" + label);
                        }
                    }
                    string contextName = contexts[iteration % contexts.Length];
                    string uri = contextName == "foreignObject" ? "http://www.w3.org/2000/svg" : contextName is "mi" or "annotation-xml" ? "http://www.w3.org/1998/Math/MathML" : "http://www.w3.org/1999/xhtml";
                    var owner = new HtmlDocument(); var form = owner.CreateElement("form"); owner.DocumentNode.AppendChild(form);
                    var context = owner.CreateElement(contextName, uri); form.AppendChild(context);
                    if (contextName == "annotation-xml") context.SetAttribute(new("encoding", "text/html"));
                    context.SetAttribute(new("data-context", "unchanged"));
                    string before = Snapshot(owner);
                    foreach (bool scripting in new[] { false, true })
                    {
                        var settings = new Html5ParseOptions { ScriptingEnabled = scripting };
                        var fragmentContext = new HtmlFragmentContext(context, (HtmlQuirksMode)(iteration % 3));
                        var fragment = Html5Parser.ParseFragment(raw, fragmentContext, settings); CheckGraph(fragment);
                        Require(fragment.Kind == HtmlNodeKind.Fragment && fragment.Parent == null && fragment.OwnerDocument != owner, "fresh detached fragment");
                        Equal(before, Snapshot(owner), "context untouched");
                        var repeat = Html5Parser.ParseFragment(raw, fragmentContext, settings); Equal(NodeSnapshot(fragment), NodeSnapshot(repeat), "repeat fragment");
                        var clone = fragment.Clone(); Require(clone.OwnerDocument == fragment.OwnerDocument && clone.Parent == null, "clone owner"); Equal(NodeSnapshot(fragment), NodeSnapshot(clone), "clone graph");
                        int count = fragment.Children.Count; var imported = owner.Import(fragment); Require(imported.OwnerDocument == owner && imported.Parent == null, "import owner");
                        Equal(NodeSnapshot(fragment), NodeSnapshot(imported), "import graph"); var destination = owner.CreateElement("destination"); destination.AppendChild(imported);
                        Require(imported.Children.Count == 0 && destination.Children.Count == count && fragment.Children.Count == count, "fragment child transfer");
                        CheckGraph(destination); fragments++; ownership += 3;
                    }
                    var retained = new List<HtmlToken>(); Html5Tokenizer.Tokenize(raw, retained.Add); string frozen = JsonSerializer.Serialize(retained);
                    Require(retained.Count(t => t.Kind == HtmlTokenKind.EndOfFile) == 1, "one EOF");
                    Html5Tokenizer.Tokenize("<p a=changed><?Other changed?>"); Equal(frozen, JsonSerializer.Serialize(retained), "retained callback snapshots"); tokens++;
                }
            }
            uint byteState = 0x4D374254;
            int ByteNext(int max) { byteState = unchecked(byteState * 1664525 + 1013904223); return (int)(byteState % (uint)max); }
            for (int iteration = 0; iteration < 64; iteration++)
            {
                byte[] payload = new byte[1 + ByteNext(128)]; for (int i = 0; i < payload.Length; i++) payload[i] = (byte)ByteNext(256);
                byte[] bytes = [0xEF, 0xBB, 0xBF, .. "<p>"u8, .. payload];
                for (int profile = 0; profile < 3; profile++)
                {
                    var doc = profile == 0 ? HtmlParser.Parse(bytes) : Html5Parser.Parse(bytes, new() { ScriptingEnabled = profile == 2 });
                    CheckGraph(doc.DocumentNode); string expected = Snapshot(doc); byteDocuments++;
                    foreach (int chunk in new[] { 1, 2, 7, 31 })
                    {
                        using var stream = new CallerStream(bytes, chunk);
                        var loaded = profile == 0 ? HtmlParser.Load(stream) : Html5Parser.Load(stream, new() { ScriptingEnabled = profile == 2 });
                        Equal(expected, Snapshot(loaded), "byte seed=0x4D374254/iteration=" + iteration + "/profile=" + profile + "/chunk=" + chunk + "/hex=" + Convert.ToHexString(bytes));
                        CheckGraph(loaded.DocumentNode); Require(!stream.Disposed, "binary stream ownership"); byteStreams++;
                    }
                }
            }
            // Exact scalar input boundaries and all finite quota families, on both profiles.
            foreach (bool legacy in new[] { false, true })
            {
                HtmlDocument Parse(string input, XmlResourceLimits limits) => legacy ? HtmlParser.Parse(input, new() { Limits = limits }) : Html5Parser.Parse(input, new() { Limits = limits });
                _ = Parse("<p>😀", new() { DocumentCharacters = 4 });
                ExpectQuota(() => Parse("<p>😀", new() { DocumentCharacters = 3 })); quotas++;
                foreach (var item in new (string Input, XmlResourceLimits Limits)[]
                {
                    ("<p>abc", new() { ExpandedCharacters = 2 }),
                    ("<longname>", new() { TokenCharacters = 3 }),
                    ("<p a=1 b=2>", new() { AttributesPerElement = 1 }),
                    ("<p><b><i><u><em>", new() { Depth = 3 }),
                    ("<p a=1 b=2 c=3 d=4>", new() { InternedNames = 3 })
                }) { ExpectQuota(() => Parse(item.Input, item.Limits)); quotas++; }
            }
            return JsonSerializer.Serialize(new { seed = Seed, iterations = Iterations, cultures = 3, profiles = 3, byteSeed = 0x4D374254, byteIterations = 64, byteDocuments, byteStreams, documents, streams, fragments, ownership, quotas, cancellations, faults, tokens, runtime = RuntimeInformation.FrameworkDescription });
        }
        finally { CultureInfo.CurrentCulture = previousCulture; CultureInfo.CurrentUICulture = previousUi; }
    }
    private static void ExpectQuota(Action action)
    {
        try { action(); }
        catch (XmlParseException error) when (error.Diagnostics.Any(d => d.Code.Contains("limit", StringComparison.OrdinalIgnoreCase))) { return; }
        catch (InvalidOperationException error) when (error.Message.Contains("quota", StringComparison.OrdinalIgnoreCase)) { return; }
        throw new InvalidOperationException("Expected a named quota failure.");
    }
    private static void Expect<T>(Action action, Func<T, bool> predicate) where T : Exception
    { try { action(); } catch (T e) when (predicate(e)) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static void Equal(string expected, string actual, string label)
    { if (!StringComparer.Ordinal.Equals(expected, actual)) throw new InvalidOperationException(label + "\nexpected=" + expected + "\nactual=" + actual); }
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
    private static string Snapshot(HtmlDocument doc) => JsonSerializer.Serialize(new { doc.QuirksMode, doc.ScriptingEnabled, tree = NodeSnapshot(doc.DocumentNode) });
    private static string NodeSnapshot(HtmlNode n) => JsonSerializer.Serialize(new { n.Kind, n.LocalName, n.NamespaceUri, n.Prefix, n.Value, n.Doctype, attributes = n.Attributes, children = n.Children.Select(NodeSnapshot), template = n.TemplateContent == null ? null : NodeSnapshot(n.TemplateContent) });
    private static void CheckGraph(HtmlNode root)
    {
        var visited = new HashSet<HtmlNode>(ReferenceEqualityComparer.Instance);
        void Visit(HtmlNode node)
        {
            Require(visited.Add(node), "cycle/duplicate identity"); Require(node.OwnerDocument == root.OwnerDocument, "permanent owner");
            Require(node.FirstChild == node.Children.FirstOrDefault() && node.LastChild == node.Children.LastOrDefault(), "endpoint links");
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i]; Require(child.Parent == node && child.PreviousSibling == (i == 0 ? null : node.Children[i - 1]) && child.NextSibling == (i + 1 == node.Children.Count ? null : node.Children[i + 1]), "parent/sibling links"); Visit(child);
            }
            if (node.TemplateContent is { } content) { Require(content.Parent == null && content.Kind == HtmlNodeKind.Fragment, "template graph"); Visit(content); }
        }
        Visit(root);
    }
    private sealed class CallerStream(byte[] bytes, int chunk, IOException? failure = null, Action? onRead = null) : Stream
    {
        private int position;
        internal bool Disposed; internal int Reads;
        public override int Read(byte[] buffer, int offset, int count)
        {
            Reads++; onRead?.Invoke(); if (failure != null) throw failure;
            int length = Math.Min(Math.Min(chunk, count), bytes.Length - position); bytes.AsSpan(position, length).CopyTo(buffer.AsSpan(offset)); position += length; return length;
        }
        public override bool CanRead => !Disposed; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => bytes.Length; public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
