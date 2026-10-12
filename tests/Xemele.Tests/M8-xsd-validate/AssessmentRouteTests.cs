using Xunit;
namespace Xemele.Tests.M8XsdValidate;
public sealed class AssessmentRouteTests
{
    private const string Xsi="http://www.w3.org/2001/XMLSchema-instance";
    [Theory]
    [InlineData(false,"xs:int","1",true)] [InlineData(false,"xs:int","bad",false)] [InlineData(false,"missing","1",false)]
    [InlineData(true,"xs:int","1",true)] [InlineData(true,"xs:int","bad",false)] [InlineData(true,"missing","1",false)]
    public void DeclarationFreeRootAndStrictWildcardResolveGlobalType(bool strictChild,string type,string content,bool expected)
    {
        var p=EvaluatorTests.Prepare(strictChild?"<xs:element name='r'><xs:complexType><xs:sequence><xs:any processContents='strict'/></xs:sequence></xs:complexType></xs:element>":"");
        string element=$"<u xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='{Xsi}' xsi:type='{type}'>{content}</u>";
        var result=p.Run(strictChild?"<r>"+element+"</r>":element);Assert.Equal(expected,result.IsValid);Assert.True(result.IsComplete);
        if(type=="xs:int" && content=="bad")Assert.Contains(result.Diagnostics,d=>d.Code.StartsWith("XSD.Datatype",StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(false,"",false)] [InlineData(false,"1",true)] [InlineData(true,"",false)] [InlineData(true,"1",true)]
    public void TypeOnlyNilCannotBypassSimpleContentValidation(bool laxChild,string content,bool expected)
    {
        var p=EvaluatorTests.Prepare(laxChild?"<xs:element name='r'/>":"");
        string element=$"<u xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='{Xsi}' xsi:type='xs:int' xsi:nil='true'>{content}</u>";
        var result=p.Run(laxChild?"<r>"+element+"</r>":element);Assert.Equal(expected,result.IsValid);
        Assert.DoesNotContain(result.Diagnostics,d=>d.Code=="XSD.Nil");
    }
    [Theory]
    [InlineData("",true)] [InlineData("extension",false)] [InlineData("restriction",false)]
    public void ComplexExtensionContinuesThroughSimpleUnionMemberRelation(string block,bool expected)
    {
        var p=EvaluatorTests.Prepare($"<xs:simpleType name='U'><xs:union memberTypes='xs:int xs:boolean'/></xs:simpleType><xs:complexType name='C'><xs:simpleContent><xs:extension base='xs:int'/></xs:simpleContent></xs:complexType><xs:element name='r' type='U' block='{block}'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='C'>1</r>").IsValid);
        Assert.False(p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='C'>bad</r>").IsValid);
    }
    [Theory]
    [InlineData("",true)] [InlineData("restriction",true)] [InlineData("extension",false)]
    public void ComplexExtensionIsNotSimpleRestrictionForFinal(string block,bool expected)
    {
        var p=EvaluatorTests.Prepare($"<xs:simpleType name='S' final='restriction'><xs:restriction base='xs:int'/></xs:simpleType><xs:complexType name='C'><xs:simpleContent><xs:extension base='S'/></xs:simpleContent></xs:complexType><xs:element name='r' type='S' block='{block}'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='C'>1</r>").IsValid);
    }
}
