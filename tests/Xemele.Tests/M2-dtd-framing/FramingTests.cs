using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Input;
using Xunit;

namespace Xemele.Tests;

public sealed class FramingTests
{
    private static readonly Uri Source = new("https://fixture.invalid/external.dtd");
    private static readonly string[] Declared =
    ["<?xml encoding='UTF-8'?>", "<?xml version=\"1.0\" encoding=\"UTF-16LE\" ?>",
     "<?xml\r\nversion \t= '1.0'\r\nencoding = 'a_9.-'\r\n?>", "<?xml\tencoding='X'?>body"];
    private static readonly string[] Absent =
    ["", "<", "<?", "<?x", "<?xm", "<?xml", "<?xml-stylesheet?>", " <?xml encoding='X'?>", "😀body", "<?xml😀tail", "\r\nbody", "中"];
    private static readonly string[] Rejected =
    ["<?xml ?>", "<?xml version='1.0'?>", "<?xml encoding=''?>", "<?xml encoding='9x'?>",
     "<?xml encoding='u:8'?>", "<?xml version='1.1' encoding='X'?>", "<?xml encoding='X' standalone='yes'?>",
     "<?xml version='1.0'encoding='X'?>", "<?xml encoding='X' version='1.0'?>", "<?xml encoding='X'? >",
     "<?xml encoding='X'", "<?xml encoding='X'>", "<?xml encoding='X' encoding='X'?>",
     "<?xml ENCODING='X'?>", "<?xml encoding='😀'?>", "\uD800", "\uDC00", "\0"];
    public static IEnumerable<object[]> Splits()
    {
        // MTP's transport identity collapsed the lone-high/low-surrogate rows.
        // An ASCII fixture identifier keeps every malformed UTF-16 case distinct.
        int caseId = 0;
        foreach (var group in new[] { (Declared, TextDeclarationStep.Declared), (Absent, TextDeclarationStep.Absent), (Rejected, TextDeclarationStep.Rejected) })
            foreach (string text in group.Item1)
            {
                for (int split = 0; split <= text.Length; split++)
                    foreach (bool mapped in new[] { false, true }) yield return [caseId, text, group.Item2.ToString(), split, mapped];
                caseId++;
            }
    }
    private static long[] Mappings(string text)
    {
        long[] result = new long[text.Length]; long offset = 3;
        for (int i = 0; i < text.Length; i++)
        {
            result[i] = offset;
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            { result[++i] = offset; offset += 4; }
            else offset += text[i] < 128 ? 1 : 3;
        }
        return result;
    }
    [Theory, MemberData(nameof(Splits))]
    public void EverySplit(int caseId, string text, string expected, int split, bool mapped)
    {
        Assert.InRange(caseId, 0, Declared.Length + Absent.Length + Rejected.Length - 1);
        using var oracle = new XmlTextDeclarationFramer(new(), Source);
        using var framer = new XmlTextDeclarationFramer(new(), Source);
        long[] offsets = mapped ? Mappings(text) : [];
        var whole = oracle.Append(text, offsets, true, TestContext.Current.CancellationToken);
        var first = framer.Append(text.AsSpan(0, split), mapped ? offsets.AsSpan(0, split) : [], false, TestContext.Current.CancellationToken);
        var result = first;
        int accepted = first.Consumed;
        if (first.Step == TextDeclarationStep.NeedInput)
        {
            Assert.Equal(split, first.Consumed);
            result = framer.Append(text.AsSpan(split), mapped ? offsets.AsSpan(split) : [], true, TestContext.Current.CancellationToken);
            accepted += result.Consumed;
        }
        Assert.Equal(expected, result.Step.ToString());
        Assert.Equal(whole.Step, result.Step);
        Assert.Equal(whole.Declaration, result.Declaration);
        Assert.Equal(whole.Diagnostic, result.Diagnostic);
        Assert.Equal(whole.Consumed, accepted);
        Assert.Equal(whole.NextLocation.LineNumber, result.NextLocation.LineNumber);
        Assert.Equal(whole.NextLocation.LinePosition, result.NextLocation.LinePosition);
        Assert.Equal(Source, result.NextLocation.SourceUri);
        if (result.Step == TextDeclarationStep.Absent)
        {
            Assert.InRange(accepted, 0, 7);
            char[] chars = new char[7]; long[] maps = mapped && text.Length != 0 ? new long[7] : [];
            Assert.Equal(oracle.PrefixLocation, framer.PrefixLocation);
            Assert.Equal(accepted, framer.DrainPrefix(chars, maps));
            Assert.Equal(text[..accepted], new string(chars, 0, accepted));
            if (mapped) Assert.Equal(offsets[..accepted], maps[..accepted]);
            Assert.Throws<InvalidOperationException>(() => framer.DrainPrefix(chars, maps));
        }
        else Assert.Throws<InvalidOperationException>(() => framer.DrainPrefix(new char[7], []));
        Assert.Throws<InvalidOperationException>(() => framer.Append([], [], true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void LiveBudgetRefusesBeforeAcceptingNextDeclarationScalar()
    {
        var ledger = new Ledger(8);
        using var f = new XmlTextDeclarationFramer(new() { TokenCharacters = null }, ledger, Source);
        Assert.Equal(TextDeclarationStep.NeedInput, f.Append("<?xml ", [], false, TestContext.Current.CancellationToken).Step);
        Assert.Equal(6, ledger.Charged);
        var result = f.Append(new string(' ', 10000), [], false, TestContext.Current.CancellationToken);
        Assert.Equal(TextDeclarationStep.Rejected, result.Step);
        Assert.Equal(2, result.Consumed);
        Assert.Same(ledger.Refusal, result.Diagnostic);
        Assert.Equal(8, ledger.Charged);
        Assert.Equal(new XmlLocation(Source, 1, 9, null), result.Diagnostic!.Location);
    }
    [Fact]
    public void DeferredPrefixAndAbsenceChargeExactlyOnce()
    {
        var ledger = new Ledger(100);
        using var f = new XmlTextDeclarationFramer(new(), ledger);
        Assert.Equal(TextDeclarationStep.NeedInput, f.Append("<?xml", [], false, TestContext.Current.CancellationToken).Step);
        Assert.Equal(0, ledger.Charged);
        Assert.Equal(TextDeclarationStep.Absent, f.Append("😀body", [], true, TestContext.Current.CancellationToken).Step);
        char[] prefix = new char[7]; int count = f.DrainPrefix(prefix, []);
        Assert.Equal(7, count); Assert.Equal(0, ledger.Charged);
        // The downstream consumer charges six scalars, rather than seven UTF-16 units.
        foreach (var rune in new string(prefix, 0, count).EnumerateRunes()) Assert.Null(ledger.ChargeExpandedCharacters(1, default));
        Assert.Equal(6, ledger.Charged);
    }
    [Theory]
    [InlineData(5, 6)] [InlineData(6, 6)]
    public void RecognitionRefusalHasOnlyBoundedAcceptedPrefix(int limit, int consumed)
    {
        using var f = new XmlTextDeclarationFramer(new() { ExpandedCharacters = limit });
        var result = f.Append("<?xml  body", [], false, TestContext.Current.CancellationToken);
        Assert.Equal(TextDeclarationStep.Rejected, result.Step);
        Assert.Equal(consumed, result.Consumed);
    }
    [Fact]
    public void ExactAndOneOverScalarLimitsWithCrLf()
    {
        const string input = "<?xml\r\nencoding='X'?>";
        int normalized = input.Length - 1;
        using var exact = new XmlTextDeclarationFramer(new() { TokenCharacters = normalized, ExpandedCharacters = input.Length, DocumentCharacters = 1 });
        Assert.Equal(TextDeclarationStep.Declared, exact.Append(input, [], true, TestContext.Current.CancellationToken).Step);
        using var token = new XmlTextDeclarationFramer(new() { TokenCharacters = normalized - 1 });
        Assert.Equal("XML.ResourceLimit", token.Append(input, [], true, TestContext.Current.CancellationToken).Diagnostic!.Code);
        using var expansion = new XmlTextDeclarationFramer(new() { ExpandedCharacters = input.Length - 1 });
        var r = expansion.Append(input, [], true, TestContext.Current.CancellationToken);
        Assert.Equal(input.Length - 1, r.Consumed);
        Assert.Equal("XML.ResourceLimit", r.Diagnostic!.Code);
    }
    [Fact]
    public void SnapshotAndBorrowedInputOwnership()
    {
        char[] input = "<?xml encoding='UTF-8'?>tail".ToCharArray();
        long[] mappings = Mappings(new string(input));
        using var f = new XmlTextDeclarationFramer(new(), Source);
        var result = f.Append(input, mappings, true, TestContext.Current.CancellationToken);
        Array.Fill(input, 'x'); Array.Fill(mappings, 0);
        Assert.Equal("UTF-8", result.Declaration!.Value.EncodingName);
        Assert.Equal(new XmlLocation(Source, 1, 1, 3), result.Declaration.Value.Location);
        Assert.Equal(new XmlLocation(Source, 1, 25, 27), result.NextLocation);
        f.Dispose(); Assert.Equal("UTF-8", result.Declaration.Value.EncodingName);
        using var absent = new XmlTextDeclarationFramer(new(), Source);
        input = "<?xml-stylesheet".ToCharArray(); mappings = Mappings(new string(input));
        absent.Append(input, mappings, true, TestContext.Current.CancellationToken); Array.Fill(input, 'x'); Array.Fill(mappings, 0);
        char[] prefix = new char[7]; long[] original = new long[7];
        Assert.Throws<ArgumentException>(() => absent.DrainPrefix(new char[6], new long[6]));
        Assert.Throws<ArgumentException>(() => absent.DrainPrefix(prefix, []));
        Assert.Equal(6, absent.DrainPrefix(prefix, original));
        Assert.Equal("<?xml-", new string(prefix, 0, 6)); Assert.Equal(8, original[5]);
    }
    [Fact]
    public void EofUnknownByteBoundaryAndNormalizedCursor()
    {
        using var f = new XmlTextDeclarationFramer(new(), Source);
        var r = f.Append("<?xml\r\nencoding='X'", Mappings("<?xml\r\nencoding='X'"), true, TestContext.Current.CancellationToken);
        Assert.Equal(TextDeclarationStep.Rejected, r.Step);
        Assert.Equal(new XmlLocation(Source, 2, 13, null), r.Diagnostic!.Location);
        using var empty = new XmlTextDeclarationFramer(new(), Source);
        r = empty.Append([], [], true, TestContext.Current.CancellationToken);
        Assert.Equal(new XmlLocation(Source, 1, 1, null), r.NextLocation);
        Assert.Equal(r.NextLocation, empty.PrefixLocation);
    }
    [Fact]
    public void MappingMisuseBeforeMutationAndSplitSurrogate()
    {
        using var f = new XmlTextDeclarationFramer(new());
        Assert.Throws<ArgumentException>(() => f.Append("ab", [1], false, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => f.Append("ab", [2, 1], false, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => f.Append("a", [-1], false, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => f.Append("😀", [1, 2], false, TestContext.Current.CancellationToken));
        Assert.Equal(TextDeclarationStep.NeedInput, f.Append("<?", [3, 4], false, TestContext.Current.CancellationToken).Step);
        Assert.Throws<ArgumentException>(() => f.Append("x", [], false, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => f.Append("x", [2], false, TestContext.Current.CancellationToken));
        Assert.Equal(TextDeclarationStep.Absent, f.Append("z", [5], false, TestContext.Current.CancellationToken).Step);
        using var split = new XmlTextDeclarationFramer(new());
        split.Append("\uD83D", [7], false, TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => split.Append("\uDE00", [8], false, TestContext.Current.CancellationToken));
        Assert.Equal(TextDeclarationStep.Absent, split.Append("\uDE00", [7], true, TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void CancellationCallbackFaultReentrancyAndDisposal()
    {
        using var canceled = new XmlTextDeclarationFramer(new());
        Assert.Throws<OperationCanceledException>(() => canceled.Append("<?xml ", [], false, new(true)));
        Assert.Throws<InvalidOperationException>(() => canceled.Append([], [], true, TestContext.Current.CancellationToken));
        var thrown = new IOException("ledger"); var ledger = new Ledger(100) { Throw = thrown };
        using var f = new XmlTextDeclarationFramer(new(), ledger);
        Assert.Same(thrown, Assert.Throws<IOException>(() => f.Append("<?xml ", [], false, TestContext.Current.CancellationToken)));
        Assert.Throws<InvalidOperationException>(() => f.Append([], [], true, TestContext.Current.CancellationToken));
        ledger = new Ledger(100);
        using var reentrant = new XmlTextDeclarationFramer(new(), ledger);
        ledger.Callback = () =>
        {
            Assert.Throws<InvalidOperationException>(() => reentrant.Append([], [], false, TestContext.Current.CancellationToken));
            Assert.Throws<InvalidOperationException>(() => reentrant.Dispose());
        };
        Assert.Equal(TextDeclarationStep.Declared, reentrant.Append("<?xml encoding='X'?>", [], true, TestContext.Current.CancellationToken).Step);
        reentrant.Dispose(); reentrant.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reentrant.Append([], [], true, TestContext.Current.CancellationToken));
        Assert.Throws<ObjectDisposedException>(() => reentrant.DrainPrefix(new char[7], []));
        Assert.Throws<ObjectDisposedException>(() => _ = reentrant.PrefixLocation);
    }
    [Theory]
    [InlineData("utf8")] [InlineData("utf16le")] [InlineData("utf16be")]
    public void ActualM1DecoderCompositionBeforeBody(string kind)
    {
        Encoding codec = kind switch { "utf16le" => new UnicodeEncoding(false, true, true), "utf16be" => new UnicodeEncoding(true, true, true), _ => new UTF8Encoding(true, true) };
        string label = kind switch { "utf16le" => "UTF-16LE", "utf16be" => "UTF-16BE", _ => "UTF-8" };
        string text = $"<?xml encoding='{label}'?>";
        byte[] bytes = [.. codec.GetPreamble(), .. codec.GetBytes(text), 0xFF];
        using var decoder = new XmlInputDecoder(new(), Source);
        using var framer = new XmlTextDeclarationFramer(new(), Source);
        char[] scratch = new char[4]; long[] offsets = new long[4];
        int used = 0; TextDeclarationResult result = default;
        while (true)
        {
            var d = decoder.Decode(bytes.AsSpan(used, Math.Min(1, bytes.Length - used)), scratch, offsets, false, TestContext.Current.CancellationToken);
            Assert.NotEqual(DecodeStep.InvalidInput, d.Step); used += d.ConsumedBytes;
            if (d.WrittenCharacters == 0) continue;
            result = framer.Append(scratch.AsSpan(0, d.WrittenCharacters), offsets.AsSpan(0, d.WrittenCharacters), false, TestContext.Current.CancellationToken);
            if (result.Step != TextDeclarationStep.NeedInput) break;
        }
        Assert.Equal(TextDeclarationStep.Declared, result.Step);
        Assert.Equal(bytes.Length - 1, used);
        Assert.Null(decoder.ValidateDeclaration(result.Declaration!.Value.EncodingName, result.Declaration.Value.Location));
        Assert.Equal(codec.GetPreamble().Length, result.Declaration.Value.Location.ByteOffset);
        Assert.Equal(DecodeStep.InvalidInput, decoder.Decode(bytes.AsSpan(used), scratch, offsets, true, TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void ActualDecoderBomOnlyAndEmptySource()
    {
        foreach (byte[] bytes in new byte[][] { [], [0xEF, 0xBB, 0xBF], [0xFF, 0xFE], [0xFE, 0xFF] })
        {
            using var decoder = new XmlInputDecoder(new()); using var f = new XmlTextDeclarationFramer(new());
            char[] output = new char[2]; long[] maps = new long[2];
            var decoded = decoder.Decode(bytes, output, maps, true, TestContext.Current.CancellationToken);
            Assert.Equal(DecodeStep.Completed, decoded.Step); Assert.Equal(0, decoded.WrittenCharacters);
            var framed = f.Append([], [], true, TestContext.Current.CancellationToken);
            Assert.Equal(TextDeclarationStep.Absent, framed.Step); Assert.Null(framed.NextLocation.ByteOffset);
            Assert.Null(decoder.ValidateDeclaration([], framed.NextLocation));
            Assert.Equal(0, f.DrainPrefix(new char[7], []));
        }
    }
    [Fact]
    public void LongLabelGrowthOneUnitChunksAndCultureIndependence()
    {
        string label = "a" + new string('B', 4096) + "_9.-";
        string input = "<?xml version='1.0' encoding='" + label + "'?>";
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("tr-TR");
            using var f = new XmlTextDeclarationFramer(new() { TokenCharacters = input.Length, ExpandedCharacters = input.Length });
            TextDeclarationResult result = default;
            for (int i = 0; i < input.Length; i++)
            {
                result = f.Append(input.AsSpan(i, 1), [], i == input.Length - 1, TestContext.Current.CancellationToken);
                Assert.Equal(1, result.Consumed);
                if (i < input.Length - 1) Assert.Equal(TextDeclarationStep.NeedInput, result.Step);
            }
            Assert.Equal(TextDeclarationStep.Declared, result.Step);
            Assert.Equal(label, result.Declaration!.Value.EncodingName);
            Assert.Equal("1.0", result.Declaration.Value.Version);
            using var small = new XmlTextDeclarationFramer(new() { TokenCharacters = input.Length - 1 });
            Assert.Equal("XML.ResourceLimit", small.Append(input, [], true, TestContext.Current.CancellationToken).Diagnostic!.Code);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void SharedBudgetDeclarationRawUnitsAndCancellationDuringWork()
    {
        const string text = "<?xml\r\nencoding='X'?>";
        var ledger = new Ledger(text.Length);
        using var f = new XmlTextDeclarationFramer(new(), ledger);
        Assert.Equal(TextDeclarationStep.Declared, f.Append(text, [], true, TestContext.Current.CancellationToken).Step);
        Assert.Equal(text.Length, ledger.Charged);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        ledger = new Ledger(1000);
        using var interrupted = new XmlTextDeclarationFramer(new(), ledger);
        ledger.Callback = () => { if (ledger.Charged >= 7) cancellation.Cancel(); };
        Assert.Throws<OperationCanceledException>(() => interrupted.Append("<?xml     encoding='X'?>", [], true, cancellation.Token));
        Assert.InRange(ledger.Charged, 7, 8);
        Assert.Throws<InvalidOperationException>(() => interrupted.Append([], [], true, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void ConstructorValidationAndStateSnapshot()
    {
        Assert.Throws<ArgumentNullException>(() => new XmlTextDeclarationFramer(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new XmlTextDeclarationFramer(new() { TokenCharacters = 0 }));
        Assert.Throws<ArgumentException>(() => new XmlTextDeclarationFramer(new(), new Uri("relative", UriKind.Relative)));
        Assert.Throws<ArgumentNullException>(() => new XmlTextDeclarationFramer(new(), (IDtdBudget)null!));
        XmlResourceLimits limits = new() { ExpandedCharacters = 6 };
        using var f = new XmlTextDeclarationFramer(limits);
        limits = limits with { ExpandedCharacters = 100 };
        Assert.Equal(TextDeclarationStep.Rejected, f.Append("<?xml encoding='X'?>", [], true, TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void WarmWhitespacePrimitiveHasNoAllocations()
    {
        var request = FramingAllocationChild.EnterChild();
        if (request is null)
        {
            var pair = FramingAllocationChild.RunPair();
            Assert.Equal(0, pair.Real.AllocatedBytes);
            Assert.True(pair.Positive.AllocatedBytes > 0, pair.Directory);
            return;
        }
        var cancellation = TestContext.Current.CancellationToken;
        using var f = new XmlTextDeclarationFramer(new() { TokenCharacters = null, ExpandedCharacters = null });
        var prefix = f.Append("<?xml ", [], false, cancellation);
        Assert.Equal(6, prefix.Consumed);
        Assert.Equal(TextDeclarationStep.NeedInput, prefix.Step);
        Assert.Null(prefix.Diagnostic);
        Assert.Null(prefix.Declaration);
        object? escaped = null;
        FramingAllocationChild.Stage(request, "warm-start");
        var warm = FramingAllocationProbe.AppendChecked(f, cancellation, false, ref escaped);
        FramingAllocationChild.Stage(request, "measured-start");
        var measured = FramingAllocationProbe.AppendChecked(f, cancellation, request.Mode == "positive", ref escaped);
        FramingAllocationChild.Stage(request, "measured-end");
        var completion = f.Append(FramingAllocationProbe.Completion, [], true, cancellation);
        FramingAllocationChild.Write(request, warm, measured, completion, (escaped as byte[])?.Length ?? 0);
        Assert.Equal(32000, warm.Consumed);
        Assert.Equal(32000, measured.Consumed);
        Assert.Equal(new XmlLocation(null, 1, 32007, null), warm.Cursor);
        Assert.Equal(new XmlLocation(null, 1, 64007, null), measured.Cursor);
        Assert.Equal(TextDeclarationStep.Declared, completion.Step);
        Assert.Equal(FramingAllocationProbe.Completion.Length, completion.Consumed);
        Assert.Null(completion.Diagnostic);
        Assert.Equal("X", completion.Declaration!.Value.EncodingName);
        Assert.Equal(new XmlLocation(null, 1, 64007 + FramingAllocationProbe.Completion.Length, null), completion.NextLocation);
        if (request.Mode == "real") Assert.Equal(0, measured.Bytes);
        else
        {
            Assert.True(measured.Bytes > 0);
            Assert.Equal(37, Assert.IsType<byte[]>(escaped).Length);
        }
    }
    private sealed class Ledger(long maximum) : IDtdBudget
    {
        public long Charged; public XmlDiagnostic? Refusal; public Exception? Throw; public Action? Callback;
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at)
        {
            Callback?.Invoke(); if (Throw is not null) throw Throw;
            if (count > maximum - Charged) return Refusal = new("TEST.Refused", XmlDiagnosticSeverity.Fatal, "Ledger refused.", at);
            Charged += count; return null;
        }
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at) => throw new InvalidOperationException("Framer must not charge names.");
        public XmlDiagnostic? EnterEntity(XmlLocation at) => throw new InvalidOperationException("Framer must not enter entities.");
        public void ExitEntity() => throw new InvalidOperationException("Framer must not exit entities.");
    }
}
