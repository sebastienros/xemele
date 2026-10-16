using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Xemele.Validation;
using Xunit;
using Document = Xemele.XmlDocument;

namespace Xemele.Tests.DtdInstanceProvenance;

public sealed class InstanceProvenanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Children = "<!ELEMENT r (a*)><!ELEMENT a EMPTY>";
    private static Document Parse(string body, string declarations = Children) => Document.Parse(
        "<!DOCTYPE r [" + declarations + "]><r>" + body + "</r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, Ct);

    [Theory]
    [InlineData("a", "809f059232c1a694b91da30c23ee993eedd87fb5bd4d824440db8bcbd3992452", "char")]
    [InlineData("g", "5f3a35757058984efb8d664c9d3a7d13c9fcb998d0f7968a9ea575d46bffafdc", "char")]
    [InlineData("h", "a6fc844add6b6483a3108fde489e64f1f1475b69764b600a02ae81362cf32003", "char")]
    [InlineData("13", "40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14", "char")]
    [InlineData("a", "809f059232c1a694b91da30c23ee993eedd87fb5bd4d824440db8bcbd3992452", "byte")]
    [InlineData("g", "5f3a35757058984efb8d664c9d3a7d13c9fcb998d0f7968a9ea575d46bffafdc", "byte")]
    [InlineData("h", "a6fc844add6b6483a3108fde489e64f1f1475b69764b600a02ae81362cf32003", "byte")]
    [InlineData("13", "40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14", "byte")]
    [InlineData("a", "809f059232c1a694b91da30c23ee993eedd87fb5bd4d824440db8bcbd3992452", "async")]
    [InlineData("g", "5f3a35757058984efb8d664c9d3a7d13c9fcb998d0f7968a9ea575d46bffafdc", "async")]
    [InlineData("h", "a6fc844add6b6483a3108fde489e64f1f1475b69764b600a02ae81362cf32003", "async")]
    [InlineData("13", "40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14", "async")]
    public async Task OriginalPinnedNormativeCasesAreCompleteInvalid(string id, string hash, string transport)
    {
        var cache = Xemele.TestUtilities.TestRepository.CorpusResources;
        var path = id == "13" ? "xmlconf/eduni/errata-3e/E13.xml" : "xmlconf/eduni/errata-2e/E15" + id + ".xml";
        var bytes = File.ReadAllBytes(Path.Combine(cache, path));
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        using var stream = new OneByteStream(bytes);
        var document = transport == "char" ? Document.Parse(Encoding.UTF8.GetString(bytes), options, Ct) :
            transport == "byte" ? Document.Load(stream, options, Ct) : await Document.LoadAsync(stream, options, Ct);
        Assert.True(document.SourceInputComplete);
        var result = DtdSchema.FromDocument(document).Validate(document);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal(id == "13" ? "DTD.UndeclaredEntity" : "DTD.Content", result.Diagnostics[0].Code);
        if (id == "13")
        {
            Assert.Equal("ent2", Assert.Single(document.UnresolvedReferences).Name);
        }
    }

    [Theory]
    [InlineData(" ", true)]
    [InlineData("&#32;", false)]
    [InlineData("&#x20;", false)]
    [InlineData("&s;", true)]
    [InlineData("&bad;", false)]
    [InlineData("&nested;", false)]
    [InlineData("<![CDATA[ ]]>", false)]
    public void LiteralAndReplacementCharacterReferenceWhitespaceRemainDistinct(string body, bool valid)
    {
        var document = Parse(body, Children + "<!ENTITY s '&#32;'><!ENTITY bad '&#38;#32;'><!ENTITY nested '&bad;'>");
        var result = DtdSchema.FromDocument(document).Validate(document);
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
        if (valid)
        {
            Assert.Null(document.ValidationInstanceDiagnostic);
        }
    }

    [Theory]
    [InlineData("EMPTY", true)]
    [InlineData("ANY", false)]
    [InlineData("(#PCDATA)", false)]
    public void EmptyEntityReferenceWitnessAppliesOnlyToOriginalCurrentDeclarationCategory(string profile, bool complete)
    {
        var document = Parse("&empty;", "<!ELEMENT r EMPTY><!ENTITY empty ''>");
        Assert.True(document.RootElement.FirstChild.IsDefault);
        Assert.NotNull(document.ValidationInstanceDiagnostic);
        Assert.False(DtdSchema.FromDocument(document).Validate(document).IsValid);
        var result = DtdSchema.Compile("<!ELEMENT r " + profile + ">", cancellationToken: Ct).Validate(document);
        Assert.False(result.IsValid);
        Assert.Equal(complete, result.IsComplete);
    }

    [Theory]
    [InlineData("mutate")]
    [InlineData("restore")]
    [InlineData("clone")]
    [InlineData("manual")]
    public void ChangedOrCopiedDomDoesNotReuseSourceWitness(string action)
    {
        var document = Parse("&empty;", "<!ELEMENT r EMPTY><!ENTITY empty ''>");
        var schema = DtdSchema.FromDocument(document);
        if (action == "mutate")
        {
            document.RootElement.SetAttribute("a", "v");
        }
        else if (action == "restore")
        {
            document.RootElement.SetAttribute("a", "v");
            document.RootElement.RemoveAttribute("a");
        }
        else
        {
            document = action == "clone" ? document.DocumentNode.Clone().OwnerDocument : Document.Parse("<r/>", cancellationToken: Ct);
        }

        var result = schema.Validate(document);
        Assert.True(result.IsComplete);
        Assert.Equal(action != "mutate", result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message == "EMPTY content contains a reference.");
    }

    [Fact]
    public void FirstOnlyOwnedWitnessRetainsOriginalReferenceLocationAndDeclaration()
    {
        var document = Parse(string.Concat(Enumerable.Repeat("&#32;", 100)));
        var first = document.ValidationInstanceDiagnostic;
        Assert.NotNull(first);
        Assert.Equal("r", document.ValidationInstanceDeclaration!.Name);
        var result = DtdSchema.FromDocument(document).Validate(document);
        Assert.Same(first, Assert.Single(result.Diagnostics));
        Assert.True(first.Location.LinePosition > 0);
    }

    [Fact]
    public void CompleteKnownMissingReferencesStaySymbolicAndBounded()
    {
        var document = Parse("&missing;&other;", "<!ENTITY % p '<!ELEMENT r ANY>'>%p;");
        var sidecars = document.UnresolvedReferences;
        var schema = DtdSchema.FromDocument(document);
        var result = schema.Validate(document);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal(sidecars[0].Location, Assert.Single(result.Diagnostics).Location);
        Assert.Same(sidecars, document.UnresolvedReferences);
        Assert.Equal(2, sidecars.Count);
        Assert.True(document.RootElement.FirstChild.IsDefault);
        var refused = schema.Validate(document, new()
        {
            MaxOperations = 1
        });
        Assert.False(refused.IsComplete);
        Assert.Equal("VALIDATION.Limit", refused.Diagnostics[^1].Code);
        Assert.False(DtdSchema.Compile("<!ELEMENT r ANY>", cancellationToken: Ct).Validate(document).IsComplete);
        document.RootElement.SetAttribute("a", "v");
        Assert.False(schema.Validate(document).IsComplete);
        Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(document));
    }

    [Fact]
    public void UnreadDeclaredRecoveryAndIncompleteAttributeInputNeverBecomeKnownInvalidProof()
    {
        var unread = Document.Parse("<!DOCTYPE r [<!ELEMENT r ANY>%unknown;]><r>&missing;</r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, Ct);
        Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(unread));
        var declared = Parse("", "<!ELEMENT r ANY><!ENTITY declared 'text'>");
        declared.AddUnresolvedReference("declared", "&declared;", new(null, 1, 1, null), declared.RootElement);
        declared.MarkValidationSourceComplete();
        Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(declared));
        var incomplete = Parse("&missing;", "<!ELEMENT r ANY><!ENTITY % p ''>%p;<!ATTLIST r a CDATA '&unknown;'>");
        Assert.True(incomplete.HasIncompleteAttributes);
        Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(incomplete));
        var recovered = Document.ParseWithDiagnostics("<!DOCTYPE r [<!ELEMENT r ANY><!ENTITY % p ''>%p;]><r>&missing;</r>", new()
        {
            Recover = true,
            DtdProcessing = XmlDtdProcessing.Parse
        }, Ct).Value!;
        Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(recovered));
    }

    [Fact]
    public void WitnessStillHonorsCancellationDiagnosticAndParserWorkCaps()
    {
        var document = Parse("&#32;");
        var schema = DtdSchema.FromDocument(document);
        var result = schema.Validate(document, new()
        {
            MaxOperations = 1
        });
        Assert.False(result.IsComplete);
        Assert.Equal("VALIDATION.Limit", result.Diagnostics[^1].Code);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => schema.Validate(document, new() { CancellationToken = canceled.Token }));
        Assert.StartsWith(".NET 10.", RuntimeInformation.FrameworkDescription);
    }

    [Fact]
    public void IgnoredFirstCategoryCannotMaskLaterErasedNestedViolation()
    {
        var d = Parse("&#32;<a>&empty;</a>", "<!ELEMENT r (a*)><!ELEMENT a EMPTY><!ENTITY empty ''>");
        var changed = DtdSchema.Compile("<!ELEMENT r ANY><!ELEMENT a EMPTY>", cancellationToken: Ct).Validate(d);
        Assert.False(changed.IsComplete);
        Assert.Equal("DTD.IncompleteInput", changed.Diagnostics[^1].Code);
        var equivalent = DtdSchema.Compile("<!ELEMENT r (a+)><!ELEMENT a EMPTY>", cancellationToken: Ct).Validate(d);
        Assert.True(equivalent.IsComplete);
        Assert.False(equivalent.IsValid);
        Assert.Contains(equivalent.Diagnostics, x => x.Code == "DTD.Content");
    }

    [Theory]
    [InlineData("&amp;")]
    [InlineData("&#32;")]
    [InlineData("&empty;")]
    public void EmptyAllReferenceKindsRetainFirstWitness(string content)
    {
        var d = Parse(content, "<!ELEMENT r EMPTY><!ENTITY empty ''>");
        Assert.NotNull(d.ValidationInstanceDiagnostic);
        Assert.False(DtdSchema.FromDocument(d).Validate(d).IsValid);
    }

    [Theory]
    [InlineData("&#65;")]
    [InlineData("&#x1F600;")]
    public void NonWhitespaceCharacterReferencesUseOrdinaryCurrentTreeInvalidity(string content)
    {
        var d = Parse(content);
        Assert.Null(d.ValidationInstanceDiagnostic);
        Assert.False(DtdSchema.FromDocument(d).Validate(d).IsValid);
    }

    [Fact]
    public void IndependentBclControlsAndNormativeDiscrepancyAreExplicit()
    {
        // BCL agrees on literal S/non-S/mixed and EMPTY text, but erases E15
        // references and accepts the three normative invalid lexical vectors.
        foreach (var (body, profile, valid) in new[] { (" ", "(r*)", true), ("x", "(r*)", false), ("x", "EMPTY", false), ("x", "(#PCDATA)", true) })
        {
            var actual = true;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, ValidationType = ValidationType.DTD, XmlResolver = null };
            settings.ValidationEventHandler += (_, _) => actual = false;
            using var reader = XmlReader.Create(new StringReader("<!DOCTYPE r [<!ELEMENT r " + profile + ">]><r>" + body + "</r>"), settings);
            while (reader.Read())
            {
            }
            Assert.Equal(valid, actual);
        }
    }

    private sealed class OneByteStream(byte[] data) : MemoryStream(data, false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
