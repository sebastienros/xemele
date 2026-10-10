using System.Runtime.CompilerServices;
using System.Text.Json;
using Xemele;
using Xemele.Tests.Tree;

if (args.Length != 1 || args[0] is not ("original" or "equivalent" or "helper")) throw new ArgumentException("Known diagnostic mode required.");
var d=new XmlDocument();var e=d.CreateElement("e","urn:e","p");var a=e.SetAttribute("a","v");e.SetNamespaceDeclaration("p","urn:e");
for(int i=0;i<10;i++)e.AppendChild(d.CreateText("x"));
long Walk(){long sum=0;for(int i=0;i<1000;i++){foreach(var n in e.Children)sum+=n.Value.Length;foreach(var x in e.Attributes)sum+=x.Value.Length;foreach(var ns in e.NamespaceDeclarations)sum+=ns.Value.Length;sum+=d.Intern("urn:e".AsSpan()).Length;}return sum;}
long warmConsumed, measuredConsumed, primitiveBytes;
object? escaped=null;
if(args[0]=="original")
{
    warmConsumed=Walk();long before=GC.GetAllocatedBytesForCurrentThread();measuredConsumed=Walk();primitiveBytes=GC.GetAllocatedBytesForCurrentThread()-before;
}
else if(args[0]=="equivalent")
{
    var warm=Equivalent(d,e);warmConsumed=warm.Consumed;
    var measured=Equivalent(d,e);measuredConsumed=measured.Consumed;primitiveBytes=measured.Bytes;
}
else
{
    var warm=TreeAllocationProbe.Walk(d,e,false,ref escaped);warmConsumed=warm.Consumed;
    var measured=TreeAllocationProbe.Walk(d,e,false,ref escaped);measuredConsumed=measured.Consumed;primitiveBytes=measured.Bytes;
}
bool attributeIdentity=a==e.Attributes[0];
if(warmConsumed!=21000 || measuredConsumed!=21000 || !attributeIdentity)throw new InvalidOperationException("Work mismatch.");
Console.WriteLine(JsonSerializer.Serialize(new{mode=args[0],runtime=Environment.Version.ToString(),warmIterations=1000,measuredIterations=1000,warmConsumed,measuredConsumed,primitiveBytes,attributeIdentity}));

[MethodImpl(MethodImplOptions.NoInlining)]
static (long Bytes,long Consumed) Equivalent(XmlDocument d,XmlNode e)
{
    long before=GC.GetAllocatedBytesForCurrentThread();long sum=0;
    for(int i=0;i<1000;i++)
    {
        foreach(var n in e.Children)sum+=n.Value.Length;
        foreach(var x in e.Attributes)sum+=x.Value.Length;
        foreach(var ns in e.NamespaceDeclarations)sum+=ns.Value.Length;
        sum+=d.Intern("urn:e".AsSpan()).Length;
    }
    return(GC.GetAllocatedBytesForCurrentThread()-before,sum);
}
