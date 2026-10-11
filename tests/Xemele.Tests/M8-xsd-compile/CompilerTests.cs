using System.Numerics;
using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdCompile;

public sealed class CompilerTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Wrap(string body, string attributes = "") => $"<xs:schema xmlns:xs='{Xs}' {attributes}>{body}</xs:schema>";
    internal static XsdCompiledSchema Compile(string body, string attributes = "", SchemaCompileOptions? options = null)
    {
        var context = new SchemaCompileContext(options); var scope = context.Parse(Wrap(body, attributes));
        return new XmlSchemaCompiler().Compile(scope.Document, context.Options, context.Operation);
    }
    private static SchemaName Name(string local, string ns = "") => new(ns, local);
    [Fact]
    public void RealGraphContainsExplicitBuiltinsAndSeparateLocalIds()
    {
        var graph = Compile("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='same'/><xs:element name='same' type='xs:string'/></xs:sequence></xs:complexType></xs:element><xs:element name='same'/>");
        Assert.Equal(2, graph.GlobalElements.Count); Assert.Equal(4, graph.Elements.Count);
        var type = Assert.Single(graph.Types, t => t.Anonymous && t.ContentKind == XsdContentKind.ElementOnly);
        var group = Assert.IsType<XsdGroupTerm>(type.Particle!.Term);
        int first = Assert.IsType<XsdElementTerm>(group.Particles[0].Term).DeclarationId, second = Assert.IsType<XsdElementTerm>(group.Particles[1].Term).DeclarationId;
        Assert.NotEqual(first, second); Assert.NotEqual(graph.GlobalElements[Name("same")], first);
        Assert.NotNull(graph.Datatypes.FindType(Name("anySimpleType", Xs))); Assert.Null(graph.Datatypes.Find(Xs));
        Assert.Contains(graph.Types, t => t.Name == Name("anyType", Xs) && t.Particle!.MaxOccurs is null);
        Assert.Equal(Name("anyType", Xs), graph.Elements[first].Type);
    }
    [Fact]
    public void ImmutableGraphOwnsAllListsAndSurvivesSourceMutation()
    {
        var context = new SchemaCompileContext(); var scope = context.Parse(Wrap("<xs:element name='root'/>"));
        var graph = new XmlSchemaCompiler().Compile(scope.Document, context.Options, context.Operation);
        scope.Document.RootElement.SetAttribute("targetNamespace", "changed"); Assert.Equal(Name("root"), graph.Elements[0].Name);
        Assert.Throws<NotSupportedException>(() => ((IList<XsdElementDeclaration>)graph.Elements).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<SchemaName,int>)graph.GlobalElements).Clear());
        var items = new List<XsdParticle>(); var term = new XsdGroupTerm(XsdCompositor.Sequence, items); items.Add(new(new XsdElementTerm(0), 1, 1, default)); Assert.Empty(term.Particles);
        var replaced = term with { Particles = items }; items.Clear(); Assert.Single(replaced.Particles);
    }
    [Fact]
    public void BigIntegerAndUnboundedOccurrencesAreDistinct()
    {
        const string Huge = "99999999999999999999999999999999999999999999999";
        var graph = Compile($"<xs:complexType name='T'><xs:sequence><xs:element name='finite' minOccurs='0' maxOccurs='{Huge}'/><xs:any minOccurs='0' maxOccurs='unbounded'/></xs:sequence></xs:complexType>");
        var group = Assert.IsType<XsdGroupTerm>(graph.Types.Single(t => t.Name == Name("T")).Particle!.Term);
        Assert.Equal(BigInteger.Parse(Huge), group.Particles[0].MaxOccurs); Assert.Null(group.Particles[1].MaxOccurs);
    }
    [Fact]
    public void DefaultAndFixedConstraintsRetainDeclarationAndOverrideMaps()
    {
        var graph = Compile("<xs:attribute name='q' type='xs:QName' fixed='p:value' xmlns:p='urn:global'/><xs:complexType name='T'><xs:attribute ref='t:q' fixed='p:value' xmlns:p='urn:local'/></xs:complexType><xs:element name='r' type='xs:QName' default='p:item' xmlns:p='urn:element'/>", "targetNamespace='urn:t' xmlns:t='urn:t'");
        var attribute = graph.Attributes[graph.GlobalAttributes[Name("q","urn:t")]];
        var binding = graph.Types.Single(t => t.Name == Name("T","urn:t")).Attributes[0];
        Assert.Equal("urn:global", attribute.Context.Prefixes["p"]); Assert.Equal("urn:local", binding.Context.Prefixes["p"]);
        Assert.Equal("urn:element", graph.Elements[0].Context.Prefixes["p"]); Assert.Equal("p:value", binding.FixedValue);
    }
    [Fact]
    public void DistinctFinalAndBlockSetsExpandAllAndDefaultControls()
    {
        var graph = Compile("<xs:complexType name='T' block='#all' final='#all'/><xs:simpleType name='S' final='#all'><xs:restriction base='xs:string'/></xs:simpleType><xs:element name='r' block='#all' final='#all'/>");
        var type = graph.Types.Single(t=>t.Name==Name("T")); Assert.Equal(2,type.Block.Count); Assert.Equal(2,type.Final.Count);
        var element=graph.Elements[0]; Assert.Equal(3,element.Block.Count); Assert.Equal(2,element.Final.Count);
        var simple=graph.SimpleTypes.Single(t=>t.Name==Name("S")); Assert.Equal(3,simple.Final.Count);
        var defaults = Compile("<xs:complexType name='T'/><xs:element name='r'/>", "finalDefault='#all' blockDefault='#all'");
        Assert.Equal(2, defaults.Types.Single(t=>t.Name==Name("T")).Final.Count);
    }
    [Fact]
    public void GroupsExpandWhileLocationsAndDeclarationIdsRemainSeparate()
    {
        var graph=Compile("<xs:group name='g'><xs:sequence><xs:element name='a'/></xs:sequence></xs:group><xs:attributeGroup name='ag'><xs:attribute name='x' type='xs:string'/></xs:attributeGroup><xs:complexType name='T'><xs:group ref='g'/><xs:attributeGroup ref='ag'/></xs:complexType>");
        var type=graph.Types.Single(t=>t.Name==Name("T")); Assert.IsType<XsdGroupTerm>(type.Particle!.Term); Assert.Single(type.Attributes);
        Assert.Empty(graph.GlobalElements); Assert.Empty(graph.GlobalAttributes);
    }
    [Fact]
    public void RecursiveContentTypeReferencesAreFiniteEdges()
    {
        var graph=Compile("<xs:complexType name='Node'><xs:sequence><xs:element name='child' type='Node' minOccurs='0'/></xs:sequence></xs:complexType><xs:element name='root' type='Node'/>");
        Assert.Equal(2,graph.Elements.Count); Assert.Single(graph.Types, t=>t.Name==Name("Node"));
    }
    [Fact]
    public void SimpleTypesRetainFacetContextsListsUnionAndStableAnonymousNames()
    {
        const string Body="<xs:simpleType name='S'><xs:restriction base='xs:QName'><xs:enumeration value='p:item' xmlns:p='urn:facet'/></xs:restriction></xs:simpleType><xs:simpleType name='L'><xs:list><xs:simpleType><xs:restriction base='xs:integer'/></xs:simpleType></xs:list></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='S xs:string'><xs:simpleType><xs:restriction base='xs:token'/></xs:simpleType></xs:union></xs:simpleType>";
        var first=Compile(Body); var second=Compile(Body);
        Assert.Equal(first.Types.Where(t=>t.Anonymous).Select(t=>t.Name),second.Types.Where(t=>t.Anonymous).Select(t=>t.Name));
        Assert.Equal("urn:facet",first.SimpleTypes.Single(t=>t.Name==Name("S")).Facets[0].Context.Prefixes["p"]);
        Assert.Equal(XmlDatatypeVariety.List,first.Datatypes.FindType(Name("L"))!.Variety);
        Assert.Equal(3,first.Datatypes.FindType(Name("U"))!.MemberTypes.Count);
    }
    [Fact]
    public void SubstitutionClosureIsTransitiveAndHonorsElementBlock()
    {
        var graph=Compile("<xs:element name='a' type='xs:anyType' abstract='true'/><xs:element name='b' substitutionGroup='a' type='xs:string'/><xs:element name='c' substitutionGroup='b' type='xs:token'/>");
        Assert.Equal(new[]{1,2},XsdSubstitutionGroups.Members(graph,0,new SchemaCompileOperation()));
        var blocked=Compile("<xs:element name='a' block='substitution'/><xs:element name='b' substitutionGroup='a'/>"); Assert.Empty(XsdSubstitutionGroups.Members(blocked,0,new SchemaCompileOperation()));
    }
    [Fact]
    public void ComplexExtensionCarriesBaseContentAttributesAndWildcard()
    {
        var graph=Compile("<xs:complexType name='B'><xs:sequence><xs:element name='a'/></xs:sequence><xs:attribute name='x'/><xs:anyAttribute namespace='##other' processContents='lax'/></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:extension base='B'><xs:sequence><xs:element name='b'/></xs:sequence><xs:attribute name='y'/></xs:extension></xs:complexContent></xs:complexType>");
        var type=graph.Types.Single(t=>t.Name==Name("D")); Assert.Equal(XsdDerivation.Extension,type.Derivation); Assert.Equal(2,type.Attributes.Count); Assert.NotNull(type.AnyAttribute);
        Assert.Equal(2,Assert.IsType<XsdGroupTerm>(type.Particle!.Term).Particles.Count);
    }
    [Fact]
    public void SimpleContentRetainsDerivedDefinitionAndNotationAndIdentityDefinitions()
    {
        var graph=Compile("<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:string'/></xs:simpleContent></xs:complexType><xs:complexType name='T'><xs:simpleContent><xs:restriction base='B'><xs:pattern value='[a-z]+'/></xs:restriction></xs:simpleContent></xs:complexType><xs:notation name='n' public='image'/><xs:element name='r'><xs:key name='k'><xs:selector xpath='item'/><xs:field xpath='@id'/></xs:key><xs:keyref name='kr' refer='k'><xs:selector xpath='item'/><xs:field xpath='@ref'/></xs:keyref></xs:element>");
        var type=graph.Types.Single(t=>t.Name==Name("T")); Assert.Equal(XsdContentKind.Simple,type.ContentKind); Assert.Single(graph.Datatypes.FindType(type.SimpleType!.Value)!.Facets);
        Assert.Equal("image",graph.Notations[Name("n")].PublicId); Assert.Equal(2,graph.Identities.Count);
    }
    [Theory]
    [InlineData("<xs:element name='r'/><xs:element name='r'/>")]
    [InlineData("<xs:simpleType name='T'><xs:restriction base='T'/></xs:simpleType>")]
    [InlineData("<xs:complexType name='T'><xs:complexContent><xs:extension base='T'/></xs:complexContent></xs:complexType>")]
    [InlineData("<xs:element name='a' substitutionGroup='b'/><xs:element name='b' substitutionGroup='a'/>")]
    [InlineData("<xs:element name='r' type='missing'/>")]
    [InlineData("<xs:element name='r' type='unknown:T'/>")]
    [InlineData("<xs:group name='g'><xs:sequence><xs:group ref='g'/></xs:sequence></xs:group>")]
    [InlineData("<xs:attributeGroup name='g'><xs:attributeGroup ref='g'/></xs:attributeGroup>")]
    [InlineData("<xs:complexType name='T'><xs:sequence><xs:element name='x' minOccurs='2' maxOccurs='1'/></xs:sequence></xs:complexType>")]
    [InlineData("<xs:complexType name='T'><xs:sequence><xs:element name='x' minOccurs='-1'/></xs:sequence></xs:complexType>")]
    [InlineData("<xs:complexType name='T'><xs:all maxOccurs='unbounded'><xs:element name='x'/></xs:all></xs:complexType>")]
    [InlineData("<xs:complexType name='T'><xs:all><xs:element name='x' maxOccurs='2'/></xs:all></xs:complexType>")]
    [InlineData("<xs:complexType name='T'><xs:sequence><xs:all/></xs:sequence></xs:complexType>")]
    [InlineData("<xs:attribute name='a' type='xs:anyType'/>")]
    [InlineData("<xs:element name='r' default='a' fixed='b'/>")]
    [InlineData("<xs:element name='r' final='substitution'/>")]
    [InlineData("<xs:complexType name='T' block='substitution'/>")]
    [InlineData("<xs:complexType name='B' final='extension'/><xs:complexType name='D'><xs:complexContent><xs:extension base='B'/></xs:complexContent></xs:complexType>")]
    [InlineData("<xs:element name='a' type='xs:integer'/><xs:element name='b' substitutionGroup='a' type='xs:string'/>")]
    [InlineData("<xs:simpleType name='L'><xs:list itemType='xs:NMTOKENS'/></xs:simpleType>")]
    [InlineData("<xs:complexType name='T'><xs:attribute name='a'/><xs:attribute name='a'/></xs:complexType>")]
    [InlineData("<xs:complexType name='T'><xs:assert test='true()'/></xs:complexType>")]
    [InlineData("<xs:element name='r' nillable='yes'/>")]
    [InlineData("<xs:element name='r' nonsense='value'/>")]
    [InlineData("<xs:element name='r'><xs:keyref name='k' refer='missing'><xs:selector xpath='x'/><xs:field xpath='@id'/></xs:keyref></xs:element>")]
    public void InvalidSchemaRejectsWithOwnedRestrictionDiagnostics(string body)
    { var error=Assert.Throws<SchemaCompileException>(()=>Compile(body)); Assert.Contains(error.Diagnostics,d=>d.Code=="XSD.SchemaRestriction"&&d.Severity==XmlDiagnosticSeverity.Error); }
    [Fact]
    public void QuotasCancellationIncompleteAndConcurrentMutationAreHonest()
    {
        var limit=Assert.Throws<SchemaCompileException>(()=>Compile("<xs:element name='r'/>",options:new(){MaxComponents=1})); Assert.Equal("SCHEMA.Limit",limit.Diagnostics[^1].Code);
        using var canceled=new CancellationTokenSource(); canceled.Cancel(); Assert.Throws<OperationCanceledException>(()=>Compile("",options:new(){CancellationToken=canceled.Token}));
        var context=new SchemaCompileContext(); var document=context.Parse(Wrap("<xs:element name='r'/>")).Document;
        var changing=new MutatingOperation(context.Operation,()=>document.RootElement.SetAttribute("changed","yes"));
        Assert.Throws<InvalidOperationException>(()=>new XmlSchemaCompiler().Compile(document,context.Options,changing));
    }
    private sealed class MutatingOperation(ISchemaOperation inner,Action mutate):ISchemaOperation
    {
        private bool changed;
        public XmlResourceLimits Limits=>inner.Limits; public CancellationToken CancellationToken=>inner.CancellationToken; public bool IsComplete=>inner.IsComplete; public long? RemainingWork=>inner.RemainingWork;
        public bool Charge(long work,XmlLocation at){if(!changed&&work>0){changed=true;mutate();}return inner.Charge(work,at);}
        public bool ChargeComponents(int count,XmlLocation at)=>inner.ChargeComponents(count,at); public bool AddDiagnostic(XmlDiagnostic d)=>inner.AddDiagnostic(d); public void Refuse(string c,string m,XmlLocation at)=>inner.Refuse(c,m,at);
    }
}
