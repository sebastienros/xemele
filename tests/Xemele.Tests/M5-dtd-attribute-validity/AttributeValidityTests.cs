using System.Runtime.InteropServices;
using Xemele.Parsing.Dtd;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M5DtdAttributeValidity;

public sealed class AttributeValidityTests
{
    private static readonly string Header = "<!ELEMENT r ANY><!NOTATION n SYSTEM 'n'><!ENTITY e SYSTEM 'e' NDATA n>";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static CompiledDtdModel CompileModel(string body)
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new());
        var parsed = parser.Append(body, [], true, Ct);
        Assert.Equal(DtdParseStep.Completed, parsed.Step);
        return DtdCompiler.Compile(parser.Model, new());
    }
    private static XmlDocument Manual(string name = "r")
    {
        var d = new XmlDocument();
        d.DocumentNode.AppendChild(d.CreateElement(name));
        return d;
    }

    [Theory]
    [InlineData("ENTITY", "undef")]
    [InlineData("ENTITIES", "undef other")]
    [InlineData("IDREF", "undef")]
    [InlineData("IDREFS", "undef other")]
    public void UnusedDefaultsAreSyntacticAtCompilation(string type, string value)
    {
        var schema = DtdSchema.Compile(Header + "<!ELEMENT unused ANY><!ATTLIST unused a " + type + " '" + value + "'>", cancellationToken: Ct);
        Assert.True(schema.Validate(Manual()).IsValid);
    }

    [Theory]
    [InlineData("ENTITY", "undef", false)]
    [InlineData("ENTITY", "e", true)]
    [InlineData("ENTITIES", "e undef", false)]
    [InlineData("ENTITIES", "e e", true)]
    public void MissingOrdinaryDefaultsRemainAllowedButActualValuesRequireMembership(string type, string value, bool valid)
    {
        var schema = DtdSchema.Compile(Header + "<!ATTLIST r a " + type + " '" + value + "'>", cancellationToken: Ct);
        var d = Manual();
        var revision = d.ContentRevision;
        var result = schema.Validate(d);
        Assert.True(result.IsComplete);
        Assert.True(result.IsValid);
        Assert.Equal(revision, d.ContentRevision);
        Assert.Equal(0, d.RootElement.Attributes.Count);
        var parsed = XmlDocument.Parse("<!DOCTYPE r [" + Header + "<!ATTLIST r a " + type + " '" + value + "'>]><r/>",
            new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, Ct);
        Assert.Equal(1, parsed.RootElement.Attributes.Count);
        Assert.Equal(valid, DtdSchema.FromDocument(parsed).Validate(parsed).IsValid);
        d.RootElement.SetAttribute("a", "e");
        Assert.True(schema.Validate(d).IsValid);
        d.RootElement.SetAttribute("a", "undef");
        Assert.False(schema.Validate(d).IsValid);
    }

    [Theory]
    [InlineData("ENTITY")]
    [InlineData("ENTITIES")]
    public void FixedMissingEntityDefaultsAndLimitsRemainStructured(string type)
    {
        var schema = DtdSchema.Compile(Header + "<!ATTLIST r a " + type + " #FIXED 'undef'>", cancellationToken: Ct);
        var d = Manual();
        Assert.True(schema.Validate(d).IsValid);
        Assert.Equal(0, d.RootElement.Attributes.Count);
        var limited = schema.Validate(d, new()
        {
            MaxOperations = 1
        });
        Assert.False(limited.IsComplete);
        Assert.Equal("VALIDATION.Limit", limited.Diagnostics[^1].Code);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Equal(cts.Token, Assert.Throws<OperationCanceledException>(() => schema.Validate(d, new() { CancellationToken = cts.Token })).CancellationToken);
    }

    [Theory]
    [InlineData("ID")]
    [InlineData("IDREF")]
    [InlineData("IDREFS")]
    [InlineData("ENTITY")]
    [InlineData("ENTITIES")]
    [InlineData("NOTATION (n)")]
    public void NameTypedValuesRejectColonInEveryPosition(string type)
    {
        var model = CompileModel(Header + "<!ATTLIST r a " + type + " #IMPLIED>");
        foreach (var value in new[] { ":x", "x:y", "x:", "x::y", "根:名", "x p:y" })
        {
            Assert.False(model.Attributes[0].IsValueValid(value, model, new()));
        }
    }

    [Theory]
    [InlineData("IDREF")]
    [InlineData("IDREFS")]
    [InlineData("ENTITY")]
    [InlineData("ENTITIES")]
    public void NameTypedDefaultsRejectColonAndMalformedSyntax(string type)
    {
        foreach (var value in new[] { "p:name", "1bad", "" })
        {
            Assert.Throws<XmlParseException>(() => DtdSchema.Compile(Header + "<!ATTLIST r a " + type + " '" + value + "'>", cancellationToken: Ct));
        }
    }

    [Theory]
    [InlineData("NMTOKEN", "a:b")]
    [InlineData("NMTOKENS", "a:b 1:c")]
    [InlineData("(a:b|c:d)", "a:b")]
    public void NmTokensAndEnumerationRetainColon(string type, string value)
    {
        var schema = DtdSchema.Compile(Header + "<!ATTLIST r a " + type + " '" + value + "'>", cancellationToken: Ct);
        var d = Manual();
        d.RootElement.SetAttribute("a", value);
        Assert.True(schema.Validate(d).IsValid);
    }

    [Fact]
    public void BorrowedNamesPreserveUnicodeScalarLimitsAndCancellation()
    {
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
        foreach (var value in new[] { "根", "a😀", "a\u0301" })
        {
            Assert.True(DtdLexical.Name(value, false, true, new()));
        }

        foreach (var value in new[] { "", "1a", "a:b", new string(['a', (char)0xD800]), new string([(char)0xDC00]) })
        {
            Assert.False(DtdLexical.Name(value, false, true, new()));
        }

        Assert.True(DtdLexical.Name("a😀", false, true, new(new()
        {
            Limits = new()
            {
                TokenCharacters = 2
            }
        })));
        var quota = new ValidationOperation(new()
        {
            Limits = new()
            {
                TokenCharacters = 1
            }
        });
        Assert.False(DtdLexical.Name("a😀", false, true, quota));
        Assert.False(quota.IsComplete);
        var work = new ValidationOperation(new()
        {
            MaxOperations = 1
        });
        Assert.False(DtdLexical.Name("ab", false, true, work));
        Assert.False(work.IsComplete);
        using var cts = new CancellationTokenSource();
        var run = new ValidationOperation(new()
        {
            CancellationToken = cts.Token
        });
        cts.Cancel();
        Assert.Equal(cts.Token, Assert.Throws<OperationCanceledException>(() => DtdLexical.Name("ab", false, true, run)).CancellationToken);
    }

    [Theory]
    [InlineData("E9a.xml", true)]
    [InlineData("E06i.xml", true)]
    [InlineData("ns045.xml", false)]
    [InlineData("ns046.xml", false)]
    public void OriginalPinnedCorpusRegressions(string file, bool valid)
    {
        var xml = File.ReadAllText(Path.Combine(FindRoot(), "tests/fixtures/M5-dtd-attribute-validity", file));
        var d = XmlDocument.Parse(xml, new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, Ct);
        var schema = DtdSchema.FromDocument(d);
        var result = schema.Validate(d);
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
    }
    private static string FindRoot()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json")))
        {
            path = path.Parent;
        }

        return path?.FullName ?? throw new InvalidOperationException();
    }
}
