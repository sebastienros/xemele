extern alias M1;
extern alias M2;
using Xunit;
using X = M1::Xemele;
using Y = M2::Xemele;
using FoundationModels;
namespace ContractChecks;
public sealed class ContractTests
{
    [Fact]
    public void StageSurfacesAreAdditiveAndM1HasNoFutureReferences()
    {
        var m1 = typeof(X.XmlParseOptions).Assembly;
        Assert.Null(m1.GetType("Xemele.XmlResourceResolver"));
        Assert.Null(typeof(X.XmlParseOptions).GetProperty("Recover"));
        Assert.Null(typeof(X.XmlParseOptions).GetProperty("DtdProcessing"));
        foreach (var p in typeof(X.XmlParseOptions).GetProperties()) Assert.NotNull(typeof(Y.XmlParseOptions).GetProperty(p.Name));
        Assert.False(new Y.XmlParseOptions().Recover);
        Assert.Equal(Y.XmlDtdProcessing.Prohibit, new Y.XmlParseOptions().DtdProcessing);
        Assert.Null(new Y.XmlParseOptions().Resolver);
    }
    [Fact]
    public void EveryLimitHasFiniteDefaultAndUnlimitedIsExplicit()
    {
        var limits = new X.XmlResourceLimits(); limits.Validate();
        foreach (var property in typeof(X.XmlResourceLimits).GetProperties())
        {
            Assert.True(Convert.ToInt64(property.GetValue(limits)) > 0);
            var zero = new X.XmlResourceLimits(); property.SetValue(zero, Convert.ChangeType(0, Nullable.GetUnderlyingType(property.PropertyType)!));
            Assert.Throws<ArgumentOutOfRangeException>(zero.Validate);
            var unlimited = new X.XmlResourceLimits(); property.SetValue(unlimited, null); unlimited.Validate();
        }
        Assert.Equal(16_777_216, limits.DocumentCharacters);
        Assert.Equal(256, limits.Depth);
    }
    [Fact]
    public void HandlesHaveOwnerIdentityStableGrowthAndExplicitDefault()
    {
        var document = new X.XmlDocument(); var node = document.CreateElement("first"); var copy = node;
        for (int i = 0; i < 4096; i++) document.CreateElement("more");
        Assert.Equal(node, copy); Assert.Equal("first", node.Name); Assert.Same(document, node.Owner);
        var other = new X.XmlDocument(); Assert.NotEqual(node, other.CreateElement("first"));
        Assert.NotEqual(node, other.ImportNode(node)); Assert.True(default(X.XmlNode).IsDefault);
        Assert.Equal(default, default(X.XmlNode));
        Assert.Throws<InvalidOperationException>(() => default(X.XmlNode).Name);
    }
    [Fact]
    public void DiagnosticsOwnTheirCollectionAndResultsEnforceSuccessInvariant()
    {
        var diagnostic = new X.XmlDiagnostic("XML1001", X.XmlDiagnosticSeverity.Fatal, "Rejected", default);
        var source = new List<X.XmlDiagnostic> { diagnostic };
        var error = new X.XmlParseException(source); source.Clear(); Assert.Single(error.Diagnostics);
        Assert.Throws<ArgumentException>(() => new X.XmlParseException([]));
        Assert.Throws<ArgumentException>(() => new X.XmlParseResult<X.XmlDocument>(null, true, true, []));
        var rejected = new X.XmlParseResult<X.XmlDocument>(null, false, false, error.Diagnostics);
        Assert.False(rejected.IsWellFormed); Assert.Single(rejected.Diagnostics);
        Assert.Null(default(X.XmlLocation).ByteOffset); Assert.Equal(0, default(X.XmlLocation).LineNumber);
    }
    [Fact]
    public async Task ResolvedResourceTransfersOwnershipAndDisposesOnce()
    {
        var stream = new CountingStream();
        var resource = new Y.XmlResolvedResource(stream, new("https://example.test/entity"));
        resource.Dispose(); await resource.DisposeAsync(); Assert.Equal(1, stream.CloseCount);
        Assert.Throws<ArgumentException>(() => new Y.XmlResolvedResource(new MemoryStream(), new("relative", UriKind.Relative)));
    }
    private sealed class CountingStream : MemoryStream
    {
        public int CloseCount { get; private set; }
        protected override void Dispose(bool disposing) { if (disposing) CloseCount++; base.Dispose(disposing); }
    }
    [Fact]
    public void ConformanceFailuresAndExclusionsAreDistinctFromRejection()
    {
        var diagnostics = new List<M1::Xemele.Testing.ConformanceDiagnostic> { new("REF1001", "crash", null, 0, 0) };
        var outcome = new M1::Xemele.Testing.ConformanceOutcome(M1::Xemele.Testing.ConformanceStatus.RunnerFailure, diagnostics, "process exited");
        diagnostics.Clear(); Assert.Single(outcome.Diagnostics);
        Assert.NotEqual(M1::Xemele.Testing.ConformanceStatus.Rejected, outcome.Status);
        Assert.Throws<ArgumentException>(() => new M1::Xemele.Testing.ConformanceOutcome(M1::Xemele.Testing.ConformanceStatus.Excluded, []));
        Assert.Throws<ArgumentException>(() => new M1::Xemele.Testing.ConformanceOutcome(M1::Xemele.Testing.ConformanceStatus.RunnerFailure, []));
    }
    [Fact]
    public void SpanUsageCompilesAndMaterializesOnlyAtDestination()
    {
        static X.XmlDocument Parse(ReadOnlySpan<char> input, X.XmlParseOptions? options, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Assert.Equal("<x/>", input.ToString()); options!.Limits.Validate(); return new(); }
        var destination = new X.XmlDocument();
        var node = X.Usage.ParseAndImport(Parse, "<x/>", destination); Assert.Same(destination, node.Owner);
    }
    [Fact]
    public void PauseConsumesDeliveredUnitsAndEofIsExactlyOnce()
    {
        var scanner = new Pump(); var sink = new Sink { Pause = true }; int offset = 0;
        const string source = "abc";
        while (offset < source.Length)
        {
            var result = scanner.Append(source.AsSpan(offset), true, sink, TestContext.Current.CancellationToken);
            Assert.Equal(ScanStep.Paused, result.Step); Assert.Equal(1, result.Consumed); offset += result.Consumed;
        }
        Assert.Equal(ScanStep.Paused, scanner.Append([], true, sink, TestContext.Current.CancellationToken).Step);
        Assert.Equal(ScanStep.Completed, scanner.Append([], true, sink, TestContext.Current.CancellationToken).Step);
        Assert.Equal("abc", sink.Text.ToString()); Assert.Equal(1, sink.Ends);
        Assert.Throws<InvalidOperationException>(() => scanner.Append([], true, sink, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void CallbackAndCancellationExceptionsPropagateAndFaultScanner()
    {
        var scanner = new Pump(); var sink = new Sink { Error = new IOException("callback") };
        Assert.Same(sink.Error, Assert.Throws<IOException>(() => scanner.Append("x", true, sink, TestContext.Current.CancellationToken)));
        Assert.Throws<InvalidOperationException>(() => scanner.Append([], true, sink, TestContext.Current.CancellationToken));
        var canceled = new CancellationToken(true); var second = new Pump();
        Assert.Throws<OperationCanceledException>(() => second.Append("x", true, new Sink(), canceled));
    }
    [Fact]
    public void ReentrancyIsRejectedAndNoInputViewIsStored()
    {
        var scanner = new Pump(); var sink = new ReentrantSink(scanner);
        Assert.Throws<InvalidOperationException>(() => scanner.Append("x", true, sink, TestContext.Current.CancellationToken));
        Assert.All(typeof(Pump).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), f => Assert.Equal(typeof(bool), f.FieldType));
    }
    private sealed class Sink : ICharacterSink
    {
        public bool Pause; public Exception? Error; public int Ends;
        public System.Text.StringBuilder Text { get; } = new();
        public bool Fragment(ReadOnlySpan<char> fragment, bool tokenEnded)
        { if (Error is not null) throw Error; Text.Append(fragment); if (tokenEnded) Ends++; return !Pause; }
    }
    private sealed class ReentrantSink(Pump scanner) : ICharacterSink
    { public bool Fragment(ReadOnlySpan<char> fragment, bool tokenEnded) { scanner.Append([], false, this, default); return true; } }
}
