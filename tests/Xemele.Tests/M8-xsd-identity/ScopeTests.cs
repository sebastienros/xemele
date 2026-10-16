using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdIdentity;

public sealed class ScopeTests
{
    private static XsdCompiledSchema Scopes(string parent = "", string child = "") => IdentityTests.Schema(
        $"<xs:element name='root'>{parent}</xs:element><xs:element name='group'>{child}</xs:element>");
    [Fact]
    public void SameConstraintInSiblingScopesStartsFresh()
    {
        Assert.True(IdentityTests.Run(Scopes(child: IdentityTests.Constraint()), "<root><group><row id='x'/></group><group><row id='x'/></group></root>").Complete().IsValid);
    }
    [Theory]
    [InlineData("<group><row id='x'/></group><use id='x'/>", true)]
    [InlineData("<group><row id='x'/></group><group><row id='x'/></group><use id='x'/>", false)]
    [InlineData("<group><row id='x'/></group><group><row id='y'/></group><use id='x'/>", true)]
    [InlineData("<group><row id='x'/></group><group><row id='x'/></group><group><row id='x'/></group><use id='x'/>", false)]
    public void DescendantKeysPropagateAndConflictsAreRemoved(string content, bool valid)
    {
        var schema = Scopes(IdentityTests.Constraint("keyref", "use", "@id", "R", "K"), IdentityTests.Constraint());
        Assert.Equal(valid, IdentityTests.Run(schema, $"<root>{content}</root>").Complete().IsValid);
    }
    [Fact]
    public void ChildKeyrefCannotSeeAncestorOrSiblingKey()
    {
        var schema = Scopes(IdentityTests.Constraint(), IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        Assert.False(IdentityTests.Run(schema, "<root><row id='x'/><group><use id='x'/></group></root>").Complete().IsValid);
        schema = Scopes(child: IdentityTests.Constraint() + IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        Assert.False(IdentityTests.Run(schema, "<root><group><row id='x'/></group><group><use id='x'/></group></root>").Complete().IsValid);
    }
    [Fact]
    public void SelfEmbeddingOwnTuplesTakePriorityOverInheritedConflict()
    {
        var schema = Scopes(child: IdentityTests.Constraint() + IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        var xml = "<root><group><row id='x'/><group><row id='x'/></group><group><row id='x'/></group><use id='x'/></group></root>";
        Assert.True(IdentityTests.Run(schema, xml).Complete().IsValid);
    }
    [Fact]
    public void SelfEmbeddingSelectionOfSameNodeDoesNotConflict()
    {
        var schema = Scopes(child: IdentityTests.Constraint(selector: ".//row") + IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        Assert.True(IdentityTests.Run(schema, "<root><group><group><row id='x'/></group><use id='x'/></group></root>").Complete().IsValid);
    }
    [Fact]
    public void TwoFieldsAreAnOrderedTuple()
    {
        var key = "<xs:key name='K'><xs:selector xpath='row'/><xs:field xpath='@a'/><xs:field xpath='@b'/></xs:key>";
        var reference = "<xs:keyref name='R' refer='K'><xs:selector xpath='use'/><xs:field xpath='@a'/><xs:field xpath='@b'/></xs:keyref>";
        var schema = IdentityTests.Simple(key + reference);
        Assert.True(IdentityTests.Run(schema, "<root><row a='x' b='y'/><row a='y' b='x'/><use a='y' b='x'/></root>").Complete().IsValid);
        Assert.False(IdentityTests.Run(schema, "<root><row a='x' b='y'/><use a='y' b='x'/></root>").Complete().IsValid);
    }
    [Theory]
    [InlineData("missing", false)]
    [InlineData("K", true)]
    public void ReferencesAreValidatedByIdentityCompiler(string target, bool valid)
    {
        var schema = IdentityTests.Simple(IdentityTests.Constraint() + IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        schema = schema with
        {
            Identities = [schema.Identities[0], schema.Identities[1] with { Refer = new("", target) }]
        };
        if (valid)
        {
            Assert.NotNull(new XsdIdentityCompiler().Compile(schema, new SchemaCompileOperation()));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => new XsdIdentityCompiler().Compile(schema, new SchemaCompileOperation()));
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KeyrefTargetsAndCardinalityReject(bool anotherReference)
    {
        var schema = IdentityTests.Simple(IdentityTests.Constraint() + IdentityTests.Constraint("keyref", "use", "@id", "R", "K"));
        var reference = schema.Identities[1] with
        {
            Refer = anotherReference ? new("", "R") : new("", "K"),
            Fields = anotherReference ? ["@id"] : ["@id", "@other"]
        };
        schema = schema with
        {
            Identities = [schema.Identities[0], reference]
        };
        Assert.Throws<SchemaCompileException>(() => new XsdIdentityCompiler().Compile(schema, new SchemaCompileOperation()));
    }
}
