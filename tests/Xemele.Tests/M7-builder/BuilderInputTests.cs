using System.Text;
using Xemele.Html;
using Xunit;
namespace Xemele.Tests.HtmlTreeConstruction;

public sealed class BuilderInputTests
{
    [Fact]
    public void EveryWhatwgLabelAndGeneratedIndexRowMatchesOriginalSnapshot()
    {
        var root = Path.Combine(FindRoot(), "tests/fixtures/M7-builder/encoding");
        int labels = 0, cells = 0;
        using var encoding = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "encodings.json")));
        foreach (var group in encoding.RootElement.EnumerateArray())
        {
            foreach (var item in group.GetProperty("encodings").EnumerateArray())
            {
                foreach (var label in item.GetProperty("labels").EnumerateArray())
                {
                    var expected = item.GetProperty("name").GetString()!.ToLowerInvariant();
                    var actual = label.GetString()!;
                    Assert.Equal(expected, HtmlEncodingTables.Label(Encoding.ASCII.GetBytes(actual)));
                    Assert.Equal(expected, HtmlEncodingTables.Label(Encoding.ASCII.GetBytes(" \t" + actual.ToUpperInvariant() + "\r\n")));
                    Assert.Equal(expected, HtmlEncodingTables.Label(actual));
                    Assert.Equal(expected, HtmlEncodingTables.Label(" \t" + actual.ToUpperInvariant() + "\r\n"));
                    labels++;
                }
            }
        }

        using var indexes = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "indexes.json")));
        foreach (var item in indexes.RootElement.EnumerateObject())
        {
            var table = HtmlEncodingTables.Index(item.Name);
            var i = 0;
            foreach (var cell in item.Value.EnumerateArray())
            {
                if (cell.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var value in cell.EnumerateArray())
                    {
                        Assert.Equal(value.GetInt32(), table[i++]);
                    }
                }
                else
                {
                    Assert.Equal(cell.ValueKind == System.Text.Json.JsonValueKind.Null ? -1 : cell.GetInt32(), table[i++]);
                }
            }
            Assert.Equal(i, table.Length);
            cells += i;
        }
        Assert.Equal(228, labels);
        Assert.Equal(91711, cells);
        Assert.Null(HtmlEncodingTables.Label("not-an-encoding"));
        Assert.Null(HtmlEncodingTables.Label("utf-8x"));
        Assert.Null(HtmlEncodingTables.Label("Koi8-r"));
    }

    [Theory]
    [InlineData("<meta CHARSET=windows-1251 charset=utf-8>", "windows-1251")]
    [InlineData("<meta content='text/html; charset=koi8-r' charset=iso-8859-15>", "iso-8859-15")]
    [InlineData("<meta charset=iso-8859-15 content='text/html; charset=koi8-r'>", "iso-8859-15")]
    [InlineData("<meta content='text/html; charset=windows-1251'>", "windows-1252")]
    [InlineData("<meta content='charset = \"UTF-8\"' http-equiv=CONTENT-TYPE>", "utf-8")]
    [InlineData("<p charset=utf-8><meta charset=windows-1251>", "windows-1251")]
    [InlineData("<meta charset=utf-8", "windows-1252")]
    [InlineData("<meta charset='utf-8'", "utf-8")]
    [InlineData("<meta charset='utf-8", "windows-1252")]
    public void BorrowedPrescanDuplicatePragmaAndTruncatedAttributes(string input, string expected)
        => Assert.Equal(expected, HtmlByteInput.Sniff(Encoding.UTF8.GetBytes(input)).Encoding);
    [Fact]
    public void BorrowedPrescanDenseIgnoredAndNonAsciiNames()
    {
        Assert.Equal("windows-1251", HtmlByteInput.Sniff(Encoding.UTF8.GetBytes("<meta " + string.Concat(Enumerable.Repeat("x ", 450)) + "charset=windows-1251>")).Encoding);
        Assert.Equal("utf-8", HtmlByteInput.Sniff(Encoding.UTF8.GetBytes("<ignored 数据='utf-8' charset=koi8-r><meta 数据=x charset=utf-8>")).Encoding);
    }
    [Fact]
    public void OriginalPinnedEncodingVectors()
    {
        var root = FindRoot();
        var count = 0;
        var failures = new List<string>();
        foreach (var file in new[] { "tests1.dat", "tests2.dat", "test-yahoo-jp.dat" })
        {
            var path = Path.Combine(root, "tests/fixtures/M7-tokenizer/encoding", file);
            var cases = Encoding.Latin1.GetString(File.ReadAllBytes(path)).Split("#data\n", StringSplitOptions.None);
            for (var i = 1; i < cases.Length; i++)
            {
                var marker = cases[i].IndexOf("#encoding\n", StringComparison.Ordinal);
                Assert.True(marker >= 0);
                var source = marker == 0 ? "" : cases[i][..(marker - 1)];
                var expected = cases[i][(marker + 10)..].Trim();
                var canonical = HtmlEncodingTables.Label(expected);
                var bytes = Encoding.Latin1.GetBytes(source);
                var actual = HtmlByteInput.Sniff(bytes.AsSpan(0, Math.Min(1024, bytes.Length))).Encoding;
                count++;
                if (file == "tests1.dat" && i - 1 is >= 47 and <= 53)
                {
                    Assert.Equal("iso-8859-2", canonical);
                    Assert.True(Encoding.Latin1.GetString(bytes).IndexOf("<meta", StringComparison.Ordinal) >= 1024);
                    Assert.Equal("windows-1252", actual);
                    continue;
                }
                if (canonical != actual)
                {
                    failures.Add($"{file}:{i - 1}: expected {canonical}; actual {actual}");
                }
            }
        }
        Assert.Equal(82, count);
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    [Fact]
    public void BomSniffWindowAndIncrementalCallerStream()
    {
        var utf8 = Encoding.UTF8.GetBytes("<p>é😀");
        Assert.Equal("é😀", Html5Parser.Parse([0xEF, 0xBB, 0xBF, .. utf8]).RootElement.GetTextContent());
        byte[] little = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("<meta charset=windows-1252><p>é😀")];
        Assert.Equal("é😀", Html5Parser.Parse(little).RootElement.GetTextContent());
        Assert.Equal("é", Html5Parser.Parse("<p>é").RootElement.GetTextContent());
        Assert.Equal("€", Html5Parser.Parse("<p>"u8.ToArray().Concat(new byte[] { 0x80 }).ToArray()).RootElement.GetTextContent());
        var bytes = Encoding.UTF8.GetBytes("<meta charset=utf-8><table>π<tr><td>😀</table><p>" + new string('x', 20000));
        using var stream = new ShortReadStream(bytes, 1);
        Assert.Equal(Html5Parser.Parse(bytes).RootElement.GetTextContent(), Html5Parser.Load(stream, cancellationToken: TestContext.Current.CancellationToken).RootElement.GetTextContent());
        Assert.False(stream.WasDisposed);
        Assert.True(stream.ReadCalls > 20000);
        using var cancelled = new ShortReadStream(bytes, 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Html5Parser.Load(cancelled, cancellationToken: cts.Token));
        Assert.Equal(0, cancelled.ReadCalls);
        Assert.False(cancelled.WasDisposed);
        Assert.Equal("windows-1252", HtmlByteInput.Sniff(Encoding.ASCII.GetBytes(new string(' ', 1024) + "<meta charset=utf-8>").AsSpan(0, 1024)).Encoding);
    }
    [Fact]
    public void MergedParsedTextAndSelectedContentKeepPerTokenQuotas()
    {
        var settings = new Html5ParseOptions { Limits = new() { TokenCharacters = 32 } };
        var text = new string('x', 100_000);
        var doc = Html5Parser.Parse("<select><button><selectedcontent></button><option>" + text, settings);
        var select = doc.RootElement.Children[1].FirstChild!;
        Assert.Equal(text, select.FirstChild!.FirstChild!.GetTextContent());
        Assert.Equal(text, select.Children[1].GetTextContent());
        Assert.Single(select.Children[1].Children);
        Assert.Throws<InvalidOperationException>(() => doc.CreateText(text));
        Assert.Throws<InvalidOperationException>(() => select.Children[1].FirstChild!.Value = text);
        using var stream = new ShortReadStream(Encoding.ASCII.GetBytes("<p>" + text), 17);
        var loaded = Html5Parser.Load(stream, settings, TestContext.Current.CancellationToken);
        Assert.Equal(text, loaded.RootElement.GetTextContent());
        Assert.Single(loaded.RootElement.Children[1].FirstChild!.Children);
    }

    [Fact]
    public void EncodingDeclarationWindowBoundaryAndNoChardetOrExecutedMeta()
    {
        var declaration = "<meta charset=iso8859-2>";
        byte[] atBoundary = [.. Encoding.ASCII.GetBytes(new string(' ', 1024 - declaration.Length) + declaration + "<p>"), 0xA1];
        Assert.Equal("iso-8859-2", HtmlByteInput.Sniff(atBoundary.AsSpan(0, 1024)).Encoding);
        Assert.Equal("Ą", Html5Parser.Parse(atBoundary).RootElement.GetTextContent());
        byte[] outside = [.. Encoding.ASCII.GetBytes(new string(' ', 1025 - declaration.Length) + declaration + "<p>"), 0xA1];
        Assert.Equal("windows-1252", HtmlByteInput.Sniff(outside.AsSpan(0, 1024)).Encoding);
        Assert.Equal("¡", Html5Parser.Parse(outside).RootElement.GetTextContent());
        var big5 = File.ReadAllBytes(Path.Combine(FindRoot(), "tests/fixtures/M7-tokenizer/encoding/chardet/test_big5.txt"));
        Assert.Equal("windows-1252", HtmlByteInput.Sniff(big5.AsSpan(0, Math.Min(1024, big5.Length))).Encoding);
        var scripted = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(FindRoot(), "tests/fixtures/M7-tokenizer/encoding/scripted/tests1.dat")));
        var end = scripted.IndexOf("#encoding", StringComparison.Ordinal);
        var raw = Encoding.Latin1.GetBytes(scripted[6..(end - 1)]);
        Assert.Contains("document.write", scripted, StringComparison.Ordinal);
        Assert.Equal("windows-1252", HtmlByteInput.Sniff(raw).Encoding);
    }

    [Theory]
    [InlineData("big5", new byte[] { 0x88, 0x62 }, "Ê̄")]
    [InlineData("shift_jis", new byte[] { 0x82, 0xA0 }, "あ")]
    [InlineData("euc-jp", new byte[] { 0xA4, 0xA2 }, "あ")]
    [InlineData("euc-kr", new byte[] { 0xB0, 0xA1 }, "가")]
    [InlineData("gb18030", new byte[] { 0xC4, 0xE3 }, "你")]
    [InlineData("gb18030", new byte[] { 0x94, 0x39, 0xFC, 0x36 }, "😀")]
    [InlineData("iso-2022-jp", new byte[] { 0x1B, 0x24, 0x42, 0x24, 0x22, 0x1B, 0x28, 0x42 }, "あ")]
    [InlineData("iso-2022-jp", new byte[] { 0x1B, 0x24, 0x42, 0x1B, 0x24 }, "��")]
    [InlineData("utf-8", new byte[] { 0xE0, 0x80, 0x41 }, "��A")]
    public void DecoderStateCrossesEveryChunk(string encoding, byte[] bytes, string expected)
    {
        for (var split = 0; split <= bytes.Length; split++)
        {
            var text = new StringBuilder();
            var tokens = new HtmlTokenizer(t => { if (t.Kind == HtmlTokenKind.Character) { text.Append(t.Data); } }, new());
            var decoder = new HtmlByteDecoder(encoding, tokens);
            decoder.Feed(bytes.AsSpan(0, split), 0);
            decoder.Feed(bytes.AsSpan(split), split);
            decoder.Complete(bytes.Length);
            tokens.Complete(bytes.Length);
            Assert.Equal(expected, text.ToString());
        }
    }
    internal static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "Xemele.slnx")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException("No source root.");
    }
    private sealed class ShortReadStream(byte[] bytes, int chunk) : Stream
    {
        private int position; internal bool WasDisposed; internal int ReadCalls;
        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            var amount = Math.Min(Math.Min(count, chunk), bytes.Length - position);
            bytes.AsSpan(position, amount).CopyTo(buffer.AsSpan(offset, amount));
            position += amount;
            return amount;
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => bytes.Length; public override long Position
        {
            get => position; set => throw new NotSupportedException();
        }
        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
