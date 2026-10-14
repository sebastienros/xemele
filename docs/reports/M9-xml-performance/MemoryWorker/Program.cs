using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Xemele.XPath;
using Candidate = Xemele.XmlDocument;
using Bcl = System.Xml.XmlDocument;

if(args[0]=="layout") {
 var f=args[1];var bytes=File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("XEMELE_PERF_ROOT")!,"benchmarks/baselines/release/fixtures/")+f+".xml");
 object doc=Parse(args[2],bytes);
 var methods=typeof(RuntimeHelpers).GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Where(x=>x.Name.Contains("Object")||x.Name.Contains("Size")).Select(x=>x.ToString()).ToArray();
 Console.WriteLine(JsonSerializer.Serialize(methods));
 GraphSize(doc);
 if(args[2]!="xemele")return;
 foreach(var field in typeof(Candidate).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)) {
  var value=field.GetValue(doc);if(value==null)continue;
  if(field.Name is "Nodes" or "AttributeSlots" or "NamespaceSlots" or "nodeLocations" or "attributeLocations" or "namespaceLocations") {
   var data=(Array)value.GetType().GetField("data",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;
   var count=(int)value.GetType().GetProperty("Count",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;
   var sizeMethod=typeof(System.Runtime.CompilerServices.Unsafe).GetMethod("SizeOf")!.MakeGenericMethod(data.GetType().GetElementType()!);
   int size=(int)sizeMethod.Invoke(null,null)!;
   var arrays=new HashSet<Array>(ReferenceEqualityComparer.Instance){data};
   var pages=(Array)value.GetType().GetField("pages",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;
   foreach(var page in pages) if(page is Array array)arrays.Add(array);
   Console.WriteLine(JsonSerializer.Serialize(new{name=field.Name,count,capacity=arrays.Sum(a=>a.Length),slotBytes=size,
    arrayBytes=arrays.Sum(a=>24L+(long)a.Length*size)+24L+8L*pages.Length}));
  }
 }
 return;
}
if (args.Length is not (4 or 5) || args[0] is not ("xemele" or "bcl") || args.Length == 5 && args[4] != "qualify") throw new ArgumentException("xemele|bcl fixture manifest output [qualify]");
string engine = args[0]; byte[] input = File.ReadAllBytes(args[1]);
using var manifest = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var fixture = manifest.RootElement.GetProperty("fixtures").EnumerateArray().Single(r => Path.GetFileName(r.GetProperty("path").GetString()) == Path.GetFileName(args[1]));
if (Convert.ToHexStringLower(SHA256.HashData(input)) != fixture.GetProperty("sha256").GetString()) throw new InvalidDataException("Fixture hash differs.");
string expected = fixture.GetProperty("semantic").GetProperty("sha256").GetString()!;
string expression = fixture.GetProperty("xpath").GetString()!;
double expectedXPath = fixture.GetProperty("xpathExpected").GetDouble();

if (args.Length == 5)
{
    object value = Parse(engine, input);
    string digest = Verify(engine, value, expected, expression, expectedXPath, true);
    File.WriteAllText(args[3], JsonSerializer.Serialize(new { engine, qualification = true, digest, expectedXPath }));
    GC.KeepAlive(value); return;
}
// DOM retention warms only parsing and its consumed common-model digest.
Warm(engine, input, expected, expression, expectedXPath);
Collect();
var controls=MeasureHeapControls();
long controlLive = MeasureControl();
if (controlLive < 9 * 1024 * 1024) throw new InvalidDataException("Retained-memory meter missed the 9 MiB control.");
Collect();
var rows = new List<object>();
for (int cycle = 0; cycle < 3; cycle++)
{
    Collect(); long baseline = GC.GetTotalMemory(true), allocationStart = GC.GetAllocatedBytesForCurrentThread();
    CreateHeld(engine, input);
    long allocationBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
    Collect(); long heldLiveBytes = GC.GetTotalMemory(false);
    var heldInfo=GC.GetGCMemoryInfo(); long heldHeap=heldInfo.HeapSizeBytes-heldInfo.FragmentedBytes;
    string digest = VerifyHeld(engine, expected, expression, expectedXPath);
    ClearHeld(); Collect();
    long releasedLiveBytes = GC.GetTotalMemory(false), retainedBytes = heldLiveBytes - releasedLiveBytes;
    if (retainedBytes <= 0 || allocationBytes <= 0) throw new InvalidDataException($"Invalid counters: baseline={baseline}, held={heldLiveBytes}, released={releasedLiveBytes}, allocated={allocationBytes}.");
    var releasedInfo=GC.GetGCMemoryInfo(); long releasedHeap=releasedInfo.HeapSizeBytes-releasedInfo.FragmentedBytes;
    long releasedDelta = releasedLiveBytes - baseline;
    rows.Add(new { cycle, retainedBytes, allocationBytes, heldHeap, releasedHeap, graphEstimate=heldHeap-releasedHeap, baseline, heldLiveBytes, releasedLiveBytes, releasedDelta, digest });
}
var output = new { engine, fixture = fixture.GetProperty("id").GetString(), inputBytes = input.Length,
    runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    os = RuntimeInformation.OSDescription, productionSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Candidate).Assembly.Location))),
    workerSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Meter).Assembly.Location))), qualificationExecuted = false,
    controlLive, controls, rows, expectedXPath, expectedSemanticSha256 = expected, controlledTimingCredit = false };
