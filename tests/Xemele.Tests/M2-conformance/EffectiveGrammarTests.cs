using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.ConformanceGrammar;

public sealed class EffectiveGrammarTests
{
    // Expected names/values are authored from Fifth Edition [4]/[4a], XML
    // sections 2.11/3.3.3/4.4 and Namespaces 1.0 sections 5/6/7.
    [Theory]
    [InlineData("<\u037f a='&#x9;&#xA;&#xD;'/>", "\u037f", "\t\n\r", "")]
    [InlineData("<\udb7f\udfff/>", "\udb7f\udfff", null, "")]
    [InlineData("<_\u00b7/>", "_\u00b7", null, "")]
    [InlineData("<?xml version='1.10'?><r>\u0085\u2028\r\n&#xD;</r>", "r", null, "\u0085\u2028\n\r")]
    [InlineData("<!DOCTYPE r [<!ENTITY e '&#x9;'><!ATTLIST r a NMTOKENS '  x   y  '>]><r>&e;</r>", "r", "x y", "\t")]
    [InlineData("<!DOCTYPE r [<!ENTITY e '&#x9;'><!ATTLIST r a CDATA '&e;&#x9;'>]><r/>", "r", " \t", "")]
    [InlineData("<!DOCTYPE r [<!ELEMENT r EMPTY><!ATTLIST r a (x|y) 'z'>]><r>nonvalidating</r>", "r", "z", "nonvalidating")]
    public void AuthoredModelsHoldAcrossEveryCharacterAndSurrogateSplit(string input, string local, string? attribute, string text)
    {
        for (var split = 0; split <= input.Length; split++)
        {
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
            using var core = new XmlParserCore(options);
            var sink = new DomSink(options);
            var first = core.Append(input.AsSpan(0, split), [], false, sink, TestContext.Current.CancellationToken);
            Assert.Equal(split, first.Consumed);
            Assert.Equal(ScanStep.NeedInput, first.Step);
            var last = core.Append(input.AsSpan(split), [], true, sink, TestContext.Current.CancellationToken);
            Assert.Equal(input.Length - split, last.Consumed);
            Assert.Equal(ScanStep.Completed, last.Step);
            Assert.Null(last.Diagnostic);
            Assert.Equal(local, sink.Document.RootElement.LocalName);
            Assert.Equal(attribute, sink.Document.RootElement.GetAttribute("a"));
            Assert.Equal(text, sink.Document.RootElement.GetTextContent());
        }
    }

    [Theory]
    [InlineData("<\u037e/>")]
    [InlineData("<\udb80\udc00/>")]
    [InlineData("<\u00b7/>")]
    [InlineData("<r xmlns:p=''/>")]
    [InlineData("<r xmlns:a='urn:x' xmlns:b='urn:x' a:n='1' b:n='2'/>")]
    [InlineData("<?xml version='1.1'?><r>&#x1F;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY p:q 'unused'>]><r/>")]
    [InlineData("<!DOCTYPE r [<!NOTATION p:q SYSTEM 'unused'>]><r/>")]
    public void MandatoryEffectiveProfileViolationsReject(string input)
    {
        Assert.False(XmlDocument.TryParse(input, out var document, out var diagnostics,
            new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, TestContext.Current.CancellationToken));
        Assert.Null(document);
        Assert.NotEmpty(diagnostics);
    }

    [Fact]
    public void UnresolvedReferenceHasAnOwnedAnchorAndNeverBecomesReplacementText()
    {
        var document = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>before&unknown;<c/>after</r>",
            new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, TestContext.Current.CancellationToken);
        var reference = Assert.Single(document.UnresolvedReferences);
        Assert.Equal("unknown", reference.Name);
        Assert.Equal("&unknown;", reference.LexicalReference);
        Assert.Equal(document.RootElement, reference.Parent);
        Assert.Equal(1, reference.ChildBoundary);
        Assert.Equal("before", reference.PreviousSibling.Value);
        Assert.Equal(0, reference.Sequence);
        Assert.Throws<InvalidOperationException>(() => document.RootElement.GetTextContent());
    }

    [Fact]
    public void IncompleteDefaultsKeepSymbolicStageOnePartsAndApplicationIdentity()
    {
        var document = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a NMTOKENS '  before &unknown; after&#9;'>]><r/>",
            new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, TestContext.Current.CancellationToken);
        var declaration = Assert.Single(document.IncompleteAttributeDefaults);
        var applied = Assert.Single(document.IncompleteAttributeValues);
        Assert.Same(declaration, applied.Default);
        Assert.Same(declaration.Parts, applied.Parts);
        Assert.Equal("  before &unknown; after&#9;", declaration.LexicalValue);
        Assert.True(declaration.IsTokenized);
        Assert.False(declaration.IsExternal);
        Assert.Equal("  before ", Assert.IsType<XmlKnownAttributeText>(declaration.Parts[0]).Text);
        var unknown = Assert.IsType<XmlUnknownAttributeReference>(declaration.Parts[1]);
        Assert.Equal("unknown", unknown.Name);
        Assert.Equal("&unknown;", unknown.LexicalReference);
        Assert.Equal(" after\t", Assert.IsType<XmlKnownAttributeText>(declaration.Parts[2]).Text);
        Assert.Throws<InvalidOperationException>(() => document.RootElement.GetAttribute("a"));
    }
}
