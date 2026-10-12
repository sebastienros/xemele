using Xemele.Validation;
using Xunit;
using P = Xemele.Tests.M8XsdValidate.EvaluatorTests;
namespace Xemele.Tests.M8XsdInstanceHintOrdering;

public sealed class OrderingTests
{
    internal const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    internal const string Prefixes = "xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:p='urn:p' xmlns:q='urn:q' xmlns:xs='http://www.w3.org/2001/XMLSchema'";
    [Theory]
    [InlineData("same-tag-element", "<p:a xsi:schemaLocation='urn:p one'/>", 0)]
    [InlineData("same-tag-attribute-before", "<a p:v='1' xsi:schemaLocation='urn:p one'/>", 0)]
    [InlineData("same-tag-attribute-after", "<a xsi:schemaLocation='urn:p one' p:v='1'/>", 0)]
    [InlineData("same-tag-many-before", "<p:a p:v='1' q:v='2' xsi:schemaLocation='urn:p one urn:q two'/>", 0)]
    [InlineData("same-tag-many-after", "<p:a xsi:schemaLocation='urn:p one urn:q two' q:v='2' p:v='1'/>", 0)]
    [InlineData("ancestor", "<p:a><b xsi:schemaLocation='urn:p one'/></p:a>", 1)]
    [InlineData("sibling", "<p:a/><b xsi:schemaLocation='urn:p one'/>", 1)]
    [InlineData("attribute-only", "<a p:v='1'/><b xsi:schemaLocation='urn:p one'/>", 1)]
    [InlineData("declaration-only", "<a xmlns:z='urn:p'/><b xsi:schemaLocation='urn:p one'/>", 0)]
    [InlineData("QName-value-only", "<a xsi:type='xs:anyType'/><b xsi:schemaLocation='http://www.w3.org/2001/XMLSchema one'/>", 0)]
    [InlineData("hint-tokens-only", "<a xsi:schemaLocation='urn:p urn:q'/><b xsi:schemaLocation='urn:p two urn:q three'/>", 0)]
    [InlineData("repeated-before-use", "<a xsi:schemaLocation='urn:p one'/><b xsi:schemaLocation='urn:p one'/><p:c/>", 0)]
    [InlineData("repeated-after-use", "<p:a xsi:schemaLocation='urn:p one'/><b xsi:schemaLocation='urn:p one'/>", 1)]
    [InlineData("all-pairs-one-error", "<p:a/><q:a/><b xsi:schemaLocation='urn:unused zero urn:p one urn:q two urn:p three'/>", 1)]
    [InlineData("two-attributes", "<p:a/><b xsi:schemaLocation='urn:p one' xsi:noNamespaceSchemaLocation='two'/>", 2)]
    [InlineData("two-tags", "<p:a/><b xsi:schemaLocation='urn:p one'/><c xsi:schemaLocation='urn:p two'/>", 2)]
    [InlineData("XML-whitespace", "<p:a/><b xsi:schemaLocation='&#x9;urn:p&#xA;one&#xD;urn:q&#x9;two&#x20;'/>", 1)]
    [InlineData("Unicode-space-not-delimiter", "<a xsi:schemaLocation='urn:p&#xA0;one'/>", 0)]
    public void PhysicalStartTagsDetermineOrdering(string label, string body, int errors)
    {
        _ = label; var result = P.Prepare("<xs:element name='r'/>").Run($"<r {Prefixes}>{body}</r>");
        Assert.True(result.IsComplete); Assert.Equal(errors, result.Diagnostics.Count(d => d.Code == "XSD.HintOrder"));
        if (label == "Unicode-space-not-delimiter") Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Attribute");
        else Assert.Equal(errors == 0, result.IsValid);
    }
    [Theory]
    [InlineData("same-tag-root", "<r xmlns='urn:r' xsi:noNamespaceSchemaLocation='one' v='x'/>", 0)]
    [InlineData("same-tag-reverse", "<r xmlns='urn:r' v='x' xsi:noNamespaceSchemaLocation='one'/>", 0)]
    [InlineData("no-empty-use", "<r xmlns='urn:r'><a xsi:noNamespaceSchemaLocation='one'/></r>", 0)]
    [InlineData("unqualified-attribute", "<r xmlns='urn:r' v='x'><a xsi:noNamespaceSchemaLocation='one'/></r>", 1)]
    [InlineData("empty-element", "<r xmlns='urn:r'><a xmlns=''/><b xsi:noNamespaceSchemaLocation='one'/></r>", 1)]
    [InlineData("unqualified-root", "<r xmlns='' xsi:noNamespaceSchemaLocation='one'/>", 0)]
    public void EmptyNamespaceIsARealPhysicalName(string label, string xml, int errors)
    {
        var schema = XmlSchema.Compile($"<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' {(label == "unqualified-root" ? "" : "targetNamespace='urn:r'")}><xs:element name='r'/></xs:schema>");
        xml = xml.Replace("<r ", $"<r xmlns:xsi='{Xsi}' ", StringComparison.Ordinal);
        var result = schema.Validate(xml, new() { CancellationToken = TestContext.Current.CancellationToken });
        Assert.True(result.IsComplete); Assert.Equal(errors, result.Diagnostics.Count(d => d.Code == "XSD.HintOrder"));
        Assert.True(errors == 0 ? result.IsValid : !result.IsValid); _ = label;
    }
    [Theory]
    [InlineData("unknown-type", "xsi:type='missing'")]
    [InlineData("invalid-nil", "xsi:nil='BAD'")]
    [InlineData("odd-pairs", "xsi:schemaLocation='single'")]
    [InlineData("unknown-xsi", "xsi:unknown='value'")]
    public void SkippedLexicalAndSemanticAssessmentStaysSkipped(string label, string attributes)
    {
        var p = P.Prepare("<xs:element name='r'><xs:complexType><xs:sequence><xs:any processContents='skip'/></xs:sequence></xs:complexType></xs:element><xs:element name='a' type='xs:int'/>");
        var result = p.Run($"<r {Prefixes}><a {attributes}>bad<n/></a></r>");
        Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.Empty(result.Diagnostics); _ = label;
    }
    [Theory]
    [InlineData("skip-names-later-hint", "<p:a/><b xsi:schemaLocation='urn:p one'/>", 1)]
    [InlineData("skip-attribute-names", "<a p:v='1'/><b xsi:schemaLocation='urn:p one'/>", 1)]
    [InlineData("skip-hint-same-tag", "<p:a xsi:schemaLocation='urn:p one'/>", 0)]
    [InlineData("skip-nested", "<a><p:b/><c xsi:schemaLocation='urn:p one' xsi:nil='BAD' xsi:type='missing'/></a>", 1)]
    public void ChosenSkipPolicyChecksOnlyGlobalOrdering(string label, string content, int errors)
    {
        var p = P.Prepare("<xs:element name='r'><xs:complexType><xs:sequence><xs:any processContents='skip' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:element>");
        var result = p.Run($"<r {Prefixes}>{content}</r>"); Assert.True(result.IsComplete);
        Assert.Equal(errors == 0, result.IsValid); Assert.Equal(errors, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d => Assert.Equal("XSD.HintOrder", d.Code)); _ = label;
    }
    [Theory]
    [InlineData("odd-pairs", "xsi:schemaLocation='urn:p'", "XSD.Attribute")]
    [InlineData("invalid-uri", "xsi:noNamespaceSchemaLocation='%'", "XSD.Datatype.Lexical")]
    public void NormalLexicalErrorsRemainAssessed(string label, string attribute, string code)
    {
        var result = P.Prepare("<xs:element name='r'/>").Run($"<r {Prefixes} {attribute}/>");
        Assert.True(result.IsComplete); Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Code == code); _ = label;
    }
    [Fact]
    public void ContentFailureStillTracksLaterGlobalNamesAndHints()
    {
        var p = P.Prepare("<xs:element name='r'><xs:complexType><xs:sequence><xs:element name='expected'/></xs:sequence></xs:complexType></xs:element><xs:element name='n' type='xs:int'/>");
        var result = p.Run($"<r {Prefixes}><u><p:a/></u><b xsi:schemaLocation='urn:p one'/><n>bad</n></r>");
        Assert.True(result.IsComplete); Assert.False(result.IsValid);
        Assert.Single(result.Diagnostics, d => d.Code == "XSD.Content"); Assert.Single(result.Diagnostics, d => d.Code == "XSD.HintOrder");
        Assert.Contains(result.Diagnostics, d => d.Code.StartsWith("XSD.Datatype", StringComparison.Ordinal));
    }
    [Fact]
    public void OrderingDiagnosticsHaveOriginalAttributeLocationsAndHooksStaySingle()
    {
        var p = P.Prepare("<xs:element name='r'/>");
        var document = XmlDocument.Parse($"<r {Prefixes}>\n<p:a/>\n<b\n xsi:schemaLocation='urn:p one urn:p two'\n xsi:noNamespaceSchemaLocation='three'/>\n</r>", cancellationToken: TestContext.Current.CancellationToken);
        XmlNode b = default;
        foreach (var child in document.RootElement.Children) if (child.Kind == XmlNodeKind.Element && child.LocalName == "b") b = child;
        Assert.False(b.IsDefault);
        var hook = new Hook(); long revision = document.ContentRevision;
        var result = p.Run(document, factory: hook);
        Assert.True(result.IsComplete); Assert.False(result.IsValid);
        var errors = result.Diagnostics.Where(d => d.Code == "XSD.HintOrder").ToArray(); Assert.Equal(2, errors.Length);
        Assert.Equal(document.OriginalLocation(b.Attributes[0]), errors[0].Location); Assert.Equal(document.OriginalLocation(b.Attributes[1]), errors[1].Location);
        Assert.Equal(new[] { "+r", "+a", "-a", "+b", "-b", "-r", "complete" }, hook.Events);
        Assert.Equal(2, hook.Attributes.Count); Assert.All(hook.Attributes, a => { Assert.True(a.IsValid); Assert.NotNull(a.Value); });
        Assert.Equal(XmlDatatypeVariety.List, hook.Attributes[0].Value!.Variety); Assert.Equal(4, hook.Attributes[0].Value!.Items!.Count);
        Assert.False(hook.Leaves.Single(v => v.Name == "b").Value.IsValid);
        Assert.Equal(revision, document.ContentRevision);
    }
    internal sealed class Hook : IXsdIdentityFactory, IXsdIdentitySession
    {
        internal readonly List<string> Events = []; internal readonly List<XsdValidatedValue> Attributes = [];
        internal readonly List<(string Name, XsdValidatedValue Value)> Leaves = []; internal long? Remaining;
        internal Action<XmlNode, ISchemaOperation>? OnEnter;
        public IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document) => this;
        public void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation operation) { Events.Add("+" + node.LocalName); OnEnter?.Invoke(node, operation); }
        public void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation operation) => Attributes.Add(value);
        public void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation operation) { Events.Add("-" + node.LocalName); Leaves.Add((node.LocalName, value)); }
        public void Complete(ISchemaOperation operation) { Events.Add("complete"); Remaining = operation.RemainingWork; }
    }
}
