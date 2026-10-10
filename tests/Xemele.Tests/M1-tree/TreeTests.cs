using System.Reflection;
using Xunit;
namespace Xemele.Tests.Tree;
public class TreeTests
{
    [Fact]
    public void DefaultHandlesAndMissingNavigation()
    {
        XmlNode node = default; XmlAttribute a = default; XmlNamespaceDeclaration n = default;
        Assert.True(node.IsDefault); Assert.Equal(default, node); Assert.Equal(0, node.GetHashCode());
        Assert.True(a.IsDefault); Assert.Equal(default, a); Assert.Equal(0, a.GetHashCode());
        Assert.True(n.IsDefault); Assert.Equal(default, n); Assert.Equal(0, n.GetHashCode());
        foreach (var property in typeof(XmlNode).GetProperties().Where(p => p.Name != "IsDefault"))
            Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => property.GetValue(node)).InnerException);
        foreach (Type type in new[] {typeof(XmlAttribute),typeof(XmlNamespaceDeclaration)})
            foreach (var property in type.GetProperties().Where(p => p.Name != "IsDefault"))
                Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => property.GetValue(Activator.CreateInstance(type))).InnerException);
        Assert.Throws<InvalidOperationException>(() => node.AppendChild(default));
        Assert.Throws<InvalidOperationException>(() => node.Value = "x");
        Assert.Throws<InvalidOperationException>(() => a.Value = "x");
        Assert.Throws<InvalidOperationException>(() => n.Value = "x");
        var doc = new XmlDocument(); Assert.True(doc.RootElement.IsDefault); Assert.False(doc.DocumentNode.IsDefault);
        var e = doc.CreateElement("root");
        Assert.True(e.Parent.IsDefault && e.FirstChild.IsDefault && e.LastChild.IsDefault && e.PreviousSibling.IsDefault && e.NextSibling.IsDefault);
        Assert.Equal(0, e.Children.Count); Assert.Throws<ArgumentOutOfRangeException>(() => e.Children[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => e.Attributes[0]); Assert.Throws<ArgumentOutOfRangeException>(() => e.NamespaceDeclarations[0]);
        Assert.Throws<InvalidOperationException>(() => default(XmlNodeChildren).Count);
    }
    [Fact]
    public void StableGrowthIdentityDetachedAndOrderedMoves()
    {
        var d = new XmlDocument(); var root = d.CreateElement("root"); d.DocumentNode.AppendChild(root);
        var a = d.CreateText("a"); var b = d.CreateText("b"); var c = d.CreateText("c");
        root.AppendChild(a); root.AppendChild(b); root.InsertBefore(c,b);
        Assert.Equal("acb", root.GetTextContent()); Assert.Equal(a, c.PreviousSibling); Assert.Equal(b, c.NextSibling);
        for (int i=0;i<4000;i++) d.CreateElement("extra");
        Assert.Equal(root,d.RootElement); Assert.Equal(a,root.Children[0]);
        root.InsertBefore(b,a); Assert.Equal("bac",root.GetTextContent());
        long v=d.MutationVersion; root.InsertBefore(a,a); Assert.Equal(v,d.MutationVersion);
        a.Remove(); a.Remove(); Assert.Equal("a",a.Value); Assert.True(a.Parent.IsDefault); Assert.Same(d,a.OwnerDocument);
        root.AppendChild(a); Assert.Equal("bca",root.GetTextContent());
        Assert.NotEqual(a,new XmlDocument().CreateText("a")); Assert.NotEqual(a,a.Clone());
    }
    [Fact]
    public void AtomicInsertionValidationAndFragmentTransfer()
    {
        var d=new XmlDocument(); var root=d.CreateElement("root"); d.DocumentNode.AppendChild(root);
        var child=d.CreateElement("child"); root.AppendChild(child);
        Assert.Throws<ArgumentException>(()=>child.AppendChild(root));
        Assert.Throws<ArgumentException>(()=>root.AppendChild(new XmlDocument().CreateText("x")));
        Assert.Throws<ArgumentException>(()=>root.AppendChild(d.DocumentNode));
        Assert.Throws<InvalidOperationException>(()=>d.DocumentNode.Remove());
        Assert.Throws<ArgumentException>(()=>root.InsertBefore(child,d.CreateText("foreign-reference")));
        Assert.Throws<InvalidOperationException>(()=>root.InsertBefore(child,default));
        var f=d.CreateFragment(); var comment=d.CreateComment("valid"); f.AppendChild(comment); f.AppendChild(d.CreateElement("second"));
        long v=d.MutationVersion;
        Assert.Throws<ArgumentException>(()=>d.DocumentNode.AppendChild(f));
        Assert.Equal(v,d.MutationVersion); Assert.Equal(2,f.Children.Count); Assert.Equal(f,comment.Parent);
        root.InsertBefore(f,child); Assert.Equal(0,f.Children.Count); Assert.Equal(comment,root.FirstChild);
        var bad=d.CreateFragment(); bad.AppendChild(d.CreateComment("ok")); bad.AppendChild(d.CreateText("bad"));
        v=d.MutationVersion; Assert.Throws<ArgumentException>(()=>d.DocumentNode.AppendChild(bad)); Assert.Equal(v,d.MutationVersion); Assert.Equal(2,bad.Children.Count);
        var empty=d.CreateFragment(); root.AppendChild(empty); Assert.True(empty.Parent.IsDefault);
        Assert.Throws<ArgumentException>(()=>empty.AppendChild(empty));
        Assert.Throws<InvalidOperationException>(()=>comment.AppendChild(child));
    }
    [Fact]
    public void AttributesAndDeclarationsHaveIndependentOrderedIdentity()
    {
        var d=new XmlDocument(); var e=d.CreateElement("e");
        var a=e.SetAttribute("name","one","urn:n","p"); var b=e.SetAttribute("name","two");
        var n=e.SetNamespaceDeclaration("p","urn:n");
        long v=d.MutationVersion; Assert.Equal(a,e.SetAttribute("name","three","urn:n","q")); Assert.Equal(v,d.MutationVersion);
        Assert.Equal("q",a.Prefix); Assert.Equal("three",e.GetAttribute("name","urn:n")); Assert.Equal("two",e.GetAttribute("name"));
        Assert.Equal(2,e.Attributes.Count); Assert.Equal(1,e.NamespaceDeclarations.Count); Assert.Equal(a,e.Attributes[0]);
        a.Remove(); Assert.False(a.IsAttached); Assert.Equal(e,a.OwnerElement); a.Value="detached"; Assert.Equal("detached",a.Value);
        var again=e.SetAttribute("name","four","urn:n","p"); Assert.NotEqual(a,again); Assert.Equal(b,e.Attributes[0]); Assert.Equal(again,e.Attributes[1]);
        Assert.True(e.RemoveAttribute("name")); Assert.False(e.RemoveAttribute("name")); Assert.Null(e.GetAttribute("missing"));
        Assert.Equal(n,e.SetNamespaceDeclaration("p","urn:new")); Assert.Equal("urn:new",n.Value);
        n.Remove(); n.Value="urn:detached"; Assert.False(n.IsAttached); Assert.Equal(e,n.OwnerElement);
        Assert.NotEqual(n,e.SetNamespaceDeclaration("p","urn:again"));
        Assert.True(e.RemoveNamespaceDeclaration("p")); Assert.False(e.RemoveNamespaceDeclaration("p"));
        var text=d.CreateText("x"); Assert.Equal(0,text.Attributes.Count); Assert.Equal(0,text.NamespaceDeclarations.Count); Assert.Null(text.GetAttribute("x"));
        Assert.Throws<InvalidOperationException>(()=>text.SetAttribute("x","y")); Assert.Throws<InvalidOperationException>(()=>text.RemoveAttribute("x"));
        Assert.Throws<InvalidOperationException>(()=>text.SetNamespaceDeclaration("p","u")); Assert.Throws<InvalidOperationException>(()=>text.RemoveNamespaceDeclaration("p"));
    }
    [Fact]
    public void XmlNamesScalarsAndReservedBindingsAreValidatedBeforeMutation()
    {
        var d=new XmlDocument(); var e=d.CreateElement("根𐀀","urn:任意","前");
        foreach(string name in new[]{"","1bad","a:b","a b","\uD800","\u037E","\U000F0000"}) Assert.Throws<ArgumentException>(()=>d.CreateElement(name));
        foreach(string value in new[]{"\0","\u0001","\uD800","\uDC00","\uFFFE","\uFFFF"})
        {
            Assert.Throws<ArgumentException>(()=>d.CreateText(value)); Assert.Throws<ArgumentException>(()=>e.SetAttribute("bad",value));
        }
        Assert.Equal("\t\r\n😀",d.CreateText("\t\r\n😀").Value);
        Assert.Throws<ArgumentException>(()=>d.CreateComment("a--b")); Assert.Throws<ArgumentException>(()=>d.CreateComment("end-"));
        Assert.Throws<ArgumentException>(()=>d.CreateCData("]] >".Replace(" ",""))); Assert.Throws<ArgumentException>(()=>d.CreateProcessingInstruction("XML",""));
        Assert.Throws<ArgumentException>(()=>d.CreateProcessingInstruction("valid","?>")); Assert.Equal("a:b",d.CreateProcessingInstruction("a:b","").LocalName);
        foreach(var binding in new[]{("p",""),("xml","urn:wrong"),("p",TreeValidation.XmlUri),("xmlns","urn:x"),("p",TreeValidation.XmlnsUri)})
            Assert.Throws<ArgumentException>(()=>e.SetNamespaceDeclaration(binding.Item1,binding.Item2));
        e.SetNamespaceDeclaration("xml",TreeValidation.XmlUri); e.SetNamespaceDeclaration("","");
        Assert.Throws<ArgumentException>(()=>d.CreateElement("xmlns")); Assert.Throws<ArgumentException>(()=>d.CreateElement("e","","p"));
        var a=e.SetAttribute("a","old"); var ns=e.SetNamespaceDeclaration("p","urn:p"); long v=d.MutationVersion;
        Assert.Throws<ArgumentException>(()=>a.Value="\0"); Assert.Throws<ArgumentException>(()=>ns.Value=""); Assert.Equal("old",a.Value); Assert.Equal("urn:p",ns.Value); Assert.Equal(v,d.MutationVersion);
        Assert.Throws<ArgumentNullException>(()=>d.CreateText(null!)); Assert.Throws<ArgumentNullException>(()=>d.CreateElement(null!));
        Assert.Throws<InvalidOperationException>(()=>e.Value="");
    }
    [Fact]
    public void AllEnumeratorsTrackGlobalStructuralVersionButAllowValueChanges()
    {
        var d=new XmlDocument();var e=d.CreateElement("e");var text=d.CreateText("x");e.AppendChild(text); var a=e.SetAttribute("a","v");var n=e.SetNamespaceDeclaration("p","urn:p");
        var c=e.Children.GetEnumerator();var attrs=e.Attributes.GetEnumerator();var ns=e.NamespaceDeclarations.GetEnumerator();
        Assert.Throws<InvalidOperationException>(()=>c.Current); Assert.True(c.MoveNext()); Assert.True(attrs.MoveNext()); Assert.True(ns.MoveNext());
        text.Value="y"; a.Value="new"; n.Value="urn:new"; e.SetAttribute("a","again",""," ".Trim()); d.CreateElement("detached");
        Assert.Equal(text,c.Current); Assert.Equal(a,attrs.Current); Assert.Equal(n,ns.Current);
        var unrelated=d.CreateElement("unrelated"); unrelated.SetAttribute("other","v");
        Assert.Throws<InvalidOperationException>(()=>c.Current); Assert.Throws<InvalidOperationException>(()=>attrs.Current); Assert.Throws<InvalidOperationException>(()=>ns.Current);
        Assert.Throws<InvalidOperationException>(()=>c.MoveNext()); Assert.Throws<InvalidOperationException>(()=>attrs.MoveNext()); Assert.Throws<InvalidOperationException>(()=>ns.MoveNext());
        c=e.Children.GetEnumerator(); Assert.True(c.MoveNext());Assert.False(c.MoveNext());Assert.False(c.MoveNext());Assert.Throws<InvalidOperationException>(()=>c.Current);
        Assert.Throws<InvalidOperationException>(()=>default(XmlAttributes.Enumerator).MoveNext());
        foreach(Action structural in new Action[]{()=>text.Remove(),()=>a.Remove(),()=>n.Remove(),()=>e.AppendChild(text),()=>e.SetAttribute("new","v"),()=>e.SetNamespaceDeclaration("q","urn:q")})
        {
            c=e.Children.GetEnumerator();attrs=e.Attributes.GetEnumerator();ns=e.NamespaceDeclarations.GetEnumerator();structural();
            Assert.Throws<InvalidOperationException>(()=>c.MoveNext()); Assert.Throws<InvalidOperationException>(()=>attrs.MoveNext()); Assert.Throws<InvalidOperationException>(()=>ns.MoveNext());
        }
    }
    [Fact]
    public void QuotasAndVersionOverflowRejectWithoutObservableChange()
    {
        var d=new XmlDocument(new(){InternedNames=2,Depth=1,DocumentCharacters=1,TokenCharacters=1,AttributesPerElement=1});
        var e=d.CreateElement("e"); d.CreateElement("e");
        Assert.Throws<InvalidOperationException>(()=>d.CreateElement("new","urn:needs-two"));
        var a=e.SetAttribute("a","long value"); Assert.Equal("long value",a.Value);
        Assert.Throws<InvalidOperationException>(()=>e.SetAttribute("b","x")); Assert.Equal(1,e.Attributes.Count);
        Assert.Throws<ArgumentException>(()=>e.SetAttribute("a","changed","","newprefix")); Assert.Equal("long value",a.Value);
        var limited=new XmlDocument(new(){InternedNames=4}); var le=limited.CreateElement("e"); var la=le.SetAttribute("a","old","urn:a","p");
        Assert.Throws<InvalidOperationException>(()=>le.SetAttribute("a","changed","urn:a","q")); Assert.Equal("old",la.Value); Assert.Equal("p",la.Prefix);
        d.CreateText(new string('x',10000));
        var unlimited=new XmlDocument(new(){InternedNames=null});for(int i=0;i<100;i++)unlimited.CreateElement("n"+i);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XmlDocument(new(){InternedNames=0}));Assert.Throws<ArgumentNullException>(()=>new XmlDocument(null!));
        var other=new XmlDocument();var child=other.CreateElement("e");other.DocumentNode.AppendChild(child);
        typeof(XmlDocument).GetProperty("MutationVersion",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(other,long.MaxValue);
        Assert.Throws<InvalidOperationException>(()=>child.Remove());Assert.Equal(other.DocumentNode,child.Parent);
        Assert.Throws<InvalidOperationException>(()=>child.SetAttribute("a","v"));Assert.Equal(0,child.Attributes.Count);
        child.Value.ToString(); var text=other.CreateText("x"); text.Value="allowed";Assert.Equal("allowed",text.Value);
        Assert.Throws<InvalidOperationException>(()=>child.AppendChild(text));Assert.True(text.Parent.IsDefault);
    }
    [Fact]
    public void CloneImportAndTextAreIterativeAndRetainAllKindsAndMembers()
    {
        var d=new XmlDocument(new(){Depth=1});var root=d.CreateElement("root");d.DocumentNode.AppendChild(root);
        root.SetAttribute("a","v","urn:a","p");root.SetNamespaceDeclaration("p","urn:a");
        var current=root;for(int i=0;i<12000;i++){var child=d.CreateElement("e");current.AppendChild(child);current=child;}
        current.AppendChild(d.CreateText("a"));current.AppendChild(d.CreateCData("b"));current.AppendChild(d.CreateComment("ignored"));current.AppendChild(d.CreateProcessingInstruction("pi","ignored"));
        Assert.Equal("ab",root.GetTextContent());var shallow=root.Clone(false); Assert.Equal(0,shallow.Children.Count);Assert.Equal("v",shallow.GetAttribute("a","urn:a"));Assert.NotEqual(root.Attributes[0],shallow.Attributes[0]);
        var clone=root.Clone();Assert.Equal("ab",clone.GetTextContent());Assert.NotEqual(root,clone);Assert.Same(d,clone.OwnerDocument);
        var dest=new XmlDocument();var imported=dest.ImportNode(root);Assert.Equal("ab",imported.GetTextContent());Assert.Same(dest,imported.OwnerDocument);Assert.True(imported.Parent.IsDefault);
        Assert.Throws<ArgumentException>(()=>dest.ImportNode(d.DocumentNode));
        var document=d.DocumentNode.Clone();Assert.NotSame(d,document.OwnerDocument);Assert.Equal("ab",document.GetTextContent());
        var quota=new XmlDocument(new(){InternedNames=1});long version=quota.MutationVersion;Assert.Throws<InvalidOperationException>(()=>quota.ImportNode(root));Assert.Equal(version,quota.MutationVersion);quota.CreateElement("different");
        root.SetTextContent("replacement");Assert.Equal("replacement",root.GetTextContent());Assert.True(current.Parent.IsDefault==false);Assert.True(clone.Parent.IsDefault);
        root.SetTextContent("");Assert.Equal(0,root.Children.Count);Assert.Throws<InvalidOperationException>(()=>d.DocumentNode.SetTextContent("x"));
        var comment=d.CreateComment("ok");Assert.Equal("ok",comment.GetTextContent());Assert.Throws<ArgumentException>(()=>comment.SetTextContent("--"));
    }
    [Fact]
    public void WarmTraversalAndRepeatedNameInternProbesAllocateNothing()
    {
        var d=new XmlDocument();var e=d.CreateElement("e","urn:e","p");var a=e.SetAttribute("a","v");e.SetNamespaceDeclaration("p","urn:e");
        for(int i=0;i<10;i++)e.AppendChild(d.CreateText("x"));
        long Walk(){long sum=0;for(int i=0;i<1000;i++){foreach(var n in e.Children)sum+=n.Value.Length;foreach(var x in e.Attributes)sum+=x.Value.Length;foreach(var ns in e.NamespaceDeclarations)sum+=ns.Value.Length;sum+=d.Intern("urn:e".AsSpan()).Length;}return sum;}
        Walk();long before=GC.GetAllocatedBytesForCurrentThread();long consumed=Walk();long delta=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(21000,consumed);Assert.Equal(0,delta);Assert.Equal(a,e.Attributes[0]);
    }
    [Fact]
    public void HandlesKeepTheirOwnerAlive()
    {
        static (XmlAttribute,WeakReference) Make(){var d=new XmlDocument();var e=d.CreateElement("e");return(e.SetAttribute("a","v"),new WeakReference(d));}
        var (a,weak)=Make();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Assert.True(weak.IsAlive);Assert.Equal("v",a.Value);GC.KeepAlive(a);
    }
    [Fact]
    public void BorrowedInternLookupMatchesOrdinalOracleAcrossCulturesAndUnknowns()
    {
        var d=new XmlDocument(new(){InternedNames=null});
        string[] names=["a","ab","abc","XML","xml","é","根","𐀀",new string('l',200),"I","İ"];
        foreach(var name in names)d.CreateElement(name);
        var prior=System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture=new("tr-TR");
            foreach(string probe in names.Concat(new[]{"","unknown","AB","i","a\0","\uD800",new string('l',201)}))
            {
                string? expected=probe.Length==0?"":names.FirstOrDefault(n=>n.AsSpan().SequenceEqual(probe));
                char[] buffer=("_"+probe+"_").ToCharArray();
                Assert.Equal(expected,d.FindInternedName(buffer.AsSpan(1,probe.Length)));
            }
            var backing="_abc_".ToCharArray();Assert.Equal("abc",d.FindInternedName(backing.AsSpan(1,3)));backing[1]='z';Assert.Equal("abc",d.FindInternedName("abc"));
        }
        finally {System.Globalization.CultureInfo.CurrentCulture=prior;}
    }
}
