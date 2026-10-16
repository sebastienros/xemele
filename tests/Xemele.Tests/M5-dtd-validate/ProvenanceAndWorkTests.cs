using System.Text;
using Xemele.Validation;
using Xunit;
using Document = Xemele.XmlDocument;

namespace Xemele.Tests.DtdValidation;

public sealed class ProvenanceAndWorkTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string External = "<!ELEMENT r (a*)><!ELEMENT a EMPTY>";
    private static Document Standalone(string body, string dtd = External) => Document.Parse(
        "<?xml version='1.0' standalone='yes'?><!DOCTYPE r SYSTEM 'urn:dtd'><r>" + body + "</r>",
        new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = new Subset(dtd)
        }, Ct);

    [Fact]
    public void OriginalNumericReferenceAndCdataWhitespaceViolateChildrenContent()
    {
        var document = Standalone("&#32;");
        var schema = DtdSchema.FromDocument(document);
        Assert.False(schema.Validate(document).IsValid);
        Assert.False(schema.Validate(Standalone("&#32;")).IsValid); // Separate equivalent effective model.
        Assert.False(DtdSchema.Compile(External, cancellationToken: Ct).Validate(document).IsValid);
        var cdata = schema.Validate(Standalone("<![CDATA[ \t]]>"));
        Assert.True(cdata.IsComplete);
        Assert.False(cdata.IsValid);
        Assert.All(cdata.Diagnostics, d => Assert.Equal("DTD.Content", d.Code));
        var error = Assert.Throws<XmlParseException>(() => Standalone(" "));
        Assert.Equal("XML.Standalone", Assert.Single(error.Diagnostics).Code);
    }

    [Theory]
    [InlineData("replace")]
    [InlineData("restore")]
    [InlineData("insert")]
    [InlineData("attribute")]
    public void AnyMutationInvalidatesWholeDocumentWhitespaceSourceProof(string mutation)
    {
        var document = Standalone("&#32;");
        var schema = DtdSchema.FromDocument(document);
        if (mutation == "replace")
        {
            var text = document.RootElement.FirstChild;
            text.Value = "\t";
        }
        else if (mutation == "restore")
        {
            var text = document.RootElement.FirstChild;
            text.Value = "x";
            text.Value = " ";
        }
        else if (mutation == "insert")
        {
            document.RootElement.AppendChild(document.CreateText(" "));
        }
        else
        {
            document.RootElement.SetAttribute("not-declared", "v");
        }

        var result = schema.Validate(document);
        Assert.False(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal("DTD.IncompleteInput", result.Diagnostics[^1].Code);
        Assert.Equal(XmlDiagnosticSeverity.Fatal, result.Diagnostics[^1].Severity);
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("no-dtd")]
    [InlineData("any")]
    [InlineData("internal")]
    public void UncheckedOrDifferentOriginalDeclarationCannotAssertStandaloneWhitespaceSafety(string profile)
    {
        Document document;
        if (profile == "manual")
        {
            document = new();
            var root = document.CreateElement("r");
            document.DocumentNode.AppendChild(root);
            root.AppendChild(document.CreateText(" "));
            document.SetValidationDeclaration(true, new(null, 1, 1, null));
        }
        else
        {
            document = profile == "any"
            ? Standalone(" ", "<!ELEMENT r ANY>")
            : profile == "internal"
            ? Document.Parse("<?xml version='1.0' standalone='yes'?><!DOCTYPE r [<!ELEMENT r (a*)><!ELEMENT a EMPTY>]><r> </r>", new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, Ct)
            : Document.Parse("<?xml version='1.0' standalone='yes'?><r> </r>", cancellationToken: Ct);
        }

        var result = DtdSchema.Compile(External, cancellationToken: Ct).Validate(document);
        Assert.False(result.IsComplete);
        Assert.Equal("DTD.IncompleteInput", result.Diagnostics[^1].Code);
    }

    [Fact]
    public void CdataHasKnownOriginEvenAfterMutationAndMissingSourceStamp()
    {
        var document = Standalone("<![CDATA[ ]]>");
        var schema = DtdSchema.FromDocument(document);
        var cdata = document.RootElement.FirstChild;
        cdata.Value = "\t";
        var result = schema.Validate(document);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.All(result.Diagnostics, d => Assert.Equal("DTD.Content", d.Code));
    }

    [Theory]
    [InlineData("                                                                ", 64)]
    [InlineData("<!--comment-->", 14)]
    [InlineData("<!--😀-->", 8)]
    public void CompileChargesUniqueSourceScalarsExactlyBeforeEmptyModelCompilation(string source, int scalars)
    {
        Assert.NotNull(DtdSchema.Compile(source, limits: new()
        {
            EvaluationWork = scalars
        }, cancellationToken: Ct));
        var error = Assert.Throws<XmlParseException>(() => DtdSchema.Compile(source, limits: new() { EvaluationWork = scalars - 1 }, cancellationToken: Ct));
        Assert.Equal("VALIDATION.Limit", Assert.Single(error.Diagnostics).Code);
    }

    [Fact]
    public void CompileChargesNestedParameterSourceWorkAndDisposesOnQuotaRefusal()
    {
        const string source = "<!ENTITY % p SYSTEM 'urn:child'>%p;";
        var resolver = new Subset(new string(' ', 100));
        var exception = Assert.Throws<XmlParseException>(() => DtdSchema.Compile(source, resolver, new() { EvaluationWork = 80 }, Ct));
        Assert.Equal("VALIDATION.Limit", Assert.Single(exception.Diagnostics).Code);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
        var enough = new Subset(new string(' ', 100));
        Assert.NotNull(DtdSchema.Compile(source, enough, new()
        {
            EvaluationWork = 1000
        }, Ct));
        Assert.Equal(1, enough.Disposals);
    }

    [Fact]
    public void CompileRejectsUnreadParameterDeclarationsInsteadOfValidatingEffectiveOmission()
    {
        var exception = Assert.Throws<XmlParseException>(() => DtdSchema.Compile("%missing;<!ELEMENT r EMPTY>", cancellationToken: Ct));
        Assert.Equal("DTD.UndeclaredEntity", Assert.Single(exception.Diagnostics).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedUnreadParameterRefusesFromDocumentAndCurrentDomValidation(bool bytes)
    {
        const string xml = "<!DOCTYPE r SYSTEM 'urn:dtd'><r/>";
        var resolver = new Subset("<!ELEMENT r EMPTY>\n%missing;<!ATTLIST r required CDATA #REQUIRED>");
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
        var document = bytes ? await Document.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options, Ct) : Document.Parse(xml, options, Ct);
        Assert.True(document.SourceInputComplete);
        Assert.NotNull(document.Dtd!.UnreadParameterLocation);
        var error = Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(document));
        var diagnostic = Assert.Single(error.Diagnostics);
        Assert.Equal("DTD.IncompleteInput", diagnostic.Code);
        Assert.Equal(XmlDiagnosticSeverity.Fatal, diagnostic.Severity);
        Assert.Equal(document.Dtd.UnreadParameterLocation, diagnostic.Location);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
        var result = DtdSchema.Compile("<!ELEMENT r EMPTY>", cancellationToken: Ct).Validate(document);
        Assert.False(result.IsComplete);
        Assert.Equal("DTD.IncompleteInput", result.Diagnostics[^1].Code);
    }

    private sealed class Subset(string text) : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Opens++;
            return new(new Tracked(Encoding.UTF8.GetBytes(text), this), new Uri("https://offline.invalid/source.dtd"));
        }
        private sealed class Tracked(byte[] bytes, Subset owner) : MemoryStream(bytes, false)
        {
            public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                }

                base.Dispose(disposing);
            }
        }
    }
}
