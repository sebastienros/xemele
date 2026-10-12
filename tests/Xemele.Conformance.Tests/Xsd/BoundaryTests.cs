using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Conformance.Tests.Xsd;
public sealed class BoundaryTests
{
    private const string Schema = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='r' type='xs:int'/></xs:schema>";
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PublicTransportsKeepCompleteInvalidityAndCallerOwnership(int transport)
    {
        using var schemaStream = new MemoryStream(Encoding.UTF8.GetBytes(Schema));
        var schemaDoc = XmlDocument.Parse(Schema); string before = schemaDoc.ToXmlString();
        var schema = transport switch { 0 => XmlSchema.Compile(Schema), 1 => XmlSchema.Compile(schemaDoc), _ => XmlSchema.Compile(schemaStream) };
        using var instance = new MemoryStream(Encoding.UTF8.GetBytes("<r>bad</r>"));
        var document = XmlDocument.Parse("<r>bad</r>"); string original = document.ToXmlString();
        var result = transport switch { 0 => schema.Validate("<r>bad</r>"), 1 => schema.Validate(document), _ => schema.Validate(instance) };
        Assert.True(result.IsComplete); Assert.False(result.IsValid); Assert.NotEmpty(result.Diagnostics);
        Assert.True(schemaStream.CanRead); Assert.True(instance.CanRead); Assert.Equal(before,schemaDoc.ToXmlString()); Assert.Equal(original,document.ToXmlString());
    }
    [Fact]
    public void WorkRefusalDoesNotMeanSemanticInvalidity()
    {
        var compile = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Schema,new() { MaxOperations=1 }));
        Assert.Contains(compile.Diagnostics,d=>d.Code.Contains("Limit",StringComparison.Ordinal));
        var result = XmlSchema.Compile(Schema).Validate("<r>3</r>",new() { MaxOperations=1 });
        Assert.False(result.IsComplete); Assert.False(result.IsValid); Assert.Contains(result.Diagnostics,d=>d.Severity==XmlDiagnosticSeverity.Fatal);
    }
    [Fact]
    public void CancellationRemainsAnOperationFailure()
    {
        using var cts = new CancellationTokenSource();cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(()=>XmlSchema.Compile(Schema,new() { CancellationToken=cts.Token }));
        var schema=XmlSchema.Compile(Schema);Assert.ThrowsAny<OperationCanceledException>(()=>schema.Validate("<r>3</r>",new() { CancellationToken=cts.Token }));
    }
}
