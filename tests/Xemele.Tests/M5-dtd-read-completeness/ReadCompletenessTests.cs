using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xemele.Parsing.Dtd;
using Model = Xemele.Parsing.Dtd.DtdModel;
using Xunit;

namespace Xemele.Tests.DtdReadCompleteness;

public sealed class ReadCompletenessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Source = new("urn:dtd:source");
    private static readonly Uri Child = new("urn:dtd:child");
    private static readonly XmlLocation PriorAt = new(Source, 3, 4, 65);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstSkippedParameterLocationSurvivesEveryChunkSplit(bool bytes)
    {
        const string text = "<!ELEMENT r EMPTY>\n%missing;\n%other;<!ATTLIST r a CDATA 'ignored'>";
        long[] offsets = [.. Enumerable.Range(20, text.Length).Select(i => (long)i)];
        for (var split = 0; split <= text.Length; split++)
        {
            using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
            parser.ConfigureDeclarationReading(false);
            if (bytes)
            {
                parser.CompleteByteSource(20 + text.Length);
            }

            Assert.Equal(DtdParseStep.NeedInput, parser.Append(text.AsSpan(0, split), bytes ? offsets.AsSpan(0, split) : [], false, Ct).Step);
            var done = parser.Append(text.AsSpan(split), bytes ? offsets.AsSpan(split) : [], true, Ct);
            Assert.Equal(DtdParseStep.Completed, done.Step);
            Assert.Null(done.Diagnostic);
            var at = new XmlLocation(Source, 2, 1, bytes ? 20 + text.IndexOf('%') : null);
            Assert.Equal(at, parser.FirstUnreadParameter);
            Assert.Equal(at, parser.Model.UnreadParameterLocation);
            Assert.Equal(DtdParticleKind.Empty, Assert.Single(parser.Model.Elements).Content.Kind);
            Assert.Empty(parser.Model.Attributes);
            Assert.Null(parser.Model.DeclarationValidityDiagnostic);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteModelsAndExistingConstructorCallsKeepNull(bool external)
    {
        using var parser = new DtdParser(external ? DtdSubsetKind.External : DtdSubsetKind.Internal, new(), Source);
        parser.ConfigureDeclarationReading(false);
        Assert.Equal(DtdParseStep.Completed, parser.Append("<!ELEMENT r EMPTY><!ENTITY % p '<!ELEMENT child ANY>'>%p;", [], true, Ct).Step);
        Assert.Null(parser.Model.UnreadParameterLocation);
        Assert.Null(parser.FirstUnreadParameter);
        Assert.Equal(2, parser.Model.Elements.Count);
        Assert.Null(new Model([], [], [], [], []).UnreadParameterLocation);
    }
    [Fact]
    public void UnreadInsideExternalParameterEntityKeepsOriginalChildByteLocation()
    {
        using var parser = new DtdParser(DtdSubsetKind.External, new(), Source);
        parser.ConfigureDeclarationReading(false);
        Assert.Equal(DtdParseStep.NeedEntity, parser.Append("<!ELEMENT r EMPTY><!ENTITY % p SYSTEM 'urn:child'>%p;", [], true, Ct).Step);
        parser.PushEntity(Child);
        const string text = "\n%missing;\n%other;";
        long[] offsets = [.. Enumerable.Range(50, text.Length).Select(i => (long)i)];
        parser.CompleteByteSource(50 + text.Length);
        Assert.Equal(DtdParseStep.NeedInput, parser.Append(text, offsets, true, Ct).Step);
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Equal(new XmlLocation(Child, 2, 1, 51), parser.Model.UnreadParameterLocation);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalSubsetEvidenceReachesSyncAndAsyncDom(bool insideParameter)
    {
        const string xml = "<!DOCTYPE r SYSTEM 'urn:dtd:external'><r/>";
        const string header = "<!ELEMENT r EMPTY>";
        var external = insideParameter ? header + "<!ENTITY % p SYSTEM 'urn:pe'>%p;" : header + "\n%missing;";
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Resolver(external, "\n%missing;") };
        var sync = XmlDocument.Parse(xml, options, Ct);
        using var stream = new ChunkStream(Encoding.UTF8.GetBytes(xml));
        var async = await XmlDocument.LoadAsync(stream, options, Ct);
        var expected = new XmlLocation(insideParameter ? new Uri("urn:dtd:pe") : Child, 2, 1, insideParameter ? 1 : header.Length + 1);
        foreach (var document in new[] { sync, async })
        {
            Assert.Equal(expected, document.Dtd!.UnreadParameterLocation);
            Assert.Equal("r", document.RootElement.LocalName);
            Assert.Equal(DtdParticleKind.Empty, Assert.Single(document.Dtd.Elements).Content.Kind);
        }
        Assert.True(stream.CanRead);
    }
    [Fact]
    public async Task InternalUnreadSurvivesExternalCompositionOnSyncAndAsyncDom()
    {
        const string xml = "<!DOCTYPE r SYSTEM 'urn:dtd:external' [<!ELEMENT r EMPTY>%missing;]><r/>";
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Resolver("%another;") };
        var sync = XmlDocument.Parse(xml, options, Ct);
        using var stream = new ChunkStream(Encoding.UTF8.GetBytes(xml));
        var async = await XmlDocument.LoadAsync(stream, options, Ct);
        Assert.Equal(new XmlLocation(null, 1, xml.IndexOf("%missing;", StringComparison.Ordinal) + 1, null), sync.Dtd!.UnreadParameterLocation);
        Assert.Equal(new XmlLocation(null, 1, xml.IndexOf("%missing;", StringComparison.Ordinal) + 1, xml.IndexOf("%missing;", StringComparison.Ordinal)), async.Dtd!.UnreadParameterLocation);
        Assert.Equal(DtdParticleKind.Empty, Assert.Single(async.Dtd.Elements).Content.Kind);
    }
    [Fact]
    public void SeedCarriesEarliestPriorEvidenceWithoutChangingDeclarationReadingControls()
    {
        var diagnostic = new XmlDiagnostic("DTD.DuplicateElement", XmlDiagnosticSeverity.Error, "Test", PriorAt);
        var source = new Model([new("r", new(DtdParticleKind.Empty, null, DtdOccurrence.Once, []), false, default)], [], [], [], [], diagnostic, PriorAt);
        using var parser = External();
        parser.ConfigureDeclarationReading(false);
        parser.SeedPriorModel(source, Ct);
        Assert.Null(parser.FirstUnreadParameter);
        const string current = "<!ATTLIST r a CDATA 'kept'>\n%later;<!ATTLIST r b CDATA 'ignored'><!ELEMENT r ANY>";
        Assert.Equal(DtdParseStep.Completed, parser.Append(current, [], true, Ct).Step);
        Assert.Equal(new XmlLocation(Child, 2, 1, null), parser.FirstUnreadParameter);
        Assert.Equal(PriorAt, parser.Model.UnreadParameterLocation);
        Assert.Equal("a", Assert.Single(parser.Model.Attributes).Name);
        Assert.Same(diagnostic, parser.Model.DeclarationValidityDiagnostic);
        Assert.Equal(DtdParticleKind.Empty, Assert.Single(parser.Model.Elements).Content.Kind);
    }
    [Fact]
    public void ExplicitConfigureReadingStillControlsSuppressionAndRetainsItsLocation()
    {
        using var parser = External();
        parser.ConfigureDeclarationReading(false, PriorAt);
        parser.SeedPriorModel(new Model([], [], [], [], []), Ct);
        Assert.Equal(PriorAt, parser.FirstUnreadParameter);
        Assert.Equal(DtdParseStep.Completed, parser.Append("<!ELEMENT r EMPTY><!ATTLIST r a CDATA 'ignored'>", [], true, Ct).Step);
        Assert.Equal(PriorAt, parser.Model.UnreadParameterLocation);
        Assert.Empty(parser.Model.Attributes);
    }
    [Fact]
    public void FailedAndCanceledSeedLeaveRetainedMetadataUnpublished()
    {
        var source = new Model([], [], [], [], [], unreadParameterLocation: PriorAt);
        using var parser = External();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => parser.SeedPriorModel(source, canceled.Token)).CancellationToken);
        Assert.Null(Retained(parser));
        var broken = new Model([], [], [], [], [null!], unreadParameterLocation: PriorAt);
        Assert.Throws<NullReferenceException>(() => parser.SeedPriorModel(broken, Ct));
        Assert.Null(Retained(parser));
        parser.SeedPriorModel(source, Ct);
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Equal(PriorAt, parser.Model.UnreadParameterLocation);
    }
    [Fact]
    public void MidStagingCancellationDoesNotPublishAnyMetadataOrCollections()
    {
        using var cancellation = new CancellationTokenSource();
        var element = new DtdElementDeclaration("r", new(DtdParticleKind.Empty, null, DtdOccurrence.Once, []), false, default);
        var source = new Model([element], [], [], [], [], unreadParameterLocation: PriorAt);
        // Fault injection only: cancel after the first staged element has been copied.
        typeof(Model).GetField("<Elements>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(source, new CancelAfterElement(element, cancellation));
        using var parser = External();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => parser.SeedPriorModel(source, cancellation.Token)).CancellationToken);
        Assert.Null(Retained(parser));
        Assert.Null(parser.FirstUnreadParameter);
        parser.SeedPriorModel(new Model([], [], [], [], []), Ct);
        Assert.Equal(DtdParseStep.Completed, parser.Append([], [], true, Ct).Step);
        Assert.Null(parser.Model.UnreadParameterLocation);
        Assert.Empty(parser.Model.Elements);
    }
    [Fact]
    public void RetainedLocationIsGetterOnlyScalarStorageWithStraightFieldLoadIl()
    {
        Assert.StartsWith(".NET 10.", RuntimeInformation.FrameworkDescription);
        var property = typeof(Model).GetProperty("UnreadParameterLocation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(property.SetMethod);
        Assert.Equal(typeof(XmlLocation?), property.PropertyType);
        var il = property.GetMethod!.GetMethodBody()!.GetILAsByteArray()!;
        Assert.Equal(7, il.Length);
        Assert.Equal(0x02, il[0]);
        Assert.Equal(0x7B, il[1]);
        Assert.Equal(0x2A, il[6]);
        Assert.Equal(typeof(XmlLocation?), property.GetMethod.Module.ResolveField(BitConverter.ToInt32(il, 2))!.FieldType);
        Assert.Equal(40, Unsafe.SizeOf<XmlLocation?>());
        var constructor = typeof(Model).GetConstructors().Single();
        var constructorIl = constructor.GetMethodBody()!.GetILAsByteArray()!;
        var token = property.GetMethod.Module.ResolveField(BitConverter.ToInt32(il, 2))!.MetadataToken;
        byte[] store = [0x02, 0x0E, 0x07, 0x7D, .. BitConverter.GetBytes(token)];
        Assert.True(constructorIl.AsSpan().IndexOf(store) >= 0); // ldarg.0; ldarg.s 7; stfld
        Assert.True(constructor.GetParameters()[^1].IsOptional);
        Assert.Null(constructor.GetParameters()[^1].DefaultValue);
        Assert.Equal(PriorAt, new Model([], [], [], [], [], unreadParameterLocation: PriorAt).UnreadParameterLocation);
    }
    private static XmlLocation? Retained(DtdParser parser) => (XmlLocation?)typeof(DtdParser)
        .GetField("priorUnreadParameterLocation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(parser);
    private static DtdParser External() => new(DtdSubsetKind.External, new(), new Ledger(), new XmlLocation(Child, 1, 1, null));
    private sealed class Ledger : IDtdBudget
    {
        public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location) => null;
        public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location) => null;
        public XmlDiagnostic? EnterEntity(XmlLocation location) => null;
        public void ExitEntity()
        {
        }
    }
    private sealed class CancelAfterElement(DtdElementDeclaration element, CancellationTokenSource cancellation) : IReadOnlyList<DtdElementDeclaration>
    {
        public int Count => 1; public DtdElementDeclaration this[int index] => index == 0 ? element : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<DtdElementDeclaration> GetEnumerator()
        {
            yield return element;
            cancellation.Cancel();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class Resolver(string external, string parameter = "") : XmlResourceResolver
    {
        private XmlResolvedResource ResolveCore(XmlResourceRequest request, bool asyncOnly)
        {
            var pe = request.SystemId == "urn:pe";
            return new(new ChunkStream(Encoding.UTF8.GetBytes(pe ? parameter : external), asyncOnly), pe ? new Uri("urn:dtd:pe") : Child);
        }
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ResolveCore(request, false);
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return ResolveCore(request, true);
        }
    }
    private sealed class ChunkStream(byte[] bytes, bool asyncOnly = true) : MemoryStream(bytes, false)
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