File.WriteAllText(args[3], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { engine, fixture = fixture.GetProperty("id").GetString(), controlLive, rows }));

[MethodImpl(MethodImplOptions.NoInlining)]
static object RetainedControl() { byte[] value = new byte[9 * 1024 * 1024]; value[0] = 42; value[^1] = 43; return value; }
[MethodImpl(MethodImplOptions.NoInlining)]
static long MeasureControl() { CreateControlHeld(); long held = GC.GetTotalMemory(true); ClearHeld(); Collect(); return held - GC.GetTotalMemory(true); }
[MethodImpl(MethodImplOptions.NoInlining)]
static void CreateControlHeld() => Meter.Held = RetainedControl();
[MethodImpl(MethodImplOptions.NoInlining)]
static void CreateSizedControl(int bytes) { var array=new byte[bytes];array[0]=42;array[^1]=43;Meter.Held=array; }
static object[] MeasureHeapControls() {
 var results=new List<object>();
 foreach(int bytes in new[]{32768,9*1024*1024}) {
  Collect();CreateSizedControl(bytes);Collect();var held=GC.GetGCMemoryInfo();long heldTotal=GC.GetTotalMemory(false);
  ClearHeld();Collect();var released=GC.GetGCMemoryInfo();long releasedTotal=GC.GetTotalMemory(false);
  long graph=held.HeapSizeBytes-held.FragmentedBytes-released.HeapSizeBytes+released.FragmentedBytes;
  if(Math.Abs(graph-(bytes+24))>1024)throw new Exception("Heap control invalid");
  results.Add(new{bytes,graph,totalMemoryDelta=heldTotal-releasedTotal});
 }return results.ToArray();
}
[MethodImpl(MethodImplOptions.NoInlining)]
static void Warm(string engine, byte[] input, string expected, string expression, double number)
{ object value = Parse(engine, input); Verify(engine, value, expected, expression, number); GC.KeepAlive(value); }
[MethodImpl(MethodImplOptions.NoInlining)]
static void CreateHeld(string engine, byte[] input) => Meter.Held = Parse(engine, input);
[MethodImpl(MethodImplOptions.NoInlining)]
static string VerifyHeld(string engine, string expected, string expression, double number) => Verify(engine, Meter.Held!, expected, expression, number);
[MethodImpl(MethodImplOptions.NoInlining)]
static void ClearHeld() { GC.KeepAlive(Meter.Held); Meter.Held = null; }
static void Collect() { GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true); GC.WaitForPendingFinalizers(); GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true); }
[MethodImpl(MethodImplOptions.NoInlining)]
static object Parse(string engine, byte[] input)
{
    if (engine == "xemele") return Candidate.Parse(input, new() { DtdProcessing = Xemele.XmlDtdProcessing.Parse });
    var document = new Bcl { PreserveWhitespace = true, XmlResolver = null };
    using var stream = new MemoryStream(input, writable: false);
    using var reader = System.Xml.XmlReader.Create(stream, new XmlReaderSettings
    { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, IgnoreWhitespace = false, MaxCharactersInDocument = 16777216, MaxCharactersFromEntities = 16777216 });
    document.Load(reader); return document;
}
static string Verify(string engine, object value, string expected, string expression, double expectedXPath, bool qualify = false)
{
    string immediate = engine == "xemele" ? Semantic.Candidate((Candidate)value) : Semantic.Bcl(((Bcl)value).DocumentElement!);
    if (immediate != expected) throw new InvalidDataException($"{engine} semantic digest differs: {immediate} != {expected}");
    if (!qualify) return immediate;
    string digest;
    if (engine == "xemele")
    {
        var document = (Candidate)value; digest = Semantic.Candidate(document.RootElement);
        var context = new XPathContext { Limits = new() { MaxInputItems = 1000000, MaxNodeSetItems = 1000000, MaxOperations = 100000000, MaxInputCharacters = 134217728, MaxStringCharacters = 16777216 } };
        if (XPathExpression.Compile(expression).Evaluate(document.DocumentNode, context).AsNumber() != expectedXPath) throw new InvalidDataException("Candidate XPath oracle differs.");
        using var bytes = new MemoryStream(); document.Save(bytes);
        if (Semantic.Candidate(Candidate.Parse(bytes.ToArray(), new() { DtdProcessing = Xemele.XmlDtdProcessing.Parse })) != expected)
            throw new InvalidDataException("Candidate serialization roundtrip differs.");
    }
    else
    {
        var document = (Bcl)value; digest = Semantic.Bcl(document.DocumentElement!);
        if ((double)document.CreateNavigator()!.Evaluate(expression) != expectedXPath) throw new InvalidDataException("BCL XPath oracle differs.");
        using var bytes = new MemoryStream();
        using (var writer = System.Xml.XmlWriter.Create(bytes, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false, Indent = false })) document.Save(writer);
        var loaded = (Bcl)Parse("bcl", bytes.ToArray());
        if (Semantic.Bcl(loaded.DocumentElement!) != expected) throw new InvalidDataException("BCL serialization roundtrip differs.");
    }
    if (digest != expected) throw new InvalidDataException($"{engine} semantic digest differs: {digest} != {expected}");
    return digest;
}


