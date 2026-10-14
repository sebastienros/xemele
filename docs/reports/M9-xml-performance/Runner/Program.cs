using Xemele.XmlPerformance;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Xml;
using System.Text;
using System.Diagnostics;
using System.Text.Json;
using Xemele.Streaming;
using Candidate=Xemele.XmlDocument;
using Bcl=System.Xml.XmlDocument;
if(args[0]=="fingerprint") {
 foreach(string path in args.Skip(1)) {
  var context=new VariantContext(Path.GetFullPath(path));
  var assembly=context.LoadFromAssemblyPath(Path.GetFullPath(path));
  var type=assembly.GetType("Xemele.Parsing.Syntax.XmlParserCore")!;
  Console.WriteLine(path+" "+assembly.GetCustomAttributesData().First(a=>a.AttributeType==typeof(System.Reflection.AssemblyInformationalVersionAttribute)).ConstructorArguments[0].Value);
  foreach(string name in new[]{"Append","AppendCore","ParseClose","ReadName","ConsumeNormalizedTextRun"}) {
   var method=type.GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
   Console.WriteLine(name+" "+(method?.GetMethodBody()?.GetILAsByteArray()?.Length.ToString()??"absent"));
  }
 } return;
}
if(args[0]=="name-check") { foreach(var shape in new[]{"html","xml","prefix","unicode","long"}) { new BorrowedNames{Shape=shape}.Setup(); Console.WriteLine("PASS exact names "+shape); } return; }
if(args[0]=="ownership") { BenchmarkRunner.Run<TextOwnership>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="symbols") { BenchmarkRunner.Run<BorrowedNames>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="names") { BenchmarkRunner.Run<NameCaches>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="duplicates") { BenchmarkRunner.Run<AttributeDuplicateFilters>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="parser-scans") { BenchmarkRunner.Run<ParserScans>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="scans") { BenchmarkRunner.Run<AttributeScans>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="bdn") { BenchmarkRunner.Run<DomComparison>(args:args.Skip(1).ToArray()); return; }
if(args[0]=="check") {
 foreach(var f in new[]{"feed-small","svg-small","deep-medium","entity-medium","xhtml-medium","build-medium","soap-large","configuration-large","business-large","text-large"}) {
  var w=new Workload(f);int expected=(int)w.Run("bs");
  foreach(var m in new[]{"xs","xsc","bsc"})if((int)w.Run(m)!=expected)throw new Exception(f+" "+m+" count mismatch");
  Console.WriteLine("PASS "+f+" element/text counts="+expected);
 }return;
}
if(args[0]=="paired") {
 string candidateMethod=args.Length>3?args[3]:"xb";
 string baselineMethod=candidateMethod switch{"xb"=>"bb","xc"=>"bc","xs"=>"bs","xsc"=>"bsc",_=>throw new ArgumentException("paired method must be xb, xc, xs or xsc")};
 var all=new List<object>();
 var fixtures=args.Length>4?args.Skip(4).ToArray():new[]{"feed-small","svg-small","deep-medium","entity-medium","xhtml-medium","build-medium","soap-large","configuration-large","business-large","text-large"};
 foreach(var fixture in fixtures) {
  var context=new VariantContext(Path.GetFullPath(args[1]));
  var assembly=context.LoadFromAssemblyPath(typeof(Workload).Assembly.Location);
  var type=assembly.GetType("Workload")!;
  var target=Activator.CreateInstance(type,fixture)!;
  var previous=type.GetMethod("Run")!.CreateDelegate<Func<string,object>>(target);
  // Give both variants the same loading model. Default-context BDN remains
  // the primary evidence; the original asymmetric screening overstated gains.
  var afterContext=new VariantContext(typeof(Candidate).Assembly.Location);
  var afterAssembly=afterContext.LoadFromAssemblyPath(typeof(Workload).Assembly.Location);
  var afterType=afterAssembly.GetType("Workload")!;
  var afterTarget=Activator.CreateInstance(afterType,fixture)!;
  var after=afterType.GetMethod("Run")!.CreateDelegate<Func<string,object>>(afterTarget);
  var current=new Workload(fixture);
  var methods=new Dictionary<string,Func<object>>{{"before",()=>previous(candidateMethod)},{"after",()=>after(candidateMethod)},{"bcl",()=>current.Run(baselineMethod)}};
  foreach(var method in methods.Values)for(int i=0;i<5;i++)GC.KeepAlive(method());
  var rows=methods.Keys.ToDictionary(k=>k,k=>new List<object>());
  for(int round=0;round<7;round++)foreach(var key in round%2==0?methods.Keys:methods.Keys.Reverse()){
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var method=methods[key];
   long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();int count=0;
   do{GC.KeepAlive(method());count++;}while(timer.Elapsed.TotalMilliseconds<200);
   timer.Stop();rows[key].Add(new{round,count,us=timer.Elapsed.TotalMicroseconds/count,bytes=(double)(GC.GetAllocatedBytesForCurrentThread()-allocated)/count});
  }
  all.Add(new{fixture,candidateMethod,contexts="balanced",rows});Console.WriteLine(fixture);
 }
 File.WriteAllText(args[2],JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));return;
}
if(args[0]=="quick") {
 var all=new List<object>();
 foreach(var fixture in new[]{"feed-small","svg-small","deep-medium","entity-medium","xhtml-medium","build-medium","soap-large","configuration-large","business-large","text-large"}) {
  var w=new Workload(fixture);var methods=new[]{"xb","bb"};
  foreach(var m in methods) for(int i=0;i<5;i++)GC.KeepAlive(w.Run(m));
  var rows=new Dictionary<string,List<object>>();foreach(var m in methods)rows[m]=new();
  for(int round=0;round<7;round++)foreach(var m in round%2==0?methods:methods.Reverse()){
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();long a=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();int count=0;
   do{GC.KeepAlive(w.Run(m));count++;}while(sw.Elapsed.TotalMilliseconds<200);
   sw.Stop();rows[m].Add(new{round,count,us=sw.Elapsed.TotalMicroseconds/count,bytes=(double)(GC.GetAllocatedBytesForCurrentThread()-a)/count});
  }
  all.Add(new{fixture,inputBytes=w.Bytes.Length,rows});Console.WriteLine(fixture);
 }
 File.WriteAllText(args[1],JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));return;
}
if(args[0]=="profile") { var w=new Workload(args[1]);var stop=Stopwatch.StartNew();int n=0;while(stop.Elapsed.TotalSeconds<20){GC.KeepAlive(w.Run(args[2]));n++;}Console.WriteLine(n);return; }
if(args[0]=="probe") {
 var all=new List<object>();
 foreach(var fixture in new[]{"feed-small","svg-small","soap-large","text-large"}) {
  var w=new Workload(fixture);var methods=new[]{"xb","bb","xc","bc","xs","bs","xsc","bsc"};
  foreach(var method in methods) for(int i=0;i<10;i++) GC.KeepAlive(w.Run(method));
  var rows=new Dictionary<string,List<object>>();foreach(var m in methods)rows[m]=new();
  for(int round=0;round<7;round++)foreach(var method in round%2==0?methods:methods.Reverse()){
   GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
   long a=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();int count=0;
   do{GC.KeepAlive(w.Run(method));count++;}while(sw.Elapsed.TotalMilliseconds<200);
   sw.Stop();long allocation=GC.GetAllocatedBytesForCurrentThread()-a;
   rows[method].Add(new{round,count,us=sw.Elapsed.TotalMicroseconds/count,bytes=(double)allocation/count});
  }
  all.Add(new{fixture,inputBytes=w.Bytes.Length,rows});Console.WriteLine(fixture);
 }
 File.WriteAllText(args[1],JsonSerializer.Serialize(all,new JsonSerializerOptions{WriteIndented=true}));return;
}
[MemoryDiagnoser,ShortRunJob]
public class DomComparison {
 [Params("feed-small","svg-small","deep-medium","entity-medium","xhtml-medium","build-medium","soap-large","configuration-large","business-large","text-large")]
 public string Fixture{get;set;}="feed-small";
 private Workload workload=null!;
 [GlobalSetup]public void Setup(){workload=new(Fixture);GC.KeepAlive(workload.Run("xb"));GC.KeepAlive(workload.Run("bb"));}
 [Benchmark]public object XemeleUtf8()=>workload.Run("xb");
 [Benchmark(Baseline=true)]public object BclUtf8()=>workload.Run("bb");
}
public sealed class Workload {
 public byte[] Bytes; public string Characters;
 private readonly Xemele.XmlParseOptions options=new(){DtdProcessing=Xemele.XmlDtdProcessing.Parse};
 private readonly XmlReaderSettings settings=new(){DtdProcessing=DtdProcessing.Parse,XmlResolver=null,IgnoreWhitespace=false,MaxCharactersInDocument=16777216,MaxCharactersFromEntities=16777216};
 private readonly CountHandler handler=new();
 public Workload(string fixture){Bytes=File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("XEMELE_PERF_ROOT")!,"benchmarks/baselines/release/fixtures/")+fixture+".xml");Characters=Encoding.UTF8.GetString(Bytes);}
 public object Run(string method) {
  if(method=="xb")return Candidate.Parse(Bytes.AsSpan(),options);
  if(method=="xc")return Candidate.Parse(Characters,options);
  if(method is "xs" or "xsc") {handler.Elements=handler.TextLength=0;if(method=="xs")XmlSaxParser.Parse(Bytes.AsSpan(),handler,options);else XmlSaxParser.Parse(Characters,handler,options);return handler.Elements+handler.TextLength;}
  using var source=method is "bc" or "bsc"?null:new MemoryStream(Bytes,false);
  using var chars=source is null?new StringReader(Characters):null;
  using var reader=source is null?System.Xml.XmlReader.Create(chars!,settings):System.Xml.XmlReader.Create(source,settings);
  if(method is "bs" or "bsc"){int count=0;while(reader.Read()){if(reader.NodeType==XmlNodeType.Element)count++;else if(reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)count+=reader.Value.Length;}return count;}
  var doc=new Bcl{PreserveWhitespace=true,XmlResolver=null};doc.Load(reader);return doc;
 }
 private sealed class CountHandler:XmlSaxHandler {public int Elements,TextLength;public override void StartElement(XmlStartElement element)=>Elements++;public override void Text(ReadOnlySpan<char> text)=>TextLength+=text.Length;}
}

sealed class VariantContext(string candidatePath):System.Runtime.Loader.AssemblyLoadContext
{
 protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name) => name.Name == "Xemele" ? LoadFromAssemblyPath(candidatePath) : null;
}
