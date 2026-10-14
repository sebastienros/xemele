using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xemele;
using Xemele.Streaming;
using Xemele.Fuzz.Streaming;
using Reader = Xemele.Streaming.XmlReader;

if (args is ["fuzz-control"])
{
    var seeds = new[] { "<r/>", "<r>α😀<![CDATA[x]]></r>", "<!DOCTYPE r [<!ATTLIST r a CDATA 'v'>]><r/>", "<r a='1' a='2'/>", "<r>" };
    int operations = 0; List<object> failures = [];
    async Task Run(byte[] bytes, int seed) { operations++; try { await StreamingFuzz.Exercise(bytes, seed); } catch (Exception e) { failures.Add(new { inputBase64 = Convert.ToBase64String(bytes), seed, error = e.ToString() }); } }
    foreach (string seed in seeds)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(seed);
        await Run(bytes, 1771);
        for (int i = 0; i < bytes.Length; i++)
        { var mutated = (byte[])bytes.Clone(); mutated[i] ^= 0x80; await Run(mutated, i); }
    }
    Console.WriteLine(JsonSerializer.Serialize(new { operations, seeds = seeds.Length, failures })); if (failures.Count != 0) Environment.ExitCode = 1; return;
}
if (args is not [var mode, var sizeText, var report] || mode is not ("sax" or "sax-async" or "pull" or "pull-async" or "push" or "negative")) throw new ArgumentException("mode bytes report");
long size = long.Parse(sizeText, System.Globalization.CultureInfo.InvariantCulture);
if (size < 8192 || size > 1073741824) throw new ArgumentOutOfRangeException(nameof(size));
var options = new XmlParseOptions { Limits = new() { DocumentCharacters = null, ExpandedCharacters = null, EvaluationWork = null, Depth = 64, TokenCharacters = 65536 } };
using var source = new GeneratedXml(size);
var sink = new Counter(source);
HeapMeter.Controls();
long baseline = HeapMeter.Read(), allocated = GC.GetTotalAllocatedBytes(true);
var watch = Stopwatch.StartNew();
object? parser = null;
if (mode is "sax" or "sax-async")
{ if (mode == "sax") XmlSaxParser.Parse(source, sink, options); else await XmlSaxParser.ParseAsync(source, sink, options); }
else if (mode is "pull" or "pull-async")
{
    using var reader = Reader.Create(source, options); parser = reader;
    while (mode == "pull" ? reader.Read() : await reader.ReadAsync())
    {
        switch (reader.TokenKind)
        {
            case XmlTokenKind.StartElement: sink.Start(); break;
            case XmlTokenKind.EndElement: sink.Ends++; break;
            case XmlTokenKind.Text: sink.AcceptText(reader.ValueSpan.Length); break;
            case XmlTokenKind.EndDocument: sink.EndDocument(); break;
        }
    }
}
else
{
    using var push = new XmlPushParser(sink, options); parser = push;
    byte[] buffer = new byte[4096]; int n;
    while ((n = source.Read(buffer)) != 0) push.Append(buffer.AsSpan(0, n));
    push.Complete();
}
watch.Stop(); GC.KeepAlive(parser);
long allocationBytes = GC.GetTotalAllocatedBytes(true) - allocated;
if (sink.Starts != source.BlockCount + 1 || sink.Ends != sink.Starts || sink.Completed != 1 || sink.Characters != source.ExpectedText || !sink.Early) throw new InvalidDataException("Generated semantic/early callback oracle failed.");
// A retained document-sized positive control must violate the exact same absolute live-heap oracle.
byte[]? retainedControl = mode == "negative" ? new byte[9 * 1024 * 1024] : null;
if (retainedControl is not null) sink.Sample();
long peak = sink.LiveSamples.Max();
bool within = peak <= 8 * 1024 * 1024;
var output = new { mode, inputBytes = size, blockBytes = 4096, depth = 2, vocabulary = new[] { "r", "e", "a" }, maximumTokenCharacters = 4089,
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    assemblySha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(XmlPushParser).Assembly.Location))),
    runnerSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Counter).Assembly.Location))),
    baselineManagedLiveBytes = baseline, conservativeMaximumWholeHeapLiveBytes = peak, parserLiveUpperBoundBytes = peak,
    limitBytes = 8 * 1024 * 1024, withinLimit = within, liveSamples = sink.LiveSamples, allocationBytes,
    peakProcessWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64 is > 0 and var rss ? (long?)rss : null, rssUnavailable = Process.GetCurrentProcess().PeakWorkingSet64 <= 0, elapsedSeconds = watch.Elapsed.TotalSeconds,
    sink.Starts, sink.Ends, sink.Characters, sink.Completed, earlyCallback = sink.Early, source.BytesRead,
    source.EofRead, sourceBytesExcluded = false, sinkBytesExcluded = false };
