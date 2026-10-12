using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFacade;

public sealed class PublicTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}' xmlns:p='urn:p'>{body}</xs:schema>";
    internal static XmlSchema Compile(string body) => XmlSchema.Compile(Wrap(body));
    [Theory]
    [InlineData("<root><n>2</n></root>", true)]
    [InlineData("<root><n>0</n></root>", false)]
    [InlineData("<root><n>x</n></root>", false)]
    [InlineData("<root/>", false)]
    [InlineData("<root><n>2</n><n>3</n></root>", false)]
    public void ContentAndFacets(string instance, bool expected)
    {
        var schema = Compile("<xs:simpleType name='P'><xs:restriction base='xs:int'><xs:minInclusive value='1'/></xs:restriction></xs:simpleType><xs:element name='root'><xs:complexType><xs:sequence><xs:element name='n' type='P'/></xs:sequence></xs:complexType></xs:element>");
        AssertResult(schema.Validate(instance), expected);
    }
    [Theory]
    [InlineData("<root/>", true)] [InlineData("<root n='01.0'/>", true)] [InlineData("<root n='2'/>", false)]
    public void TypedFixedAndDefaults(string xml, bool expected)
    { AssertResult(Compile("<xs:element name='root'><xs:complexType><xs:attribute name='n' type='xs:decimal' fixed='1'/></xs:complexType></xs:element>").Validate(xml), expected); }
    [Theory]
    [InlineData("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>", true)]
    [InlineData("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'>1</root>", false)]
    [InlineData("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xs='http://www.w3.org/2001/XMLSchema' xsi:type='xs:int'>1</root>", true)]
    [InlineData("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xs='http://www.w3.org/2001/XMLSchema' xsi:type='xs:string'>1</root>", false)]
    public void NilAndType(string xml, bool expected)
    { AssertResult(Compile("<xs:element name='root' type='xs:decimal' nillable='true'/>").Validate(xml), expected); }
    [Theory]
    [InlineData("strict", false)] [InlineData("lax", true)] [InlineData("skip", true)]
    public void Wildcards(string policy, bool expected)
    { AssertResult(Compile($"<xs:element name='root'><xs:complexType><xs:sequence><xs:any processContents='{policy}'/></xs:sequence><xs:anyAttribute processContents='{policy}'/></xs:complexType></xs:element>").Validate("<root a='x'><unknown/></root>"), expected); }
    [Theory]
    [InlineData("<root n='1'/>", true)] [InlineData("<root/>", false)] [InlineData("<root n='bad'/>", false)] [InlineData("<root n='1' p='x'/>", false)]
    public void AttributeUses(string xml, bool expected)
    { AssertResult(Compile("<xs:element name='root'><xs:complexType><xs:attribute name='n' type='xs:int' use='required'/><xs:attribute name='p' use='prohibited'/></xs:complexType></xs:element>").Validate(xml), expected); }
    [Theory]
    [InlineData("<root><row id='a'/><row ref='a'/></root>", true)]
    [InlineData("<root><row id='a'/><row id='a'/></root>", false)]
    [InlineData("<root><row ref='missing'/></root>", false)]
    public void IdAndReference(string xml, bool expected)
    { AssertResult(Compile("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='row' maxOccurs='unbounded'><xs:complexType><xs:attribute name='id' type='xs:ID'/><xs:attribute name='ref' type='xs:IDREF'/></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element>").Validate(xml), expected); }
    [Theory]
    [InlineData("key", "<root><row n='1'/><row n='2'/></root>", true)]
    [InlineData("key", "<root><row/></root>", false)]
    [InlineData("unique", "<root><row/><row/></root>", true)]
    [InlineData("unique", "<root><row n='1'/><row n='01'/></root>", false)]
    public void Identity(string kind, string xml, bool expected)
    { AssertResult(Compile($"<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='row' maxOccurs='unbounded'><xs:complexType><xs:attribute name='n' type='xs:int'/></xs:complexType></xs:element></xs:sequence></xs:complexType><xs:{kind} name='K'><xs:selector xpath='row'/><xs:field xpath='@n'/></xs:{kind}></xs:element>").Validate(xml), expected); }
    [Theory]
    [InlineData("1", true)] [InlineData("2", false)]
    public void KeyReference(string reference, bool expected)
    { AssertResult(Compile("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='row'><xs:complexType><xs:attribute name='n' type='xs:int'/></xs:complexType></xs:element><xs:element name='ref'><xs:complexType><xs:attribute name='n' type='xs:int'/></xs:complexType></xs:element></xs:sequence></xs:complexType><xs:key name='K'><xs:selector xpath='row'/><xs:field xpath='@n'/></xs:key><xs:keyref name='R' refer='K'><xs:selector xpath='ref'/><xs:field xpath='@n'/></xs:keyref></xs:element>").Validate($"<root><row n='1'/><ref n='{reference}'/></root>"), expected); }
    [Fact]
    public async Task ConcurrentRunsAndSnapshots()
    {
        var source = XmlDocument.Parse(Wrap("<xs:element name='root' type='xs:int' default='7'/>"), cancellationToken: TestContext.Current.CancellationToken);
        var schema = XmlSchema.Compile(source); var instance = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(() => schema.Validate(instance), TestContext.Current.CancellationToken)));
        Assert.All(results, result => AssertResult(result, true));
        Assert.Equal("", instance.RootElement.GetTextContent());
        source.RootElement.Remove(); AssertResult(schema.Validate("<root>8</root>"), true);
        AssertResult(schema.Validate("<root>bad</root>"), false);
    }
    [Fact]
    public void AllInputFormsLeaveCallerStreamsOpen()
    {
        string source = Wrap("<xs:element name='root' type='xs:int'/>");
        using var schemaStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        using var instanceStream = new MemoryStream("<root>7</root>"u8.ToArray());
        AssertResult(XmlSchema.Compile(schemaStream).Validate(instanceStream), true);
        Assert.True(schemaStream.CanRead); Assert.True(instanceStream.CanRead);
        AssertResult(XmlSchema.Compile(XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken)).Validate(XmlDocument.Parse("<root>7</root>", cancellationToken: TestContext.Current.CancellationToken)), true);
    }
    [Theory]
    [InlineData("<root>")] [InlineData("<!DOCTYPE root><root/>")]
    public void MalformedInstancesAreStopped(string xml)
    { var result = Compile("<xs:element name='root'/>").Validate(xml); Assert.False(result.IsComplete); Assert.False(result.IsValid); Assert.NotEmpty(result.Diagnostics); }
    [Fact]
    public void LimitsAndCancellation()
    {
        var schema = Compile("<xs:element name='root'/>");
        foreach (long cap in new long[] { 1, 7, 14 })
        { var result = schema.Validate("<root/>", new() { MaxOperations = cap }); Assert.False(result.IsComplete); Assert.Contains(result.Diagnostics, d => d.Code == "VALIDATION.Limit"); }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => XmlSchema.Compile(Wrap(""), new() { CancellationToken = cancellation.Token }));
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate("<root/>", new() { CancellationToken = cancellation.Token }));
    }
    internal static void AssertResult(ValidationResult result, bool expected)
    { Assert.True(result.IsComplete); Assert.Equal(expected, result.IsValid); }
}
