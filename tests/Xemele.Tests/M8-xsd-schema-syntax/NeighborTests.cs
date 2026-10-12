using Xemele.Validation;
using Xunit;
using Ledger = Xemele.Tests.M8XsdIdentity.OperationTests.Ledger;
namespace Xemele.Tests.M8XsdSchemaSyntax;

public sealed class NeighborTests
{
    internal static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + body + "</xs:schema>";
    [Theory]
    [InlineData("en")][InlineData("EN-us")][InlineData("abcdefgh-12345678")][InlineData(" \t en-US \r\n")]
    public void DocumentationLanguageUsesCollapsedXsLanguage(string language)
    { Assert.NotNull(XmlSchema.Compile(Wrap($"<xs:annotation><xs:documentation xml:lang='{language}'>根<foreign xmlns='urn:payload' xml:lang='' minOccurs='bad'/></xs:documentation><xs:appinfo><xs:unknown/></xs:appinfo></xs:annotation>"))); }
    [Theory]
    [InlineData("")][InlineData(" \t\r\n")][InlineData("en US")][InlineData("1en")][InlineData("é")]
    [InlineData("en-")][InlineData("-en")][InlineData("en--US")][InlineData("abcdefghi")][InlineData("en-123456789")]
    public void InvalidDocumentationLanguageIsSyntaxRejection(string language)
    {
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:annotation><xs:documentation xml:lang='{language}'/></xs:annotation>")));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
        Assert.DoesNotContain(error.Diagnostics, d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal));
    }
    [Fact]
    public void AbsentLanguageAndOpaqueAppinfoAreLegal()
    { Assert.NotNull(XmlSchema.Compile(Wrap("<xs:annotation><xs:documentation>text</xs:documentation><xs:appinfo xml:lang=''><xs:unknown/></xs:appinfo></xs:annotation>"))); }
    [Theory]
    [InlineData("minOccurs='1'")][InlineData("maxOccurs='1'")][InlineData("minOccurs='0'")][InlineData("ref='G'")]
    public void GlobalGroupRejectsLocalAttributes(string attributes)
    { Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:group name='G' {attributes}><xs:sequence><xs:element name='row'/></xs:sequence></xs:group>"))); }
    [Fact]
    public void GlobalDefinitionAndLocalOccurrencesRemainLegal()
    { Assert.NotNull(XmlSchema.Compile(Wrap("<xs:group name='G'><xs:sequence><xs:element name='row'/></xs:sequence></xs:group><xs:element name='root'><xs:complexType><xs:group ref='G' minOccurs='0' maxOccurs='unbounded'/></xs:complexType></xs:element>"))); }
    [Theory]
    [InlineData(".", ".")][InlineData(".//child::row", "attribute::id")][InlineData("child::row", "./attribute::id")]
    public void LegalRestrictedPathsPublishThroughFacade(string selector, string field)
    { Assert.NotNull(XmlSchema.Compile(Identity(selector, field))); }
    [Theory]
    [InlineData("self::node()", "@id")][InlineData("row/self::node()", "@id")]
    [InlineData("descendant-or-self::node()/child::row", "@id")]
    [InlineData("row", "self::node()")][InlineData("row", "descendant-or-self::node()/self::node()")]
    public void ExplicitSelfAxesRejectThroughFacade(string selector, string field)
    { var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Identity(selector, field))); Assert.Contains(error.Diagnostics, d => d.Code == "XSD.Identity"); }
    private static string Identity(string selector, string field) => Wrap($"<xs:element name='root'><xs:unique name='K'><xs:selector xpath='{selector}'/><xs:field xpath='{field}'/></xs:unique></xs:element>");
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void LanguageWhitespaceScanHasExactWorkAndCancellation(bool trailing)
    {
        string language = trailing ? "en" + new string(' ',8192) : new string(' ',8192) + "en";
        var doc = XmlDocument.Parse(Wrap($"<xs:annotation><xs:documentation xml:lang='{language}'/></xs:annotation>"));
        var compiler = new XmlSchemaCompiler(); var observed = new Ledger();
        Assert.NotNull(compiler.Compile(doc,new(),observed)); Assert.True(observed.Work >= 8192);
        Assert.NotNull(compiler.Compile(doc,new(),new Ledger(observed.Work,observed.Components)));
        var shortWork = new Ledger(observed.Work-1); Assert.Throws<SchemaCompileException>(() => compiler.Compile(doc,new(),shortWork)); Assert.False(shortWork.IsComplete);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancel = new Ledger(token:cts.Token) { OnCharge=n=>{if(n==512)cts.Cancel();},Limits=new() { EvaluationWork=null } };
        Assert.Throws<OperationCanceledException>(() => compiler.Compile(doc,new(),cancel)); Assert.Equal(512,cancel.Calls); Assert.Empty(cancel.Diagnostics);
    }
}
