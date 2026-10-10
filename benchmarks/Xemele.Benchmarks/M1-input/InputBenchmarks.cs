using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Input;
using Xemele.Internal.Operations;

[MemoryDiagnoser]
public class InputBenchmarks
{
    [Params("short", "long", "multilingual")] public string Corpus { get; set; } = "short";
    private byte[] bytes = null!;
    private char[] chars = null!;
    private long[] offsets = null!;
    private XmlInputDecoder decoder = null!;
    private ScalarLocation baselineLocation = null!;
    private long baselineBytes;
    [GlobalSetup]
    public void Setup()
    {
        string text = Corpus switch { "long" => new string('a',4096), "multilingual" => string.Concat(Enumerable.Repeat("é😀\r\n中",128)), _ => "<r/>" };
        bytes=Encoding.UTF8.GetBytes(text);chars=new char[text.Length+4];offsets=new long[chars.Length];
        decoder=new(new(){Limits=new(){ExternalBytes=null}});
        // Steady mid-source encoding already selected; baseline starts identically.
        decoder.Decode("abcd"u8,chars,offsets,false,default);
        baselineLocation=new(null);for(int i=0;i<4;i++)baselineLocation.TryAdvance(new Rune('a'));baselineBytes=4;
    }
    [Benchmark(Baseline=true)]
    public long StraightforwardStrictUtf8()
    {
        int consumed=0,written=0;
        while(consumed<bytes.Length)
        {
            CancellationToken.None.ThrowIfCancellationRequested();
            if(Rune.DecodeFromUtf8(bytes.AsSpan(consumed),out var scalar,out int used)!=OperationStatus.Done)throw new Exception();
            if(used>long.MaxValue-baselineBytes || !baselineLocation.TryAdvance(scalar))throw new Exception();
            int units=scalar.EncodeToUtf16(chars.AsSpan(written));offsets[written]=baselineBytes;
            if(units==2)offsets[written+1]=baselineBytes;
            written+=units;consumed+=used;baselineBytes+=used;
        }
        return offsets[written-1]+written;
    }
    [Benchmark]
    public long BoundedDecoder()
    {
        var r=decoder.Decode(bytes,chars,offsets,false,default);
        if(r.Step!=DecodeStep.NeedInput)throw new Exception();return offsets[r.WrittenCharacters-1]+r.WrittenCharacters;
    }
}
[MemoryDiagnoser]
public class InputOwnershipBenchmarks
{
    private readonly char[] chars=new char[8];private readonly long[] offsets=new long[8];
    [Benchmark] public object Setup() => new XmlInputDecoder(new());
    [Benchmark] public object MalformedDiagnostic()
    {
        using var decoder=new XmlInputDecoder(new());
        return decoder.Decode([0xFF,0x41,0x41,0x41],chars,offsets,true,default).Diagnostic!;
    }
}
