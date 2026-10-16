using System.Runtime.CompilerServices;
using Xemele.Parsing.Input;

namespace Xemele.Tests.EncodingCompatibility;

internal static class EncodingAllocationProbe
{
    internal sealed record LabelReport(string Label, string Encoding, int InputLength,
        int WarmCalls, int MeasuredCalls, long WarmBytes, long AllocatedBytes,
        long WarmConsumed, long MeasuredConsumed, long WarmWritten, long MeasuredWritten,
        long FirstOffset, long LastOffset, string Scalars, int EscapedObjectLength);
    private static byte[]? escaped;

    internal static LabelReport[] Run(string[] labels, Func<string, XmlInputDecoder> selected,
        CancellationToken cancellation, bool positive)
    {
        var reports = new LabelReport[labels.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            var label = labels[labelIndex];
            using var d = selected(label);
            var input = label == "Windows-1252" ? [0x80, 0x97, 0xE9, 13, 10] : label == "ISO-8859-1" ? [0x80, 0xE9, 13, 10] : "abcd\r\n"u8.ToArray();
            var output = new char[8];
            var maps = new long[8];
            var scalars = label == "Windows-1252" ? "€—é\r\n" : label == "ISO-8859-1" ? "\u0080é\r\n" : "abcd\r\n";
            var encoding = label switch
            {
                "US-ASCII" => XmlInputEncoding.Ascii,
                "ISO-8859-1" => XmlInputEncoding.Latin1,
                "Windows-1252" => XmlInputEncoding.Windows1252,
                _ => throw new InvalidOperationException("Unexpected legacy label.")
            };
            escaped = null;
            var warm = DecodeChecked(d, input, output, maps, scalars, encoding, cancellation, 10000, false, 4);
            var first = 4L + 10000L * input.Length;
            var measured = DecodeChecked(d, input, output, maps, scalars, encoding, cancellation, 10000, positive, first);
            reports[labelIndex] = new(label, d.Encoding.ToString(), input.Length, 10000, 10000, warm, measured,
                10000L * input.Length, 10000L * input.Length, 10000L * scalars.Length, 10000L * scalars.Length,
                first, maps[scalars.Length - 1], scalars, escaped?.Length ?? 0);
        }
        return reports;
    }

    // Both snapshots and the complete synchronous Decode workload share this boundary.
    // Successful checks allocate nothing; setup, reporting and assertions are outside.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static long DecodeChecked(XmlInputDecoder decoder, byte[] input, char[] output,
        long[] maps, string expected, XmlInputEncoding encoding, CancellationToken cancellation,
        int iterations, bool positive, long firstOffset)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var result = decoder.Decode(input, output, maps, false, cancellation);
            if (result.Step != DecodeStep.NeedInput || result.Diagnostic is not null
                || result.ConsumedBytes != input.Length || result.WrittenCharacters != expected.Length
                || decoder.Encoding != encoding || !output.AsSpan(0, result.WrittenCharacters).SequenceEqual(expected))
            {
                throw new InvalidOperationException("Legacy Decode label/scalar/progress mismatch.");
            }

            for (var j = 0; j < result.WrittenCharacters; j++)
            {
                if (maps[j] != firstOffset + (long)i * input.Length + j)
                {
                    throw new InvalidOperationException("Legacy Decode absolute offset mismatch.");
                }
            }

            if (positive)
            {
                escaped = new byte[37];
            }
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
