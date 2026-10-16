using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8RngValidate;

public sealed class EvaluationTests
{
    internal const string Ns = "http://relaxng.org/ns/structure/1.0";
    internal const string Xsd = "http://www.w3.org/2001/XMLSchema-datatypes";
    internal static string Element(string content, string extra = "") => "<element xmlns='" + Ns + "' name='r' " + extra + ">" + content + "</element>";
    internal static RelaxNgSchema Schema(string content, string extra = "") => RelaxNgSchema.Compile(Element(content, extra));
    [Theory]
    [InlineData("<empty/>", "<r/>", true)]
    [InlineData("<empty/>", "<r> \t\n</r>", true)]
    [InlineData("<empty/>", "<r>x</r>", false)]
    [InlineData("<empty/>", "<r><x/></r>", false)]
    [InlineData("<text/>", "<r/>", true)]
    [InlineData("<text/>", "<r>a<![CDATA[b]]><!--ignored--><?pi ignored?>c</r>", true)]
    [InlineData("<text/>", "<r><x/></r>", false)]
    [InlineData("<value type='string'>ab</value>", "<r>a<![CDATA[b]]></r>", true)]
    [InlineData("<value type='string'>ab</value>", "<r>a<!--x--><?pi?>b</r>", true)]
    [InlineData("<value type='string'>ab</value>", "<r> ab </r>", false)]
    [InlineData("<value>a b</value>", "<r> \ta\nb  </r>", true)]
    [InlineData("<value>a b</value>", "<r>a&#160;b</r>", false)]
    [InlineData("<value type='string'/>", "<r/>", true)]
    [InlineData("<value type='string'> </value>", "<r/>", false)]
    [InlineData("<choice><value>x</value><empty/></choice>", "<r> </r>", true)]
    [InlineData("<attribute name='a'><empty/></attribute>", "<r a=' '/>", true)]
    [InlineData("<attribute name='a'><empty/></attribute>", "<r a='x'/>", false)]
    [InlineData("<attribute name='a'><text/></attribute>", "<r/>", false)]
    [InlineData("<group><attribute name='a'><text/></attribute><attribute name='b'><text/></attribute></group>", "<r b='2' a='1'/>", true)]
    [InlineData("<attribute name='a'><text/></attribute>", "<r a='1' b='2'/>", false)]
    [InlineData("<group><element name='a'><empty/></element><element name='b'><empty/></element></group>", "<r> <a/> \n<b/> </r>", true)]
    [InlineData("<group><element name='a'><empty/></element><element name='b'><empty/></element></group>", "<r><b/><a/></r>", false)]
    [InlineData("<group><optional><element name='a'><empty/></element></optional><element name='b'><empty/></element></group>", "<r><b/><a/></r>", false)]
    [InlineData("<interleave><element name='a'><empty/></element><element name='b'><empty/></element></interleave>", "<r><b/><a/></r>", true)]
    [InlineData("<interleave><element name='a'><empty/></element><element name='b'><empty/></element></interleave>", "<r><a/><a/></r>", false)]
    [InlineData("<mixed><oneOrMore><element name='a'><empty/></element></oneOrMore></mixed>", "<r>x<a/>y<a/>z</r>", true)]
    [InlineData("<oneOrMore><element name='a'><empty/></element></oneOrMore>", "<r/>", false)]
    [InlineData("<oneOrMore><element name='a'><empty/></element></oneOrMore>", "<r><a/><a/></r>", true)]
    [InlineData("<list><group><value>a</value><value>b</value></group></list>", "<r> a\tb\n</r>", true)]
    [InlineData("<list><group><value>a</value><value>b</value></group></list>", "<r>ab</r>", false)]
    [InlineData("<list><empty/></list>", "<r/>", true)]
    [InlineData("<list><oneOrMore><value>a</value></oneOrMore></list>", "<r> </r>", false)]
    [InlineData("<data type='token'><except><value>bad</value></except></data>", "<r> bad </r>", false)]
    [InlineData("<data type='token'><except><value>bad</value></except></data>", "<r>good</r>", true)]
    [InlineData("<choice><element name='a'><value>x</value></element><element name='a'><value>y</value></element></choice>", "<r><a>y</a></r>", true)]
    public void IndependentWeakMatchingSequenceAndAttributeExpectations(string pattern, string instance, bool valid)
    {
        var result = Schema(pattern).Validate(instance);
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            Assert.Empty(result.Diagnostics);
        }
        else
        {
            Assert.Equal("RNG.Instance", Assert.Single(result.Diagnostics).Code);
        }
    }
    [Fact]
    public void RecursiveGraphInterleaveAndAmbiguousChoicesRetainAllAlternatives()
    {
        var source = "<grammar xmlns='" + Ns + "'><start><ref name='node'/></start><define name='node'><element name='n'><interleave><optional><attribute name='id'><text/></attribute></optional><zeroOrMore><ref name='node'/></zeroOrMore></interleave></element></define></grammar>";
        var schema = RelaxNgSchema.Compile(source);
        Assert.True(schema.Validate("<n><n id='a'><n/></n><n/></n>").IsValid);
        Assert.False(schema.Validate("<n><n><wrong/></n></n>").IsValid);
        var ambiguous = Schema("<interleave><choice><element name='a'><empty/></element><element name='b'><empty/></element></choice><element name='c'><empty/></element></interleave>");
        Assert.True(ambiguous.Validate("<r><c/><b/></r>").IsValid);
        Assert.False(ambiguous.Validate("<r><a/><b/></r>").IsValid);
    }
    [Fact]
    public void NameClassChoiceNamespaceAndExceptAreAppliedToExpandedNames()
    {
        var schema = RelaxNgSchema.Compile("<element xmlns='" + Ns + "'><anyName><except><nsName ns='urn:blocked'/></except></anyName><zeroOrMore><attribute><choice><name ns='urn:a'>x</name><name>y</name></choice><text/></attribute></zeroOrMore><empty/></element>");
        Assert.True(schema.Validate("<p:r xmlns:p='urn:ok' xmlns:a='urn:a' a:x='v' y='v'/>").IsValid);
        Assert.False(schema.Validate("<r xmlns='urn:blocked'/>").IsValid);
        Assert.False(schema.Validate("<r x='v'/>").IsValid);
    }
    [Fact]
    public async Task CompiledSchemaIsIndependentOfSourceAndSafeForConcurrentRuns()
    {
        var doc = XmlDocument.Parse(Element("<data type='int'><param name='minInclusive'>1</param></data>", "datatypeLibrary='" + Xsd + "'"), cancellationToken: TestContext.Current.CancellationToken);
        var schema = RelaxNgSchema.Compile(doc);
        doc.RootElement.SetAttribute("name", "changed");
        var instance = XmlDocument.Parse("<r>2</r>", cancellationToken: TestContext.Current.CancellationToken);
        var revision = instance.ContentRevision;
        var runs = new Task<bool>[32];
        for (var i = 0; i < runs.Length; i++)
        {
            var index = i;
            runs[i] = Task.Run(() => schema.Validate(index % 2 == 0 ? instance : XmlDocument.Parse("<r>0</r>", cancellationToken: TestContext.Current.CancellationToken)).IsValid == (index % 2 == 0), TestContext.Current.CancellationToken);
        }
        Assert.All(await Task.WhenAll(runs), Assert.True);
        Assert.Equal(revision, instance.ContentRevision);
    }
}
