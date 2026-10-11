using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Xemele.Html;
namespace Xemele.Benchmarks.LegacyHtml;
[MemoryDiagnoser,ShortRunJob]
public class LegacyScanBenchmarks
{
 [Params("short-hit","short-miss","long-hit","long-miss","dense","multilingual")]public string Case{get;set;}="";
 private string input="";
 private static readonly SearchValues<char> Stops=SearchValues.Create("<&\r\n\0");
 [GlobalSetup]public void Setup()=>input=Case switch{"short-hit"=>"abc&","short-miss"=>"abc","long-hit"=>new string('x',8192)+"<","long-miss"=>new string('x',8192),"dense"=>"a&b<c\nd\re\0f",_=>"尾巴😀"+new string('x',4096)+"&"};
 [Benchmark(Baseline=true)]public int Loop()=>Loop(input);
 [Benchmark]public int CachedSearch()=>input.AsSpan().IndexOfAny(Stops);
 internal static int Loop(ReadOnlySpan<char> source){for(int i=0;i<source.Length;i++)if(source[i] is '<' or '&' or '\r' or '\n' or '\0')return i;return -1;}
}
[MemoryDiagnoser,ShortRunJob]
public class LegacyNameBenchmarks
{
 [Params("p","data-name","unknown-maximum-length-name")]public string Input{get;set;}="";
 private readonly LegacyNamePool pool=new(100);
 private readonly Dictionary<string,string> baseline=new(StringComparer.Ordinal);
 [GlobalSetup]public void Setup(){pool.Get(Input);baseline.Add(Input,Input);}
 [Benchmark(Baseline=true)]public string AllocatedLookup(){string name=new(Input.AsSpan());return baseline[name];}
 [Benchmark]public string BorrowedLookup()=>pool.Get(Input.AsSpan());
}
[MemoryDiagnoser,ShortRunJob]
public class LegacyCompositionBenchmarks
{
 [Params("repeated","distinct","multilingual","malformed")]public string Case{get;set;}="";
 private string input="";private byte[] bytes=[];
 [GlobalSetup]public void Setup(){input=Workload(Case);bytes=Encoding.UTF8.GetBytes(input);}
 internal static string Workload(string name)=>name switch{"repeated"=>string.Concat(Enumerable.Repeat("<p class=same>owned text&amp;</p>",32)),"distinct"=>string.Concat(Enumerable.Range(0,32).Select(i=>"<x"+i+" data-"+i+"=value>owned</x"+i+">")),"multilingual"=>"<meta charset=utf-8>"+string.Concat(Enumerable.Repeat("<p>尾巴😀&NotEqualTilde;</p>",32)),_=>"<table><tr><td>a<td>b<tr><th>c</table><p>one<p>two<!--bad--!>"};
 [Benchmark(Baseline=true)]public HtmlDocument Characters()=>HtmlParser.Parse(input);
 [Benchmark]public HtmlDocument Bytes()=>HtmlParser.Parse(bytes);
 [Benchmark]public HtmlDocument ChunkStream(){using var stream=new SmallStream(bytes);return HtmlParser.Load(stream);}
 private sealed class SmallStream(byte[] bytes):MemoryStream(bytes){public override int Read(Span<byte> b)=>base.Read(b[..Math.Min(b.Length,17)]);}
}
