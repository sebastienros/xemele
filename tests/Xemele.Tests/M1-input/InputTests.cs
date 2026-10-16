using System.Text;
using System.Reflection;
using Xemele.Parsing.Input;
using Xunit;
namespace Xemele.Tests.Input;

public class InputTests
{
    private static (string Text, long[] Offsets, XmlDiagnostic? Error) Run(byte[] bytes, int split, int capacity = 2, string? declaration = null)
    {
        using var decoder = new XmlInputDecoder(new());
        var text = new StringBuilder();
        var offsets = new List<long>();
        var chars = new char[capacity];
        var maps = new long[capacity];
        var start = 0;
        foreach (var end in new[] { split, bytes.Length })
        {
            var final = end == bytes.Length;
            for (var attempts = 0; ; attempts++)
            {
                Assert.True(attempts <= bytes.Length + 5);
                var result = decoder.Decode(bytes.AsSpan(start, end - start), chars, maps, final, TestContext.Current.CancellationToken);
                Assert.InRange(result.ConsumedBytes, 0, end - start);
                Assert.InRange(result.WrittenCharacters, 0, capacity);
                text.Append(chars, 0, result.WrittenCharacters);
                offsets.AddRange(maps.AsSpan(0, result.WrittenCharacters).ToArray());
                start += result.ConsumedBytes;
                if (result.Step == DecodeStep.InvalidInput)
                {
                    return (text.ToString(), offsets.ToArray(), result.Diagnostic);
                }

                Assert.Null(result.Diagnostic);
                if (result.Step == DecodeStep.Completed)
                {
                    var error = decoder.ValidateDeclaration(declaration ?? "", new(null, 1, 1, 0));
                    return (text.ToString(), offsets.ToArray(), error);
                }
                if (result.Step == DecodeStep.NeedInput)
                {
                    Assert.Equal(end, start);
                    Assert.False(final);
                    break;
                }
                Assert.True(result.ConsumedBytes != 0 || result.WrittenCharacters != 0);
            }
        }
        throw new Exception("No completion");
    }
    [Fact]
    public void EverySplitAndCapacityMatchesStrictOracleAndOriginalScalarMapping()
    {
        string[] texts = ["", "<r/>", "<r>é\r\n😀\t中\rX\n</r>", "\uFEFF<r/>"];
        foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, false, true), new UnicodeEncoding(true, false, true) })
        {
            foreach (var bom in new[] { false, true })
            {
                foreach (var text in texts)
                {
                    byte[] preamble = bom ? encoding switch
                    {
                        UTF8Encoding => [0xEF, 0xBB, 0xBF],
                        UnicodeEncoding u when u.CodePage == 1200 => [0xFF, 0xFE],
                        _ => [0xFE, 0xFF]
                    } : [];
                    // BOM-less UTF16 requires its XML signature; other sources are ambiguous by design.
                    if (!bom && ((encoding is UnicodeEncoding && !text.StartsWith('<')) || text.StartsWith('\uFEFF')))
                    {
                        continue;
                    }

                    byte[] bytes = [.. preamble, .. encoding.GetBytes(text)];
                    var expected = new List<long>();
                    long offset = preamble.Length;
                    foreach (var rune in text.EnumerateRunes())
                    {
                        for (var i = 0; i < rune.Utf16SequenceLength; i++)
                        {
                            expected.Add(offset);
                        }

                        offset += encoding.GetByteCount(rune.ToString());
                    }
                    foreach (var capacity in new[] { 2, 3, 7, 128 })
                    {
                        for (var split = 0; split <= bytes.Length; split++)
                        {
                            var label = encoding is UnicodeEncoding ? encoding.CodePage == 1200 ? "UTF-16LE" : "UTF-16BE" : null;
                            var (Text, Offsets, Error) = Run(bytes, split, capacity, label);
                            Assert.Null(Error);
                            Assert.Equal(text, Text);
                            Assert.Equal(expected, Offsets);
                        }
                    }
                }
            }
        }
    }
    [Fact]
    public void InvalidUtf8EverySplitReportsSequenceStartAndScalarLocation()
    {
        byte[][] invalid = [[0x80], [0xC0, 0xAF], [0xE0, 0x80, 0x80], [0xED, 0xA0, 0x80], [0xF4, 0x90, 0x80, 0x80], [0xF5, 0x80, 0x80, 0x80], [0xC2], [0xE2, 0x82], [0xF0, 0x9F, 0x98], [0xE2, 0x28, 0xA1]];
        var prefix = Encoding.UTF8.GetBytes("😀\r\nX");
        foreach (var suffix in invalid)
        {
            byte[] bytes = [.. prefix, .. suffix];
            for (var split = 0; split <= bytes.Length; split++)
            {
                var (Text, Offsets, Error) = Run(bytes, split);
                Assert.NotNull(Error);
                Assert.Equal("INPUT.InvalidEncoding", Error.Code);
                Assert.Equal(prefix.Length, Error.Location.ByteOffset);
                Assert.Equal(2, Error.Location.LineNumber);
                Assert.Equal(2, Error.Location.LinePosition);
                Assert.Equal("😀\r\nX", Text);
            }
        }
    }
    [Fact]
    public void Utf16UnmatchedPairsOddBytesAndIncompleteBomRejectAtEverySplit()
    {
        foreach (var little in new[] { true, false })
        {
            var encoding = new UnicodeEncoding(!little, false, true);
            byte[] preamble = little ? [0xFF, 0xFE] : [0xFE, 0xFF];
            byte[][] bad = little ? [[0x00, 0xDC], [0x00, 0xD8], [0x00, 0xD8, 0x41, 0], [0x41]] : [[0xDC, 0], [0xD8, 0], [0xD8, 0, 0, 0x41], [0x41]];
            foreach (var suffix in bad)
            {
                byte[] prefix = [.. preamble, .. encoding.GetBytes("<r>😀\r\nX")];
                byte[] bytes = [.. prefix, .. suffix];
                for (var split = 0; split <= bytes.Length; split++)
                {
                    var (Text, Offsets, Error) = Run(bytes, split);
                    Assert.Equal("INPUT.InvalidEncoding", Error!.Code);
                    Assert.Equal(prefix.Length, Error.Location.ByteOffset);
                    Assert.Equal(2, Error.Location.LinePosition);
                }
            }
        }
        foreach (var bytes in new byte[][] { [0xEF], [0xEF, 0xBB], [0xFF], [0xFE] })
        {
            for (var split = 0; split <= bytes.Length; split++)
            {
                Assert.Equal("INPUT.InvalidEncoding", Run(bytes, split).Error!.Code);
            }
        }
    }
    [Fact]
    public void UnsupportedSignaturesRejectWithoutOutputAtEverySplit()
    {
        foreach (var bytes in new byte[][] { [0, 0, 0xFE, 0xFF], [0xFF, 0xFE, 0, 0], [0, 0, 0, 0x3C], [0x3C, 0, 0, 0], [0, 0, 0x3C, 0], [0, 0x3C, 0, 0], [0x4C, 0x6F, 0xA7, 0x94] })
        {
            for (var split = 0; split <= bytes.Length; split++)
            {
                var (Text, Offsets, Error) = Run(bytes, split);
                Assert.Equal("", Text);
                Assert.Equal("INPUT.UnsupportedEncoding", Error!.Code);
                Assert.Equal(0, Error.Location.ByteOffset);
            }
        }
    }
    [Fact]
    public void DeclarationLabelsMatchFullyAsciiInsensitiveAndNeverSwitchEncoding()
    {
        (byte[] Bytes, string[] Good, string[] Mismatch)[] cases = [
            (Encoding.UTF8.GetBytes("<r/>"),["","utf-8","UtF-8"],["UTF-16","UTF-16LE","UTF-16BE"]),
            ([0xFF,0xFE,0x3C,0,0x72,0],["","utf-16","UTF-16LE"],["UTF-8","UTF-16BE"]),
            ([0xFE,0xFF,0,0x3C,0,0x72],["","UTF-16","UTF-16BE"],["UTF-8","UTF-16LE"]),
            ([0x3C,0,0x72,0],["UTF-16LE"],["","UTF-16","UTF-8","UTF-16BE"]),
            ([0,0x3C,0,0x72],["UTF-16BE"],["","UTF-16","UTF-8","UTF-16LE"])
        ];
        foreach (var (Bytes, Good, Mismatch) in cases)
        {
            foreach (var label in Good)
            {
                Assert.Null(Run(Bytes, 0, 2, label).Error);
            }

            foreach (var label in Mismatch)
            {
                Assert.Equal("INPUT.EncodingMismatch", Run(Bytes, 0, 2, label).Error!.Code);
            }

            foreach (var label in new[] { "utf8", "UTF-8x", "UTF-16L", "ASCII", "ſTF-8", "UTF-8 ", new string('x', 10000) })
            {
                Assert.Equal("INPUT.UnsupportedEncoding", Run(Bytes, 0, 2, label).Error!.Code);
            }
        }
    }
    [Fact]
    public void LifecycleCapacityCancellationAndSnapshotsAreEnforced()
    {
        Assert.Throws<ArgumentNullException>(() => new XmlInputDecoder(null!));
        Assert.Throws<ArgumentException>(() => new XmlInputDecoder(new(), new Uri("relative", UriKind.Relative)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XmlInputDecoder(new() { Limits = new() { ExternalBytes = 0 } }));
        using var d = new XmlInputDecoder(new());
        Assert.Throws<InvalidOperationException>(() => d.ValidateDeclaration("", default));
        Assert.Throws<ArgumentException>(() => d.Decode([], new char[1], new long[1], false, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => d.Decode([], new char[2], new long[3], false, TestContext.Current.CancellationToken));
        Assert.Equal(XmlInputEncoding.Undetermined, d.Encoding);
        var r = d.Decode("abcd"u8, new char[2], new long[2], true, TestContext.Current.CancellationToken);
        Assert.Equal(DecodeStep.NeedOutput, r.Step);
        Assert.Throws<InvalidOperationException>(() => d.Decode([], new char[2], new long[2], false, TestContext.Current.CancellationToken));
        r = d.Decode("abcd"u8[r.ConsumedBytes..], new char[2], new long[2], true, TestContext.Current.CancellationToken);
        Assert.Equal(DecodeStep.Completed, r.Step);
        Assert.Null(d.ValidateDeclaration("UTF-8", default));
        Assert.Throws<InvalidOperationException>(() => d.ValidateDeclaration("UTF-8", default));
        Assert.Throws<InvalidOperationException>(() => d.Decode([], new char[2], new long[2], true, TestContext.Current.CancellationToken));
        d.Dispose();
        d.Dispose();
        Assert.Throws<ObjectDisposedException>(() => d.ValidateDeclaration("", default));
        Assert.Throws<ObjectDisposedException>(() => d.Encoding);
        using var cancelled = new XmlInputDecoder(new());
        using var token = new CancellationTokenSource();
        token.Cancel();
        var ex = Assert.Throws<OperationCanceledException>(() => cancelled.Decode([], new char[2], new long[2], true, token.Token));
        Assert.Equal(token.Token, ex.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => cancelled.Decode([], new char[2], new long[2], true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void CarryIsOwnedAndPrimaryByteCounterHasIndependentCheckedCapacity()
    {
        using var d = new XmlInputDecoder(new());
        byte[] bytes = [0xEF, 0xBB];
        Assert.Equal(DecodeStep.NeedInput, d.Decode(bytes, new char[2], new long[2], false, TestContext.Current.CancellationToken).Step);
        Array.Fill(bytes, (byte)0);
        var chars = new char[2];
        var offsets = new long[2];
        var r = d.Decode([0xBF, 0x41], chars, offsets, true, TestContext.Current.CancellationToken);
        Assert.Equal(DecodeStep.Completed, r.Step);
        Assert.Equal('A', chars[0]);
        Assert.Equal(3, offsets[0]);
        foreach (var limit in new[] { 1L, 3L, 4L })
        {
            using var bounded = new XmlInputDecoder(new()
            {
                Limits = new()
                {
                    ExternalBytes = limit
                }
            });
            r = bounded.Decode("abcd"u8, new char[8], new long[8], true, TestContext.Current.CancellationToken);
            Assert.Equal(DecodeStep.Completed, r.Step);
            Assert.Equal(4, r.ConsumedBytes);
        }
        using var overflow = new XmlInputDecoder(new()
        {
            Limits = new()
            {
                ExternalBytes = null
            }
        });
        overflow.Decode("abcd"u8, new char[8], new long[8], false, TestContext.Current.CancellationToken);
        typeof(XmlInputDecoder).GetField("acceptedBytes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(overflow, long.MaxValue);
        var overflowResult = overflow.Decode("z"u8, chars, offsets, true, TestContext.Current.CancellationToken);
        Assert.Equal(DecodeStep.InvalidInput, overflowResult.Step);
        Assert.Equal(0, overflowResult.ConsumedBytes);
        Assert.Equal(long.MaxValue, overflowResult.Diagnostic!.Location.ByteOffset);
    }
    [Fact]
    public void ByteAtATimeDrainAndDeclarationBeforeMalformedBodyHaveStableOrdering()
    {
        var bytes = Encoding.UTF8.GetBytes("<r>é😀\r\n中</r>");
        using var d = new XmlInputDecoder(new());
        var chars = new char[2];
        var maps = new long[2];
        var text = new StringBuilder();
        var offsets = new List<long>();
        for (var i = 0; i < bytes.Length; i++)
        {
            var used = 0;
            var attempts = 0;
            while (true)
            {
                Assert.True(attempts++ < 6);
                var r = d.Decode(bytes.AsSpan(i + used, 1 - used), chars, maps, i == bytes.Length - 1, TestContext.Current.CancellationToken);
                used += r.ConsumedBytes;
                text.Append(chars, 0, r.WrittenCharacters);
                offsets.AddRange(maps.AsSpan(0, r.WrittenCharacters).ToArray());
                if (r.Step != DecodeStep.NeedOutput)
                {
                    Assert.Equal(1, used);
                    Assert.NotEqual(DecodeStep.InvalidInput, r.Step);
                    break;
                }
            }
        }
        var (Text, Offsets, Error) = Run(bytes, 0);
        Assert.Equal(Text, text.ToString());
        Assert.Equal(Offsets, offsets);
        // Adapter framing is independent of transport chunks: submit only one new
        // byte until reconciliation. The decoder does not parse XML declarations.
        foreach (var transportChunk in new[] { 1, 2, 7, 1000 })
        {
            using var framed = new XmlInputDecoder(new());
            byte[] source = [.. Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='UTF-16'?>"), 0xFF];
            var consumed = 0;
            var prolog = new StringBuilder();
            while (!prolog.ToString().EndsWith("?>", StringComparison.Ordinal))
            {
                var transportEnd = Math.Min(source.Length, ((consumed / transportChunk) + 1) * transportChunk);
                Assert.True(transportEnd > consumed);
                var r = framed.Decode(source.AsSpan(consumed, 1), chars, maps, false, TestContext.Current.CancellationToken);
                consumed += r.ConsumedBytes;
                prolog.Append(chars, 0, r.WrittenCharacters);
                Assert.Null(r.Diagnostic);
            }
            var declarationAt = new XmlLocation(null, 1, 1, 0);
            var diagnostic = framed.ValidateDeclaration("UTF-16", declarationAt);
            Assert.Equal("INPUT.EncodingMismatch", diagnostic!.Code);
            Assert.Equal(source.Length - 1, consumed);
            Assert.Throws<InvalidOperationException>(() => framed.Decode(source.AsSpan(consumed), chars, maps, true, TestContext.Current.CancellationToken));
        }
    }
    [Fact]
    public void ContiguousErrorConsumptionStopsBeforeOffendingSequence()
    {
        foreach (var bytes in new byte[][] { [0xFF, 0x41, 0x41, 0x41], [0xEF, 0xBB, 0xBF, 0xFF] })
        {
            using var d = new XmlInputDecoder(new());
            var r = d.Decode(bytes, new char[8], new long[8], true, TestContext.Current.CancellationToken);
            Assert.Equal(DecodeStep.InvalidInput, r.Step);
            Assert.Equal(bytes[0] == 0xEF ? 3 : 0, r.ConsumedBytes);
            Assert.Equal(r.ConsumedBytes, r.Diagnostic!.Location.ByteOffset);
        }
        using var validPrefix = new XmlInputDecoder(new());
        var result = validPrefix.Decode([0x41, 0x42, 0xFF, 0x44], new char[8], new long[8], true, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.ConsumedBytes);
        Assert.Equal(2, result.WrittenCharacters);
        Assert.Equal(2, result.Diagnostic!.Location.ByteOffset);
    }
    [Fact]
    public void WarmDecodeHasZeroAllocationWithBoundedState()
    {
        using var d = new XmlInputDecoder(new()
        {
            Limits = new()
            {
                ExternalBytes = null
            }
        });
        var input = Encoding.UTF8.GetBytes("<r>é😀\r\n中</r>");
        var output = new char[100];
        var offsets = new long[100];
        for (var i = 0; i < 10000; i++)
        {
            d.Decode(input, output, offsets, false, TestContext.Current.CancellationToken);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            d.Decode(input, output, offsets, false, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