File.WriteAllText(report, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(File.ReadAllText(report));
GC.KeepAlive(retainedControl);
if (within == (mode == "negative")) throw new InvalidDataException("Live memory oracle failed.");

sealed class Counter(GeneratedXml source) : XmlSaxHandler
{
    public long Characters, Starts, Ends, Completed;
    public bool Early;
    public readonly List<long> LiveSamples = new(66);
    private long nextSample = 16 * 1024 * 1024;
    public void Start() { Starts++; if (Starts == 1) Early = source.BytesRead < source.Size && !source.EofRead; }
    public override void StartElement(XmlStartElement e) => Start();
    public override void EndElement(XmlEndElement e) => Ends++;
    public override void EndDocument() { Completed++; Sample(); }
    public override void Text(ReadOnlySpan<char> text) => AcceptText(text.Length);
    public void AcceptText(int length)
    {
        Characters += length;
        if (source.BytesRead >= nextSample) { Sample(); nextSample += 16 * 1024 * 1024; }
    }
    public void Sample() => LiveSamples.Add(HeapMeter.Read());
}
sealed class GeneratedXml : Stream
{
    private readonly byte[] block = new byte[4096];
    public long Size { get; }
    public long BlockCount { get; }
    public long ExpectedText { get; }
    public long BytesRead { get; private set; }
    public bool EofRead { get; private set; }
    public GeneratedXml(long size)
    {
        Size = size; BlockCount = (size - 7) / 4096;
        Array.Fill(block, (byte)'x'); "<e a='v'>"u8.CopyTo(block); "</e>"u8.CopyTo(block.AsSpan(4092));
        ExpectedText = BlockCount * (4096 - 13) + size - 7 - BlockCount * 4096;
    }
    public override int Read(Span<byte> destination)
    {
        int n = (int)Math.Min(Math.Min(destination.Length, 4096), Size - BytesRead);
        if (n == 0) EofRead = true;
        for (int i = 0; i < n; i++)
        {
            long p = BytesRead + i;
            destination[i] = p < 3 ? "<r>"u8[(int)p] : p >= Size - 4 ? "</r>"u8[(int)(p - Size + 4)] : p < 3 + BlockCount * 4096 ? block[(int)((p - 3) % 4096)] : (byte)'x';
        }
        BytesRead += n; return n;
    }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); if (BytesRead % (1024 * 1024) == 0) await Task.Yield(); return Read(buffer.Span); }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => Size;
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

static class HeapMeter
{
    private static object? held;
    internal static long Read()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true); GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var info = GC.GetGCMemoryInfo(); return info.HeapSizeBytes - info.FragmentedBytes;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Hold(int size) { var bytes = new byte[size]; bytes[0] = 1; bytes[^1] = 2; held = bytes; }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Release() { GC.KeepAlive(held); held = null; }
    internal static void Controls()
    {
        foreach (int size in new[] { 32768, 9437184 })
        {
            Read(); Hold(size); long live = Read(); Release(); long released = Read();
            if (Math.Abs((live - released) - (size + 24L)) > 1024) throw new InvalidDataException("Compacted heap control failed.");
        }
    }
}
