using System.Text.Json;
using Xemele.Parsing.Input;
using Xemele.Tests.EncodingCompatibility;

if (args.Length != 1 || args[0] is not ("original" or "equivalent" or "helper")) throw new ArgumentException("original|equivalent|helper");
string[] labels = ["US-ASCII","ISO-8859-1","Windows-1252"];
var cancellation = CancellationToken.None;
if (args[0] == "helper")
{
    Console.WriteLine(JsonSerializer.Serialize(new { mode=args[0], runtime=Environment.Version.ToString(), labels=EncodingAllocationProbe.Run(labels,Selected,cancellation,false) }));
    return;
}
var profiles = new List<object>();
foreach (string label in labels)
{
    long setupBefore = GC.GetAllocatedBytesForCurrentThread();
    using var d=Selected(label);
    byte[] input=label=="Windows-1252" ? [0x80,0x97,0xE9,13,10] : label=="ISO-8859-1" ? [0x80,0xE9,13,10] : "abcd\r\n"u8.ToArray();
    char[] output=new char[8];long[] maps=new long[8];
    string expected=label=="Windows-1252" ? "€—é\r\n" : label=="ISO-8859-1" ? "\u0080é\r\n" : "abcd\r\n";
    long setup=GC.GetAllocatedBytesForCurrentThread()-setupBefore;
    long warmBefore=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<10000;i++)
    {
        var r=d.Decode(input,output,maps,false,cancellation);
        if(args[0]=="equivalent") Check(r,input,output,maps,expected,4L+(long)i*input.Length);
    }
    long warm=GC.GetAllocatedBytesForCurrentThread()-warmBefore;
    long before=GC.GetAllocatedBytesForCurrentThread();
    for(int i=0;i<10000;i++)
    {
        var r=d.Decode(input,output,maps,false,cancellation);
        if(args[0]=="equivalent") Check(r,input,output,maps,expected,4L+(10000L+i)*input.Length);
    }
    long measured=GC.GetAllocatedBytesForCurrentThread()-before;
    if(!output.AsSpan(0,expected.Length).SequenceEqual(expected) || maps[expected.Length-1]!=3L+20000L*input.Length) throw new InvalidOperationException("Final progress");
    profiles.Add(new { label,encoding=d.Encoding.ToString(),coldSetupBytes=setup,warmBytes=warm,allocatedBytes=measured,warmCalls=10000,measuredCalls=10000,inputLength=input.Length,lastOffset=maps[expected.Length-1],scalars=expected });
}
Console.WriteLine(JsonSerializer.Serialize(new { mode=args[0],runtime=Environment.Version.ToString(),labels=profiles,scope="Decode only; declaration selection outside measurements; default GC/JIT; original counters include caller; anomalies retained without inferred cause" }));

static XmlInputDecoder Selected(string label)
{
    var decoder=new XmlInputDecoder(new());
    Span<char> output=stackalloc char[4];Span<long> maps=stackalloc long[4];
    if(decoder.Decode("abcd"u8,output,maps,false,CancellationToken.None).Step!=DecodeStep.NeedInput || decoder.ValidateDeclaration(label,new(null,1,1,0)) is not null) throw new InvalidOperationException("Selection");
    return decoder;
}
static void Check(DecodeResult r,byte[] input,char[] output,long[] maps,string expected,long offset)
{
    if(r.Step!=DecodeStep.NeedInput || r.Diagnostic is not null || r.ConsumedBytes!=input.Length || r.WrittenCharacters!=expected.Length || !output.AsSpan(0,r.WrittenCharacters).SequenceEqual(expected)) throw new InvalidOperationException("Progress");
    for(int j=0;j<r.WrittenCharacters;j++) if(maps[j]!=offset+j) throw new InvalidOperationException("Offsets");
}
