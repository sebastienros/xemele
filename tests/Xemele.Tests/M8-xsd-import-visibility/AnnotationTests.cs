using System.Text;
using System.Text.Json;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdImportVisibility.Support;
namespace Xemele.Tests.M8XsdImportVisibility;

public sealed class AnnotationTests
{
    private const string InvalidOne = "9999999999999999999999999999999999999999999999999999999999999anyURI:";
    private const string InvalidTwo = "1111111111111111111http://foo/bar";
    private static string Annotation(string wrapper, string value) => Wrap($"\n<xs:annotation><xs:{wrapper} source='{value}'><payload xmlns='urn:opaque'><xs:not-an-xsd-construct source='1:bad'/> mixed text</payload></xs:{wrapper}></xs:annotation><xs:element name='root' type='xs:int'/>");
    [Fact]
    public void UnchangedOriginalAnnotationRejectsBeforeCompositionWithoutResolver()
    {
        string path = Path.Combine(OriginalTests.Root(), "msData/datatypes/Facets/anyURI/anyURI_a001.xsd");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(OriginalTests.Root(), "manifest.json")));
        var original = manifest.RootElement.GetProperty("annotation_original").GetProperty("schema");
        Assert.Equal("invalid", original.GetProperty("expected").GetString());
        Assert.Equal("invalid", original.GetProperty("originalExpected")[0].GetProperty("validity").GetString());
        Assert.Equal("SCHEMA.ResourceDenied", manifest.RootElement.GetProperty("annotation_earlier_observation").GetProperty("actual").GetString());
        using var input = File.OpenRead(path);
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input, new() { BaseUri = new(path) }));
        var diagnostic = Assert.Single(error.Diagnostics); Assert.Equal("XSD.SchemaRestriction", diagnostic.Code); Assert.Contains("annotation source", diagnostic.Message);
        Assert.Equal(new Uri(path), diagnostic.Location.SourceUri); Assert.Equal(12, diagnostic.Location.LineNumber); Assert.NotNull(diagnostic.Location.ByteOffset); Assert.True(input.CanRead);
    }
    [Theory]
    [InlineData("appinfo", InvalidOne)] [InlineData("documentation", InvalidOne)]
    [InlineData("appinfo", InvalidTwo)] [InlineData("documentation", InvalidTwo)]
    [InlineData("appinfo", " &#x9;1:bad&#xA; ")] [InlineData("documentation", " 1:bad ")]
    [InlineData("appinfo", "%xy")] [InlineData("documentation", "%xy")]
    public void InvalidUriOnEachWrapperFailsAtSourceAttributeBeforeResolver(string wrapper, string value)
    {
        var resolver = Map(new());
        var error = Reject(Annotation(wrapper, value).Replace("<xs:element", "<xs:include schemaLocation='unavailable'/><xs:element", StringComparison.Ordinal), resolver);
        var at = Assert.Single(error.Diagnostics).Location; Assert.Equal(Basis, at.SourceUri); Assert.Equal(2, at.LineNumber); Assert.True(at.LinePosition > 1); Assert.Null(at.ByteOffset); Assert.Empty(resolver.Requests);
    }
    public static IEnumerable<object[]> ValidSources()
    {
        foreach (string wrapper in new[] { "appinfo", "documentation" })
            foreach (string value in new[] { "", "123", "../123", "http://example.test/a", "urn:example:opaque", "./1:ok", "?q=1", "#fragment", "&#x9;  http://example.test/a &#xA;", "path  with   spaces", "路径/日本語", "//example.test/path", "https://[::1]/a" }) yield return [wrapper, value];
    }
    [Theory]
    [MemberData(nameof(ValidSources))]
    public void NumericRelativeAbsoluteWhitespaceAndOpaquePayloadAreAccepted(string wrapper, string value)
    {
        var resolver = Map(new()); var schema = XmlSchema.Compile(Annotation(wrapper, value), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<root>2</root>").IsValid); Assert.False(schema.Validate("<root>bad</root>").IsValid); Assert.Empty(resolver.Requests);
    }
    [Theory]
    [InlineData("appinfo", false)] [InlineData("appinfo", true)]
    [InlineData("documentation", false)] [InlineData("documentation", true)]
    public void ExactWorkComponentsAndSharedPublicParsingStayBounded(string wrapper, bool publicParsing)
    {
        string text = Annotation(wrapper, " &#x9;http://example.test/" + new string('a', 300) + "  ");
        if (publicParsing)
        {
            const long cap = 1_000_000;
            void Run(SchemaCompileContext context) => new XmlSchemaCompiler().Compile(context.Parse(text).Document, context);
            var measured = new SchemaCompileContext(new() { MaxOperations = cap }); Run(measured); long used = cap - measured.Operation.RemainingWork!.Value;
            Assert.True(used > text.Length);
            var exact = new SchemaCompileContext(new() { MaxOperations = used }); Run(exact); Assert.Equal(0, exact.Operation.RemainingWork);
            var shortCap = new SchemaCompileContext(new() { MaxOperations = used - 1 }); Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(shortCap)).Diagnostics, d => d.Code == "SCHEMA.Limit");
        }
        else
        {
            void Run(BoundaryTests.Ledger operation) => new XmlSchemaCompiler().Compile(XmlDocument.Parse(text), new(), operation);
            var measured = new BoundaryTests.Ledger(); Run(measured);
            Run(new(measured.Work, measured.Components));
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(measured.Work - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(components: measured.Components - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
        }
    }
    [Theory]
    [InlineData("appinfo")] [InlineData("documentation")]
    public void CancellationDuringSourceLexicalWorkUsesOperationToken(string wrapper)
    {
        using var cancellation = new CancellationTokenSource();
        string text = Annotation(wrapper, "http://example.test/" + new string('a', 1024));
        var operation = new CancelAtSource(cancellation, text.IndexOf("source=", StringComparison.Ordinal) - text.IndexOf('\n'));
        var document = XmlDocument.Parse(text, cancellationToken: TestContext.Current.CancellationToken);
        var error = Assert.Throws<OperationCanceledException>(() => new XmlSchemaCompiler().Compile(document, new(), operation));
        Assert.Equal(cancellation.Token, error.CancellationToken); Assert.Equal(64, operation.SourceWork);
    }
    private sealed class CancelAtSource(CancellationTokenSource cancellation, int column) : ISchemaOperation
    {
        private readonly BoundaryTests.Ledger inner = new(token: cancellation.Token);
        internal int SourceWork;
        public XmlResourceLimits Limits => inner.Limits;
        public CancellationToken CancellationToken => cancellation.Token;
        public bool IsComplete => inner.IsComplete;
        public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long amount, XmlLocation at)
        {
            bool result = inner.Charge(amount, at);
            // Match the source attribute column, distinct from the wrapper.
            // Only the source lexical adapter uses this location.
            if (amount > 0 && at.LineNumber == 2 && at.LinePosition == column && ++SourceWork == 64) cancellation.Cancel();
            return result;
        }
        public bool ChargeComponents(int amount, XmlLocation at) => inner.ChargeComponents(amount, at);
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
    }
    [Theory]
    [InlineData("appinfo")] [InlineData("documentation")]
    public void IncludedAnnotationFailureKeepsRedirectedAttributeByteLocation(string wrapper)
    {
        Uri final = new("https://redirect.test/annotations/final.xsd");
        var resolver = new Resolver((_, _) => XmlResourceResolution.Unavailable);
        resolver = new((_, _) => resolver.Resolved(Annotation(wrapper, InvalidTwo), final));
        var error = Reject(Wrap("<xs:include schemaLocation='child'/>"), resolver);
        var at = Assert.Single(error.Diagnostics).Location; Assert.Equal(final, at.SourceUri); Assert.Equal(2, at.LineNumber); Assert.NotNull(at.ByteOffset);
        Assert.True(at.LinePosition > 10); Assert.Single(resolver.Requests); resolver.Disposed();
    }
}
