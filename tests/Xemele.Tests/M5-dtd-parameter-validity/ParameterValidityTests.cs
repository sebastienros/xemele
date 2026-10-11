using System.Text;
using Xemele.Parsing.Dtd;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.DtdParameterValidity;

public sealed class ParameterValidityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Source = new("urn:dtd:source"), Child = new("urn:dtd:redirected");
    [Theory]
    [InlineData("xmltest002", "DTD.ImproperGroupNesting")]
    [InlineData("xmltest005", "DTD.ImproperDeclarationNesting")]
    [InlineData("xmltest006", "DTD.ImproperDeclarationNesting")]
    [InlineData("xmltest022", "DTD.ImproperConditionalNesting")]
    [InlineData("ibm49", "DTD.ImproperGroupNesting")]
    [InlineData("ibm50", "DTD.ImproperGroupNesting")]
    [InlineData("ibm51", "DTD.ImproperGroupNesting")]
    [InlineData("E14", "DTD.ImproperDeclarationNesting")]
    public async Task EightPinnedFailuresStayWellFormedButRejectSchemaAtEverySplit(string name, string code)
    {
        string body = File.ReadAllText(Path.Combine(Repo(), "tests", "fixtures", "M5-dtd-parameter-validity", name + ".dtd"));
        for (int split = 0; split <= body.Length; split++)
        {
            using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
            Assert.Equal(DtdParseStep.NeedInput, parser.Append(body.AsSpan(0, split), [], false, Ct).Step);
            Assert.Equal(DtdParseStep.Completed, parser.Append(body.AsSpan(split), [], true, Ct).Step);
            Assert.Equal(code, parser.Model.DeclarationValidityDiagnostic!.Code);
            Assert.Equal(XmlDiagnosticSeverity.Error, parser.Model.DeclarationValidityDiagnostic.Severity);
            Assert.NotEmpty(parser.Model.Elements);
        }
        Assert.Equal(code, Assert.Single(Assert.Throws<XmlParseException>(() => DtdSchema.Compile(body, cancellationToken: Ct)).Diagnostics).Code);
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Resolver(body) };
        const string xml = "<!DOCTYPE r SYSTEM 'urn:dtd'><r/>";
        var sync = XmlDocument.Parse(xml, options, Ct);
        using var stream = new ChunkStream(Encoding.UTF8.GetBytes(xml));
        var async = await XmlDocument.LoadAsync(stream, options, Ct);
        foreach (var document in new[] { sync, async })
        {
            Assert.Equal(code, document.Dtd!.DeclarationValidityDiagnostic!.Code);
            Assert.Equal(Child, document.Dtd.DeclarationValidityDiagnostic.Location.SourceUri);
            Assert.Equal(code, Assert.Single(Assert.Throws<XmlParseException>(() => DtdSchema.FromDocument(document)).Diagnostics).Code);
        }
        Assert.True(stream.CanRead);
    }
    [Theory]
    [InlineData("<!ENTITY % p '(a|b)'><!ELEMENT r %p;>")]
    [InlineData("<!ENTITY % p '(a,b)'><!ELEMENT r (%p;,c)>")]
    [InlineData("<!ENTITY % p '(#PCDATA|a)*'><!ELEMENT r %p;>")]
    [InlineData("<!ENTITY % p 'a,b'><!ELEMENT r (%p;)>")]
    [InlineData("<!ENTITY % p '<!ELEMENT r EMPTY>'>%p;")]
    [InlineData("<!ENTITY % p 'INCLUDE'><![%p;[<!ELEMENT r EMPTY>]]>")]
    [InlineData("<!ENTITY % p 'IGNORE'><![%p;[not markup %not-expanded;]]><!ELEMENT r EMPTY>")]
    [InlineData("<!ENTITY % p '<![INCLUDE[<!ELEMENT r EMPTY>]]>'>%p;")]
    [InlineData("<!ENTITY % p 'EMPTY'><!ELEMENT r %p;>")]
    public void ProperNestedAndKeywordOnlyReferencesStayValid(string body)
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
        Assert.Equal(DtdParseStep.Completed, parser.Append(body, [], true, Ct).Step);
        Assert.Null(parser.Model.DeclarationValidityDiagnostic);
        Assert.NotNull(DtdSchema.Compile(body, cancellationToken: Ct));
    }
    [Theory]
    [InlineData("<!ENTITY % p 'IGNORE['><![%p;ignored]]><!ELEMENT r EMPTY>", "DTD.ImproperConditionalNesting")]
    [InlineData("<!ENTITY % p 'INCLUDE['><![%p;<!ELEMENT r EMPTY>]]>", "DTD.ImproperConditionalNesting")]
    [InlineData("<!ENTITY % p ')'><!ELEMENT r (a%p;>", "DTD.ImproperGroupNesting")]
    [InlineData("<!ENTITY % p '>'><!NOTATION n SYSTEM 'n' %p;", "DTD.ImproperDeclarationNesting")]
    [InlineData("<!ENTITY % p '>'><!ENTITY e 'text' %p;", "DTD.ImproperDeclarationNesting")]
    public void BoundaryKindsBeyondOriginalEightRemainValidityOnly(string body, string code)
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
        Assert.Equal(DtdParseStep.Completed, parser.Append(body, [], true, Ct).Step);
        Assert.Equal(code, parser.Model.DeclarationValidityDiagnostic!.Code);
    }
    [Fact]
    public void ExternalParameterBoundaryRetainsRedirectedOriginalByteLocation()
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
        const string parent = "<!ENTITY % p SYSTEM 'child'><!ELEMENT r EMPTY %p;";
        Assert.Equal(DtdParseStep.NeedEntity, parser.Append(parent, [], true, Ct).Step);
        parser.PushEntity(Child);
        parser.CompleteByteSource(92);
        Assert.Equal(DtdParseStep.NeedInput, parser.Append("\n>", [90, 91], true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Equal(new XmlLocation(Child, 2, 1, 91), parser.Model.DeclarationValidityDiagnostic!.Location);
    }
    [Fact]
    public void PriorDiagnosticIdentityAndFirstBindingSurviveNewBoundaryAndSeedCancellation()
    {
        var diagnostic = new XmlDiagnostic("DTD.Prior", XmlDiagnosticSeverity.Error, "Existing", new(Source, 2, 3, 4));
        var source = new Xemele.Parsing.Dtd.DtdModel([new("r", new(DtdParticleKind.Empty, null, DtdOccurrence.Once, []), false, default)], [], [], [], [], diagnostic);
        using var parser = new DtdParser(DtdSubsetKind.External, new(), new Ledger(), new(Source, 1, 1, null));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => parser.SeedPriorModel(source, cancel.Token));
        parser.SeedPriorModel(source, Ct);
        Assert.Equal(DtdParseStep.Completed, parser.Append("<!ENTITY % p '>'><!ELEMENT r ANY %p;", [], true, Ct).Step);
        Assert.Same(diagnostic, parser.Model.DeclarationValidityDiagnostic);
        Assert.Equal(DtdParticleKind.Empty, Assert.Single(parser.Model.Elements).Content.Kind);
    }
    [Theory]
    [InlineData(false, "<!ENTITY % p '<!ELEMENT r EMPTY'>%p;")]
    [InlineData(false, "<!ENTITY % p '(a)'><!ELEMENT r %p;>")]
    [InlineData(true, "<!ENTITY % p ']]>'><![INCLUDE[<!ELEMENT r EMPTY>%p;")]
    public void ExistingWellFormednessRejectionsRemain(bool external, string body)
    {
        using var parser = new DtdParser(external ? DtdSubsetKind.External : DtdSubsetKind.Internal, new(), Source);
        Assert.Equal(DtdParseStep.Rejected, parser.Append(body, [], true, Ct).Step);
    }
    [Fact]
    public void QuotasAndCanceledInputStillRejectBeforeCompletion()
    {
        const string body = "<!ENTITY % p '(a,b)'><!ELEMENT r %p;>";
        using var parser = new DtdParser(DtdSubsetKind.External, new() { TokenCharacters = 3 }, Source);
        Assert.Equal(DtdParseStep.Rejected, parser.Append(body, [], true, Ct).Step);
        using var canceledParser = new DtdParser(DtdSubsetKind.External, new(), Source);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Equal(cancel.Token, Assert.Throws<OperationCanceledException>(() => canceledParser.Append(body, [], true, cancel.Token)).CancellationToken);
    }
    private static string Repo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException();
    }
    private sealed class Ledger : IDtdBudget
    {
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation at) => null;
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation at) => null;
        public XmlDiagnostic? EnterEntity(XmlLocation at) => null;
        public void ExitEntity() { }
    }
    private sealed class Resolver(string body) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return new(new ChunkStream(Encoding.UTF8.GetBytes(body), false), Child); }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return new(new ChunkStream(Encoding.UTF8.GetBytes(body)), Child); }
    }
    private sealed class ChunkStream(byte[] bytes, bool asyncOnly = true) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> buffer)
        { if (asyncOnly) throw new InvalidOperationException("sync read"); return base.Read(buffer[..Math.Min(buffer.Length, 1)]); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return base.Read(buffer.Span[..Math.Min(buffer.Length, 1)]); }
    }
}
