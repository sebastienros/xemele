using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Conformance.Tests.RelaxNg;

public sealed class BoundaryTests
{
    private const string Source = "<element xmlns='http://relaxng.org/ns/structure/1.0' name='r'><empty/></element>";
    [Theory]
    [InlineData("")][InlineData("<r>")][InlineData("<r/><other/>")][InlineData("<!DOCTYPE r><r/>")][InlineData("<r>&unknown;</r>")]
    public void SyntaxStoppedIsDistinctFromCompleteInvalidAndStreamsRemainOpen(string xml)
    {
        var schema = RelaxNgSchema.Compile(Source, new() { CancellationToken = TestContext.Current.CancellationToken });
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        foreach (var result in new[] { schema.Validate(xml, new() { CancellationToken = TestContext.Current.CancellationToken }), schema.Validate(input, new() { CancellationToken = TestContext.Current.CancellationToken }) })
        { Assert.False(result.IsValid); Assert.False(result.IsComplete); Assert.Contains(result.Diagnostics, d => d.Severity == XmlDiagnosticSeverity.Fatal && d.Code == "SCHEMA.Syntax"); }
        Assert.True(input.CanRead);
        var invalid = schema.Validate("<r>nonempty</r>", new() { CancellationToken = TestContext.Current.CancellationToken });
        Assert.True(invalid.IsComplete); Assert.False(invalid.IsValid); Assert.Contains(invalid.Diagnostics, d => d.Code == "RNG.Instance");
    }
    [Fact]
    public void OperationStoppedAndRecoveredDomRemainIncomplete()
    {
        var schema = RelaxNgSchema.Compile(Source, new() { CancellationToken = TestContext.Current.CancellationToken });
        var result = schema.Validate("<r/>", new() { MaxOperations = 1, CancellationToken = TestContext.Current.CancellationToken });
        Assert.False(result.IsValid); Assert.False(result.IsComplete); Assert.Contains(result.Diagnostics, d => d.Severity == XmlDiagnosticSeverity.Fatal);
        var partial = XmlDocument.ParseWithDiagnostics("<r><![CDATA[broken", new() { Recover = true }, TestContext.Current.CancellationToken).Value!;
        string before = partial.ToXmlString(cancellationToken: TestContext.Current.CancellationToken);
        var stopped = schema.Validate(partial, new() { CancellationToken = TestContext.Current.CancellationToken });
        Assert.False(stopped.IsComplete); Assert.False(stopped.IsValid); Assert.Equal("SCHEMA.IncompleteInput", Assert.Single(stopped.Diagnostics).Code);
        Assert.Equal(before, partial.ToXmlString(cancellationToken: TestContext.Current.CancellationToken));
    }
    [Fact]
    public void SemanticRejectionRetainsCallerDomSourceLocation()
    {
        var schema = RelaxNgSchema.Compile(Source, new() { CancellationToken = TestContext.Current.CancellationToken });
        using var stream = new MemoryStream("<?xml version='1.0'?>\r\n<r>é</r>"u8.ToArray());
        var document = XmlDocument.Load(stream, null, sourceUri: new("urn:rng-corpus:instance"), cancellationToken: TestContext.Current.CancellationToken);
        string before = document.ToXmlString(cancellationToken: TestContext.Current.CancellationToken);
        var result = schema.Validate(document, new() { CancellationToken = TestContext.Current.CancellationToken }); Assert.True(result.IsComplete); Assert.False(result.IsValid);
        var location = Assert.Single(result.Diagnostics).Location; Assert.Equal(new Uri("urn:rng-corpus:instance"), location.SourceUri); Assert.Equal(2, location.LineNumber); Assert.Equal(1, location.LinePosition); Assert.Equal(23, location.ByteOffset);
        Assert.True(stream.CanRead); Assert.Equal(before, document.ToXmlString(cancellationToken: TestContext.Current.CancellationToken));
    }
}
