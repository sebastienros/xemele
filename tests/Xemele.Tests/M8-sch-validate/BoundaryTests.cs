using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchValidate;

public sealed class BoundaryTests
{
    private static SchematronSchema Schema() => EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"));
    private sealed class ReadStream(Action action, byte[]? data = null) : Stream
    {
        private readonly MemoryStream inner = new(data ?? []);
        internal int Reads; internal bool Closed;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }
        public override int Read(Span<byte> buffer)
        {
            Reads++;
            action();
            return inner.Read(buffer);
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        protected override void Dispose(bool disposing)
        {
            Closed = true;
            inner.Dispose();
            base.Dispose(disposing);
        }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<item>")]
    [InlineData("<item/><other/>")]
    [InlineData("<!DOCTYPE item><item/>")]
    [InlineData("<item>&unknown;</item>")]
    public void OwnedStringAndStreamSyntaxRejectionIsAnIncompleteResult(string source)
    {
        var schema = Schema();
        var result = schema.Validate(source);
        Assert.False(result.Validation.IsComplete);
        Assert.False(result.Validation.IsValid);
        Assert.Equal("SCHEMA.Syntax", Assert.Single(result.Validation.Diagnostics).Code);
        Assert.Empty(result.Findings);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var streamed = schema.Validate(stream);
        Assert.False(streamed.Validation.IsComplete);
        Assert.True(stream.CanRead);
        Assert.Equal("SCHEMA.Syntax", Assert.Single(streamed.Validation.Diagnostics).Code);
    }

    [Fact]
    public void CallerStreamPositionAndOpenStateArePreservedForBothCompileAndValidate()
    {
        var source = "prefix<schema xmlns='" + EvaluationTests.Ns + "'>" + EvaluationTests.Pattern("<report test='true()'>x</report>") + "</schema>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
        stream.Position = 6;
        var schema = SchematronSchema.Compile(stream);
        Assert.Equal(stream.Length, stream.Position);
        Assert.True(stream.CanRead);
        using var instance = new MemoryStream("prefix<item/>"u8.ToArray());
        instance.Position = 6;
        Assert.Single(schema.Validate(instance).Findings);
        Assert.Equal(instance.Length, instance.Position);
        Assert.True(instance.CanRead);
    }

    [Fact]
    public void ExternalExceptionsIncludingCallerParseExceptionsPropagateUnchanged()
    {
        var schema = Schema();
        Exception[] failures = [new IOException("caller"), new XmlParseException([new("CALLER.Syntax", XmlDiagnosticSeverity.Fatal, "caller", default)]), new InvalidOperationException("caller")];
        foreach (var failure in failures)
        {
            using var stream = new ReadStream(() => throw failure);
            Assert.Same(failure, Record.Exception(() => schema.Validate(stream)));
            Assert.False(stream.Closed);
            Assert.Same(failure, Record.Exception(() => SchematronSchema.Compile(stream)));
            Assert.False(stream.Closed);
        }
    }

    [Fact]
    public void UnknownPhaseRejectsBeforeAnyCallerReadAndOptionErrorsAreArguments()
    {
        using var stream = new ReadStream(() => throw new Exception("must not read"));
        var schema = Schema();
        Assert.Throws<ArgumentException>(() => schema.Validate(stream, new() { Phase = "unknown" }));
        Assert.Equal(0, stream.Reads);
        Assert.Throws<ArgumentNullException>(() => schema.Validate((string)null!));
        Assert.Throws<ArgumentNullException>(() => SchematronSchema.Compile((XmlDocument)null!));
        Assert.Throws<ArgumentNullException>(() => schema.Validate(stream, new() { Validation = null! }));
        Assert.Throws<ArgumentNullException>(() => schema.Validate(stream, new() { XPathLimits = null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => schema.Validate(stream, new() { Validation = new() { MaxOperations = 0 } }));
        Assert.Throws<ArgumentException>(() => SchematronSchema.Compile(stream, new() { Schema = new() { BaseUri = new Uri("relative", UriKind.Relative) } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => SchematronSchema.Compile(stream, new() { Edition = (SchematronEdition)8 }));
        Assert.Throws<ArgumentNullException>(() => SchematronSchema.Compile(stream, new() { Schema = null! }));
        Assert.Equal(0, stream.Reads);
        using var unreadable = new MemoryStream();
        unreadable.Dispose();
        Assert.Throws<ArgumentException>(() => schema.Validate(unreadable));
        Assert.Throws<ArgumentException>(() => SchematronSchema.Compile(unreadable));
    }

    [Fact]
    public void CompileOwnsSyntaxFailuresAndSupportsAllFrozenOverloads()
    {
        var source = "<schema xmlns='" + EvaluationTests.Ns + "'>" + EvaluationTests.Pattern("<assert test='true()'/>") + "</schema>";
        var document = XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(SchematronSchema.Compile(document).Validate("<item/>").Validation.IsValid);
        Assert.True(SchematronSchema.Compile(source).Validate("<item/>").Validation.IsValid);
        Assert.Equal("SCHEMA.Syntax", Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile("<schema>")).Diagnostics[0].Code);
        using var stream = new MemoryStream("<schema>"u8.ToArray());
        Assert.Equal("SCHEMA.Syntax", Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(stream)).Diagnostics[0].Code);
        Assert.True(stream.CanRead);
        Assert.Equal("SCH.QueryBinding", Assert.Throws<SchemaCompileException>(() => EvaluationTests.Compile(EvaluationTests.Pattern("<assert test='true()'/>"), "queryBinding='xpath2'")).Diagnostics[0].Code);
    }

    [Fact]
    public void IncompleteDocumentProvenanceRejectsWithoutClaimingAssertionFailure()
    {
        var recovered = XmlDocument.ParseWithDiagnostics("<item><![CDATA[broken", new()
        {
            Recover = true
        }, TestContext.Current.CancellationToken);
        Assert.NotNull(recovered.Value);
        var result = Schema().Validate(recovered.Value);
        Assert.False(result.Validation.IsComplete);
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Single(result.Validation.Diagnostics).Code);
        Assert.Empty(result.Findings);
        Assert.Equal("SCHEMA.IncompleteInput", Assert.Throws<SchemaCompileException>(() => SchematronSchema.Compile(recovered.Value)).Diagnostics[0].Code);
    }

    [Fact]
    public void CancellationAtEntryAndAfterCallerReadPropagatesAndKeepsStreamOpen()
    {
        var schema = Schema();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var untouched = new ReadStream(() => throw new Exception("must not read"));
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate(untouched, new() { Validation = new() { CancellationToken = cancellation.Token } }));
        Assert.Equal(0, untouched.Reads);
        Assert.ThrowsAny<OperationCanceledException>(() => SchematronSchema.Compile(untouched, new() { Schema = new() { CancellationToken = cancellation.Token } }));
        using var during = new CancellationTokenSource();
        using var stream = new ReadStream(() => during.Cancel(), "<item/>"u8.ToArray());
        Assert.ThrowsAny<OperationCanceledException>(() => schema.Validate(stream, new() { Validation = new() { CancellationToken = during.Token } }));
        Assert.False(stream.Closed);
    }
}
