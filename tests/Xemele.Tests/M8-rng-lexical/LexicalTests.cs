using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8RngLexical;

public sealed class LexicalTests
{
    private const string Ns = "http://relaxng.org/ns/structure/1.0";
    internal static string Root(string attributes = "", string content = "<empty/>") =>
        "<element xmlns='" + Ns + "' name='r' " + attributes + ">" + content + "</element>";
    private static string FixtureRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tests/fixtures/M8-rng-lexical");
            if (Directory.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repair fixtures unavailable.");
    }

    [Theory]
    [InlineData("055", true)]
    [InlineData("056", false)]
    [InlineData("057", false)]
    [InlineData("058", false)]
    [InlineData("070", false)]
    [InlineData("072", false)]
    [InlineData("073", false)]
    [InlineData("074", false)]
    [InlineData("079", false)]
    public void OriginalJingPredicatesRemainUnchanged(string id, bool accepted)
    {
        string root = FixtureRoot();
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, "jing-" + id + ".rng"));
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "witnesses.json")));
        var item = inventory.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "jing-" + id);
        Assert.Equal(item.GetProperty("schema").GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.Equal(accepted ? "accepted" : "rejected", item.GetProperty("schemaExpected").GetString());
        using var input = new MemoryStream(bytes);
        if (accepted) Assert.True(RelaxNgSchema.Compile(input).Validate("<foo/>").IsValid);
        else Assert.Contains(Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(input)).Diagnostics, d => d.Code == "RNG.SchemaRestriction");
    }

    [Theory]
    [InlineData("")]
    [InlineData("http:ok")]
    [InlineData("urn:x")]
    [InlineData("foo:/")]
    [InlineData("foo://")]
    [InlineData("foo://a@b:80/p;v?q=[]")]
    [InlineData("foo://[2001:db8::1]:80/a")]
    [InlineData("foo:?")]
    [InlineData("foo:%23")]
    [InlineData("foo:a%20b")]
    [InlineData("foo:é space")]
    [InlineData("foo:😀")]
    [InlineData("foo:a\\b")]
    public void IndependentAbsoluteUriBoundariesAccept(string library)
    {
        Assert.True(RelaxNgSchema.Compile(Root("datatypeLibrary='" + library + "'")).Validate("<r/>").IsValid);
    }

    [Theory]
    [InlineData("foo:")]
    [InlineData("relative")]
    [InlineData("//host/path")]
    [InlineData("/path")]
    [InlineData("1foo:x")]
    [InlineData("+foo:x")]
    [InlineData("foo :x")]
    [InlineData("foo:x#")]
    [InlineData("foo:x#f")]
    [InlineData("foo:%")]
    [InlineData("foo:%0")]
    [InlineData("foo:%xx")]
    [InlineData("foo:%0g")]
    [InlineData("foo://[bad]/")]
    [InlineData("foo://[::1]:x/")]
    [InlineData("foo:[x]")]
    [InlineData("foo:/[x]")]
    public void IndependentAbsoluteUriBoundariesReject(string library)
    {
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(Root("datatypeLibrary='" + library + "'"))).Diagnostics, d => d.Code == "RNG.SchemaRestriction");
    }

    internal static RngCompiledSchema CompileGraph(string source)
    {
        var context = new SchemaCompileContext(null);
        return new RelaxNgCompiler().Compile(context.Parse(source).Document, context.Options, context.Operation);
    }

    [Theory]
    [InlineData("foo:é space", "foo:%C3%A9%20space")]
    [InlineData("foo:😀", "foo:%F0%9F%98%80")]
    [InlineData("foo:a\\b", "foo:a%5Cb")]
    [InlineData("foo:%23", "foo:%23")]
    public void InheritedLibraryPersistsNormativeEscapeIdentity(string raw, string expected)
    {
        var options = new SchemaCompileOptions();
        var normalize = Normalizer(options, new SchemaCompileOperation(options));
        Assert.Equal(expected, normalize(raw, default));
        Assert.Equal(expected, normalize(expected, default));
        // Unsupported custom libraries still pass lexical checks before the
        // existing datatype-library restriction; no registry change is implied.
        Assert.Equal("RNG.Datatype", Assert.Throws<SchemaCompileException>(() => CompileGraph(Root("datatypeLibrary='" + raw + "'", "<data type='custom'/>"))).Diagnostics[0].Code);
        var empty = CompileGraph(Root("datatypeLibrary='" + raw + "'", "<data datatypeLibrary='' type='string'/>"));
        Assert.Equal("", Assert.Single(empty.Patterns, p => p.Datatype is not null).Datatype!.Value.NamespaceUri);
    }

    private static IEnumerable<string> NamePositions(string name)
    {
        yield return "<element xmlns='" + Ns + "' name='" + name + "'><empty/></element>";
        yield return Root(content: "<element><name>" + name + "</name><empty/></element>");
        yield return Root(content: "<attribute name='" + name + "'/>");
        yield return Root(content: "<attribute><name>" + name + "</name></attribute>");
        yield return "<grammar xmlns='" + Ns + "'><start><ref name='" + name + "'/></start><define name='" + name + "'>" + Root() + "</define></grammar>";
        yield return Root("xmlns:p='urn:n'", "<element name='p:" + name + "'><empty/></element>");
        yield return Root("xmlns:p='urn:n'", "<element><name>p:" + name + "</name><empty/></element>");
    }

    [Theory]
    [InlineData("_", true)]
    [InlineData("aี", true)]
    [InlineData("ีa", false)]
    [InlineData("a·", true)]
    [InlineData("·a", false)]
    [InlineData("À", true)]
    [InlineData("×", false)]
    [InlineData("ͅ", false)]
    [InlineData("͆", false)]
    [InlineData("一", true)]
    [InlineData("龥", true)]
    [InlineData("龦", false)]
    [InlineData("가", true)]
    [InlineData("힣", true)]
    [InlineData("힤", false)]
    [InlineData("𐀀", false)]
    [InlineData("a𐀀", false)]
    [InlineData("a:b:c", false)]
    [InlineData("", false)]
    public void HistoricalNamesHaveIndependentStartContinuationAndUnicodeBoundaries(string name, bool accepted)
    {
        foreach (string source in NamePositions(name))
        {
            if (accepted) Assert.NotEmpty(CompileGraph(source).Patterns);
            else Assert.Contains(Assert.Throws<SchemaCompileException>(() => CompileGraph(source)).Diagnostics, d => d.Code == "RNG.SchemaRestriction");
        }
    }

    [Theory]
    [InlineData("<ref name='ี'/>")]
    [InlineData("<parentRef name='ี'/>")]
    [InlineData("<data type='ี'/>")]
    [InlineData("<value type='ี'>v</value>")]
    [InlineData("<data type='string'><param name='ี'>v</param></data>")]
    public void EveryNcNamePositionIsCheckedEvenInDiscardedPatterns(string pattern)
    {
        string source = "<grammar xmlns='" + Ns + "'><start>" + Root() + "</start><define name='unused'>" + pattern + "</define></grammar>";
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => CompileGraph(source)).Diagnostics, d => d.Code == "RNG.SchemaRestriction");
    }

    [Theory]
    [InlineData("datatypeLibrary", 0xD800)]
    [InlineData("datatypeLibrary", 0xDC00)]
    [InlineData("name", 0xD800)]
    [InlineData("name", 0xDC00)]
    public void MalformedUtf16CannotEnterDocumentsOrPublishSchemas(string attribute, int surrogate)
    {
        // Metadata string literals cannot preserve an unpaired UTF-16 unit;
        // construct it at runtime so the test really exercises malformed input.
        string value = (attribute == "name" ? "a" : "foo:x") + (char)surrogate;
        var document = XmlDocument.Parse(Root(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.ThrowsAny<ArgumentException>(() => document.RootElement.SetAttribute(attribute, value));
        string source = attribute == "name" ? "<element xmlns='" + Ns + "' name='" + value + "'><empty/></element>" : Root("datatypeLibrary='" + value + "'");
        Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(source));
        if (attribute == "datatypeLibrary")
        {
            var options = new SchemaCompileOptions();
            var error = Assert.Throws<SchemaCompileException>(() => Normalizer(options, new SchemaCompileOperation(options))(value, default));
            Assert.Equal("RNG.SchemaRestriction", Assert.Single(error.Diagnostics).Code);
        }
    }

    [Fact]
    public void LexicalDiagnosticPreservesChildResourceSource()
    {
        var final = new Uri("https://example.test/final.rng");
        var options = new SchemaCompileOptions { Resolver = new Resolver(Root("datatypeLibrary='foo:%xx'"), final) };
        var error = Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile("<externalRef xmlns='" + Ns + "' href='urn:child'/>", options));
        var diagnostic = Assert.Single(error.Diagnostics);
        Assert.Equal("RNG.SchemaRestriction", diagnostic.Code); Assert.Equal(final, diagnostic.Location.SourceUri);
        Assert.Equal(1, diagnostic.Location.LineNumber); Assert.True(diagnostic.Location.LinePosition > 0); Assert.NotNull(diagnostic.Location.ByteOffset);
    }

    private sealed class Resolver(string text, Uri final) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token = default) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), final);
        public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken token = default) => ValueTask.FromResult(Resolve(request, token));
    }

    [Theory]
    [InlineData("foo:" + "é x", "aี")]
    [InlineData("foo://[2001:db8::1]:80/a?q", "一")]
    [InlineData("foo:%20", "a·")]
    public void AllLexicalScansShareExactWorkAndComponentQuotas(string library, string name)
    {
        var document = XmlDocument.Parse("<element xmlns='" + Ns + "' name='" + name + "' datatypeLibrary='" + library + "'><empty/></element>", cancellationToken: TestContext.Current.CancellationToken);
        var observed = new Observed(new());
        new RelaxNgCompiler().Compile(document, new(), observed);
        Assert.True(observed.Work > 0); Assert.True(observed.Components > 0);
        var exact = new SchemaCompileOptions { MaxOperations = observed.Work, MaxComponents = observed.Components };
        new RelaxNgCompiler().Compile(document, exact, new SchemaCompileOperation(exact));
        foreach (var limits in new[] {
            exact with { MaxOperations = observed.Work - 1 },
            exact with { Limits = new() { EvaluationWork = observed.Work - 1, Diagnostics = 1 } },
            exact with { MaxComponents = observed.Components - 1 } })
        {
            var error = Assert.Throws<SchemaCompileException>(() => new RelaxNgCompiler().Compile(document, limits, new SchemaCompileOperation(limits)));
            Assert.Equal("SCHEMA.Limit", Assert.Single(error.Diagnostics).Code);
        }
    }

    [Fact]
    public void LongProjectedUriScanStopsAtItsQuotaAndCancellationWithSourceLocation()
    {
        string library = "foo:" + new string('a', 4096);
        var document = XmlDocument.Parse(Root("datatypeLibrary='" + library + "'"), cancellationToken: TestContext.Current.CancellationToken);
        var source = new Uri("https://example.test/root.rng");
        var options = new SchemaCompileOptions { BaseUri = source, MaxOperations = 2 * library.Length + 100, Limits = new() { Diagnostics = 1 } };
        var error = Assert.Throws<SchemaCompileException>(() => new RelaxNgCompiler().Compile(document, options, new SchemaCompileOperation(options)));
        var terminal = Assert.Single(error.Diagnostics);
        Assert.Equal("SCHEMA.Limit", terminal.Code); Assert.Equal(source, terminal.Location.SourceUri);
        Assert.Equal(1, terminal.Location.LineNumber); Assert.True(terminal.Location.LinePosition > 0);
        using var canceled = new CancellationTokenSource();
        var cancelOptions = new SchemaCompileOptions { CancellationToken = canceled.Token };
        var observed = new Observed(cancelOptions, work => { if (work == 2 * library.Length + 100) canceled.Cancel(); });
        Assert.ThrowsAny<OperationCanceledException>(() => new RelaxNgCompiler().Compile(document, cancelOptions, observed));
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => RelaxNgSchema.Compile(document, cancelOptions));
    }


    [Theory]
    [InlineData(" ", 1, 1)]
    [InlineData("é", 1, 2)]
    [InlineData("😀", 2, 4)]
    public void ProjectionHasIndependentlyDerivedExactWorkAndOwnedReservation(string escaped, int utf16Units, int utf8Bytes)
    {
        string prefix = "foo:" + new string('a', 512);
        string input = prefix + escaped;
        Assert.Equal(utf16Units, escaped.Length);
        int outputLength = prefix.Length + 3 * utf8Bytes;
        // Four scheme chars, all raw UTF-16 fragment chars, shared URI:
        // 4 scheme + 1 authority probe + body scalars; length-pass scalars;
        // exactly outputLength emitted chars. This is not an observed replay.
        long beforeOutput = 5 + prefix.Length + utf16Units + 2 * (prefix.Length + 1);
        long expectedWork = beforeOutput + outputLength;
        int expectedComponents = 2 + (outputLength + 255) / 256; // adapter + owner + blocks
        var at = new XmlLocation(new Uri("https://example.test/projected.rng"), 3, 7, 42);
        var exact = new SchemaCompileOptions { MaxOperations = expectedWork, MaxComponents = expectedComponents };
        var observed = new Observed(exact);
        string result = Normalizer(exact, observed)(input, at);
        Assert.Equal(outputLength, result.Length); Assert.StartsWith(prefix, result);
        Assert.Equal(expectedWork, observed.Work); Assert.Equal(expectedComponents, observed.Components);
        foreach (var options in new[] { exact with { MaxOperations = expectedWork - 1 }, exact with { MaxComponents = expectedComponents - 1 } })
        {
            var error = Assert.Throws<SchemaCompileException>(() => Normalizer(options, new SchemaCompileOperation(options))(input, at));
            var diagnostic = Assert.Single(error.Diagnostics);
            Assert.Equal("SCHEMA.Limit", diagnostic.Code); Assert.Equal(at, diagnostic.Location);
        }
        var duringOutput = exact with { MaxOperations = beforeOutput + prefix.Length + 1, MaxComponents = null };
        var stopped = Assert.Throws<SchemaCompileException>(() => Normalizer(duringOutput, new SchemaCompileOperation(duringOutput))(input, at));
        Assert.Equal(at, Assert.Single(stopped.Diagnostics).Location);
        using var canceled = new CancellationTokenSource();
        var unlimited = new SchemaCompileOptions { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = canceled.Token };
        var cancelOp = new Observed(unlimited, work => { if (work == beforeOutput + prefix.Length + 2) canceled.Cancel(); });
        Assert.ThrowsAny<OperationCanceledException>(() => Normalizer(unlimited, cancelOp)(input, at));
        Assert.Equal(beforeOutput + prefix.Length + 1, cancelOp.Work);
    }

    [Fact]
    public void AsciiLibraryRetainsItsOwnerWithoutOutputReservation()
    {
        string value = "foo:" + new string('a', 512);
        var options = new SchemaCompileOptions { MaxOperations = 5 + 3 * value.Length, MaxComponents = 1 };
        var observed = new Observed(options);
        Assert.Same(value, Normalizer(options, observed)(value, default));
        Assert.Equal(5 + 3 * value.Length, observed.Work); Assert.Equal(1, observed.Components);
    }

    private static Func<string, XmlLocation, string> Normalizer(SchemaCompileOptions options, ISchemaOperation operation)
    {
        var buildType = typeof(RelaxNgCompiler).GetNestedType("Build", BindingFlags.NonPublic)!;
        var build = Activator.CreateInstance(buildType, BindingFlags.Instance | BindingFlags.NonPublic, null, [options, operation], null)!;
        return buildType.GetMethod("Library", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<string, XmlLocation, string>>(build);
    }

    private sealed class Observed(SchemaCompileOptions options, Action<long>? callback = null) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(options);
        internal long Work; internal int Components;
        public XmlResourceLimits Limits => inner.Limits;
        public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete;
        public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at)
        {
            callback?.Invoke(Work + work);
            if (!inner.Charge(work, at)) return false;
            Work += work; return true;
        }
        public bool ChargeComponents(int count, XmlLocation at)
        {
            if (!inner.ChargeComponents(count, at)) return false;
            Components += count; return true;
        }
        public bool AddDiagnostic(XmlDiagnostic value) => inner.AddDiagnostic(value);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
    }
}
