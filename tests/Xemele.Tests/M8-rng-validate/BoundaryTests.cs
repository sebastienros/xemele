using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8RngValidate;

public sealed class BoundaryTests
{
    [Theory]
    [InlineData("")] [InlineData("<r>")] [InlineData("<r/><other/>")] [InlineData("<!DOCTYPE r><r/>")] [InlineData("<r>&unknown;</r>")]
    public void OwnedMalformedInputIsIncompleteAndCallerStreamStaysOpen(string text)
    {
        var schema = EvaluationTests.Schema("<empty/>");
        var result = schema.Validate(text); Assert.False(result.IsComplete); Assert.Equal("SCHEMA.Syntax", Assert.Single(result.Diagnostics).Code);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)); Assert.False(schema.Validate(stream).IsComplete); Assert.True(stream.CanRead);
        Assert.Equal("SCHEMA.Syntax", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(text)).Diagnostics[0].Code);
    }
    [Fact]
    public void FrozenOverloadsRespectCallerPositionAndDocumentProvenance()
    {
        string source = EvaluationTests.Element("<empty/>");
        var document = XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(RelaxNgSchema.Compile(document).Validate("<r/>").IsValid);
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("prefix" + source)); bytes.Position = 6;
        var schema = RelaxNgSchema.Compile(bytes); Assert.True(bytes.CanRead); Assert.Equal(bytes.Length, bytes.Position);
        using var instance = new MemoryStream("prefix<r/>"u8.ToArray()); instance.Position = 6;
        Assert.True(schema.Validate(instance).IsValid); Assert.True(instance.CanRead); Assert.Equal(instance.Length, instance.Position);
        var partial = XmlDocument.ParseWithDiagnostics("<r><![CDATA[broken", new() { Recover = true }, TestContext.Current.CancellationToken).Value!;
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Single(schema.Validate(partial).Diagnostics).Code);
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(partial)).Diagnostics[0].Code);
    }
    [Fact]
    public void InvalidOptionsAndCancellationRejectBeforeCallerRead()
    {
        var schema = EvaluationTests.Schema("<empty/>"); using var input = new ReadStream(() => throw new Exception("unexpected read"));
        Assert.Throws<ArgumentNullException>(() => RelaxNgSchema.Compile((string)null!));
        Assert.Throws<ArgumentNullException>(() => schema.Validate((XmlDocument)null!));
        Assert.Throws<ArgumentNullException>(() => schema.Validate(input, new() { Limits = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => schema.Validate(input, new() { MaxOperations = 0 }));
        Assert.Throws<ArgumentException>(() => RelaxNgSchema.Compile(input, new() { BaseUri = new("relative", UriKind.Relative) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => RelaxNgSchema.Compile(input, new() { MaxComponents = 0 }));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate(input, new() { CancellationToken = cancel.Token }));
        Assert.ThrowsAny<OperationCanceledException>(() => RelaxNgSchema.Compile(input, new() { CancellationToken = cancel.Token })); Assert.Equal(0, input.Reads);
        using var unreadable = new MemoryStream(); unreadable.Dispose();
        Assert.Throws<ArgumentException>(() => schema.Validate(unreadable)); Assert.Throws<ArgumentException>(() => RelaxNgSchema.Compile(unreadable));
    }
    [Fact]
    public void StreamExceptionsPropagateWithIdentityAndAfterReadCancellationIsChecked()
    {
        var schema = EvaluationTests.Schema("<empty/>");
        foreach (var error in new Exception[] { new IOException("caller"), new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Fatal, "caller", default)]), new ArgumentException("caller") })
        {
            using var input = new ReadStream(() => throw error);
            Assert.Same(error, Record.Exception(() => schema.Validate(input))); Assert.Same(error, Record.Exception(() => RelaxNgSchema.Compile(input))); Assert.False(input.Closed);
        }
        using var cancel = new CancellationTokenSource(); using var canceled = new ReadStream(() => cancel.Cancel(), "<r/>"u8.ToArray());
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate(canceled, new() { CancellationToken = cancel.Token })); Assert.False(canceled.Closed);
    }
    [Fact]
    public void InvalidInstanceDiagnosticRetainsByteScalarSourceLocation()
    {
        var schema = EvaluationTests.Schema("<empty/>"); using var stream = new MemoryStream("<?xml version='1.0'?>\r\n<r>é</r>"u8.ToArray());
        var instance = XmlDocument.Load(stream, null, sourceUri: new("urn:instance"), cancellationToken: TestContext.Current.CancellationToken);
        var location = Assert.Single(schema.Validate(instance).Diagnostics).Location;
        Assert.Equal(new Uri("urn:instance"), location.SourceUri); Assert.Equal(2, location.LineNumber); Assert.Equal(1, location.LinePosition); Assert.Equal(23, location.ByteOffset);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedInstanceUriResolvesRelativeXmlBaseWithOrWithoutDeclaration(bool declaration)
    {
        var schema = EvaluationTests.Schema("<attribute name='xml:base'><text/></attribute><empty/>");
        string source = (declaration ? "<?xml version='1.0'?>" : "") + "<r xml:base='child/'/>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var document = XmlDocument.Load(stream, null, new("https://example.test/input.xml"), TestContext.Current.CancellationToken);
        long revision = document.ContentRevision;
        var result = schema.Validate(document);
        Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.Empty(result.Diagnostics);
        Assert.Equal(revision, document.ContentRevision);
    }
    [Theory]
    [InlineData("<r xml:base='parent/'><child/></r>")]
    [InlineData("<r xml:base='parent/'><child xml:base='nested/'/></r>")]
    public void NestedInstanceContextsResolveInheritedRelativeXmlBase(string source)
    {
        var schema = EvaluationTests.Schema("<attribute name='xml:base'><text/></attribute><element name='child'><optional><attribute name='xml:base'><text/></attribute></optional><empty/></element>");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var document = XmlDocument.Load(stream, null, new("https://example.test/input.xml"), TestContext.Current.CancellationToken);
        long revision = document.ContentRevision;
        var result = schema.Validate(document);
        Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.Empty(result.Diagnostics);
        Assert.Equal(revision, document.ContentRevision);
    }
    private sealed class ReadStream(Action action, byte[]? bytes = null) : MemoryStream(bytes ?? [])
    {
        internal int Reads; internal bool Closed;
        public override int Read(Span<byte> buffer) { Reads++; action(); return base.Read(buffer); }
        protected override void Dispose(bool disposing) { Closed = true; base.Dispose(disposing); }
    }
}
