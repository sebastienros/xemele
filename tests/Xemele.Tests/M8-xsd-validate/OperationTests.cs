using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdValidate;
public sealed class OperationTests
{
    [Theory]
    [InlineData("work")] [InlineData("depth")] [InlineData("attributes")] [InlineData("token")] [InlineData("characters")] [InlineData("diagnostics")]
    public void LimitsProduceIncompleteResult(string kind)
    {
        var prepared = EvaluatorTests.Prepare("<xs:element name='root'/>");
        var limits = new XmlResourceLimits(); var options = new ValidationOptions();
        string xml = "<root><a/></root>";
        switch (kind)
        {
            case "work": options = options with { MaxOperations = 1 }; break;
            case "depth": limits = limits with { Depth = 1 }; break;
            case "attributes": limits = limits with { AttributesPerElement = 1 }; xml = "<root a='x' b='y'/>"; break;
            case "token": limits = limits with { TokenCharacters = 2 }; break;
            case "characters": limits = limits with { DocumentCharacters = 3 }; break;
            case "diagnostics": limits = limits with { Diagnostics = 1 }; xml = "<wrong a='x' b='y'/>"; break;
        }
        var result = prepared.Run(xml, options with { Limits = limits });
        Assert.False(result.IsComplete); Assert.False(result.IsValid); Assert.Equal("VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Fact]
    public void NullCapsAndIterativeDeepTraversal()
    {
        var limits = new XmlResourceLimits { Depth = null, AttributesPerElement = null, TokenCharacters = null, DocumentCharacters = null, Diagnostics = null, EvaluationWork = null };
        var prepared = EvaluatorTests.Prepare("<xs:element name='root'/>"); var doc = new XmlDocument(limits); var root = doc.CreateElement("root"); doc.DocumentNode.AppendChild(root);
        var node = root;
        for (int i=0;i<1024;i++) { var next = doc.CreateElement("child"); node.AppendChild(next); node=next; }
        Assert.True(prepared.Run(doc, new() { Limits=limits, MaxOperations=null }).IsValid);
    }
    [Fact]
    public void DepthBoundaryAndCancellation()
    {
        var prepared = EvaluatorTests.Prepare("<xs:element name='root'/>");
        Assert.True(prepared.Run("<root><a/></root>", new() { Limits = new() { Depth = 2 } }).IsValid);
        var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.Throws<OperationCanceledException>(()=>prepared.Run("<root/>", new() { CancellationToken=cancel.Token }));
        cancel = new(); var hook = new EvaluatorTests.RecordingFactory { Callback = _ => cancel.Cancel() };
        Assert.Throws<OperationCanceledException>(()=>prepared.Run("<root/>", new() { CancellationToken=cancel.Token }, hook));
    }
    [Fact]
    public void HookExceptionsRetainProvenanceIncludingCompileException()
    {
        var prepared = EvaluatorTests.Prepare("<xs:element name='root'/>");
        var exception = new SchemaCompileException([new("CALLER.Test", XmlDiagnosticSeverity.Error, "caller", default)]);
        var hook = new EvaluatorTests.RecordingFactory { Callback = _ => throw exception };
        Assert.Same(exception, Assert.Throws<SchemaCompileException>(()=>prepared.Run("<root/>", factory:hook)));
        var doc = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken); hook = new() { Callback = _ => doc.RootElement.SetAttribute("mutated", "yes") };
        Assert.Throws<InvalidOperationException>(()=>prepared.Run(doc, factory:hook));
    }
    [Fact]
    public void PreparedProgramsRequiredAndIdentityPairInvariant()
    {
        var p = EvaluatorTests.Prepare("<xs:element name='root'/>"); var validator = new XmlSchemaValidator(); var doc = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(()=>validator.Validate(p.Schema,doc,new(),p.Programs,new XsdContentMatcher(),p.Facets,null,p.Identity));
        Assert.Throws<ArgumentException>(()=>validator.Validate(p.Schema,doc,new(),new Dictionary<SchemaName,XsdContentProgram>(),new XsdContentMatcher(),p.Facets,null,null));
        Assert.True(validator.Validate(p.Schema,doc,new(),p.Programs,new XsdContentMatcher(),p.Facets,null,null).IsValid);
    }
    [Fact]
    public async Task ConcurrentRunsHaveIsolatedIdsAndCursors()
    {
        var p = EvaluatorTests.Prepare("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='n' type='xs:ID' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:element>");
        var results = await Task.WhenAll(Enumerable.Range(0,16).Select(_=>Task.Run(()=>p.Run("<root><n>a</n><n>b</n></root>"))));
        Assert.All(results,r=>Assert.True(r.IsValid)); Assert.False(p.Run("<root><n>a</n><n>a</n></root>").IsValid);
    }
    [Fact]
    public void DiagnosticsRetainInstanceLocation()
    {
        var p = EvaluatorTests.Prepare("<xs:element name='root' type='xs:int'/>");
        var context = new SchemaCompileContext(new() { BaseUri=new("https://example.test/instance.xml") });
        var doc=context.Parse("<root>bad</root>").Document; var result=p.Run(doc);
        var error = Assert.Single(result.Diagnostics); Assert.Equal(1,error.Location.LineNumber); Assert.Equal(new Uri("https://example.test/instance.xml"),error.Location.SourceUri);
    }
    [Theory]
    [InlineData("xs:NOTATION", "n", false)] [InlineData("N", "n", true)] [InlineData("N", "missing", false)]
    public void NotationUsesSchemaDeclarationsAndRequiredEnumeration(string type,string text,bool expected)
    {
        var p=EvaluatorTests.Prepare("<xs:notation name='n' public='urn:n'/><xs:simpleType name='N'><xs:restriction base='xs:NOTATION'><xs:enumeration value='n'/></xs:restriction></xs:simpleType><xs:element name='root' type='"+type+"'/>");
        Assert.Equal(expected,p.Run("<root>"+text+"</root>").IsValid);
    }
    [Theory]
    [InlineData("ENTITY", "image", true)] [InlineData("ENTITY", "parsed", false)] [InlineData("ENTITIES", "image image", true)] [InlineData("ENTITIES", "image missing", false)]
    public void EntitiesRequireUnparsedDtdContext(string type,string text,bool expected)
    {
        var p=EvaluatorTests.Prepare("<xs:element name='root' type='xs:"+type+"'/>");
        var doc=XmlDocument.Parse("<!DOCTYPE root [<!NOTATION n SYSTEM 'urn:n'><!ENTITY image SYSTEM 'urn:image' NDATA n><!ENTITY parsed 'value'>]><root>"+text+"</root>",new() { DtdProcessing=XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        Assert.Equal(expected,p.Run(doc).IsValid); Assert.False(p.Run("<root>"+text+"</root>").IsValid);
    }
}
