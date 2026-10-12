using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdPublicationCheckpoints;

public sealed class ParsingTests
{
    [Theory]
    [InlineData(false, false, 0)] [InlineData(false, false, 1)]
    [InlineData(false, true, 0)] [InlineData(false, true, 1)]
    [InlineData(true, false, 0)] [InlineData(true, false, 1)]
    [InlineData(true, true, 0)] [InlineData(true, true, 1)]
    public void PrincipalAndIncludeScalarCaps(bool stream, bool supplementary, int over)
    {
        var text = supplementary ? "A😀é" : "ASCII";
        var principal = CheckpointTests.Wrap($"<xs:include schemaLocation='child.xsd'/><xs:annotation><xs:documentation>{text}</xs:documentation></xs:annotation>");
        var child = CheckpointTests.Wrap($"<xs:annotation><xs:documentation>{text}</xs:documentation></xs:annotation><xs:element name='r'/>");
        var total = principal.EnumerateRunes().Count() + child.EnumerateRunes().Count();
        var transferred = new CountStream(child);
        var context = new SchemaCompileContext(new() { BaseUri = CheckpointTests.Main, Limits = new() { DocumentCharacters = total - over }, Resolver = new Resolver(_ => new(transferred, new Uri("https://redirect.test/child.xsd"))) });
        using var caller = new CountStream(principal);
        var document = stream ? context.Parse(caller).Document : context.Parse(principal).Document;
        if (over == 0)
        {
            var graph = new XmlSchemaCompiler().Compile(document, context);
            Assert.Equal(new Uri("https://redirect.test/child.xsd"), Assert.Single(graph.Elements).Location.SourceUri);
            Assert.True(context.Operation.IsComplete);
        }
        else
        {
            var error = Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context));
            Assert.Equal("SCHEMA.Limit", error.Diagnostics[^1].Code); Assert.False(context.Operation.IsComplete);
        }
        Assert.Equal(1, transferred.Disposals); Assert.Equal(0, caller.Disposals); Assert.True(caller.CanRead);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(true, 0)] [InlineData(true, 1)]
    public void ExpansionCapsSharePrincipalAndInclude(bool stream, int over)
    {
        var principal = CheckpointTests.Wrap("<xs:include schemaLocation='child.xsd'/><xs:annotation><xs:documentation>&amp;&#x1F600;</xs:documentation></xs:annotation>");
        var child = CheckpointTests.Wrap("<xs:annotation><xs:documentation>&lt;</xs:documentation></xs:annotation><xs:element name='r'/>");
        var transferred = new CountStream(child);
        var context = new SchemaCompileContext(new() { BaseUri = CheckpointTests.Main, Limits = new() { ExpandedCharacters = 3 - over }, Resolver = new Resolver(_ => new(transferred, new Uri("urn:child"))) });
        using var caller = new CountStream(principal);
        var document = stream ? context.Parse(caller).Document : context.Parse(principal).Document;
        if (over == 0) Assert.Single(new XmlSchemaCompiler().Compile(document, context).Elements);
        else Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context)).Diagnostics[^1].Code);
        Assert.Equal(1, transferred.Disposals); Assert.Equal(0, caller.Disposals);
    }

    [Fact]
    public void WorkSpentParsingPrincipalRemainsChargedThroughIncludes()
    {
        var principal = CheckpointTests.Wrap("<xs:include schemaLocation='child.xsd'/>");
        var child = CheckpointTests.Wrap("<xs:element name='r'/>");
        var context = new SchemaCompileContext(new() { BaseUri = CheckpointTests.Main, MaxOperations = principal.Length + child.Length - 1, Resolver = new Resolver(_ => new(new CountStream(child), new Uri("urn:child"))) });
        var document = context.Parse(principal).Document;
        Assert.True(context.Operation.RemainingWork < child.Length);
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context)).Diagnostics[^1].Code);
    }

    [Theory]
    [InlineData("resolver")] [InlineData("read")] [InlineData("cancel")] [InlineData("entry")]
    public void ExternalFailuresKeepIdentityAndDisposeTransferredOwnership(string route)
    {
        var callerFailure = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", default)]);
        using var cancellation = new CancellationTokenSource();
        var transferred = new CountStream(CheckpointTests.Wrap("<xs:element name='r'/>"));
        if (route == "read") transferred.Failure = callerFailure;
        int calls = 0;
        var context = new SchemaCompileContext(new() { BaseUri = CheckpointTests.Main, CancellationToken = cancellation.Token, Resolver = new Resolver(_ =>
        {
            calls++;
            if (route == "resolver") throw callerFailure;
            if (route == "cancel") cancellation.Cancel();
            return new(transferred, new Uri("urn:child"));
        }) });
        using var caller = new CountStream(CheckpointTests.Wrap("<xs:include schemaLocation='child.xsd'/>"));
        var document = context.Parse(caller).Document;
        if (route == "entry") cancellation.Cancel();
        if (route is "cancel" or "entry") Assert.Throws<OperationCanceledException>(() => new XmlSchemaCompiler().Compile(document, context));
        else Assert.Same(callerFailure, Assert.Throws<XmlParseException>(() => new XmlSchemaCompiler().Compile(document, context)));
        Assert.Equal(route == "entry" ? 0 : 1, calls);
        Assert.Equal(route is "read" or "cancel" ? 1 : 0, transferred.Disposals);
        Assert.Equal(0, caller.Disposals);
    }

    [Fact]
    public void CallerReadFailureAndLegacyIndependentCompilationRemainSupported()
    {
        var failure = new IOException("caller read failure"); using var caller = new CountStream("") { Failure = failure };
        var context = new SchemaCompileContext(); Assert.Same(failure, Assert.Throws<IOException>(() => context.Parse(caller))); Assert.Equal(0, caller.Disposals);
        var parsed = XmlDocument.Parse(CheckpointTests.Wrap("<xs:include schemaLocation='urn:child'/>"), cancellationToken: TestContext.Current.CancellationToken);
        var external = new CountStream(CheckpointTests.Wrap("<xs:element name='r'/>"));
        var options = new SchemaCompileOptions { Resolver = new Resolver(_ => new(external, new Uri("urn:child"))) };
        Assert.Single(new XmlSchemaCompiler().Compile(parsed, options, new SchemaCompileOperation(options)).Elements); Assert.Equal(1, external.Disposals);
    }

    [Fact]
    public void ContextJoinRejectsNullArguments()
    {
        var compiler = new XmlSchemaCompiler();
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(null!, new SchemaCompileContext()));
        Assert.Throws<ArgumentNullException>(() => compiler.Compile(XmlDocument.Parse("<r/>", cancellationToken: TestContext.Current.CancellationToken), null!));
    }

    internal sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return resolve(request); }
    }
    internal sealed class CountStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal int Disposals; internal Exception? Failure;
        public override int Read(Span<byte> buffer) { if (Failure is { } error) throw error; return base.Read(buffer); }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}
