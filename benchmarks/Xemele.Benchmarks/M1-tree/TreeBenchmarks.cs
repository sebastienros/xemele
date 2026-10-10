using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Xemele;
namespace Xemele.TreeMeasurements;
[MemoryDiagnoser, ShortRunJob]
public class InternBenchmarks
{
    [Params(8,80)] public int Length {get;set;}
    private XmlDocument document=null!;
    private string[] names=null!, probes=null!;
    [GlobalSetup] public void Setup()
    {
        document=new(new(){InternedNames=null});names=new string[64];
        for(int i=0;i<names.Length;i++){names[i]="n"+i+new string('x',Length);document.CreateElement(names[i]);}
        probes=[names[0],names[^1],"unknown"+new string('x',Length)];
        if(Baseline()!=BorrowedDictionary())throw new Exception("Intern oracle mismatch.");
    }
    [Benchmark(Baseline=true)] public int Baseline()
    {
        int sum=0;foreach(string probe in probes)foreach(string name in names)if(name.AsSpan().SequenceEqual(probe)){sum+=name.Length;break;}return sum;
    }
    [Benchmark] public int BorrowedDictionary()
    {
        int sum=0;foreach(string probe in probes)sum+=document.FindInternedName(probe.AsSpan())?.Length??0;return sum;
    }
}
[MemoryDiagnoser, ShortRunJob]
public class TreeBenchmarks
{
    private XmlNode root, memberHeavy;
    [GlobalSetup] public void Setup()
    {
        root=Build();if(Indexed()!=Linked())throw new Exception("Traversal oracle mismatch.");
        var document=new XmlDocument();memberHeavy=document.CreateElement("members");document.DocumentNode.AppendChild(memberHeavy);
        for(int i=0;i<512;i++){memberHeavy.SetAttribute("a"+i,"value");memberHeavy.SetNamespaceDeclaration("p"+i,"urn:"+i);}
        if(ImportOwnedTree().GetTextContent()!=root.GetTextContent() || CloneOwnedDocument().GetTextContent()!=root.GetTextContent() || ImportMemberHeavy().Attributes.Count!=512)throw new Exception("Copy oracle mismatch.");
    }
    internal static XmlNode Build()
    {
        var d=new XmlDocument();var root=d.CreateElement("catalog");d.DocumentNode.AppendChild(root);root.SetNamespaceDeclaration("p","urn:items");
        for(int i=0;i<64;i++){var e=d.CreateElement("item","urn:items","p");e.SetAttribute("id","value");e.AppendChild(d.CreateText("owned text"));root.AppendChild(e);}return root;
    }
    [Benchmark(Baseline=true)] public int Indexed(){int sum=0;for(int i=0;i<root.Children.Count;i++){var e=root.Children[i];sum+=e.LocalName.Length+e.Attributes[0].Value.Length+e.FirstChild.Value.Length;}return sum;}
    [Benchmark] public int Linked(){int sum=0;foreach(var e in root.Children)sum+=e.LocalName.Length+e.Attributes[0].Value.Length+e.FirstChild.Value.Length;return sum;}
    [Benchmark] public XmlNode ConstructOwnedTree()=>Build();
    [Benchmark] public XmlNode CloneOwnedDocument()=>root.OwnerDocument.DocumentNode.Clone();
    [Benchmark] public XmlNode ImportOwnedTree()=>new XmlDocument().ImportNode(root);
    [Benchmark] public XmlNode ImportMemberHeavy()=>new XmlDocument().ImportNode(memberHeavy);
}
