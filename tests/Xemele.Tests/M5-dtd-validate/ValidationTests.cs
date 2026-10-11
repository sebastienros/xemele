using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Xemele.Validation;
using Xunit;
using Document = Xemele.XmlDocument;

namespace Xemele.Tests.DtdValidation;

public sealed class ValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DtdSchema Schema(string body) => DtdSchema.Compile(body, cancellationToken: Ct);
    private static Document Parse(string xml) => Document.Parse(xml, cancellationToken: Ct);
    private static ValidationResult Check(string body, string xml) => Schema(body).Validate(Parse(xml), new() { CancellationToken = Ct });

    [Theory]
    [InlineData("EMPTY", "", true)] [InlineData("EMPTY", " ", false)]
    [InlineData("EMPTY", "<!--c--><?pi v?>", false)] [InlineData("EMPTY", "<a/>", false)]
    [InlineData("ANY", "hello<a/>β😀", true)] [InlineData("ANY", "<unknown/>", false)]
    [InlineData("(#PCDATA)", "β😀", true)] [InlineData("(#PCDATA)", "<a/>", false)]
    [InlineData("(#PCDATA|a|b)*", "x<b/><a/>z", true)]
    [InlineData("(a,b?)", "<a/>", true)] [InlineData("(a,b?)", "<a/><b/>", true)]
    [InlineData("(a,b?)", "<b/><a/>", false)] [InlineData("(a,b?)", "", false)]
    [InlineData("((a|b)+,c*)", "<b/><a/><c/><c/>", true)]
    [InlineData("(a*)", "<![CDATA[ ]]>", false)] [InlineData("(a*)", "<![CDATA[]]>", false)]
    [InlineData("(a*)", "<![CDATA[text]]>", false)]
    [InlineData("(a*)", " \r\n<a/>\t", true)] [InlineData("(a*)", "text", false)]
    public void ContentKindsAndOccurrenceMatchBclOracle(string content, string body, bool valid)
    {
        string dtd = "<!ELEMENT r " + content + "><!ELEMENT a EMPTY><!ELEMENT b EMPTY><!ELEMENT c EMPTY>";
        string xml = "<r>" + body + "</r>";
        var result = Check(dtd, xml);
        Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
        Assert.Equal(valid, BclValid(dtd, xml));
    }

    [Theory]
    [InlineData("CDATA", "any β😀", true)]
    [InlineData("ID", "id", true)] [InlineData("ID", " two words", false)]
    [InlineData("IDREF", "id", true)] [InlineData("IDREF", "unknown", false)]
    [InlineData("IDREFS", "id other", true)] [InlineData("IDREFS", "id  other", false)]
    [InlineData("IDREFS", "", false)]
    [InlineData("NMTOKEN", "1:a", true)] [InlineData("NMTOKEN", "two words", false)]
    [InlineData("NMTOKENS", "a 1:a β", true)] [InlineData("NMTOKENS", " a", false)]
    [InlineData("(one|two)", "two", true)] [InlineData("(one|two)", "THREE", false)]
    [InlineData("ENTITY", "image", true)] [InlineData("ENTITY", "parsed", false)]
    [InlineData("ENTITIES", "image image", true)] [InlineData("ENTITIES", "image missing", false)]
    [InlineData("NOTATION (n)", "n", true)] [InlineData("NOTATION (n)", "missing", false)]
    public void AllAttributeTypesIdsReferencesAndEntityNotationConstraints(string type, string value, bool valid)
    {
        string dtd = "<!NOTATION n SYSTEM 'image'><!ENTITY image SYSTEM 'image' NDATA n><!ENTITY parsed 'text'><!ELEMENT r ANY><!ELEMENT a EMPTY><!ATTLIST r value " + type + " #IMPLIED><!ATTLIST a id ID #IMPLIED>";
        var document = Parse("<r><a id='id'/><a id='other'/></r>");
        document.RootElement.SetAttribute("value", value);
        // ID case must not duplicate the child id.
        if (type == "ID") document.RootElement.FirstChild.Remove();
        var result = Schema(dtd).Validate(document);
        Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("#REQUIRED", false, false)] [InlineData("#IMPLIED", false, true)]
    [InlineData("'default'", false, true)] [InlineData("#FIXED 'default'", false, true)]
    [InlineData("#FIXED 'default'", true, false)]
    public void MissingDefaultsAreAllowedWithoutAugmentationAndRequiredFixedAreChecked(string declaration, bool actual, bool valid)
    {
        var document = Parse("<r/>"); if (actual) document.RootElement.SetAttribute("a", "other");
        long before = document.ContentRevision;
        var result = Schema("<!ELEMENT r EMPTY><!ATTLIST r a CDATA " + declaration + ">").Validate(document);
        Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
        Assert.Equal(before, document.ContentRevision);
        Assert.Equal(actual ? "other" : null, document.RootElement.GetAttribute("a"));
    }

    [Fact]
    public void ParsedDefaultsAndFixedValuesParticipateAsActualAttributes()
    {
        const string dtd = "<!ELEMENT r EMPTY><!ATTLIST r a NMTOKENS #FIXED ' one   two ' b CDATA 'default'>";
        var document = Document.Parse("<!DOCTYPE r [" + dtd + "]><r/>", new() { DtdProcessing = XmlDtdProcessing.Parse }, Ct);
        var schema = DtdSchema.FromDocument(document);
        Assert.True(schema.Validate(document).IsValid); Assert.Equal("one two", document.RootElement.GetAttribute("a"));
        document.RootElement.SetAttribute("a", "two one");
        Assert.Contains(schema.Validate(document).Diagnostics, d => d.Code == "DTD.FixedAttribute" && d.Location == default);
    }

    [Theory]
    [InlineData("<!ELEMENT p:r EMPTY><!ATTLIST p:r xmlns:p CDATA #REQUIRED p:a CDATA #IMPLIED>", true)]
    [InlineData("<!ELEMENT r EMPTY><!ATTLIST r xmlns:p CDATA #REQUIRED p:a CDATA #IMPLIED>", false)]
    [InlineData("<!ELEMENT p:r EMPTY><!ATTLIST p:r p:a CDATA #IMPLIED>", false)]
    public void QNameMatchingAndNamespaceDeclarationsFollowDtdAttributeRules(string dtd, bool valid)
    {
        var result = Check(dtd, "<p:r xmlns:p='urn:p' p:a='v'/>");
        Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void DefaultNamespaceDeclarationHasXmlnsLexicalAttributeName()
    {
        Assert.True(Check("<!ELEMENT r EMPTY><!ATTLIST r xmlns CDATA #REQUIRED>", "<r xmlns='urn:r'/>").IsValid);
        Assert.False(Check("<!ELEMENT r EMPTY>", "<r xmlns='urn:r'/>").IsValid);
    }

    [Fact]
    public void FromDocumentBindsDoctypeRootAndCompileUsesInstanceDoctypeWhenPresent()
    {
        var document = Document.Parse("<!DOCTYPE different [<!ELEMENT r EMPTY><!ELEMENT different EMPTY>]><r/>", new() { DtdProcessing = XmlDtdProcessing.Parse }, Ct);
        Assert.False(DtdSchema.FromDocument(document).Validate(document).IsValid);
        Assert.False(Schema("<!ELEMENT r EMPTY>").Validate(document).IsValid);
        Assert.True(Schema("<!ELEMENT r EMPTY>").Validate(Parse("<r/>")).IsValid);
        Assert.Throws<InvalidOperationException>(() => DtdSchema.FromDocument(Parse("<r/>")));
        Assert.False(Schema("<!ELEMENT r EMPTY>").Validate(new Document()).IsValid);
        Assert.False(Schema("<!ELEMENT a EMPTY>").Validate(Parse("<r/>")).IsValid);
    }

    [Fact]
    public void DuplicateIdAndUnresolvedIdrefAreCompleteInvalidAndSchemasAreReusable()
    {
        var schema = Schema("<!ELEMENT r (a*)><!ELEMENT a EMPTY><!ATTLIST a id ID #IMPLIED ref IDREFS #IMPLIED>");
        var duplicate = schema.Validate(Parse("<r><a id='same'/><a id='same' ref='same missing'/></r>"));
        Assert.True(duplicate.IsComplete); Assert.False(duplicate.IsValid);
        Assert.Contains(duplicate.Diagnostics, d => d.Code == "DTD.DuplicateId"); Assert.Contains(duplicate.Diagnostics, d => d.Code == "DTD.IdRef");
        Assert.True(schema.Validate(Parse("<r><a ref='later'/><a id='later'/></r>")).IsValid);
        Assert.True(schema.Validate(Parse("<r/>")).IsValid);
    }

    [Theory]
    [InlineData("<!ELEMENT r EMPTY><!ELEMENT r ANY>", "DTD.DuplicateElement")]
    [InlineData("<!NOTATION n SYSTEM 'n'><!NOTATION n SYSTEM 'm'>", "DTD.DuplicateNotation")]
    [InlineData("<!ELEMENT r (a|a)>", "DTD.AmbiguousContent")]
    [InlineData("<!ELEMENT r (", "DTD.Syntax")]
    public void CompilationRejectsDeclarationValidityAndSyntax(string dtd, string code)
    {
        var exception = Assert.Throws<XmlParseException>(() => Schema(dtd));
        Assert.Equal(code, exception.Diagnostics[0].Code);
    }

    [Fact]
    public void FromDocumentThrowsOriginalRetainedDuplicateDiagnosticBeforeCompiler()
    {
        var document = Document.Parse("<!DOCTYPE r [<!ELEMENT r EMPTY>\n<!ELEMENT r ANY>]><r/>", new() { DtdProcessing = XmlDtdProcessing.Parse }, Ct);
        var exception = Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(document));
        Assert.Same(document.Dtd!.DeclarationValidityDiagnostic, Assert.Single(exception.Diagnostics));
        Assert.Equal(2, exception.Diagnostics[0].Location.LineNumber);
    }

    [Theory]
    [InlineData("<!DOCTYPE r [<!ENTITY % missing ''>%missing;]><r>&unknown;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY % missing ''>%missing;]><r a='&unknown;'/>")]
    public void IncompleteEntityAndAttributeSidecarsNeverBecomeValidEmptyContent(string xml)
    {
        var document = Document.Parse(xml, new() { DtdProcessing = XmlDtdProcessing.Parse }, Ct);
        var result = Schema("<!ELEMENT r ANY><!ATTLIST r a CDATA #IMPLIED>").Validate(document);
        Assert.False(result.IsComplete); Assert.False(result.IsValid);
        Assert.Equal("DTD.IncompleteInput", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void RecoveryAndMissingStandaloneProvenanceAreIncomplete()
    {
        var document = Document.ParseWithDiagnostics("<r>&#0;</r>", new() { Recover = true }, Ct).Value!;
        Assert.False(Schema("<!ELEMENT r EMPTY>").Validate(document).IsComplete);
        var manual = Parse("<r/>"); manual.SetValidationDeclaration(true, default);
        Assert.Equal("DTD.IncompleteInput", Assert.Single(Schema("<!ELEMENT r EMPTY>").Validate(manual).Diagnostics).Code);
    }

    [Fact]
    public void MutationRevisionGuardRejectsValueChangeAndExhaustedRevision()
    {
        var document = Parse("<r>x</r>");
        var compiled = DtdCompiler.Compile(document.Dtd ?? new([], [], [], [], []), new());
        var run = new DtdEvaluator(compiled, null, document, new());
        var text = document.RootElement.FirstChild; text.Value = "y";
        Assert.Throws<InvalidOperationException>(() => run.Run());
        var field = typeof(Document).GetField("<ContentRevision>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(document, -1L);
        Assert.Throws<InvalidOperationException>(() => Schema("<!ELEMENT r ANY>").Validate(document));
    }

    [Fact]
    public async Task ImmutableSchemaSupportsIndependentConcurrentOperations()
    {
        var schema = Schema("<!ELEMENT r (a*)><!ELEMENT a EMPTY><!ATTLIST a id ID #IMPLIED>");
        var documents = Enumerable.Range(0, 16).Select(_ => Parse("<r><a id='id'/></r>")).ToArray();
        var results = await Task.WhenAll(documents.Select(d => Task.Run(() => schema.Validate(d), Ct)));
        Assert.All(results, result => Assert.True(result.IsValid));
    }

    [Fact]
    public void DiagnosticsAreBoundedAndOperationLimitRefusalIsIncomplete()
    {
        var schema = Schema("<!ELEMENT r ANY>"); var document = Parse("<r><a/><b/><c/></r>");
        foreach (int cap in new[] { 1, 2, 3 })
        {
            var result = schema.Validate(document, new() { Limits = new() { Diagnostics = cap } });
            if (cap == 3) { Assert.True(result.IsComplete); Assert.False(result.IsValid); }
            else { Assert.False(result.IsComplete); Assert.Equal("VALIDATION.Limit", result.Diagnostics[^1].Code); }
            Assert.Equal(cap, result.Diagnostics.Count);
        }
        var low = schema.Validate(document, new() { MaxOperations = 1 });
        Assert.False(low.IsComplete); Assert.Equal("VALIDATION.Limit", Assert.Single(low.Diagnostics).Code);
        var min = schema.Validate(document, new() { MaxOperations = 1000, Limits = new() { EvaluationWork = 1 } });
        Assert.False(min.IsComplete);
        Assert.True(schema.Validate(Parse("<r/>"), new() { MaxOperations = null, Limits = new() { EvaluationWork = null } }).IsValid);
    }

    [Fact]
    public void DepthMemberScalarAndTokenQuotasRejectOneOver()
    {
        var schema = Schema("<!ELEMENT r ANY><!ELEMENT a ANY><!ATTLIST r x CDATA #IMPLIED xmlns:p CDATA #IMPLIED>");
        Assert.True(schema.Validate(Parse("<r><a/></r>"), new() { Limits = new() { Depth = 2 } }).IsValid);
        Assert.False(schema.Validate(Parse("<r><a/></r>"), new() { Limits = new() { Depth = 1 } }).IsComplete);
        Assert.True(schema.Validate(Parse("<r x='v'/>") , new() { Limits = new() { AttributesPerElement = 1 } }).IsValid);
        Assert.False(schema.Validate(Parse("<r x='v' xmlns:p='urn:p'/>") , new() { Limits = new() { AttributesPerElement = 1 } }).IsComplete);
        Assert.True(schema.Validate(Parse("<r>😀</r>"), new() { Limits = new() { DocumentCharacters = 2 } }).IsValid);
        Assert.False(schema.Validate(Parse("<r>😀😀</r>"), new() { Limits = new() { DocumentCharacters = 2 } }).IsComplete);
        Assert.True(schema.Validate(Parse("<r x='😀'/>") , new() { Limits = new() { TokenCharacters = 1 } }).IsValid);
        Assert.False(schema.Validate(Parse("<r x='😀😀'/>") , new() { Limits = new() { TokenCharacters = 1 } }).IsComplete);
    }

    [Fact]
    public void ArgumentsAndCancellationFailBeforeWork()
    {
        var schema = Schema("<!ELEMENT r EMPTY>"); var document = Parse("<r/>");
        Assert.Throws<ArgumentNullException>(() => DtdSchema.Compile(null!, cancellationToken: Ct));
        Assert.Throws<ArgumentNullException>(() => DtdSchema.FromDocument(null!));
        Assert.Throws<ArgumentNullException>(() => schema.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => schema.Validate(document, new() { Limits = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => schema.Validate(document, new() { MaxOperations = 0 }));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(cancel.Token, Assert.Throws<OperationCanceledException>(() => schema.Validate(document, new() { CancellationToken = cancel.Token })).CancellationToken);
        Assert.Equal(cancel.Token, Assert.Throws<OperationCanceledException>(() => DtdSchema.Compile("<!ELEMENT r EMPTY>", cancellationToken: cancel.Token)).CancellationToken);
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
    }

    private static bool BclValid(string dtd, string xml)
    {
        bool valid = true;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, ValidationType = ValidationType.DTD, XmlResolver = null };
        settings.ValidationEventHandler += (_, _) => valid = false;
        using var input = new StringReader("<!DOCTYPE r [" + dtd + "]>" + xml);
        using var reader = System.Xml.XmlReader.Create(input, settings);
        while (reader.Read()) { }
        return valid;
    }
}
