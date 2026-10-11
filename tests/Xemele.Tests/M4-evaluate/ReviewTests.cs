using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M4Evaluate;
public sealed class ReviewTests
{
    public static IEnumerable<object[]> NodeBooleanRelations()
    {
        foreach (var (op,apply) in new (string,Func<double,double,bool>)[]{("<",(a,b)=>a<b),("<=",(a,b)=>a<=b),(">",(a,b)=>a>b),(">=",(a,b)=>a>=b)})
            foreach(var (path,number) in new[]{("/r/missing",0d),("/r/zero",1d),("/r/one",1d),("/r/text",1d)})
                foreach(var (boolean,b) in new[]{("false()",0d),("true()",1d)})
                {
                    yield return new object[]{path+" "+op+" "+boolean,apply(number,b)};
                    yield return new object[]{boolean+" "+op+" "+path,apply(b,number)};
                }
    }
    [Theory,MemberData(nameof(NodeBooleanRelations))]
    public void AllRelationalOperatorsConvertWholeNodeSetsWhenOtherOperandIsBoolean(string source,bool expected)
    {
        const string xml="<r><zero>0</zero><one>1</one><text>nonnumeric</text></r>";
        var document=XmlDocument.Parse(xml,cancellationToken:TestContext.Current.CancellationToken);
        var actual=XPathExpression.Compile(source,cancellationToken:TestContext.Current.CancellationToken).Evaluate(document.RootElement).AsBoolean();
        var reference=new System.Xml.XPath.XPathDocument(new StringReader(xml)).CreateNavigator();
        Assert.Equal(expected,actual);
        Assert.Equal(expected,(bool)reference.Evaluate(source));
    }
    [Fact]
    public void DetachedContextRejectsBeforeDocumentInputLimitsOrUserDispatch()
    {
        var document=XmlDocument.Parse("<r><a/><a/><a/></r>",cancellationToken:TestContext.Current.CancellationToken);
        var detached=document.CreateElement("detached");bool called=false;
        var name=new XPathExpandedName("urn:e","f");
        var expression=XPathExpression.Compile("e:f()",new(){Namespaces=new Dictionary<string,string>{{"e","urn:e"}},Functions=[new(name,0,0)]},TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(()=>expression.Evaluate(detached,new(){Limits=new(){MaxInputItems=1},Functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{name,(_,_)=>{called=true;return default;}}}}));
        Assert.False(called);
    }
}
