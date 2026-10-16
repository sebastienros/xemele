namespace Xemele.Html;

using System.Buffers;
/// <summary>Parses HTML using the toolkit's legacy HTML parsing profile.</summary>
/// <remarks>Use <see cref="Html5Parser"/> for HTML5 tree-construction behavior.</remarks>
public static class HtmlParser
{
    /// <summary>Parses decoded HTML text into a new legacy-profile document.</summary>
    /// <param name="html">The decoded HTML input.</param>
    /// <param name="options">The resource limits, or null for defaults.</param>
    /// <returns>An owned HTML document in quirks mode with scripting disabled.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    public static HtmlDocument Parse(string html, HtmlParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var saved = HtmlOperationSnapshots.Snapshot(options);
        var input = new LegacyInput(html.AsSpan(), saved.Limits);
        return new LegacyParser(input, saved.Limits).Run();
    }
    /// <summary>Decodes and parses HTML bytes using the legacy profile's encoding selection.</summary>
    /// <param name="html">The encoded HTML input, borrowed only for this call.</param>
    /// <param name="options">The resource limits, or null for defaults.</param>
    /// <returns>An owned HTML document in quirks mode with scripting disabled.</returns>
    public static HtmlDocument Parse(ReadOnlySpan<byte> html, HtmlParseOptions? options = null)
    {
        var saved = HtmlOperationSnapshots.Snapshot(options);
        var input = new LegacyInput(html, null, saved.Limits, default);
        return new LegacyParser(input, saved.Limits).Run();
    }
    /// <summary>Reads and parses legacy-profile HTML from a stream without closing it.</summary>
    /// <param name="stream">The input stream, read from its current position.</param>
    /// <param name="options">The resource limits, or null for defaults.</param>
    /// <param name="cancellationToken">The token used to cancel reading and parsing.</param>
    /// <returns>A new legacy-profile HTML document.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static HtmlDocument Load(Stream stream, HtmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var saved = HtmlOperationSnapshots.Snapshot(options);
        cancellationToken.ThrowIfCancellationRequested();
        var input = new LegacyInput(default, stream, saved.Limits, cancellationToken);
        return new LegacyParser(input, saved.Limits).Run();
    }
}
internal ref struct LegacyParser
{
    private LegacyInput input;
    private readonly HtmlDocument document;
    private readonly XmlResourceLimits limits;
    private readonly List<HtmlNode> stack = [];
    private readonly LegacyBuffer buffer = new();
    private readonly LegacyNamePool names;
    private int tokenScalars, insertState, discardDepth, textMode;
    private long expanded;
    internal LegacyParser(LegacyInput input, XmlResourceLimits limits)
    {
        this.input = input;
        this.limits = limits;
        names = new(limits.InternedNames);
        document = new(limits);
        document.SetMetadata(HtmlQuirksMode.Quirks, false);
    }
    internal HtmlDocument Run()
    {
        while (input.Peek() >= 0)
        {
            if (textMode != 0)
            {
                Text();
                continue;
            }
            if (input.Peek() != '<')
            {
                Text();
                continue;
            }
            if (input.Match("<!--"))
            {
                input.Skip(4);
                Comment(false);
            }
            else if (input.Match("<!doctype", fold: true))
            {
                input.Skip(9);
                Doctype();
            }
            else if (input.Match("<!"))
            {
                input.Skip(2);
                Comment(true);
            }
            else if (input.Match("<?"))
            {
                input.Skip(1);
                Comment(true);
            }
            else if (input.Match("</"))
            {
                input.Skip(2);
                EndTag();
            }
            else if (Letter(input.Peek(1)))
            {
                input.Take();
                StartTag();
            }
            else
            {
                Reset();
                Append('<');
                input.Take();
                EmitText(buffer.ToString());
            }
        }
        return document;
    }
    private readonly HtmlNode Parent => stack.Count == 0 ? document.DocumentNode : stack[^1];
    private static bool Space(int c) => c is 9 or 10 or 12 or 13 or 32;
    private static bool Letter(int c) => c is >= 65 and <= 90 or >= 97 and <= 122;
    private static int Fold(int c) => c is >= 65 and <= 90 ? c + 32 : c;
    private void Reset()
    {
        buffer.Clear();
        tokenScalars = 0;
    }
    private void Append(int c)
    {
        if (c == 0)
        {
            c = 0xFFFD;
        }

        if (limits.TokenCharacters is int max && ++tokenScalars > max)
        {
            Fail("token-limit", "Token character quota exceeded.");
        }

        if (c <= 0xFFFF)
        {
            buffer.Append((char)c);
        }
        else
        {
            c -= 0x10000;
            buffer.Append((char)(0xD800 + (c >> 10)));
            buffer.Append((char)(0xDC00 + (c & 1023)));
        }
    }
    private void Append(string value)
    {
        if ((limits.TokenCharacters is not int max || tokenScalars + value.Length <= max) && value.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') < 0 && value.AsSpan().IndexOf('\0') < 0)
        {
            buffer.Append(value);
            tokenScalars += value.Length;
            return;
        }
        foreach (var r in value.EnumerateRunes())
        {
            Append(r.Value);
        }
    }
    private void Expanded(string text)
    {
        var count = Count(text);
        expanded = checked(expanded + count);
        if (limits.ExpandedCharacters is long max && expanded > max)
        {
            Fail("expanded-limit", "Expanded character quota exceeded.");
        }
    }
    private void Fail(string code, string message) => LegacyInput.Fail("HTML-legacy-" + code, message, input.Location);
    private void Blanks()
    {
        while (Space(input.Peek()))
        {
            input.Take();
        }
    }
    private static readonly SearchValues<char> NameStops = SearchValues.Create("\0\t\n\f\r />");
    private static readonly SearchValues<byte> NameByteStops = SearchValues.Create("\0\t\n\f\r />"u8);
    private static readonly SearchValues<char> AttributeNameStops = SearchValues.Create("\0\t\n\f\r />=");
    private static readonly SearchValues<byte> AttributeNameByteStops = SearchValues.Create("\0\t\n\f\r />="u8);
    private string Name(bool attribute = false)
    {
        input.NameMode(true);
        Reset();
        int sourceCount = 0, utf8Bytes = 0;
        while (true)
        {
            if (sourceCount != 0)
            {
                long room = 100 - utf8Bytes;
                if (limits.TokenCharacters is int cap && cap - sourceCount < room)
                {
                    room = cap - sourceCount;
                }

                var run = input.TakeRun(buffer, attribute ? AttributeNameStops : NameStops, attribute ? AttributeNameByteStops : NameByteStops, room, true);
                if (run > 0)
                {
                    sourceCount += run;
                    utf8Bytes += run;
                    tokenScalars += run;
                    continue;
                }
            }
            if (input.Peek() < 0)
            {
                break;
            }

            var c = input.Peek();
            if (sourceCount != 0 && (c is '/' or '>' || Space(c) || attribute && c == '='))
            {
                break;
            }

            c = Fold(input.Take());
            sourceCount++;
            if (limits.TokenCharacters is int limit && sourceCount > limit)
            {
                Fail("token-limit", "Token name quota exceeded.");
            }

            if (c == 0)
            {
                c = 0xFFFD;
            }

            var width = c < 128 ? 1 : c < 2048 ? 2 : c < 65536 ? 3 : 4;
            if (utf8Bytes + width <= 100)
            {
                Append(c);
                utf8Bytes += width;
            }
        }
        input.NameMode(false);
        return names.Get(buffer.Written);
    }
    private readonly HtmlNode? RootElement()
    {
        var children = document.DocumentNode.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Kind == HtmlNodeKind.Element)
            {
                return children[i];
            }
        }

        return null;
    }
    private void Push(string name)
    {
        if (limits.Depth is int max && stack.Count >= max)
        {
            Fail("depth-limit", "Open-element quota exceeded.");
        }

        var node = name == "html" && stack.Count == 0 ? RootElement() : null;
        if (node == null)
        {
            node = document.CreateElement(name);
            Parent.AppendParsed(node);
        }
        stack.Add(node);
        if (name == "head")
        {
            insertState = Math.Max(insertState, 1);
        }

        if (name == "body")
        {
            insertState = 2;
        }
    }
    private void Pop()
    {
        if (stack.Count != 0)
        {
            stack.RemoveAt(stack.Count - 1);
        }

        textMode = 0;
    }
    private void Implied(string name)
    {
        if (name == "html")
        {
            return;
        }

        if (stack.Count == 0)
        {
            Push("html");
        }

        if (name is "body" or "head")
        {
            return;
        }

        if (stack.Count <= 1 && name is "script" or "style" or "meta" or "link" or "title" or "base")
        {
            if (insertState < 1)
            {
                Push("head");
            }
        }
        else if (name is not ("noframes" or "frame" or "frameset") && insertState < 2)
        {
            for (var i = 0; i < stack.Count; i++)
            {
                if (stack[i].LocalName is "head" or "body")
                {
                    return;
                }
            }

            Push("body");
        }
    }
    private void StartTag()
    {
        var name = Name();
        while (stack.Count != 0 && LegacyRules.AutoClose(stack[^1].LocalName, name))
        {
            Pop();
        }

        Implied(name);
        var discard = name == "html" && stack.Count > 0 || name == "head" && stack.Count != 1 || name == "body" && stack.Exists(static n => n.LocalName == "body");
        if (discard)
        {
            discardDepth++;
        }

        var reusedRoot = !discard && name == "html" && stack.Count == 0 && RootElement() != null;
        HtmlNode? element = null;
        if (!discard)
        {
            Push(name);
            element = stack[^1];
        }
        var originalAttributes = element?.Attributes.Count ?? 0;
        long attributeCharacters = 0;
        var accepted = 0;
        var seen = discard || element?.Attributes.Count > 0 ? new HashSet<string>(StringComparer.Ordinal) : null;
        while (true)
        {
            Blanks();
            var c = input.Peek();
            if (c < 0 || c == '>' || input.Match("/>"))
            {
                break;
            }

            if (c == '/')
            {
                input.Take();
                continue;
            }
            var attribute = Name(true);
            Blanks();
            var value = LegacyRules.Boolean(attribute) ? attribute : "";
            if (input.Peek() == '=')
            {
                input.Take();
                Blanks();
                value = AttributeValue();
            }
            Expanded(value);
            attributeCharacters = checked(attributeCharacters + Count(attribute) + Count(value));
            if (limits.TokenCharacters is int max && attributeCharacters > max)
            {
                Fail("token-limit", "Aggregate attribute token quota exceeded.");
            }

            if (seen != null ? !seen.Add(attribute) : element?.GetAttribute(attribute) != null)
            {
                continue;
            }

            if (limits.AttributesPerElement is int amax && ++accepted > amax)
            {
                Fail("attribute-limit", "Attribute quota exceeded.");
            }

            if (element != null && element.GetAttribute(attribute) == null)
            {
                element.SetAttribute(new(attribute, value));
            }
        }
        if (input.Peek() < 0)
        {
            if (element != null)
            {
                if (reusedRoot)
                {
                    while (element.Attributes.Count > originalAttributes)
                    {
                        element.RemoveAttribute(element.Attributes[^1].LocalName, element.Attributes[^1].NamespaceUri);
                    }
                }
                else
                {
                    element.Remove();
                }

                Pop();
            }
            return;
        }
        if (name == "meta" && element != null)
        {
            Meta(element);
        }

        var selfClosing = input.Match("/>");
        if (selfClosing)
        {
            input.Skip(2);
        }
        else if (input.Peek() == '>')
        {
            input.Take();
        }

        if (!discard)
        {
            if (selfClosing || LegacyRules.Void(name))
            {
                Pop();
            }
            else
            {
                textMode = LegacyRules.TextMode(name);
            }
        }
    }
    private void Meta(HtmlNode element)
    {
        var charset = element.GetAttribute("charset");
        if (charset != null)
        {
            input.DeclareEncoding(charset);
            return;
        }
        if (!string.Equals(element.GetAttribute("http-equiv"), "content-type", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var content = element.GetAttribute("content");
        if (content == null)
        {
            return;
        }

        var span = content.AsSpan();
        for (var i = 0; i + 8 <= span.Length; i++)
        {
            if (span.Slice(i, 8).Equals("charset=", StringComparison.OrdinalIgnoreCase))
            {
                var begin = i + 8;
                while (begin < span.Length && span[begin] is ' ' or '\'' or '\"')
                {
                    begin++;
                }

                var end = begin;
                while (end < span.Length && span[end] is not (';' or ' ' or '\'' or '\"'))
                {
                    end++;
                }

                input.DeclareEncoding(span[begin..end]);
                return;
            }
        }
    }
    private static readonly SearchValues<char> TextStops = SearchValues.Create("<&\0\r");
    private static readonly SearchValues<byte> TextByteStops = SearchValues.Create("<&\0\r"u8);
    private static readonly SearchValues<char> DashStops = SearchValues.Create("<&\0\r-");
    private static readonly SearchValues<byte> DashByteStops = SearchValues.Create("<&\0\r-"u8);
    private static readonly SearchValues<char> DoubleStops = SearchValues.Create("\"&\0\r");
    private static readonly SearchValues<byte> DoubleByteStops = SearchValues.Create("\"&\0\r"u8);
    private static readonly SearchValues<char> SingleStops = SearchValues.Create("'&\0\r");
    private static readonly SearchValues<byte> SingleByteStops = SearchValues.Create("'&\0\r"u8);
    private static readonly SearchValues<char> UnquotedStops = SearchValues.Create("\t\n\f\r >&\0");
    private static readonly SearchValues<byte> UnquotedByteStops = SearchValues.Create("\t\n\f\r >&\0"u8);
    private readonly long Quota() => limits.TokenCharacters is int max ? max - tokenScalars : long.MaxValue;
    private string AttributeValue()
    {
        Reset();
        var quote = input.Peek();
        if (quote is '\'' or '"')
        {
            input.Take();
        }
        else
        {
            quote = -1;
        }

        var stops = quote == '"' ? DoubleStops : quote == '\'' ? SingleStops : UnquotedStops;
        var byteStops = quote == '"' ? DoubleByteStops : quote == '\'' ? SingleByteStops : UnquotedByteStops;
        while (true)
        {
            var run = input.TakeRun(buffer, stops, byteStops, Quota());
            if (run > 0)
            {
                tokenScalars += run;
                continue;
            }
            if (input.Peek() < 0)
            {
                break;
            }

            var c = input.Peek();
            if (quote < 0 ? (Space(c) || c == '>') : c == quote)
            {
                break;
            }

            if (c == '&')
            {
                Reference(true);
            }
            else
            {
                Append(input.Take());
            }
        }
        if (quote >= 0 && input.Peek() == quote)
        {
            input.Take();
        }

        return buffer.ToString();
    }
    private void EndTag()
    {
        if (input.Peek() < 0)
        {
            EmitText("</");
            return;
        }
        if (input.Peek() == '>')
        {
            input.Take();
            return;
        }
        if (!Letter(input.Peek()))
        {
            Comment(true);
            return;
        }
        var name = Name();
        while (input.Peek() >= 0 && input.Peek() != '>')
        {
            input.Take();
        }

        if (input.Peek() == '>')
        {
            input.Take();
        }

        if (discardDepth > 0 && name is "html" or "head" or "body")
        {
            discardDepth--;
            return;
        }
        int priority = LegacyRules.Priority(name), index = stack.Count - 1;
        while (index >= 0 && stack[index].LocalName != name)
        {
            if (LegacyRules.Priority(stack[index].LocalName) > priority)
            {
                return;
            }

            index--;
        }
        if (index < 0)
        {
            return;
        }

        while (stack.Count > index)
        {
            Pop();
        }
    }
    private bool AppropriateEnd(int offset = 0)
    {
        if (stack.Count == 0 || !input.Match("</", offset))
        {
            return false;
        }

        var name = stack[^1].LocalName;
        if (!input.Match(name, offset + 2, true))
        {
            return false;
        }

        var c = input.Peek(offset + 2 + name.Length);
        return c is '/' or '>' || Space(c);
    }
    private void Text()
    {
        Reset();
        var mode = textMode;
        while (true)
        {
            var run = input.TakeRun(buffer, mode is 5 or 6 ? DashStops : TextStops, mode is 5 or 6 ? DashByteStops : TextByteStops, Quota());
            if (run > 0)
            {
                tokenScalars += run;
                continue;
            }
            if (input.Peek() < 0)
            {
                break;
            }

            var c = input.Peek();
            if (c == '<')
            {
                if (mode == 0)
                {
                    break;
                }

                if (mode != 4 && AppropriateEnd())
                {
                    if (mode != 5)
                    {
                        break;
                    }

                    mode = 6;
                }
                if (mode == 3 && input.Match("<!--"))
                {
                    mode = 6;
                }
                else if (mode == 6 && input.Match("<script", fold: true) && (Space(input.Peek(7)) || input.Peek(7) is '/' or '>'))
                {
                    mode = 5;
                }
            }
            if ((mode is 5 or 6) && input.Match("-->"))
            {
                mode = 3;
            }

            if (c == 0 && mode == 0)
            {
                input.Take();
                continue;
            }
            if (c == '&' && mode is 0 or 1)
            {
                Reference(false);
            }
            else
            {
                Append(input.Take());
            }
        }
        if (buffer.Length != 0)
        {
            EmitText(buffer.ToString());
        }

        textMode = mode;
        if (input.Peek() == '<' && mode != 4 && AppropriateEnd())
        {
            input.Skip(2);
            EndTag();
        }
    }
    private void EmitText(string value)
    {
        Expanded(value);
        if (stack.Count == 0 || Parent.LocalName is "html" or "head")
        {
            var prefix = 0;
            while (prefix < value.Length && Space(value[prefix]))
            {
                prefix++;
            }

            if (prefix > 0 && stack.Count != 0)
            {
                Parent.AppendParsed(document.CreateText(value[..prefix]));
            }

            if (prefix == value.Length)
            {
                return;
            }

            if (stack.Count != 0 && Parent.LocalName == "head")
            {
                while (stack.Count != 0 && LegacyRules.AutoClose(stack[^1].LocalName, "p"))
                {
                    Pop();
                }
            }

            Implied("p");
            value = value[prefix..];
        }
        Parent.AppendParsed(document.CreateText(value));
    }
    private void Reference(bool attribute)
    {
        Span<char> ahead = stackalloc char[40];
        var available = input.PeekAsciiAhead(ahead);
        if (available > 0 && FastReference(attribute, ahead[..available]))
        {
            return;
        }

        if (input.Peek(1) == '#')
        {
            int pos = 2, radix = 10;
            if (Fold(input.Peek(pos)) == 'x')
            {
                pos++;
                radix = 16;
            }
            var digit = Digit(input.Peek(pos));
            if (digit < 0 || digit >= radix)
            {
                Append(input.Take());
                return;
            }
            input.Skip(pos);
            var cp = 0;
            while ((digit = Digit(input.Peek())) >= 0 && digit < radix)
            {
                cp = Math.Min(0x110000, cp * radix + digit);
                input.Take();
            }
            if (input.Peek() == ';')
            {
                input.Take();
            }

            Append(MapCodePoint(cp));
            return;
        }
        Span<char> candidate = stackalloc char[33];
        var length = 0;
        while (length < candidate.Length)
        {
            var c = input.Peek(length + 1);
            if (c < 0 || c > 127)
            {
                break;
            }

            candidate[length++] = (char)c;
            if (c == ';' || !Letter(c) && c is not (>= 48 and <= 57))
            {
                break;
            }
        }
        if (HtmlNamedReferences.Match(candidate[..length], attribute, out var used, out var value))
        {
            input.Skip(used + 1);
            Append(value);
        }
        else
        {
            Append(input.Take());
        }
    }
    // Resolves a reference from already-decoded ASCII lookahead; returns false (without side effects)
    // when the lookahead is too short to decide, leaving the per-scalar path to run.
    private bool FastReference(bool attribute, scoped ReadOnlySpan<char> ahead)
    {
        var n = ahead.Length;
        if (ahead[0] == '#')
        {
            int p = 1, radix = 10;
            if (p >= n)
            {
                return false;
            }

            if (Fold(ahead[p]) == 'x')
            {
                p++;
                radix = 16;
                if (p >= n)
                {
                    return false;
                }
            }
            var digit = Digit(ahead[p]);
            if (digit < 0 || digit >= radix)
            {
                Append(input.Take());
                return true;
            }
            int cp = 0, q = p;
            while (q < n)
            {
                digit = Digit(ahead[q]);
                if (digit < 0 || digit >= radix)
                {
                    break;
                }

                cp = Math.Min(0x110000, cp * radix + digit);
                q++;
            }
            if (q >= n)
            {
                return false;
            }

            input.SkipAhead(1 + q + (ahead[q] == ';' ? 1 : 0));
            Append(MapCodePoint(cp));
            return true;
        }
        var length = 0;
        var done = false;
        while (length < 33 && length < n)
        {
            int c = ahead[length++];
            if (c == ';' || !Letter(c) && c is not (>= 48 and <= 57))
            {
                done = true;
                break;
            }
        }
        if (!done && length < 33)
        {
            return false;
        }

        if (HtmlNamedReferences.Match(ahead[..length], attribute, out var used, out var value))
        {
            input.SkipAhead(used + 1);
            Append(value);
        }
        else
        {
            Append(input.Take());
        }

        return true;
    }
    private static int Digit(int c) => c is >= 48 and <= 57 ? c - 48 : c is >= 65 and <= 70 ? c - 55 : c is >= 97 and <= 102 ? c - 87 : -1;
    internal static int MapCodePoint(int cp)
    {
        ReadOnlySpan<int> map = [0x20AC, 0x81, 0x201A, 0x192, 0x201E, 0x2026, 0x2020, 0x2021, 0x2C6, 0x2030, 0x160, 0x2039, 0x152, 0x8D, 0x17D, 0x8F, 0x90, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x2DC, 0x2122, 0x161, 0x203A, 0x153, 0x9D, 0x17E, 0x178];
        return cp is >= 128 and < 160 ? map[cp - 128] : cp == 0 || cp > 0x10FFFF || cp is >= 0xD800 and <= 0xDFFF ? 0xFFFD : cp;
    }
    private void Comment(bool bogus)
    {
        Reset();
        if (!bogus && input.Peek() == '>')
        {
            input.Take();
        }
        else if (!bogus && input.Match("->"))
        {
            input.Skip(2);
        }
        else
        {
            while (input.Peek() >= 0)
            {
                if (bogus && input.Peek() == '>')
                {
                    input.Take();
                    break;
                }
                if (!bogus && input.Match("-->"))
                {
                    input.Skip(3);
                    break;
                }
                if (!bogus && input.Match("--!>"))
                {
                    input.Skip(4);
                    break;
                }
                Append(input.Take());
            }
        }
        Parent.AppendParsed(document.CreateComment(buffer.ToString()));
    }
    private void Doctype()
    {
        Blanks();
        Reset();
        while (input.Peek() >= 0 && input.Peek() != '>' && !Space(input.Peek()))
        {
            Append(input.Take());
        }

        var name = buffer.ToString();
        Blanks();
        string? pub = null, sys = null;
        if (input.Match("public", fold: true))
        {
            input.Skip(6);
            Blanks();
            pub = Literal();
            Blanks();
            if (pub != null)
            {
                sys = Literal();
            }
        }
        else if (input.Match("system", fold: true))
        {
            input.Skip(6);
            Blanks();
            sys = Literal();
        }
        while (input.Peek() >= 0 && input.Peek() != '>')
        {
            input.Take();
        }

        if (input.Peek() == '>')
        {
            input.Take();
        }

        foreach (var child in document.DocumentNode.Children)
        {
            if (child.Kind == HtmlNodeKind.DocumentType)
            {
                return;
            }
        }

        document.DocumentNode.AppendChild(document.CreateDocumentType(new(name, pub, sys, false)));
        if (name.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            document.SetMetadata(HtmlQuirksMode.NoQuirks, false);
        }
    }
    private string? Literal()
    {
        var quote = input.Peek();
        if (quote is not ('\'' or '"'))
        {
            return null;
        }

        input.Take();
        Reset();
        while (input.Peek() >= 0 && input.Peek() != quote && input.Peek() != '>')
        {
            Append(input.Take());
        }

        if (input.Peek() == quote)
        {
            input.Take();
        }

        return buffer.ToString();
    }
    private static int Count(string text)
    {
        if (text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') < 0)
        {
            return text.Length;
        }

        var count = 0;
        foreach (var r in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
