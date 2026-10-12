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

    [Theory]
    [InlineData("<n>bad</n>", "XSD.Datatype")]
    [InlineData("<required/>", "XSD.RequiredAttribute")]
    [InlineData("<fixed>2</fixed>", "XSD.Fixed")]
    [InlineData("<container/>", "XSD.Content")]
    [InlineData("<u xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='xs:int'>bad</u>", "XSD.Datatype")]
    [InlineData("<strict><unknown/></strict>", "XSD.UndeclaredElement")]
    public void LaterIndependentErrorsAreAssessed(string later, string code)
    {
        var p = P.Prepare(Root + "<xs:element name='n' type='xs:int'/><xs:element name='required'><xs:complexType><xs:attribute name='n' use='required'/></xs:complexType></xs:element><xs:element name='fixed' type='xs:int' fixed='1'/><xs:element name='container'><xs:complexType><xs:sequence><xs:element name='needed'/></xs:sequence></xs:complexType></xs:element><xs:element name='strict'><xs:complexType><xs:sequence><xs:any processContents='strict'/></xs:sequence></xs:complexType></xs:element>");
        var result = p.Run("<r><u/>" + later + "</r>");
        Assert.True(result.IsComplete); Assert.False(result.IsValid);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal("XSD.Content", result.Diagnostics[0].Code);
        Assert.StartsWith(code, result.Diagnostics[1].Code);
    }
    [Fact]
    public void FirstRejectedChildIsAssessedGloballyAndOpenDescendantsContinue()
    {
        var p = P.Prepare(Root + "<xs:element name='n' type='xs:int'/>");
        foreach (var xml in new[] { "<r><n>bad</n></r>", "<r><u><n>bad</n></u></r>" })
        {
            var result = p.Run(xml); Assert.True(result.IsComplete); Assert.Equal(2, result.Diagnostics.Count);
            Assert.Contains(result.Diagnostics, d => d.Code.StartsWith("XSD.Datatype", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Diagnostics, d => d.Code == "XSD.UndeclaredElement");
        }
    }
    [Fact]
    public void RecoveryNeverInventsALocalDeclarationByName()
    {
        var p = P.Prepare("<xs:element name='r'><xs:complexType><xs:sequence><xs:element name='a' type='xs:int'/></xs:sequence></xs:complexType></xs:element>");
        var result = p.Run("<r><u/><a>bad</a></r>");
        Assert.True(result.IsComplete); Assert.Equal("XSD.Content", Assert.Single(result.Diagnostics).Code);
    }
    [Theory]
    [InlineData("<r><u/><parent><u/></parent></r>", 2)]
    [InlineData("<r><u/><parent/></r>", 2)]
    [InlineData("<r/>", 1)]
    public void EveryFrameHasIndependentFaultAndIncompleteContent(string xml, int errors)
    {
        var p = P.Prepare(Root + "<xs:element name='parent'><xs:complexType><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType></xs:element>");
        var result = p.Run(xml); Assert.True(result.IsComplete); Assert.False(result.IsValid);
        Assert.Equal(errors, result.Diagnostics.Count); Assert.All(result.Diagnostics, d => Assert.Equal("XSD.Content", d.Code));
    }
    [Fact]
    public void InvalidAttributeDoesNotDisableLocalMatchingOrCompletion()
    {
        var p = P.Prepare("<xs:element name='r'><xs:complexType><xs:sequence><xs:element name='a' type='xs:int'/><xs:element name='b'/></xs:sequence><xs:attribute name='needed' use='required'/></xs:complexType></xs:element>");
        var result = p.Run("<r><a>bad</a></r>"); Assert.True(result.IsComplete);
        Assert.Equal(new[] { "XSD.RequiredAttribute", "XSD.Datatype.Lexical", "XSD.Content" }, result.Diagnostics.Select(d => d.Code));
    }
    [Theory]
    [InlineData("<row n='1'/><row n='01'/>", "Duplicate identity key tuple")]
    [InlineData("<row/>", "Key field")]
    [InlineData("<use n='2'/>", "Key reference")]
    public void RealKeyAndKeyrefChecksRunAfterFault(string later, string message)
    {
        var p = P.Prepare("<xs:element name='row'><xs:complexType><xs:attribute name='n' type='xs:int'/></xs:complexType></xs:element><xs:element name='use'><xs:complexType><xs:attribute name='n' type='xs:int'/></xs:complexType></xs:element><xs:element name='r'><xs:complexType><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType><xs:key name='K'><xs:selector xpath='row'/><xs:field xpath='@n'/></xs:key><xs:keyref name='R' refer='K'><xs:selector xpath='use'/><xs:field xpath='@n'/></xs:keyref></xs:element>");
        var result = p.Run("<r><u/>" + later + "</r>"); Assert.True(result.IsComplete);
        Assert.Equal(2, result.Diagnostics.Count); Assert.Equal("XSD.Identity", result.Diagnostics[1].Code);
        Assert.Contains(message, result.Diagnostics[1].Message, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("<id>a</id><id>a</id>", "XSD.DuplicateId")]
    [InlineData("<ref>missing</ref>", "XSD.IdRef")]
    public void RealDocumentIdChecksRunAfterFault(string later, string code)
    {
        var p = P.Prepare(Root + "<xs:element name='id' type='xs:ID'/><xs:element name='ref' type='xs:IDREF'/>");
        var result = p.Run("<r><u/>" + later + "</r>"); Assert.True(result.IsComplete);
        Assert.Equal(2, result.Diagnostics.Count); Assert.Equal(code, result.Diagnostics[1].Code);
        Assert.Single(p.Run("<r><u/><id>a</id><ref>a</ref></r>").Diagnostics);
    }
    [Fact]
    public void OrderedBalancedHooksKeepParentInvalidAndDescendantsValid()
    {
        var hook = new P.RecordingFactory(); var result = P.Prepare(Root).Run("<r><u><v/></u><w/></r>", factory: hook);
        Assert.True(result.IsComplete); Assert.Equal(new[] { "+r", "+u", "+v", "-v", "-u", "+w", "-w", "-r", "complete" }, hook.Events);
        Assert.Equal(new[] { true, true, true, false }, hook.Values.Select(v => v.IsValid));
    }
    [Theory]
    [InlineData(1024, true)] [InlineData(1025, false)]
    public void IndependentDatatypeErrorsStillExhaustDiagnostics(int errors, bool complete)
    {
        var p = P.Prepare(Root + "<xs:element name='n' type='xs:int'/>");
        var result = p.Run("<r><u/>" + string.Concat(Enumerable.Repeat("<n>bad</n>", errors - 1)) + "</r>", new() { Limits = new() { Diagnostics = 1024 } });
        Assert.Equal(complete, result.IsComplete); Assert.False(result.IsValid); Assert.Equal(1024, result.Diagnostics.Count);
        Assert.Equal(complete ? "XSD.Datatype.Lexical" : "VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Fact]
    public async Task RepeatAndConcurrentRunsIsolateFaultedCursors()
    {
        var p = P.Prepare(Root); Assert.Single(p.Run("<r><u/><u/></r>").Diagnostics); Assert.True(p.Run("<r><a/></r>").IsValid);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() => p.Run(i % 2 == 0 ? "<r><u/><u/></r>" : "<r><a/></r>"))));
        for (int i = 0; i < results.Length; i++) { Assert.True(results[i].IsComplete); Assert.Equal(i % 2 != 0, results[i].IsValid); Assert.Equal(i % 2 == 0 ? 1 : 0, results[i].Diagnostics.Count); }
    }
}
