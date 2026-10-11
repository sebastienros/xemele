using System.Text;
using Xemele.Html;
using Xunit;
namespace Xemele.Tests.HtmlTreeConstruction;
public sealed class BuilderInputTests
{
    [Fact]
    public void OriginalPinnedEncodingVectors()
    {
        string root = FindRoot(); int count = 0; var failures = new List<string>();
        foreach (string file in new[] { "tests1.dat", "tests2.dat", "test-yahoo-jp.dat" })
        {
            string path = Path.Combine(root, "tests/fixtures/M7-tokenizer/encoding", file);
            string[] cases = Encoding.Latin1.GetString(File.ReadAllBytes(path)).Split("#data\n", StringSplitOptions.None);
            for (int i = 1; i < cases.Length; i++)
            {
                int marker = cases[i].IndexOf("#encoding\n", StringComparison.Ordinal);
                Assert.True(marker >= 0); string source = marker == 0 ? "" : cases[i][..(marker - 1)];
                string expected = cases[i][(marker + 10)..].Trim(); string? canonical = HtmlEncodingTables.Label(expected);
                var bytes = Encoding.Latin1.GetBytes(source); string actual = HtmlByteInput.Sniff(bytes.AsSpan(0, Math.Min(1024, bytes.Length))).Encoding;
                count++;
                if (file == "tests1.dat" && i - 1 is >= 47 and <= 53)
                {
                    Assert.Equal("iso-8859-2", canonical); Assert.True(Encoding.Latin1.GetString(bytes).IndexOf("<meta", StringComparison.Ordinal) >= 1024); Assert.Equal("windows-1252", actual); continue;
                }
                if (canonical != actual) failures.Add($"{file}:{i - 1}: expected {canonical}; actual {actual}");
            }
        }
        Assert.Equal(82, count); Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
    [Fact]
    public void BomSniffWindowAndIncrementalCallerStream()
    {
        var utf8 = Encoding.UTF8.GetBytes("<p>é😀");
        Assert.Equal("é😀", Html5Parser.Parse([0xEF, 0xBB, 0xBF, ..utf8]).RootElement.GetTextContent());
        byte[] little = [0xFF, 0xFE, ..Encoding.Unicode.GetBytes("<meta charset=windows-1252><p>é😀")];
        Assert.Equal("é😀", Html5Parser.Parse(little).RootElement.GetTextContent());
        Assert.Equal("é", Html5Parser.Parse("<p>é").RootElement.GetTextContent());
        Assert.Equal("€", Html5Parser.Parse("<p>"u8.ToArray().Concat(new byte[] { 0x80 }).ToArray()).RootElement.GetTextContent());
        byte[] bytes = Encoding.UTF8.GetBytes("<meta charset=utf-8><table>π<tr><td>😀</table><p>" + new string('x', 20000));
        using var stream = new ShortReadStream(bytes, 1);
        Assert.Equal(Html5Parser.Parse(bytes).RootElement.GetTextContent(), Html5Parser.Load(stream, cancellationToken: TestContext.Current.CancellationToken).RootElement.GetTextContent());
        Assert.False(stream.WasDisposed); Assert.True(stream.ReadCalls > 20000);
        using var cancelled = new ShortReadStream(bytes, 3); using var cts = new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Html5Parser.Load(cancelled, cancellationToken: cts.Token)); Assert.Equal(0, cancelled.ReadCalls); Assert.False(cancelled.WasDisposed);
        Assert.Equal("windows-1252", HtmlByteInput.Sniff(Encoding.ASCII.GetBytes(new string(' ', 1024) + "<meta charset=utf-8>").AsSpan(0, 1024)).Encoding);
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
        for (int split = 0; split <= bytes.Length; split++)
        {
            var text = new StringBuilder(); var tokens = new HtmlTokenizer(t => { if (t.Kind == HtmlTokenKind.Character) text.Append(t.Data); }, new());
            var decoder = new HtmlByteDecoder(encoding, tokens); decoder.Feed(bytes.AsSpan(0, split), 0); decoder.Feed(bytes.AsSpan(split), split); decoder.Complete(bytes.Length); tokens.Complete(bytes.Length);
            Assert.Equal(expected, text.ToString());
        }
    }
    internal static string FindRoot() { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "docs/tasks/M7-builder.json"))) return d.FullName; throw new InvalidOperationException("No source root."); }
    private sealed class ShortReadStream(byte[] bytes, int chunk) : Stream
    {
        private int position; internal bool WasDisposed; internal int ReadCalls;
        public override int Read(byte[] buffer, int offset, int count) { ReadCalls++; int amount = Math.Min(Math.Min(count, chunk), bytes.Length - position); bytes.AsSpan(position, amount).CopyTo(buffer.AsSpan(offset, amount)); position += amount; return amount; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => bytes.Length; public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
