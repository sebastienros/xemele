using Xemele.Validation;
using Xunit;
using P = Xemele.Tests.M8XsdValidate.EvaluatorTests;
namespace Xemele.Tests.M8XsdContentFailureAssessment;

public sealed class ContinuationTests
{
    internal const string Root = "<xs:element name='r'><xs:complexType><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType></xs:element>";
    [Theory]
    [InlineData(1025)] [InlineData(2048)]
    public void HarmlessSiblingsFinishWithOneOriginalLocatedError(int count)
    {
        var p = P.Prepare(Root);
        var context = new SchemaCompileContext(new() { BaseUri = new("https://example.test/instance.xml") });
        var doc = context.Parse("<r>\n" + string.Concat(Enumerable.Repeat("<u/>\n", count)) + "</r>").Document;
        var result = p.Run(doc, new() { Limits = new() { Diagnostics = 1024 } });
        Assert.True(result.IsComplete, $"diagnostics={result.Diagnostics.Count}; last={result.Diagnostics[^1].Code}: {result.Diagnostics[^1].Message}"); Assert.False(result.IsValid);
        var error = Assert.Single(result.Diagnostics); Assert.Equal("XSD.Content", error.Code);
        Assert.Equal(doc.OriginalLocation(doc.RootElement.FirstChild.NextSibling), error.Location);
        Assert.Equal(2, error.Location.LineNumber); Assert.Equal(1, error.Location.LinePosition);
        Assert.Equal(new Uri("https://example.test/instance.xml"), error.Location.SourceUri);
    }
}
