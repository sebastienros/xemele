using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Streaming;

namespace Xemele.Benchmarks.Push;

[MemoryDiagnoser, InProcess, WarmupCount(1), IterationCount(3), InvocationCount(256)]
public class PushAppendBenchmarks
{
    [Params(4096, 65536)]
    public int ChunkSize
    {
        get; set;
    }
    private byte[] body = null!;
    private XmlPushParser parser = null!;
    private CountingHandler handler = null!;
    [GlobalSetup]
    public void Setup()
    {
        body = new byte[ChunkSize];
        Array.Fill(body, (byte)'x');
        handler = new();
        parser = new(handler, new()
        {
            Limits = new()
            {
                DocumentCharacters = null,
                TokenCharacters = null
            }
        });
        parser.Append("<r>"u8);
    }
    [Benchmark]
    public long AppendChunk()
    {
        parser.Append(body);
        return handler.Characters;
    }
    [GlobalCleanup] public void Cleanup() => parser.Dispose();
}
[MemoryDiagnoser, InProcess, WarmupCount(1), IterationCount(3), InvocationCount(64)]
public class PushDocumentBenchmarks
{
    private byte[] bytes = null!;
    [GlobalSetup] public void Setup() => bytes = Encoding.UTF8.GetBytes("<?xml version='1.0'?><r xmlns='urn:r' a='value'><![CDATA[β😀]]><!--comment--><?pi data?>" + new string('x', 8192) + "</r>");
    [Benchmark]
    public long EndToEnd()
    {
        var h = new CountingHandler();
        using var p = new XmlPushParser(h);
        for (var i = 0; i < bytes.Length; i += 127)
        {
            p.Append(bytes.AsSpan(i, Math.Min(127, bytes.Length - i)));
        }

        p.Complete();
        if (h.Characters != 8195 || h.Starts != 1 || h.Ends != 1 || h.Completed != 1)
        {
            throw new InvalidOperationException("Document oracle failed.");
        }

        return h.Characters;
    }
}
public sealed class CountingHandler : XmlSaxHandler
{
    public long Characters, Starts, Ends, Completed;
    public override void Text(ReadOnlySpan<char> text) => Characters += text.Length;
    public override void StartElement(XmlStartElement element) => Starts++;
    public override void EndElement(XmlEndElement element) => Ends++;
    public override void EndDocument() => Completed++;
}
