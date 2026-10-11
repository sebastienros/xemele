using Xemele.Streaming;
using Reader = Xemele.Streaming.XmlReader;
namespace Xemele.StreamingAcceptance;
public static class Equivalence
{
    public static async Task Verify(byte[] bytes, XmlParseOptions? options = null, int seed = 1771, CancellationToken cancellationToken = default)
    {
        var baseline = new NormalizedEvents(); XmlParseException? rejection = null; XmlDocument? dom = null;
        try { dom = XmlDocument.Parse(bytes, options, cancellationToken); } catch (XmlParseException e) { rejection = e; }
        XmlParseException? saxError = null;
        try { XmlSaxParser.Parse(bytes, baseline, options, cancellationToken); } catch (XmlParseException e) { saxError = e; }
        EqualRejection(rejection, saxError, "SAX");
        if (dom is not null) Equal(NormalizedEvents.Dom(dom).Content, baseline.Content, "DOM/SAX content");
        for (int mode = 0; mode < 5; mode++)
        {
            var actual = new NormalizedEvents(); XmlParseException? error = null;
            using var input = new RandomChunkStream(bytes, seed + mode);
            try
            {
                switch (mode)
                {
                    case 0: XmlSaxParser.Parse(input, actual, options, cancellationToken); break;
                    case 1: await XmlSaxParser.ParseAsync(input, actual, options, cancellationToken); break;
                    case 2:
                        using (var p = new XmlPushParser(actual, options, cancellationToken))
                        {
                            var random = new Random(seed);
                            for (int offset = 0; offset < bytes.Length;)
                            { p.Append([]); int n = Math.Min(random.Next(1, 258), bytes.Length - offset); p.Append(bytes.AsSpan(offset, n)); offset += n; }
                            p.Complete();
                        }
                        break;
                    default:
                        using (var r = Reader.Create(input, options, cancellationToken))
                        { while (mode == 3 ? r.Read() : await r.ReadAsync(cancellationToken)) actual.Read(r); }
                        break;
                }
            }
            catch (XmlParseException e) { error = e; }
            EqualRejection(rejection, error, "transport " + mode);
            // Strict failure prefixes are not a contract: buffered tag/resource failures can precede delivery.
            if (error is null) { Equal(baseline.Content, actual.Content, "transport content " + mode); Equal(baseline.Metadata, actual.Metadata, "transport metadata " + mode); }
        }
    }
    public static void Equal(IEnumerable<string> expected, IEnumerable<string> actual, string context)
    {
        var e = expected.ToArray(); var a = actual.ToArray();
        if (!e.SequenceEqual(a)) { int i = Enumerable.Range(0, Math.Min(e.Length, a.Length)).FirstOrDefault(i => e[i] != a[i], Math.Min(e.Length, a.Length)); throw new InvalidDataException($"{context}: first difference {i}, lengths {e.Length}/{a.Length}: {(i < e.Length ? e[i] : "EOF")} / {(i < a.Length ? a[i] : "EOF")}"); }
    }
    private static void EqualRejection(XmlParseException? expected, XmlParseException? actual, string context)
    { if ((expected is null) != (actual is null) || expected is not null && expected.Diagnostics[^1].Code != actual!.Diagnostics[^1].Code) throw new InvalidDataException(context + ": rejection mismatch."); }
}
public sealed class RandomChunkStream : MemoryStream
{
    private readonly Random random;
    private readonly byte[] bytes;
    public RandomChunkStream(byte[] bytes, int seed) : base(bytes, false) { this.bytes = bytes; random = new(seed); }
    public override int Read(Span<byte> buffer) { int n = (int)Math.Min(bytes.Length - Position, Math.Min(buffer.Length, random.Next(1, 258))); bytes.AsSpan((int)Position, n).CopyTo(buffer); Position += n; return n; }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Read(buffer.Span)); }
}
