using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele;
using Xemele.Parsing.Input;
using Xemele.Internal.Operations;
[MemoryDiagnoser, ShortRunJob]
public class EncodingBenchmarks
{
    [Params("US-ASCII","ISO-8859-1","Windows-1252")]public string Label {get;set;}="US-ASCII";
    [Params(8,4096)]public int Length {get;set;}
    private byte[] input=null!;private char[] output=null!;private long[] maps=null!;
    private XmlInputDecoder decoder=null!;private ScalarLocation baseline=null!;private long accepted;
    [GlobalSetup]public void Setup()
    {
        input=new byte[Length];output=new char[Length];maps=new long[Length];
        for(int i=0;i<Length;i++)input[i]=i%17==0?(byte)13:i%17==1?(byte)10:Label=="Windows-1252"?(byte)0x80:Label=="ISO-8859-1"?(byte)0xE9:(byte)'a';
        decoder=new(new());decoder.Decode("abcd"u8,output,maps,false,default);
        if(decoder.ValidateDeclaration(Label,default)!=null)throw new Exception();
        baseline=new(null);for(int i=0;i<4;i++)baseline.TryAdvance(new Rune('a'));accepted=4;
        long expected=Straightforward();char[] chars=(char[])output.Clone();long[] offsets=(long[])maps.Clone();
        if(BoundedDecoder()!=expected||!output.SequenceEqual(chars)||!maps.SequenceEqual(offsets))throw new Exception("Oracle mismatch");
    }
    [Benchmark(Baseline=true)]public long Straightforward()
    {
        for(int i=0;i<input.Length;i++)
        {
            CancellationToken.None.ThrowIfCancellationRequested();
            int value=input[i];if(Label=="Windows-1252"&&value==0x80)value=0x20AC;
            if(accepted==long.MaxValue||!baseline.TryAdvance(new Rune(value)))throw new Exception();
            output[i]=(char)value;maps[i]=accepted++;
        }
        return maps[^1]+output.Length;
    }
    [Benchmark]public long BoundedDecoder()
    {
        var r=decoder.Decode(input,output,maps,false,default);
        if(r.Step!=DecodeStep.NeedInput||r.WrittenCharacters!=input.Length)throw new Exception();return maps[^1]+r.WrittenCharacters;
    }
}
[MemoryDiagnoser, ShortRunJob]
public class EncodingDomBenchmarks
{
    private byte[] input=null!;
    [GlobalSetup]public void Setup()
    {
        input=[.."<?xml version='1.0' encoding='Windows-1252'?><r>"u8,..Enumerable.Repeat((byte)0x80,1024),.."</r>"u8];
        if(XmlDocument.Parse(input).RootElement.GetTextContent()!=new string('€',1024))throw new Exception();
    }
    [Benchmark]public object ParseLegacyDom()=>XmlDocument.Parse(input);
}