static void GraphSize(object root) {
 var rawSize=typeof(RuntimeHelpers).GetMethod("GetRawObjectDataSize",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
 var visited=new HashSet<object>(ReferenceEqualityComparer.Instance);var stack=new Stack<object>();stack.Push(root);
 var byType=new Dictionary<string,(long Bytes,int Count)>();long bytes=0;
 var fields=new Dictionary<Type,System.Reflection.FieldInfo[]>();
 while(stack.TryPop(out object? value)) {
  if(!visited.Add(value))continue;var type=value.GetType();
  ulong raw=(ulong)(nuint)rawSize.Invoke(null,[value])!;
  long size=(long)((raw+16+7)&~7UL);bytes+=size;
  string name=type.FullName!;var previous=byType.GetValueOrDefault(name);byType[name]=(previous.Bytes+size,previous.Count+1);
  if(value is string)continue;
  if(value is Array array){
   var element=type.GetElementType()!;
   if(element.IsPrimitive||element.IsEnum)continue;
   foreach(var item in array){if(item==null)continue;if(element.IsValueType)WalkFields(item,element);else stack.Push(item);}
  }else WalkFields(value,type);
 }
 Console.WriteLine(JsonSerializer.Serialize(new{graphBytes=bytes,objects=visited.Count,types=byType.OrderByDescending(x=>x.Value.Bytes).Take(15).Select(x=>new{type=x.Key,bytes=x.Value.Bytes,count=x.Value.Count})}));
 void WalkFields(object value,Type type) {
  if(!fields.TryGetValue(type,out var members)){
   var list=new List<System.Reflection.FieldInfo>();for(Type? t=type;t!=null;t=t.BaseType)list.AddRange(t.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly));
   members=list.ToArray();fields[type]=members;
  }
  foreach(var field in members){var ft=field.FieldType;if(ft.IsPrimitive||ft.IsEnum||ft.IsPointer)continue;
   var child=field.GetValue(value);if(child==null)continue;
   if(ft.IsValueType)WalkFields(child,ft);else stack.Push(child);
  }
 }
}

