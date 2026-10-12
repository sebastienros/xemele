using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdIdentity;

public sealed class XPathTests
{
    [Theory]
    [InlineData("row")][InlineData("./row")][InlineData(".//row")]
    [InlineData("row/.")][InlineData("././row/.")][InlineData("* | row")]
    [InlineData("child::row")][InlineData(". / child::row / .")]
    [InlineData(" . // row ")][InlineData("child :: row")][InlineData("descendant-or-self::node()/child::row")][InlineData("row/self::node()")]
    public void SelectorPathsAreUnionSets(string selector)
    { Assert.False(IdentityTests.Run(IdentityTests.Simple(IdentityTests.Constraint(selector:selector)),"<root><row id='a'/><row id='a'/></root>").Complete().IsValid); }
    [Theory]
    [InlineData("@id")][InlineData("./@id")][InlineData(".//@id")]
    [InlineData("attribute::id")][InlineData("./attribute::id")][InlineData("@* | @id")]
    public void AttributePathsUseEffectiveAttributeEvents(string field)
    { Assert.False(IdentityTests.Run(IdentityTests.Simple(IdentityTests.Constraint(field:field)),"<root><row id='a'/><row id='a'/></root>").Complete().IsValid); }
    [Theory]
    [InlineData("row", "@id")][InlineData("p:row", "@p:id")]
    [InlineData("p:*", "@p:*")][InlineData("*", "@*")]
    public void NamespaceTestsUseDefinitionContext(string selector,string field)
    {
        var schema = IdentityTests.Simple(IdentityTests.Constraint(selector:selector,field:field));
        string ns=selector.Contains('p')?" xmlns='urn:p' xmlns:z='urn:p'":" xmlns='urn:other'";
        string id=field.Contains('p')?"z:id":"id";
        var result=IdentityTests.Run(schema,$"<root><row{ns} {id}='a'/><row{ns} {id}='a'/></root>").Complete();
        Assert.Equal(selector=="row",result.IsValid); // Unprefixed names do not adopt the instance default namespace.
    }
    [Fact]
    public void CompilerRetainsSelectorAndFieldLocalPrefixRebindings()
    {
        var schema=IdentityTests.Simple("<xs:key name='K'><xs:selector xmlns:p='urn:rows' xpath='p:row'/><xs:field xmlns:p='urn:attrs' xpath='@p:id'/></xs:key>");
        Assert.False(IdentityTests.Run(schema,"<root xmlns:r='urn:rows' xmlns:a='urn:attrs'><r:row a:id='x'/><r:row a:id='x'/></root>").Complete().IsValid);
    }
    [Theory]
    [InlineData("")][InlineData(" ")][InlineData("/row")][InlineData("row//a")]
    [InlineData("..")][InlineData("row[1]")][InlineData("row[@id]")][InlineData("text()")]
    [InlineData("@id")][InlineData("attribute::id")][InlineData("row/@id")]
    [InlineData("row|")][InlineData("|row")][InlineData("row||a")][InlineData("row/")]
    [InlineData("p:row:bad")][InlineData("unknown:row")][InlineData("1row")]
    [InlineData("row + a")][InlineData("row/parent::a")][InlineData("row/ancestor::a")]
    [InlineData("row/foo()")][InlineData("row/$x")][InlineData("row/@*")]
    [InlineData("row/namespace::*")][InlineData(". / / row")][InlineData("row/self::other()")]
    public void InvalidSelectorGrammarIsRejectedBeforePublication(string selector)
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());
        schema=schema with { Identities=[schema.Identities[0] with { Selector=selector }] };
        var op=new SchemaCompileOperation();
        Assert.Throws<SchemaCompileException>(()=>new XsdIdentityCompiler().Compile(schema,op));
        Assert.Contains(op.Diagnostics,d=>d.Code=="XSD.Identity");
    }
    [Theory]
    [InlineData("@id/a")][InlineData("@id/.")][InlineData("row//@id")][InlineData("@id|@")]
    [InlineData("@p:")][InlineData("@p:1bad")][InlineData("row[1]/@id")]
    [InlineData("//row/@id")][InlineData("@id @x")]
    public void InvalidFieldGrammarRejects(string field)
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());
        schema=schema with { Identities=[schema.Identities[0] with { Fields=[field] }] };
        Assert.Throws<SchemaCompileException>(()=>new XsdIdentityCompiler().Compile(schema,new SchemaCompileOperation()));
    }
    [Fact]
    public void EveryDetachedIdentityExpressionIsCompiled()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());
        schema=schema with { Identities=schema.Identities.Append(schema.Identities[0] with { Id=12,Name=new("","Detached"),Selector="row[1]" }).ToArray() };
        Assert.Throws<SchemaCompileException>(()=>new XsdIdentityCompiler().Compile(schema,new SchemaCompileOperation()));
    }
    [Fact]
    public void UnicodeNamesAreNotReducedToAsciiOrCharCategories()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint(selector:"根😀",field:"@é"));
        Assert.False(IdentityTests.Run(schema,"<root><根😀 é='x'/><根😀 é='x'/></root>").Complete().IsValid);
    }
}
