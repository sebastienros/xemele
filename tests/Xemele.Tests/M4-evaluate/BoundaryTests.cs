using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M4Evaluate;

public sealed class BoundaryTests
{
    private static XPathExpression Compile(string source, XPathCompileOptions? options=null)=>XPathExpression.Compile(source,options,TestContext.Current.CancellationToken);
    private static XmlDocument Parse(string source)=>XmlDocument.Parse(source,cancellationToken:TestContext.Current.CancellationToken);
    private static readonly XPathExpandedName Extension=new("urn:e","f");
    private static readonly XPathCompileOptions Extensions=new(){Namespaces=new Dictionary<string,string>{{"e","urn:e"}},Functions=[new(Extension,0,0)]};
    [Fact]
    public void StepAndFilterPredicatesExposeChangingPositionAndSize()
    {
        var document=Parse("<r><a/><b/><c/></r>");
        var calls=new List<(string Name,int Position,int Size)>();
        var options=new XPathContext{Functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{Extension,(c,_)=>{calls.Add((c.ContextItem.Name,c.Position,c.Size));return XPathValue.FromBoolean(true);}}}};
        Compile("/r/c/preceding-sibling::*[e:f()][position()=1][e:f()]",Extensions).Evaluate(document.RootElement,options);
        Assert.Equal(new[]{("b",1,2),("a",2,2),("b",1,1)},calls);calls.Clear();
        Compile("(/r/c/preceding-sibling::*)[e:f()][position()=1][e:f()]",Extensions).Evaluate(document.RootElement,options);
        Assert.Equal(new[]{("a",1,2),("b",2,2),("a",1,1)},calls);
    }
    [Theory]
    [InlineData("1 + 2 * 3")]
    [InlineData("/r/a[2]/preceding-sibling::*[1]")]
    [InlineData("(/r/a | /r/a)[last()]")]
    [InlineData("/r/a != /r/a")]
    [InlineData("/r/a > /r/a")]
    [InlineData("substring(string(/r),2,3)")]
    [InlineData("sum(/r/a)")]
    public void ExactAndOneOverSharedOperationQuota(string source)
    {
        var root=Parse("<r><a>1</a><a>2</a></r>").RootElement;var expression=Compile(source);
        var expected=expression.Evaluate(root);long low=1,high=1000000;
        while(low<high)
        {
            long middle=(low+high)/2;
            try{expression.Evaluate(root,new(){Limits=new(){MaxOperations=middle}});high=middle;}
            catch(XPathEvaluationException error){Assert.Equal("XPATH-EVALUATION-LIMIT",error.Diagnostic.Code);low=middle+1;}
        }
        var actual=expression.Evaluate(root,new(){Limits=new(){MaxOperations=low}});
        Assert.Equal(expected.Kind,actual.Kind);
        if(expected.Kind==XPathValueKind.NodeSet)Assert.Equal(expected.AsNodeSet(),actual.AsNodeSet());else Assert.Equal(expected.AsString(),actual.AsString());
        Assert.Equal("XPATH-EVALUATION-LIMIT",Assert.Throws<XPathEvaluationException>(()=>expression.Evaluate(root,new(){Limits=new(){MaxOperations=low-1}})).Diagnostic.Code);
    }
    [Fact]
    public void UnusedIncomingBindingsAreCappedAndReboundBeforeDispatch()
    {
        var root=Parse("<r/>").RootElement;var name=new XPathExpandedName("","unused");bool called=false;
        var expression=Compile("e:f()",Extensions);
        var functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{Extension,(_,_)=>{called=true;return default;}}};
        Assert.Throws<XPathEvaluationException>(()=>expression.Evaluate(root,new(){Functions=functions,Limits=new(){MaxStringCharacters=64},Variables=new Dictionary<XPathExpandedName,XPathValue>{{name,XPathValue.FromString(new string('x',65))}}}));Assert.False(called);
        var other=Parse("<r/>");
        Assert.Equal("XPATH-NODESET",Assert.Throws<XPathEvaluationException>(()=>expression.Evaluate(root,new(){Functions=functions,Variables=new Dictionary<XPathExpandedName,XPathValue>{{name,XPathValue.FromNodeSet([XPathItem.FromNode(other.RootElement)])}}})).Diagnostic.Code);Assert.False(called);
        expression.Evaluate(root,new(){Functions=functions,Limits=new(){MaxStringCharacters=64},Variables=new Dictionary<XPathExpandedName,XPathValue>{{name,XPathValue.FromString(new string('x',64))}}});Assert.True(called);
    }
    [Fact]
    public void ExtensionReturnRebindingReportsTheCallSpanAndPreservesAttributeNamespaceSnapshots()
    {
        var document=Parse("<r k='old' xmlns:p='urn:old'/>");var other=Parse("<r/>");
        var expression=Compile("1 + e:f()",Extensions);
        var error=Assert.Throws<XPathEvaluationException>(()=>expression.Evaluate(document.RootElement,new(){Functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{Extension,(_,_)=>XPathValue.FromNodeSet([XPathItem.FromNode(other.RootElement)])}}}));
        Assert.Equal(new(4,5),error.Diagnostic.Span);
        var attr=document.RootElement.Attributes[0];var namespaceItem=XPathItem.FromNamespace(document.RootElement,"p");
        var captured=XPathValue.FromNodeSet([XPathItem.FromAttribute(attr),namespaceItem]);
        var variable=new XPathExpandedName("","v");
        var result=Compile("e:f() | $v",Extensions).Evaluate(document.RootElement,new(){Variables=new Dictionary<XPathExpandedName,XPathValue>{{variable,captured}},Functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{Extension,(_,_)=>{attr.Value="new";document.RootElement.RemoveAttribute("k");document.RootElement.SetNamespaceDeclaration("p","urn:new");return captured;}}}});
        Assert.Equal(new[]{"urn:old","old"},result.AsNodeSet().Select(i=>i.StringValue));
        Assert.Equal("urn:old",namespaceItem.StringValue);
        Assert.Equal("XPATH-NODESET",Assert.Throws<XPathEvaluationException>(()=>Compile("$v").Evaluate(document.RootElement,new(){Variables=new Dictionary<XPathExpandedName,XPathValue>{{variable,result}}})).Diagnostic.Code);
    }
    [Fact]
    public void RetainedDtdIdsAndIncompleteTreesFlowThroughFacade()
    {
        var document=XmlDocument.Parse("<!DOCTYPE r [<!ATTLIST a key ID #IMPLIED>]><r><a key='x'/><a key='x'/></r>",new(){DtdProcessing=XmlDtdProcessing.Parse},TestContext.Current.CancellationToken);
        Assert.Equal(2,Compile("count(id('x x'))").Evaluate(document.RootElement).AsNumber());
        var unresolved=XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>&missing;</r>",new(){DtdProcessing=XmlDtdProcessing.Parse},TestContext.Current.CancellationToken);
        Assert.Equal("XPATH-INCOMPLETE",Assert.Throws<XPathEvaluationException>(()=>Compile(".").Evaluate(unresolved.RootElement)).Diagnostic.Code);
    }
    [Fact]
    public void CompileCapsAndStaticResolutionAreUsedByPublicFacade()
    {
        Assert.Equal(1,Compile("1",new(){Limits=new(){MaxExpressionCharacters=1,MaxTokens=1,MaxAstNodes=1,MaxDepth=1}}).Evaluate(new XmlDocument().DocumentNode).AsNumber());
        Assert.Equal("XPATH-COMPILE-LIMIT",Assert.Throws<XPathCompileException>(()=>Compile("11",new(){Limits=new(){MaxExpressionCharacters=1}})).Diagnostic.Code);
        Assert.Equal("XPATH-SYNTAX",Assert.Throws<XPathCompileException>(()=>Compile("unknown() if 1")).Diagnostic.Code);
        Assert.Equal("XPATH-FUNCTION",Assert.Throws<XPathCompileException>(()=>Compile("unknown()")).Diagnostic.Code);
        Assert.Equal("XPATH-ARITY",Assert.Throws<XPathCompileException>(()=>Compile("count()")).Diagnostic.Code);
        Assert.Equal("XPATH-PREFIX",Assert.Throws<XPathCompileException>(()=>Compile("p:x")).Diagnostic.Code);
        Assert.Throws<ArgumentException>(()=>Compile("1",new(){Namespaces=new Dictionary<string,string>{{"xmlns","urn:x"}}}));
    }
}
