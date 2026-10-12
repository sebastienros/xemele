using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdValidate;

public sealed class EvaluatorTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal sealed record Prepared(XsdCompiledSchema Schema, IReadOnlyDictionary<SchemaName, XsdContentProgram> Programs, XmlFacetEvaluator Facets, XsdIdentityProgram Identity)
    {
        internal ValidationResult Run(string xml, ValidationOptions? options = null, IXsdIdentityFactory? factory = null) =>
            Run(XmlDocument.Parse(xml), options, factory);
        internal ValidationResult Run(XmlDocument document, ValidationOptions? options = null, IXsdIdentityFactory? factory = null) =>
            new XmlSchemaValidator().Validate(Schema, document, options ?? new(), Programs, new XsdContentMatcher(), Facets, factory ?? new XsdIdentityFactory(), Identity);
    }
    internal static Prepared Prepare(string body)
    {
        var context = new SchemaCompileContext();
        var compiled = new XmlSchemaCompiler().Compile(context.Parse($"<xs:schema xmlns:xs='{Xs}' xmlns:p='urn:p' xmlns:q='urn:p'>{body}</xs:schema>").Document, context.Options, context.Operation);
        var registry = new XmlDatatypeRegistry(compiled.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), context.Operation);
        var schema = compiled with { Datatypes = registry }; var facets = new XmlFacetEvaluator();
        var programs = new Dictionary<SchemaName, XsdContentProgram>();
        foreach (var type in schema.Types) programs.Add(type.Name, new XsdContentCompiler().Compile(schema, type, context.Operation));
        return new(schema, programs, facets, new XsdIdentityCompiler().Compile(schema, context.Operation));
    }
    [Theory]
    [InlineData("string", "hello", true)] [InlineData("int", "42", true)] [InlineData("int", "4x", false)]
    [InlineData("boolean", "true", true)] [InlineData("boolean", "TRUE", false)]
    [InlineData("decimal", "001.00", true)] [InlineData("date", "2025-02-29", false)]
    [InlineData("QName", "p:name", true)] [InlineData("QName", "missing:name", false)]
    public void BuiltinInstanceExpectations(string type, string text, bool expected)
    {
        var result = Prepare($"<xs:element name='root' type='xs:{type}'/>").Run($"<root xmlns:p='urn:p'>{text}</root>");
        Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid);
    }
    [Theory]
    [InlineData("<root><a>2</a><b/></root>", true)]
    [InlineData("<root><b/><a>2</a></root>", false)]
    [InlineData("<root><a>x</a><b/></root>", false)]
    [InlineData("<root><a>2</a></root>", false)]
    [InlineData("<root>text<a>2</a><b/></root>", false)]
    [InlineData("<root> \t<a>2</a><b/></root>", true)]
    [InlineData("<root><a>2<extra/></a><b/></root>", false)]
    public void OrderedLocalContent(string xml, bool expected)
    {
        var prepared = Prepare("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='a' type='xs:int'/><xs:element name='b'/></xs:sequence></xs:complexType></xs:element>");
        var result = prepared.Run(xml); Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid);
    }
    [Theory]
    [InlineData("<root n='1'/>", true)] [InlineData("<root/>", false)]
    [InlineData("<root n='0'/>", false)] [InlineData("<root n='1' blocked='x'/>", false)]
    [InlineData("<root n='1' unknown='x'/>", false)]
    public void RequiredProhibitedAndFacets(string xml, bool expected)
    {
        var result = Prepare("<xs:simpleType name='Positive'><xs:restriction base='xs:int'><xs:minInclusive value='1'/></xs:restriction></xs:simpleType><xs:element name='root'><xs:complexType><xs:attribute name='n' type='Positive' use='required'/><xs:attribute name='blocked' use='prohibited'/></xs:complexType></xs:element>").Run(xml);
        Assert.Equal(expected, result.IsValid); Assert.True(result.IsComplete);
    }
    [Theory]
    [InlineData("<root/>", true)] [InlineData("<root n='01.0'/>", true)] [InlineData("<root n='2'/>", false)]
    public void FixedEqualityUsesValues(string xml, bool expected)
    {
        var result = Prepare("<xs:element name='root'><xs:complexType><xs:attribute name='n' type='xs:decimal' fixed='1'/></xs:complexType></xs:element>").Run(xml);
        Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid);
    }
    [Theory]
    [InlineData("<root/>", true)] [InlineData("<root>01.00</root>", true)] [InlineData("<root>2</root>", false)]
    public void ElementFixed(string xml, bool expected)
    { var result = Prepare("<xs:element name='root' type='xs:decimal' fixed='1'/>").Run(xml); Assert.Equal(expected, result.IsValid); }
    [Fact]
    public void EffectiveDefaultsAreTypedRunLocalAndReachHooks()
    {
        var prepared = Prepare("<xs:element name='root' type='xs:int' default='7'/>");
        var document = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken); var recording = new RecordingFactory();
        Assert.True(prepared.Run(document, factory: recording).IsValid); Assert.Equal("", document.RootElement.GetTextContent());
        var value = Assert.Single(recording.Values); Assert.True(value.IsValid); Assert.NotNull(value.Value); Assert.Equal(new SchemaName(Xs, "int"), value.Type);
        Assert.True(prepared.Run(document).IsValid);
    }
    [Fact]
    public void BindingLocalQNameDefaultAndFixedContexts()
    {
        var prepared = Prepare("<xs:attribute name='a' type='xs:QName' fixed='p:v'/><xs:element name='root'><xs:complexType><xs:attribute ref='a' fixed='p:v' xmlns:p='urn:local'/></xs:complexType></xs:element>");
        Assert.True(prepared.Run("<root xmlns:z='urn:local' a='z:v'/>").IsValid);
        Assert.False(prepared.Run("<root xmlns:z='urn:p' a='z:v'/>").IsValid);
        var document = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken); var hook = new RecordingFactory();
        Assert.True(prepared.Run(document, factory: hook).IsValid); Assert.Equal(0, document.RootElement.Attributes.Count);
        var expected = new XmlDatatypeRegistry(); var op = new SchemaCompileOperation();
        Assert.True(expected.Find(Xs)!.TryParse(new(Xs, "QName"), "z:v", new(new Dictionary<string,string> { ["z"]="urn:local" }, "", null), op, out var value, out _));
        Assert.True(expected.Find(Xs)!.ValueEquals(value!, hook.Attributes.Single().Value!, op));
    }
    [Theory]
    [InlineData("true", "", true)] [InlineData("1", "", true)] [InlineData("false", "42", true)]
    [InlineData("true", " ", false)] [InlineData("true", "<a/>", false)] [InlineData("TRUE", "", false)]
    public void NillableInstances(string nil, string content, bool expected)
    {
        var result = Prepare("<xs:element name='root' type='xs:int' nillable='true'/>").Run($"<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='{nil}'>{content}</root>");
        Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid);
    }
    [Theory]
    [InlineData("xs:int", true)] [InlineData("xs:string", false)] [InlineData("missing", false)]
    public void TypeOverrideDerivation(string type, bool expected)
    { Assert.Equal(expected, Prepare("<xs:element name='root' type='xs:decimal'/>").Run($"<root xmlns:xs='{Xs}' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='{type}'>1</root>").IsValid); }
    [Theory]
    [InlineData("strict", false)] [InlineData("lax", true)] [InlineData("skip", true)]
    public void WildcardPoliciesAndAllScopes(string policy, bool expected)
    {
        var prepared = Prepare($"<xs:element name='root'><xs:complexType><xs:sequence><xs:any processContents='{policy}' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:element>");
        var hook = new RecordingFactory(); var result = prepared.Run("<root><unknown><nested/></unknown></root>", factory: hook);
        Assert.Equal(expected, result.IsValid); Assert.Equal(new[] { "+root", "+unknown", "+nested", "-nested", "-unknown", "-root", "complete" }, hook.Events);
    }
    [Theory]
    [InlineData("lax", false)] [InlineData("strict", false)] [InlineData("skip", true)]
    public void KnownWildcardValidatesUnlessSkip(string policy, bool expected)
    { Assert.Equal(expected, Prepare($"<xs:element name='known' type='xs:int'/><xs:element name='root'><xs:complexType><xs:sequence><xs:any processContents='{policy}'/></xs:sequence></xs:complexType></xs:element>").Run("<root><known>x</known></root>").IsValid); }
    [Theory]
    [InlineData("<root><row id='a'/><row ref='a'/></root>", true)]
    [InlineData("<root><row id='a'/><row id='a'/></root>", false)]
    [InlineData("<root><row ref='missing'/></root>", false)]
    [InlineData("<root><row refs='a b'/><row id='a'/><row id='b'/></root>", true)]
    public void DocumentIdsAndReferences(string xml, bool expected)
    {
        var prepared = Prepare("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='row' maxOccurs='unbounded'><xs:complexType><xs:attribute name='id' type='xs:ID'/><xs:attribute name='ref' type='xs:IDREF'/><xs:attribute name='refs' type='xs:IDREFS'/></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element>");
        Assert.Equal(expected, prepared.Run(xml).IsValid); Assert.True(prepared.Run("<root><row id='a'/></root>").IsValid);
    }
    [Fact]
    public void RealKeyHooksSeeTypedDefaultsAndInvalidValues()
    {
        var prepared = Prepare("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='row' maxOccurs='unbounded'><xs:complexType><xs:attribute name='n' type='xs:int' default='1'/></xs:complexType></xs:element></xs:sequence></xs:complexType><xs:unique name='U'><xs:selector xpath='row'/><xs:field xpath='@n'/></xs:unique></xs:element>");
        Assert.True(prepared.Run("<root><row/><row n='2'/></root>").IsValid);
        Assert.False(prepared.Run("<root><row/><row n='01'/></root>").IsValid);
        var invalid = prepared.Run("<root><row n='bad'/></root>"); Assert.False(invalid.IsValid); Assert.Contains(invalid.Diagnostics, d => d.Code.StartsWith("XSD.Datatype", StringComparison.Ordinal));
    }
    internal sealed class RecordingFactory : IXsdIdentityFactory, IXsdIdentitySession
    {
        internal readonly List<string> Events = []; internal readonly List<XsdValidatedValue> Values = [], Attributes = [];
        internal Action<ISchemaOperation>? Callback;
        public IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document) => this;
        public void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation operation) { Events.Add("+"+node.LocalName); Callback?.Invoke(operation); }
        public void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation operation) => Attributes.Add(value);
        public void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation operation) { Events.Add("-"+node.LocalName); Values.Add(value); }
        public void Complete(ISchemaOperation operation) => Events.Add("complete");
    }
}
