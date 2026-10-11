using System.Text;
using Xemele.Css;
using Xemele.Html;
namespace Xemele.Tool;
internal static class HtmlCommands
{
    // Optional borrowed streams are an internal test seam; public CLI uses standard/file streams.
    internal static int Run(string command, string[] args, CancellationToken cancellationToken = default, Stream? borrowedInput = null, Stream? borrowedOutput = null)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h") { Help(command); return 0; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        var token = cancellation.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            var invocation = HtmlInvocation.Parse(command, args);
            var options = new Html5ParseOptions { Limits = invocation.Limits, ScriptingEnabled = invocation.Scripting };
            var save = new HtmlSaveOptions { Mode = invocation.Mode };
            var selector = command == "html-select" ? Compile(invocation.Get("--selector")!, invocation) : null;
            HtmlFragmentContext? context = command == "html-fragment" ? Context(invocation, options, token) : null;
            using var staged = new MemoryStream(); // Deliberate final-result ownership boundary; failed work publishes no result.
            var input = OpenInput(invocation.Get("--input"), borrowedInput);
            try
            {
                if (context is not null)
                {
                    string text = ReadFragment(input, invocation.Limits.DocumentCharacters!.Value, token);
                    token.ThrowIfCancellationRequested();
                    var fragment = Html5Parser.ParseFragment(text, context, options);
                    token.ThrowIfCancellationRequested();
                    WriteText(staged, HtmlSerializer.Serialize(fragment, save));
                }
                else
                {
                    var document = invocation.Profile == "legacy" ? HtmlParser.Load(input, new() { Limits = invocation.Limits }, token) : Html5Parser.Load(input, options, token);
                    if (selector is null) HtmlSerializer.Save(document, staged, save, token);
                    else foreach (var element in selector.Select(document.DocumentNode))
                    {
                        token.ThrowIfCancellationRequested();
                        WriteText(staged, (invocation.Get("--result") == "text" ? element.GetTextContent() : HtmlSerializer.Serialize(element, save)) + "\n");
                    }
                }
            }
            finally { if (invocation.Get("--input") is not null) input.Dispose(); }
            token.ThrowIfCancellationRequested();
            Publish(staged, invocation.Get("--output"), borrowedOutput, token);
            return 0;
        }
        catch (HtmlInvocationException ex) { Error("Invocation", ex.Message); return 2; }
        catch (CssSelectorException ex) { Error("Selector", $"offset={ex.Offset}: {ex.Message}"); return 1; }
        catch (XmlParseException ex)
        {
            foreach (var d in ex.Diagnostics) Error(d.Code, $"line={d.Location.LineNumber} column={d.Location.LinePosition}: {d.Message}");
            return 1;
        }
        catch (InvalidOperationException ex) when (IsQuota(ex.Message)) { Error("Quota", ex.Message); return 1; }
        catch (HtmlRejectionException ex) { Error("Rejected", ex.Message); return 1; }
        catch (DecoderFallbackException ex) { Error("Encoding", ex.Message); return 1; }
        catch (OperationCanceledException) { Error("Canceled", "Operation canceled."); return 3; }
        catch (Exception ex) { Error("Internal", ex.Message); return 3; }
        finally { Console.CancelKeyPress -= cancel; }
    }
    // Current verified HTML engines use these exact InvalidOperationException messages
    // for finite quotas. Keep unrelated algorithm faults in the internal-failure lane.
    private static bool IsQuota(string message) => message is
        "HTML interned-name quota exceeded." or "HTML token quota exceeded." or
        "HTML attribute quota exceeded." or "HTML depth quota exceeded." or
        "HTML open-element depth quota exceeded." or
        "HTML tokenizer quota exceeded: DocumentCharacters" or
        "HTML tokenizer quota exceeded: TokenCharacters" or
        "HTML tokenizer quota exceeded: ExpandedCharacters" or
        "HTML tokenizer quota exceeded: AttributesPerElement";
    private static void Error(string code, string message) => Console.Error.WriteLine("HTML.CLI." + code + ": " + message);
    private static CssSelector Compile(string expression, HtmlInvocation invocation) => CssSelector.Compile(expression, new() { Namespaces = invocation.Namespaces });
    private static HtmlFragmentContext Context(HtmlInvocation invocation, Html5ParseOptions options, CancellationToken token)
    {
        if (invocation.Get("--context-file") is { } path)
        {
            var selector = Compile(invocation.Get("--context-selector")!, invocation);
            using var input = OpenInput(path, null);
            var document = Html5Parser.Load(input, options, token);
            var elements = selector.Select(document.DocumentNode);
            if (elements.Count != 1) throw new HtmlRejectionException("--context-selector must select exactly one element.");
            return new(elements[0], document.QuirksMode);
        }
        var owner = new HtmlDocument(invocation.Limits);
        return new(owner.CreateElement(invocation.Get("--context")!, invocation.Get("--context-namespace") ?? "http://www.w3.org/1999/xhtml"), invocation.Quirks);
    }
    private static string ReadFragment(Stream input, long scalarLimit, CancellationToken token)
    {
        // The frozen fragment API accepts a string. Bound the UTF-16 staging at twice
        // the scalar quota; the parser subsequently enforces the exact scalar units.
        long cap = scalarLimit > (long.MaxValue - 2) / 2 ? long.MaxValue : scalarLimit * 2 + 2;
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var text = new StringBuilder(); var buffer = new char[1024];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int read = reader.Read(buffer);
            if (read == 0) break;
            if (text.Length + (long)read > cap) throw new HtmlRejectionException("Fragment input exceeds document character quota.");
            text.Append(buffer, 0, read);
        }
        if (text.Length != 0 && text[0] == '\uFEFF') text.Remove(0, 1);
        return text.ToString();
    }
    internal static Stream OpenInput(string? path, Stream? borrowed)
    {
        if (path is null) return borrowed ?? Console.OpenStandardInput();
        try { return new HtmlFileInput(File.OpenRead(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new HtmlInvocationException(ex.Message); }
    }
    private static void WriteText(Stream stream, string text)
    {
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(text);
    }
    private static void Publish(MemoryStream staged, string? path, Stream? borrowed, CancellationToken token)
    {
        staged.Position = 0;
        if (path is null) { Copy(staged, borrowed ?? Console.OpenStandardOutput(), token); return; }
        string? temporary = null;
        try
        {
            string full = Path.GetFullPath(path);
            temporary = Path.Combine(Path.GetDirectoryName(full)!, ".xemele-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) Copy(staged, output, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, full, true); temporary = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new HtmlInvocationException(ex.Message); }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Copy(Stream input, Stream output, CancellationToken token)
    {
        var buffer = new byte[4096];
        int read;
        while ((read = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); }
    }
    private static void Help(string command)
    {
        Console.WriteLine("Usage: xemele " + command + " [--input PATH] [--output PATH] [--mode html5|legacy]");
        Console.WriteLine("Limits (positive integers, finite library defaults): --max-characters N --max-depth N --max-token-characters N");
        Console.WriteLine("Input/output default to stdin/stdout; UTF-8 output without BOM. Exit: 0 success, 1 rejection, 2 invocation, 3 cancellation/internal/I/O on standard streams.");
        if (command != "html-legacy") Console.WriteLine("--scripting true|false (default false): noscript parsing only; no JavaScript or network.");
        if (command == "html-select")
        {
            Console.WriteLine("Required: --selector CSS; --profile html5|legacy (default html5); --result markup|text (default markup), one match per LF in document order.");
            Console.WriteLine("Optional repeatable: --css-namespace PREFIX=URI (empty prefix sets default type namespace).");
            Console.WriteLine("CSS: lists, type/universal/id/class/attributes, descendant/child/sibling combinators, root/scope/empty, child/of-type position and nth pseudos, :not(compounds). Unsupported syntax rejects with UTF-16 offset; see samples/M7-cli/README.md.");
        }
        if (command == "html-fragment")
        {
            Console.WriteLine("HTML5 fragment, strict UTF-8 input (optional UTF-8 BOM); synchronous string parser; no encoding sniff.");
            Console.WriteLine("Required context: --context TAG [--context-namespace URI] [--quirks no-quirks|limited-quirks|quirks], or --context-file PATH --context-selector CSS [--css-namespace PREFIX=URI].");
            Console.WriteLine("Detached context defaults to HTML namespace/no-quirks. Context file uses HTML5 byte parsing and preserves selected element attributes, ancestors and document quirks. Fragment output has no wrapper.");
        }
        Console.WriteLine("Serialization default follows parser profile; HTML5 recovery and legacy recovery are distinct. No XML coercion, external entity fetch or scripting execution.");
    }
}
