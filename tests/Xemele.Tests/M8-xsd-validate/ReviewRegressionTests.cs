using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdValidate;
public sealed class ReviewRegressionTests
{
    private const string Xsi="http://www.w3.org/2001/XMLSchema-instance";
    [Theory]
    [InlineData("lax","bad",false)] [InlineData("lax","1",true)] [InlineData("skip","bad",true)]
    public void UndeclaredLaxDescendantsRemainAssessed(string policy,string value,bool expected)
    {
        var p=EvaluatorTests.Prepare($"<xs:element name='known' type='xs:int'/><xs:element name='root'><xs:complexType><xs:sequence><xs:any processContents='{policy}'/></xs:sequence></xs:complexType></xs:element>");
        var hook=new EvaluatorTests.RecordingFactory();Assert.Equal(expected,p.Run($"<root><unknown><known>{value}</known></unknown></root>",factory:hook).IsValid);
        Assert.Equal(new[] { "+root","+unknown","+known","-known","-unknown","-root","complete" },hook.Events);
    }
    [Theory]
    [InlineData("bad",false)] [InlineData("1",true)]
    public void UndeclaredLaxXsiTypeIsAssessed(string value,bool expected)
    {
        var p=EvaluatorTests.Prepare("<xs:element name='root'/>");
        Assert.Equal(expected,p.Run($"<root><unknown xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='{Xsi}' xsi:type='xs:int'>{value}</unknown></root>").IsValid);
    }
    [Theory]
    [InlineData(false,"false",false)] [InlineData(false,"0",false)] [InlineData(true,"false",true)] [InlineData(true,"0",true)]
    public void NonNillableProhibitsNilPresence(bool nillable,string lexical,bool expected)
    { Assert.Equal(expected,EvaluatorTests.Prepare($"<xs:element name='r' type='xs:int' nillable='{nillable.ToString().ToLowerInvariant()}'/>").Run($"<r xmlns:xsi='{Xsi}' xsi:nil='{lexical}'>1</r>").IsValid); }
    [Theory]
    [InlineData("xs:int","1",true)] [InlineData("xs:boolean","true",true)] [InlineData("xs:string","text",false)]
    public void UnionMemberTypeRoutes(string type,string text,bool expected)
    {
        var p=EvaluatorTests.Prepare("<xs:simpleType name='U'><xs:union memberTypes='xs:int xs:boolean'/></xs:simpleType><xs:element name='r' type='U'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='{Xsi}' xsi:type='{type}'>{text}</r>").IsValid);
    }
    [Theory]
    [InlineData("L","",true)] [InlineData("L","restriction",false)] [InlineData("U","",true)] [InlineData("U","restriction",false)]
    public void SimpleUrTypeRestrictionBlocksListAndUnion(string type,string block,bool expected)
    {
        var p=EvaluatorTests.Prepare($"<xs:simpleType name='L'><xs:list itemType='xs:int'/></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='xs:int xs:boolean'/></xs:simpleType><xs:element name='r' type='xs:anySimpleType' block='{block}'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='{type}'>1</r>").IsValid);
    }
    [Theory]
    [InlineData("1",true)] [InlineData("2",false)]
    public void FixedValueUsesDeclarationType(string text,bool expected)
    {
        var p=EvaluatorTests.Prepare("<xs:simpleType name='Small'><xs:restriction base='xs:int'><xs:pattern value='[0-9]'/></xs:restriction></xs:simpleType><xs:element name='r' type='xs:int' fixed='01'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='Small'>{text}</r>").IsValid);
    }
    [Fact]
    public void DynamicDefaultRequiresCanonicalDeclaredValue()
    {
        var p=EvaluatorTests.Prepare("<xs:simpleType name='Small'><xs:restriction base='xs:int'><xs:pattern value='[0-9]'/></xs:restriction></xs:simpleType><xs:element name='r' type='xs:int' default='01'/>");
        Assert.True(p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='Small'/>").IsValid);
    }
    [Fact]
    public void CanonicalDecimalDefaultDoesNotBecomeIntegerLexical()
    {
        // Declared decimal's canonical lexical value is 1.0. Although its value
        // equals integer one, that lexical form is invalid for selected xs:int.
        var p=EvaluatorTests.Prepare("<xs:element name='r' type='xs:decimal' default='1'/>");
        Assert.False(p.Run($"<r xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='{Xsi}' xsi:type='xs:int'/>").IsValid);
    }
    [Theory]
    [InlineData("default")] [InlineData("fixed")]
    public void EmptySelectedTypeRejectsValueConstraint(string constraint)
    {
        var p=EvaluatorTests.Prepare($"<xs:complexType name='Empty'/><xs:element name='r' {constraint}='abc'/>");
        Assert.True(p.Run("<r/>").IsValid);Assert.False(p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='Empty'/>").IsValid);
    }
    [Theory]
    [InlineData("default","<c/>",true)] [InlineData("default","",false)] [InlineData("fixed","<c/>",false)]
    public void UnusedDefaultDoesNotRejectElementOnlyContent(string constraint,string content,bool expected)
    {
        var p=EvaluatorTests.Prepare($"<xs:complexType name='E'><xs:sequence><xs:element name='c'/></xs:sequence></xs:complexType><xs:element name='r' {constraint}='abc'/>");
        Assert.Equal(expected,p.Run($"<r xmlns:xsi='{Xsi}' xsi:type='E'>{content}</r>").IsValid);
    }
    [Theory]
    [InlineData("strict",false)] [InlineData("lax",false)] [InlineData("skip",true)]
    public void TwoAssessedWildcardIdsAreInvalid(string policy,bool expected)
    { Assert.Equal(expected,WildcardId(policy,false).Run("<r a='one' b='two'/>").IsValid); }
    [Theory]
    [InlineData("strict",false)] [InlineData("lax",false)] [InlineData("skip",true)]
    public void OptionalDeclaredIdUseConflictsWithWildcardId(string policy,bool expected)
    { Assert.Equal(expected,WildcardId(policy,true).Run("<r a='one'/>").IsValid); }
    [Theory]
    [InlineData("strict")] [InlineData("lax")]
    public void LoneWildcardIdIsValid(string policy)=>Assert.True(WildcardId(policy,false).Run("<r a='one'/>").IsValid);
    private static EvaluatorTests.Prepared WildcardId(string policy,bool declared)=>EvaluatorTests.Prepare($"<xs:attribute name='a' type='xs:ID'/><xs:attribute name='b' type='xs:ID'/><xs:element name='r'><xs:complexType>{(declared?"<xs:attribute name='declared' type='xs:ID'/>":"")}<xs:anyAttribute processContents='{policy}'/></xs:complexType></xs:element>");
    [Fact]
    public void LocationListItemsAndRealUniqueEquality()
    {
        var p=EvaluatorTests.Prepare($"<xs:element name='root' xmlns:xsi='{Xsi}'><xs:complexType><xs:sequence><xs:element name='row' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:unique name='U'><xs:selector xpath='row'/><xs:field xpath='@xsi:schemaLocation'/></xs:unique></xs:element>");
        string One(string value)=>$"<row xmlns:xsi='{Xsi}' xsi:schemaLocation='{value}'/>";
        var hook=new EvaluatorTests.RecordingFactory();Assert.True(p.Run("<root>"+One("urn:a https://example.invalid/a")+"</root>",factory:hook).IsValid);
        Assert.Equal(2,Assert.Single(hook.Attributes).Value!.Items!.Count);
        Assert.True(p.Run("<root>"+One("urn:a https://example.invalid/a")+"</root>").IsValid);
        Assert.False(p.Run("<root>"+One("urn:a https://example.invalid/a")+One("urn:a https://example.invalid/a")+"</root>").IsValid);
        Assert.False(p.Run("<root>"+One("urn:a")+"</root>").IsValid);
    }
    [Theory]
    [InlineData("abcd",true)] [InlineData("abcde",false)] [InlineData("ab<![CDATA[cd]]>",true)] [InlineData("ab<![CDATA[cde]]>",false)]
    [InlineData("𐀀𐀀𐀀𐀀",true)] [InlineData("𐀀𐀀𐀀𐀀𐀀",false)]
    public void AccumulatedSimpleScalarTokenLimit(string text,bool expected)
    {
        var result=EvaluatorTests.Prepare("<xs:element name='r' type='xs:string'/>").Run("<r>"+text+"</r>",new() { Limits=new() { TokenCharacters=4 } });
        Assert.Equal(expected,result.IsValid);Assert.Equal(expected,result.IsComplete);
    }
}
