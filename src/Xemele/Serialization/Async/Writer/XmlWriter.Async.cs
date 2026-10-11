using System.Buffers;
using System.Globalization;
using System.Text;
namespace Xemele;

// State, pending tag and scope are shared with the verified synchronous writer.
// Only retained memory and integer cursors cross transport awaits.
public sealed partial class XmlWriter : IAsyncDisposable
{
    private CancellationToken asyncToken;
    private CancellationTokenSource? asyncLink;
    private void BeginAsyncCall(CancellationToken call)
    {
        asyncToken = token;
        if (call.CanBeCanceled)
        {
            if (token.CanBeCanceled && token != call)
            {
                asyncLink = CancellationTokenSource.CreateLinkedTokenSource(token, call);
                asyncToken = asyncLink.Token;
            }
            else asyncToken = call;
        }
        asyncToken.ThrowIfCancellationRequested();
    }
    private void EndAsyncCall()
    {
        asyncLink?.Dispose(); asyncLink = null; asyncToken = default;
    }
    private void AsyncTick()
    {
        if ((++steps & 2047) == 0) asyncToken.ThrowIfCancellationRequested();
    }
    public ValueTask DisposeAsync() { Dispose(); return default; }
    public async ValueTask WriteStartDocumentAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            if (started) Invalid();
            Mutable(); await StartAsync().ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteStartElementAsync(string localName, string namespaceUri = "", string prefix = "", CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ValidateExpandedAsyncInput(localName, namespaceUri, prefix);
            if (frames.Count == 0 && root) Invalid();
            Mutable(); await StartAsync().ConfigureAwait(false); await StructuralAsync().ConfigureAwait(false);
            int mark = undo.Count;
            frames.Add(new(localName, namespaceUri, prefix, mark, false, ContentMode.Undecided));
            if (frames.Count == 1) root = true;
            attributes.Clear(); declarations.Clear(); local.Clear(); reserved.Clear(); expanded.Clear();
            reserved.Add(prefix);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteNamespaceAsync(string prefix, string namespaceUri, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            await default(ValueTask).ConfigureAwait(false);
            Pending(); ValidateBindingAsyncInput(prefix, namespaceUri);
            if (local.Contains(prefix)) Invalid();
            Mutable(); local.Add(prefix); reserved.Add(prefix); declarations.Add(new(prefix, namespaceUri));
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteAttributeAsync(string localName, string value, string namespaceUri = "", string prefix = "", CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            await default(ValueTask).ConfigureAwait(false);
            Pending(); ValidateExpandedAsyncInput(localName, namespaceUri, prefix, attribute: true); ValidateTextAsyncInput(value);
            if (expanded.Contains((namespaceUri, localName))) Invalid();
            Mutable(); expanded.Add((namespaceUri, localName)); reserved.Add(prefix);
            attributes.Add(new(localName, value, namespaceUri, prefix));
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteTextAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ActiveElement(); ValidateTextAsyncInput(text.Span); CanMix();
            Mutable(); Mix(); await SealAsync(false).ConfigureAwait(false); await EscapeAsync(text, false).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteCDataAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ActiveElement(); ValidateTextAsyncInput(text.Span); DelimiterAsyncInput(text.Span, "]]>"); CanMix();
            Mutable(); Mix(); await SealAsync(false).ConfigureAwait(false); brackets = 0;
            await output.WriteAsync("<![CDATA[", asyncToken).ConfigureAwait(false); await output.WriteAsync(text, asyncToken).ConfigureAwait(false); await output.WriteAsync("]]>", asyncToken).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteCommentAsync(ReadOnlyMemory<char> text, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ValidateTextAsyncInput(text.Span); DelimiterAsyncInput(text.Span, "--");
            if (text.Span.EndsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Illegal comment delimiter.", nameof(text));
            Mutable(); await StartAsync().ConfigureAwait(false); await StructuralAsync().ConfigureAwait(false); brackets = 0;
            await output.WriteAsync("<!--", asyncToken).ConfigureAwait(false); await output.WriteAsync(text, asyncToken).ConfigureAwait(false); await output.WriteAsync("-->", asyncToken).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteProcessingInstructionAsync(string target, ReadOnlyMemory<char> data, CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ValidateNameAsyncInput(target, colon: true); ValidateTextAsyncInput(data.Span); DelimiterAsyncInput(data.Span, "?>");
            if (target.Equals("xml", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Reserved PI target.", nameof(target));
            Mutable(); await StartAsync().ConfigureAwait(false); await StructuralAsync().ConfigureAwait(false); brackets = 0;
            await output.WriteAsync("<?", asyncToken).ConfigureAwait(false); await output.WriteAsync(target, asyncToken).ConfigureAwait(false);
            if (!data.IsEmpty) { await output.WriteAsync(" ", asyncToken).ConfigureAwait(false); await output.WriteAsync(data, asyncToken).ConfigureAwait(false); } await output.WriteAsync("?>", asyncToken).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteEndElementAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            ActiveElement(); Mutable();
            var frame = frames[^1];
            if (!frame.Sealed) await SealAsync(true).ConfigureAwait(false);
            else
            {
                if (options.Indent && frame.Mode == ContentMode.Elements) await IndentAsync(frames.Count - 1).ConfigureAwait(false);
                await output.WriteAsync("</", asyncToken).ConfigureAwait(false); await QNameAsync(frame.Prefix, frame.Name).ConfigureAwait(false); await output.WriteAsync(">", asyncToken).ConfigureAwait(false);
            }
            RestoreAsyncInput(frame.Mark); frames.RemoveAt(frames.Count - 1); brackets = 0;
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask WriteEndDocumentAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        try
        {
            BeginAsyncCall(cancellationToken);
            if (!root || frames.Count != 0) Invalid();
            Mutable(); await output.CompleteAsync(asyncToken).ConfigureAwait(false); completed = true;
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        Enter(allowCompleted: true);
        try
        {
            BeginAsyncCall(cancellationToken);
            Mutable(); if (frames.Count != 0) await SealAsync(false).ConfigureAwait(false);
            await output.CompleteAsync(asyncToken).ConfigureAwait(false); asyncToken.ThrowIfCancellationRequested(); await output.Stream.FlushAsync(asyncToken).ConfigureAwait(false);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { EndAsyncCall(); Exit(); }
    }
    private async ValueTask StartAsync()
    {
        if (started) return;
        started = true;
        if (options.EmitByteOrderMark) await output.PreambleAsync(asyncToken).ConfigureAwait(false);
        if (!options.OmitXmlDeclaration)
        {
            await output.WriteAsync("<?xml version=\"1.0\" encoding=\"", asyncToken).ConfigureAwait(false); await output.WriteAsync(output.Label, asyncToken).ConfigureAwait(false); await output.WriteAsync("\"?>", asyncToken).ConfigureAwait(false);
        }
    }
    private async ValueTask StructuralAsync()
    {
        brackets = 0;
        if (frames.Count == 0)
        {
            if (options.Indent && (documentNode || !options.OmitXmlDeclaration)) await output.WriteAsync("\n", asyncToken).ConfigureAwait(false);
            documentNode = true; return;
        }
        await SealAsync(false).ConfigureAwait(false);
        var parent = frames[^1];
        if (parent.Mode == ContentMode.Undecided) { parent.Mode = ContentMode.Elements; frames[^1] = parent; }
        if (options.Indent && parent.Mode == ContentMode.Elements) await IndentAsync(frames.Count).ConfigureAwait(false);
    }
    private async ValueTask SealAsync(bool empty)
    {
        if (frames.Count == 0 || frames[^1].Sealed) return;
        var f = frames[^1];
        // Resolve all names before emitting this tag. Scope mutation failures fault
        // the operation, because the pending state is no longer recoverable.
        for (int i = 0; i < declarations.Count; i++)
        {
            AsyncTick(); var d = declarations[i];
            if (options.NamespaceHandling == XmlNamespaceHandling.Repair && d.Prefix.Length == 0 && f.Uri.Length == 0)
            { d = new(d.Prefix, ""); declarations[i] = d; }
            Bind(d.Prefix, d.Uri);
        }
        f.Prefix = ResolveAsyncInput(f.Prefix, f.Uri, false);
        attributePrefixes.Clear();
        foreach (var a in attributes) { AsyncTick(); attributePrefixes.Add(ResolveAsyncInput(a.Prefix, a.Uri, true)); }
        await output.WriteAsync("<", asyncToken).ConfigureAwait(false); await QNameAsync(f.Prefix, f.Name).ConfigureAwait(false);
        foreach (var d in declarations)
        {
            AsyncTick(); await output.WriteAsync(" xmlns", asyncToken).ConfigureAwait(false);
            if (d.Prefix.Length != 0) { await output.WriteAsync(":", asyncToken).ConfigureAwait(false); await output.WriteAsync(d.Prefix, asyncToken).ConfigureAwait(false); }
            await output.WriteAsync("=\"", asyncToken).ConfigureAwait(false); await EscapeAsync(d.Uri.AsMemory(), true).ConfigureAwait(false); await output.WriteAsync("\"", asyncToken).ConfigureAwait(false);
        }
        for (int i = 0; i < attributes.Count; i++)
        {
            AsyncTick(); var a = attributes[i]; await output.WriteAsync(" ", asyncToken).ConfigureAwait(false); await QNameAsync(attributePrefixes[i], a.Name).ConfigureAwait(false);
            await output.WriteAsync("=\"", asyncToken).ConfigureAwait(false); await EscapeAsync(a.Value.AsMemory(), true).ConfigureAwait(false); await output.WriteAsync("\"", asyncToken).ConfigureAwait(false);
        }
        await output.WriteAsync(empty ? "/>" : ">", asyncToken).ConfigureAwait(false); f.Sealed = true; frames[^1] = f;
        // Release caller strings as soon as the pending start tag is sealed.
        attributes.Clear(); declarations.Clear(); local.Clear(); reserved.Clear(); expanded.Clear(); attributePrefixes.Clear();
    }
    private string ResolveAsyncInput(string prefix, string uri, bool attribute)
    {
        if (uri == TreeValidation.XmlUri) return "xml";
        if (attribute && uri.Length == 0) return "";
        if ((!attribute || prefix.Length != 0) && Binding(prefix) == uri) return prefix;
        if (options.NamespaceHandling == XmlNamespaceHandling.Strict) Invalid();
        if (!attribute && uri.Length == 0) { Generate("", ""); return ""; }
        if (!attribute && !local.Contains(prefix)) { Generate(prefix, uri); return prefix; }
        if (attribute)
        {
            string? best = null;
            foreach (var pair in scope)
            {
                AsyncTick();
                if (pair.Key.Length != 0 && pair.Value == uri && (best is null || string.CompareOrdinal(pair.Key, best) < 0)) best = pair.Key;
            }
            if (best is not null) return best;
        }
        string fresh;
        do { AsyncTick(); generated = checked(generated + 1); fresh = "ns" + generated.ToString(CultureInfo.InvariantCulture); }
        while (scope.ContainsKey(fresh) || reserved.Contains(fresh));
        Generate(fresh, uri); return fresh;
    }
    private void RestoreAsyncInput(int mark)
    {
        for (int i = undo.Count - 1; i >= mark; i--)
        {
            AsyncTick(); var entry = undo[i];
            if (entry.Old is null) scope.Remove(entry.Prefix); else scope[entry.Prefix] = entry.Old;
        }
        undo.RemoveRange(mark, undo.Count - mark);
    }
    private void ValidateNameAsyncInput(string name, bool colon = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0) throw new ArgumentException("Empty XML name.", nameof(name));
        ReadOnlySpan<char> rest = name; bool first = true;
        while (!rest.IsEmpty)
        {
            AsyncTick();
            if (Rune.DecodeFromUtf16(rest, out var rune, out int width) != OperationStatus.Done)
                throw new ArgumentException("Invalid XML name.", nameof(name));
            int c = rune.Value;
            bool start = c is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                >= 0xc0 and <= 0xd6 or >= 0xd8 and <= 0xf6 or >= 0xf8 and <= 0x2ff or >= 0x370 and <= 0x37d or
                >= 0x37f and <= 0x1fff or >= 0x200c and <= 0x200d or >= 0x2070 and <= 0x218f or >= 0x2c00 and <= 0x2fef or
                >= 0x3001 and <= 0xd7ff or >= 0xf900 and <= 0xfdcf or >= 0xfdf0 and <= 0xfffd or >= 0x10000 and <= 0xeffff;
            if (!(start || colon && c == ':' || !first && c is '-' or '.' or >= '0' and <= '9' or 0xb7 or >= 0x300 and <= 0x36f or >= 0x203f and <= 0x2040))
                throw new ArgumentException("Invalid XML name.", nameof(name));
            first = false; rest = rest[width..];
        }
    }
    private void ValidateBindingAsyncInput(string prefix, string uri)
    {
        ArgumentNullException.ThrowIfNull(prefix); ArgumentNullException.ThrowIfNull(uri);
        if (prefix.Length != 0) ValidateNameAsyncInput(prefix); ValidateTextAsyncInput(uri);
        if (prefix == "xmlns" || uri == TreeValidation.XmlnsUri || (prefix == "xml") != (uri == TreeValidation.XmlUri) || prefix.Length != 0 && uri.Length == 0)
            throw new ArgumentException("Reserved or empty namespace binding.");
    }
    private void ValidateExpandedAsyncInput(string name, string uri, string prefix, bool attribute = false)
    {
        ValidateNameAsyncInput(name); ValidateBindingAsyncInput(prefix, uri);
        if (attribute && name == "xmlns" && uri.Length == 0 && prefix.Length == 0)
            throw new ArgumentException("Use a namespace declaration for xmlns.", nameof(name));
    }
    private void ValidateTextAsyncInput(string text) { ArgumentNullException.ThrowIfNull(text); ValidateTextAsyncInput(text.AsSpan()); }
    private void ValidateTextAsyncInput(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            AsyncTick();
            if (Rune.DecodeFromUtf16(text, out var rune, out int width) != OperationStatus.Done ||
                !(rune.Value is 9 or 10 or 13 or >= 0x20 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 and <= 0x10ffff))
                throw new ArgumentException("Illegal XML scalar.", nameof(text));
            text = text[width..];
        }
    }
    private void DelimiterAsyncInput(ReadOnlySpan<char> text, ReadOnlySpan<char> delimiter)
    {
        while (!text.IsEmpty)
        {
            asyncToken.ThrowIfCancellationRequested(); int n = Math.Min(text.Length, 2048);
            if (text[..Math.Min(text.Length, n + delimiter.Length - 1)].Contains(delimiter, StringComparison.Ordinal))
                throw new ArgumentException("Illegal XML delimiter.", nameof(text));
            text = text[n..];
        }
    }
    private async ValueTask QNameAsync(string prefix, string name)
    {
        if (prefix.Length != 0) { await output.WriteAsync(prefix, asyncToken).ConfigureAwait(false); await output.WriteAsync(":", asyncToken).ConfigureAwait(false); }
        await output.WriteAsync(name, asyncToken).ConfigureAwait(false);
    }
    private async ValueTask IndentAsync(int depth)
    {
        await output.WriteAsync("\n", asyncToken).ConfigureAwait(false);
        const string spaces = "                                                                ";
        long count = checked((long)depth * 2);
        while (count != 0) { AsyncTick(); int n = (int)Math.Min(count, spaces.Length); await output.WriteAsync(spaces.AsMemory(0, n), asyncToken).ConfigureAwait(false); count -= n; }
    }
    private async ValueTask EscapeAsync(ReadOnlyMemory<char> text, bool attribute)
    {
        int run = 0;
        for (int i = 0; i < text.Length; i++)
        {
            AsyncTick(); char c = text.Span[i];
            string? replacement = c switch
            {
                '&' => "&amp;", '<' => "&lt;", '"' when attribute => "&quot;",
                '\t' when attribute => "&#x9;", '\n' when attribute => "&#xA;", '\r' => "&#xD;",
                '>' when !attribute && brackets >= 2 => "&gt;", _ => null
            };
            if (replacement is not null)
            { await output.WriteAsync(text[run..i], asyncToken).ConfigureAwait(false); await output.WriteAsync(replacement, asyncToken).ConfigureAwait(false); run = i + 1; }
            if (!attribute) brackets = c == ']' ? Math.Min(2, brackets + 1) : 0;
            if (i - run >= 2048) { await output.WriteAsync(text[run..(i + 1)], asyncToken).ConfigureAwait(false); run = i + 1; }
        }
        await output.WriteAsync(text[run..], asyncToken).ConfigureAwait(false);
    }
}
