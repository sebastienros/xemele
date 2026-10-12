using Xemele.Validation;
using Xemele.XPath;
using Xunit;

namespace Xemele.Tests.M8SchValidate;

public sealed class LocationTests
{
    [Fact]
    public void SubjectsOverrideOnlyReportedLocationAndResultsDetachFromInstance()
    {
        var schema = EvaluationTests.Compile("<pattern><rule context='item' subject='child'><report test='true()'>rule <name/></report>" +
            "<report test='true()' subject='@code' diagnostics='d' properties='p'>check <name/></report><report test='true()' subject='missing'>empty <name/></report></rule></pattern>" +
            "<diagnostics><diagnostic id='d'>D <value-of select='name()'/></diagnostic></diagnostics><properties><property id='p'>P <name/></property></properties>");
        var document = XmlDocument.Parse("<root>\n <item code='x'><child/></item>\n</root>", cancellationToken: TestContext.Current.CancellationToken); long revision = document.ContentRevision;
        var result = schema.Validate(document); Assert.Equal(revision, document.ContentRevision);
        Assert.Equal(3, result.Findings.Count); var context = "/*[local-name()='root' and namespace-uri()=''][1]/*[local-name()='item' and namespace-uri()=''][1]";
        Assert.Equal(context, Assert.Single(result.FiredRules).Location);
        Assert.Equal(context + "/*[local-name()='child' and namespace-uri()=''][1]", result.Findings[0].Location);
        Assert.Equal(context + "/@*[local-name()='code' and namespace-uri()='']", result.Findings[1].Location);
        Assert.Equal("check item", result.Findings[1].Text); Assert.Equal("D item", Assert.Single(result.Findings[1].Diagnostics).Text);
        Assert.Equal("P item", Assert.Single(result.Findings[1].Properties).Text);
        Assert.Equal("", result.Findings[2].Location); Assert.Equal(default, result.Findings[2].SourceLocation);
        foreach (var finding in result.Findings) { Assert.Equal(context, finding.ContextLocation); Assert.Equal(2, finding.ContextSourceLocation.LineNumber); }
        Assert.True(result.Findings[1].SourceLocation.LinePosition > result.Findings[1].ContextSourceLocation.LinePosition);
        document.RootElement.SetTextContent("deleted"); Assert.Equal(context, result.Findings[1].ContextLocation); Assert.Equal("check item", result.Findings[1].Text);
    }

    [Theory]
    [InlineData("1")] [InlineData("true()")] [InlineData("&quot;text&quot;")]
    public void NonNodeSetSubjectsAreIncompleteXPathErrors(string subject)
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()' subject='" + subject + "'>x</report>"));
        var result = schema.Validate("<item/>"); Assert.False(result.Validation.IsComplete); Assert.False(result.Validation.IsValid);
        Assert.Equal("SCH.XPath", Assert.Single(result.Validation.Diagnostics).Code); Assert.Empty(result.Findings);
    }

    [Fact]
    public void PathsUseExpandedNamesPositionsAndMixedQuoteLiterals()
    {
        var schema = EvaluationTests.Compile("<ns prefix='s' uri='urn:&apos;&quot;both'/>" + EvaluationTests.Pattern("<report test='true()'>x</report>", "s:item | @code | text() | comment() | processing-instruction()"));
        var document = XmlDocument.Parse("<r xmlns:s='urn:&apos;&quot;both'><s:item code='x'>a<!--one--><?x a?>b<!--two--><?y b?></s:item><s:item/></r>", cancellationToken: TestContext.Current.CancellationToken);
        var result = schema.Validate(document); Assert.Equal(9, result.Findings.Count);
        Assert.Contains("concat(", result.Findings[0].Location); Assert.EndsWith("][2]", result.Findings[^1].Location);
        foreach (var finding in result.Findings)
        {
            var expression = XPathExpression.Compile(finding.Location, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(expression.Evaluate(document.DocumentNode).AsNodeSet());
        }
    }

    [Fact]
    public void ByteLocationsAndSourceUriSurviveOwnedParsingWhileProgrammaticNodesAreUnknown()
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<root>\n<item code='x'/></root>"));
        var uri = new Uri("https://example.test/instance.xml");
        var document = XmlDocument.Load(input, null, uri, TestContext.Current.CancellationToken);
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()' subject='@code'>x</report>"));
        var finding = Assert.Single(schema.Validate(document).Findings); Assert.Equal(uri, finding.SourceLocation.SourceUri);
        Assert.Equal(2, finding.SourceLocation.LineNumber); Assert.NotNull(finding.SourceLocation.ByteOffset);
        document.RootElement.FirstChild.NextSibling.SetAttribute("code", "changed");
        var changed = Assert.Single(schema.Validate(document).Findings); Assert.Equal(0, changed.SourceLocation.LineNumber); Assert.Null(changed.SourceLocation.ByteOffset);
    }
    [Fact]
    public void NamespaceSubjectUsesItsDeclarationSourceAndKeepsContextFocus()
    {
        var schema = EvaluationTests.Compile(EvaluationTests.Pattern("<report test='true()' subject='namespace::p'><name/></report>"));
        var document = XmlDocument.Parse("<item xmlns:p='urn:p'/>", cancellationToken: TestContext.Current.CancellationToken);
        var finding = Assert.Single(schema.Validate(document).Findings);
        Assert.Equal("item", finding.Text); Assert.EndsWith("/namespace::*[name()='p']", finding.Location);
        Assert.True(finding.SourceLocation.LinePosition > finding.ContextSourceLocation.LinePosition);
        Assert.Single(XPathExpression.Compile(finding.Location, cancellationToken: TestContext.Current.CancellationToken).Evaluate(document.DocumentNode).AsNodeSet());
    }
}