static class Meter { internal static object? Held; }
static class Semantic
{
    internal static string Candidate(Candidate document) => Candidate(document.RootElement);
    internal static string Candidate(Xemele.XmlNode node)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Visit(node, hash); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static string Bcl(System.Xml.XmlNode node)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Visit(node, hash); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static void Frame(IncrementalHash hash, char kind, params string[] values)
    {
        hash.AppendData([(byte)kind]);
        Span<byte> length = stackalloc byte[4];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes);
        }
    }
    private static void Visit(Xemele.XmlNode node, IncrementalHash hash)
    {
        if (node.Kind == Xemele.XmlNodeKind.Comment) { Frame(hash, 'C', node.Value); return; }
        if (node.Kind == Xemele.XmlNodeKind.ProcessingInstruction) { Frame(hash, 'P', node.LocalName, node.Value); return; }
        Frame(hash, 'S', node.NamespaceUri, node.LocalName);
        var attributes = new List<(string Uri, string Local, string Value)>();
        foreach (var a in node.Attributes) attributes.Add((a.NamespaceUri, a.LocalName, a.Value));
        foreach (var a in attributes.OrderBy(a => a.Uri, StringComparer.Ordinal).ThenBy(a => a.Local, StringComparer.Ordinal)) Frame(hash, 'A', a.Uri, a.Local, a.Value);
        var text = new StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.Kind is Xemele.XmlNodeKind.Text or Xemele.XmlNodeKind.CData) text.Append(child.Value);
            else { Flush(); Visit(child, hash); }
        }
        Flush(); Frame(hash, 'E');
        void Flush() { if (text.Length != 0) { Frame(hash, 'T', text.ToString()); text.Clear(); } }
    }
    private static void Visit(System.Xml.XmlNode node, IncrementalHash hash)
    {
        if (node.NodeType == XmlNodeType.Comment) { Frame(hash, 'C', node.Value!); return; }
        if (node.NodeType == XmlNodeType.ProcessingInstruction) { Frame(hash, 'P', node.LocalName, node.Value!); return; }
        Frame(hash, 'S', node.NamespaceURI, node.LocalName);
        if (node.Attributes is { } attributes)
            foreach (XmlAttribute a in attributes.Cast<XmlAttribute>().Where(a => a.Name != "xmlns" && a.Prefix != "xmlns").OrderBy(a => a.NamespaceURI, StringComparer.Ordinal).ThenBy(a => a.LocalName, StringComparer.Ordinal)) Frame(hash, 'A', a.NamespaceURI, a.LocalName, a.Value);
        var text = new StringBuilder();
        foreach (System.Xml.XmlNode child in node.ChildNodes)
        {
            if (child.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) text.Append(child.Value);
            else { Flush(); Visit(child, hash); }
        }
        Flush(); Frame(hash, 'E');
        void Flush() { if (text.Length != 0) { Frame(hash, 'T', text.ToString()); text.Clear(); } }
    }
}
