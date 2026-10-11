using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdCompile;

public sealed class LocationTests
{
    private static readonly Uri Source = new("https://example.test/schema.xsd");
    private const string Schema = """
<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:t='urn:t' targetNamespace='urn:t'>
 <xs:attribute name='q' type='xs:QName' xmlns:p='urn:global' fixed='p:item'/>
 <xs:complexType name='T'>
  <xs:sequence>
   <xs:element name='local' type='xs:QName' xmlns:p='urn:element' default='p:value'/>
  </xs:sequence>
  <xs:attribute ref='t:q' xmlns:p='urn:local' fixed='p:item'/>
 </xs:complexType>
</xs:schema>
""";
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeclarationsParticlesAndValueConstraintsCopyTheirActualOriginalLocations(bool bytes)
    {
        var context=new SchemaCompileContext(new(){BaseUri=Source}); SchemaDocumentScope scope;
        if(bytes){using var stream=new MemoryStream(Encoding.UTF8.GetBytes(Schema));scope=context.Parse(stream);}else scope=context.Parse(Schema);
        var graph=new XmlSchemaCompiler().Compile(scope.Document,context.Options,context.Operation);
        var type=graph.Types.Single(t=>t.Name==new SchemaName("urn:t","T"));var local=Assert.Single(graph.Elements);var declaration=Assert.Single(graph.Attributes);var binding=Assert.Single(type.Attributes);
        Assert.Equal(3,type.Location.LineNumber);Assert.Equal(4,type.Particle!.Location.LineNumber);Assert.Equal(5,local.Location.LineNumber);Assert.Equal(2,declaration.Location.LineNumber);Assert.Equal(7,binding.Location.LineNumber);
        Assert.Equal("urn:global",declaration.Context.Prefixes["p"]);Assert.Equal("urn:local",binding.Context.Prefixes["p"]);Assert.Equal("urn:element",local.Context.Prefixes["p"]);
        foreach(var at in new[]{type.Location,type.Particle.Location,local.Location,declaration.Location,binding.Location}){Assert.Equal(Source,at.SourceUri);Assert.True(at.LinePosition>0);Assert.Equal(bytes,at.ByteOffset.HasValue);}
        var root=scope.Document.RootElement;var attributeNode=Child(root,"attribute");var typeNode=Child(root,"complexType");var bindingNode=Child(typeNode,"attribute");var elementNode=Child(Child(typeNode,"sequence"),"element");
        Assert.Equal(scope.Location(Attr(attributeNode,"fixed")),declaration.Location);Assert.Equal(scope.Location(Attr(bindingNode,"fixed")),binding.Location);Assert.Equal(scope.Location(Attr(elementNode,"default")),local.Location);
    }
    [Fact]
    public void ExternalRestrictionDiagnosticsKeepFinalUriAndLocalScalarAndBytePositions()
    {
        var final=new Uri("https://redirect.test/final.xsd");
        const string external="<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>\n <xs:complexType name='T'>\n  <xs:sequence>\n   <xs:element name='bad' minOccurs='2' maxOccurs='1'/>\n  </xs:sequence>\n </xs:complexType>\n</xs:schema>";
        var resolver=new Resolver(final,external);
        var failure=Assert.Throws<SchemaCompileException>(()=>CompilerTests.Compile("<xs:include schemaLocation='child.xsd'/>",options:new(){BaseUri=Source,Resolver=resolver}));
        var diagnostic=Assert.Single(failure.Diagnostics);Assert.Equal("XSD.SchemaRestriction",diagnostic.Code);Assert.Equal(final,diagnostic.Location.SourceUri);Assert.Equal(4,diagnostic.Location.LineNumber);Assert.True(diagnostic.Location.LinePosition>0);Assert.NotNull(diagnostic.Location.ByteOffset);
    }
    [Fact]
    public void MutatedBeforeCompilationUsesHonestUnknownCoordinatesWithOriginalUri()
    {
        var context=new SchemaCompileContext(new(){BaseUri=Source});var document=context.Parse(Schema).Document;
        document.RootElement.SetAttribute("version","changed");
        var graph=new XmlSchemaCompiler().Compile(document,context.Options,context.Operation);
        Assert.All(graph.Elements,e=>{Assert.Equal(Source,e.Location.SourceUri);Assert.Equal(0,e.Location.LineNumber);Assert.Equal(0,e.Location.LinePosition);Assert.Null(e.Location.ByteOffset);});
    }
    private static XmlNode Child(XmlNode node,string name){foreach(var child in node.Children)if(child.Kind==XmlNodeKind.Element&&child.LocalName==name)return child;throw new Exception("Missing test node");}
    private static XmlAttribute Attr(XmlNode node,string name){foreach(var attribute in node.Attributes)if(attribute.LocalName==name)return attribute;throw new Exception("Missing test attribute");}
    private sealed class Resolver(Uri final,string text):XmlResourceResolver
    {public override XmlResolvedResource? Resolve(XmlResourceRequest request,CancellationToken token){token.ThrowIfCancellationRequested();return new(new MemoryStream(Encoding.UTF8.GetBytes(text)),final);}}
}
