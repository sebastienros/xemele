using System.Buffers;

namespace Xemele.Html;

// Bulk consumption of "plain" runs. A plain scalar is one whose per-scalar path is exactly
// "append to the current buffer and advance": no state change, no parse error, no CR handling,
// no surrogate pairing. Runs are clamped so every quota and cancellation boundary is crossed
// by the per-scalar path, which keeps limits, exceptions and chunking identical.
internal sealed partial class HtmlTokenizer
{
    private const string ControlStops = "\0\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u000B\r\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\u007F";
    private static readonly SearchValues<char> DataStops = SearchValues.Create(ControlStops + "&<");
    private static readonly SearchValues<char> RawStops = SearchValues.Create(ControlStops + "<");
    private static readonly SearchValues<char> PlainStops = SearchValues.Create(ControlStops);
    private static readonly SearchValues<char> DoubleStops = SearchValues.Create(ControlStops + "\"&");
    private static readonly SearchValues<char> SingleStops = SearchValues.Create(ControlStops + "'&");
    private static readonly SearchValues<char> UnquotedStops = SearchValues.Create(ControlStops + "\t\n\f &>\"'<=`");
    private static readonly SearchValues<char> CommentStops = SearchValues.Create(ControlStops + "<-");
    private static readonly SearchValues<char> BogusStops = SearchValues.Create(ControlStops + ">");

    private enum Bulk
    {
        Text, AttributeValue, Comment
    }

    private static bool BadNonAscii(char c) => c >= 0x7F && (c <= 0x9F || c is >= '\uD800' and <= '\uDFFF' or >= '\uFDD0' and <= '\uFDEF' or '\uFFFE' or '\uFFFF');

    private static int PlainLength(ReadOnlySpan<char> run)
    {
        var first = run.IndexOfAnyExceptInRange('\0', '\u007E');
        if (first < 0)
        {
            return run.Length;
        }

        for (var i = first; i < run.Length; i++)
        {
            if (BadNonAscii(run[i]))
            {
                return i;
            }
        }

        return run.Length;
    }

    private bool TryBulk(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets, ref int index)
    {
        if (state == State.Data && input[index] == '<')
        {
            return TryTag(input, byteOffsets, ref index);
        }

        if (state is State.Data or State.RcData && input[index] == '&')
        {
            return TryReference(input, byteOffsets, ref index);
        }

        SearchValues<char> stops;
        Bulk kind;
        switch (state)
        {
            case State.Data:
            case State.RcData:
                stops = DataStops;
                kind = Bulk.Text;
                break;
            case State.RawText:
            case State.ScriptData:
                stops = RawStops;
                kind = Bulk.Text;
                break;
            case State.PlainText:
                stops = PlainStops;
                kind = Bulk.Text;
                break;
            case State.DoubleValue:
                stops = DoubleStops;
                kind = Bulk.AttributeValue;
                break;
            case State.SingleValue:
                stops = SingleStops;
                kind = Bulk.AttributeValue;
                break;
            case State.UnquotedValue:
                stops = UnquotedStops;
                kind = Bulk.AttributeValue;
                break;
            case State.Comment:
                stops = CommentStops;
                kind = Bulk.Comment;
                break;
            case State.BogusComment:
                stops = BogusStops;
                kind = Bulk.Comment;
                break;
            default:
                return false;
        }
        var rest = input[index..];
        long room = rest.Length;
        if (limits.DocumentCharacters is long document)
        {
            room = Math.Min(room, document - documentScalars);
        }

        if (limits.ExpandedCharacters is long expanded)
        {
            room = Math.Min(room, expanded - expandedScalars);
        }

        if (cancellation.CanBeCanceled)
        {
            room = Math.Min(room, 255 - (cancellationTicks & 255));
        }

        var flushAt = 0;
        switch (kind)
        {
            case Bulk.Text:
                flushAt = limits.TokenCharacters is int textCap ? Math.Min(1024, textCap) : 1024;
                room = Math.Min(room, flushAt - characters.Length);
                break;
            case Bulk.AttributeValue:
                if (limits.TokenCharacters is int valueCap)
                {
                    room = Math.Min(room, valueCap - attributeScalars);
                }

                break;
            default:
                if (limits.TokenCharacters is int commentCap)
                {
                    room = Math.Min(room, commentCap - dataScalars);
                }

                break;
        }
        if (room <= 0)
        {
            return false;
        }
        // The scan window is bounded by the quota room so a long plain run is not rescanned per chunk.
        var window = rest[..(int)room];
        var run = window.IndexOfAny(stops);
        if (run < 0)
        {
            run = window.Length;
        }

        if (run == 0)
        {
            return false;
        }

        run = PlainLength(window[..run]);
        if (run == 0)
        {
            return false;
        }

        var span = rest[..run];
        if (!SuppressLocations && kind == Bulk.Text && characters.Length == 0)
        {
            characterOpening = new(SourceUri, line, column, byteOffsets.IsEmpty ? null : byteOffsets[index]);
        }

        expandedScalars += run;
        documentScalars += run;
        cancellationTicks = unchecked(cancellationTicks + run);
        var lastLine = span.LastIndexOf('\n');
        if (lastLine < 0)
        {
            column = checked(column + run);
        }
        else
        {
            line = checked(line + span.Count('\n'));
            column = run - lastLine;
        }
        index += run;
        switch (kind)
        {
            case Bulk.Text:
                if (treeBuilder is not null && characters.Length == 0 && (run >= flushAt || index < input.Length && input[index] == '<' && state == State.Data && index + 2 < input.Length && (Alpha(input[index + 1]) || input[index + 1] == '/' && Alpha(input[index + 2]))))
                {
                    treeBuilder.Process(new HtmlTreeBuilder.Token(HtmlTokenKind.Character, Data: new string(span)));
                    return true;
                }
                characters.Append(span);
                if (characters.Length >= flushAt)
                {
                    FlushCharacters();
                }
                break;
            case Bulk.AttributeValue:
                attrValue.Append(span);
                attributeScalars += run;
                break;
            default:
                data.Append(span);
                dataScalars += run;
                break;
        }

        return true;
    }
}
