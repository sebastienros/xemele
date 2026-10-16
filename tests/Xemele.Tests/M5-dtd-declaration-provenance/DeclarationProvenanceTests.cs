using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xemele.Parsing.Dtd;
using Model = Xemele.Parsing.Dtd.DtdModel;
using Xunit;

namespace Xemele.Tests.DtdDeclarationProvenance;

public sealed class DeclarationProvenanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Source = new("https://offline.invalid/source.dtd");
    private static readonly Uri Child = new("https://offline.invalid/child.dtd");

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DuplicateElementAndNotationAreValidityOnlyAtEverySplit(bool external, bool notation)
    {
        var first = notation ? "<!NOTATION n SYSTEM 'first'>" : "<!ELEMENT r EMPTY>";
        var second = notation ? "<!NOTATION n SYSTEM 'second'>" : "<!ELEMENT r ANY>";
        var text = first + "\r\n" + second;
        for (var split = 0; split <= text.Length; split++)
        {
            using var parser = new DtdParser(external ? DtdSubsetKind.External : DtdSubsetKind.Internal, new(), Source);
            Assert.Equal(DtdParseStep.NeedInput, parser.Append(text.AsSpan(0, split), [], false, Ct).Step);
            var done = parser.Append(text.AsSpan(split), [], true, Ct);
            Assert.Equal(DtdParseStep.Completed, done.Step);
            Assert.Null(done.Diagnostic);
            var diagnostic = parser.Model.DeclarationValidityDiagnostic!;
            Assert.Equal(notation ? "DTD.DuplicateNotation" : "DTD.DuplicateElement", diagnostic.Code);
            Assert.Equal(XmlDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(new XmlLocation(Source, 2, 1, null), diagnostic.Location);
            if (notation)
            {
                Assert.Equal("first", Assert.Single(parser.Model.Notations).SystemId);
            }
            else
            {
                Assert.Equal(DtdParticleKind.Empty, Assert.Single(parser.Model.Elements).Content.Kind);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneHundredIgnoredDuplicatesRetainFirstDiagnosticAndFirstBinding(bool notation)
    {
        var first = notation ? "<!NOTATION n SYSTEM 'first'>" : "<!ELEMENT r EMPTY>";
        var second = notation ? "<!NOTATION n SYSTEM 'second'>" : "<!ELEMENT r ANY>";
        using var parser = new DtdParser(DtdSubsetKind.Internal, new());
        Assert.Equal(DtdParseStep.NeedInput, parser.Append(first + "\n" + second, [], false, Ct).Step);
        var field = typeof(DtdParser).GetField("declarationValidityDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var diagnostic = Assert.IsType<XmlDiagnostic>(field.GetValue(parser));
        for (var i = 1; i < 100; i++)
        {
            Assert.Equal(DtdParseStep.NeedInput, parser.Append("\n" + second, [], false, Ct).Step);
            Assert.Same(diagnostic, field.GetValue(parser));
        }
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Same(diagnostic, parser.Model.DeclarationValidityDiagnostic);
        Assert.Equal(new XmlLocation(null, 2, 1, null), diagnostic.Location);
        Assert.Single(notation ? parser.Model.Notations.Cast<object>() : parser.Model.Elements.Cast<object>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParameterEntityDuplicateRetainsOriginalChildUriAndByteLocation(bool notation)
    {
        var first = notation ? "<!NOTATION n SYSTEM 'first'>" : "<!ELEMENT r EMPTY>";
        const string requestText = "<!ENTITY % p SYSTEM 'child.dtd'>%p;";
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
        var request = parser.Append(first + requestText, [], true, Ct);
        Assert.Equal(DtdParseStep.NeedEntity, request.Step);
        parser.PushEntity(Child);
        var child = "\n" + (notation ? "<!NOTATION n SYSTEM 'second'>" : "<!ELEMENT r ANY>");
        long[] offsets = [.. Enumerable.Range(50, child.Length).Select(i => (long)i)];
        parser.CompleteByteSource(50 + child.Length);
        Assert.Equal(DtdParseStep.NeedInput, parser.Append(child, offsets, true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Equal(new XmlLocation(Child, 2, 1, 51), parser.Model.DeclarationValidityDiagnostic!.Location);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossSubsetDuplicatesReachSyncAndAsyncDomWithoutParseRejection(bool notation)
    {
        var first = notation ? "<!NOTATION n SYSTEM 'first'>" : "<!ELEMENT r EMPTY>";
        var second = notation ? "<!NOTATION n SYSTEM 'second'>" : "<!ELEMENT r ANY>";
        var xml = "<!DOCTYPE r SYSTEM 'urn:dtd:external' [" + first + "]><r/>";
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Resolver("\n" + second) };
        var sync = XmlDocument.Parse(xml, options, Ct);
        using var stream = new AsyncChunk(Encoding.UTF8.GetBytes(xml));
        var async = await XmlDocument.LoadAsync(stream, options, Ct);
        Assert.Equal(sync.Dtd!.DeclarationValidityDiagnostic, async.Dtd!.DeclarationValidityDiagnostic);
        Assert.Equal(new XmlLocation(Child, 2, 1, 1), async.Dtd.DeclarationValidityDiagnostic!.Location);
        if (notation)
        {
            Assert.Equal("first", Assert.Single(async.Dtd.Notations).SystemId);
        }
        else
        {
            Assert.Equal(DtdParticleKind.Empty, Assert.Single(async.Dtd.Elements).Content.Kind);
        }

        Assert.Equal("r", async.RootElement.LocalName);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void SeedPreservesPriorDiagnosticIdentityEvenAfterAnotherKindOfDuplicate()
    {
        var ledger = new Ledger();
        var prior = Parse("<!ELEMENT r EMPTY>\n<!ELEMENT r ANY><!NOTATION n SYSTEM 'first'>", ledger);
        using var external = External(ledger);
        external.SeedPriorModel(prior, Ct);
        Assert.Equal(DtdParseStep.Completed, external.Append("<!NOTATION n SYSTEM 'second'>", [], true, Ct).Step);
        Assert.Same(prior.DeclarationValidityDiagnostic, external.Model.DeclarationValidityDiagnostic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticSeedDuplicateAndFailurePublishTransactionally(bool notation)
    {
        var ledger = new Ledger();
        var prior = Parse(notation ? "<!NOTATION n SYSTEM 'first'>" : "<!ELEMENT r EMPTY>", ledger);
        var location = new XmlLocation(Source, 9, 3, 45);
        var synthetic = new Model(notation ? [] : [prior.Elements[0], prior.Elements[0] with { Location = location }], [], [], [],
            notation ? [prior.Notations[0], prior.Notations[0] with { Location = location }] : []);
        using var external = External(ledger);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => external.SeedPriorModel(synthetic, cancel.Token));
        // Failure after a duplicate was staged must also leave the live field untouched.
        var broken = new Model(synthetic.Elements, [], [], [], [.. synthetic.Notations, null!]);
        Assert.Throws<NullReferenceException>(() => external.SeedPriorModel(broken, Ct));
        var field = typeof(DtdParser).GetField("declarationValidityDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(field.GetValue(external));
        external.SeedPriorModel(synthetic, Ct);
        Assert.Equal(DtdParseStep.Completed, external.Append([], [], true, Ct).Step);
        Assert.Equal(location, external.Model.DeclarationValidityDiagnostic!.Location);
    }

    [Fact]
    public async Task RepeatedAttributesAndEntitiesRemainPermittedFirstBindingsAcrossSubsets()
    {
        const string internalSubset = "<!ELEMENT r ANY><!ATTLIST r a CDATA 'first'><!ATTLIST r a CDATA 'second'><!ENTITY e 'inside'><!ENTITY e 'ignored'><!ENTITY % p 'first'><!ENTITY % p 'second'>";
        var xml = "<!DOCTYPE r SYSTEM 'urn:dtd:external' [" + internalSubset + "]><r>&e;</r>";
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Resolver("<!ATTLIST r a CDATA 'third'><!ENTITY e 'outside'><!ENTITY % p 'third'>") };
        var sync = XmlDocument.Parse(xml, options, Ct);
        using var stream = new AsyncChunk(Encoding.UTF8.GetBytes(xml));
        var async = await XmlDocument.LoadAsync(stream, options, Ct);
        foreach (var document in new[] { sync, async })
        {
            Assert.Null(document.Dtd!.DeclarationValidityDiagnostic);
            Assert.Equal("first", Assert.Single(document.Dtd.Attributes).DefaultValue);
            Assert.Equal("inside", Assert.Single(document.Dtd.GeneralEntities).ReplacementText);
            Assert.Equal("first", Assert.Single(document.Dtd.ParameterEntities).ReplacementText);
            Assert.Equal("inside", document.RootElement.GetTextContent());
        }
    }

    [Fact]
    public void ModelDiagnosticIsGetOnlyAndItsGetterIlIsSingleReferenceLoad()
    {
        Assert.Equal(".NET 10.0.11", RuntimeInformation.FrameworkDescription);
        var property = typeof(Model).GetProperty("DeclarationValidityDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(property.SetMethod);
        var il = property.GetMethod!.GetMethodBody()!.GetILAsByteArray()!;
        Assert.Equal(7, il.Length);
        Assert.Equal(0x02, il[0]);
        Assert.Equal(0x7B, il[1]);
        Assert.Equal(0x2A, il[6]);
        Assert.Equal(typeof(XmlDiagnostic), property.GetMethod.Module.ResolveField(BitConverter.ToInt32(il, 2))!.FieldType);
        Assert.Null(new Model([], [], [], [], []).DeclarationValidityDiagnostic);
        Assert.Equal(8, IntPtr.Size);
    }

    private static Model Parse(string text, Ledger ledger)
    {
        using var parser = new DtdParser(DtdSubsetKind.Internal, new(), ledger, new(Source, 1, 1, null));
        Assert.Equal(DtdParseStep.Completed, parser.Append(text, [], true, Ct).Step);
        return parser.Model;
    }
    private static DtdParser External(Ledger ledger)
    {
        var parser = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Child, 1, 1, null));
        parser.SetFramedInitialSource(new(Child, 1, 1, null));
        return parser;
    }
    private sealed class Ledger : IDtdBudget
    {
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location) => null;
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location) => null;
        public XmlDiagnostic? EnterEntity(XmlLocation location) => null;
        public void ExitEntity()
        {
        }
    }
    private sealed class Resolver(string text) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(new AsyncChunk(Encoding.UTF8.GetBytes(text), false), Child);
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return new(new AsyncChunk(Encoding.UTF8.GetBytes(text)), Child);
        }
    }
    private sealed class AsyncChunk(byte[] bytes, bool asyncOnly = true) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> buffer)
        {
            if (asyncOnly)
            {
                throw new InvalidOperationException("sync read");
            }

            return base.Read(buffer[..Math.Min(1, buffer.Length)]);
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return base.Read(buffer.Span[..Math.Min(1, buffer.Length)]);
        }
    }
}
