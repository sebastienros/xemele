using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8SchReport;

public sealed class ReportTests
{
    private static readonly XNamespace Ns = "http://purl.oclc.org/dsdl/svrl";
    private const string Reserved = "<&\"'\t\n\r]]> Ελληνικά 日本語 😀";
    private static SchematronFinding Finding(int fired, string text, bool failed = false, string? id = null,
        string? role = null, string? flag = null, string location = "subject", string test = "true()",
        IEnumerable<SchematronDiagnosticReference>? diagnostics = null, IEnumerable<SchematronPropertyReference>? properties = null) =>
        new(failed ? SchematronFindingKind.FailedAssert : SchematronFindingKind.SuccessfulReport,
            id, role, flag, 1, fired, "", "same", test, location, default, "focus", default, text, diagnostics ?? [], properties ?? []);

    private static SchematronResult Detached() => new(new ValidationResult(true,
        [new("SCH.Assert", XmlDiagnosticSeverity.Error, "failed", default)]), "#ALL", "",
        new Dictionary<string, string> { ["z"] = "urn:z", ["A"] = "urn:A" },
        [new(0, null, null), new(1, "", ""), new(2, "idle", null)],
        [new(41, 1, 7, "same", "focus"), new(90, 0, 7, "same", "focus"), new(4, 1, 7, "same", "focus")],
        [Finding(4,"second event"), Finding(41,"first",true,"","","", test:"false()",
            diagnostics:[new("d2","two"),new("d1","one")],properties:[new("p2","two"),new("p1","one")]), Finding(41,"last", location:"")]);

    private static SchematronResult Single(string text = "", string? title = null,
        string phase = "#ALL", IReadOnlyDictionary<string, string>? namespaces = null) => new(new ValidationResult(true, []),
        phase, title, namespaces ?? new Dictionary<string, string>(), [new(1, "", null)], [new(8, 1, 1, "same", "focus")], [Finding(8, text)]);

    [Fact]
    public void IndependentGoldenUsesKeysAndCollectionOrderWithEmptyPatternsAndEvents()
    {
        var expected = File.ReadAllText(Path.Combine(FindRoot(), "tests/fixtures/M8-sch-report/detached.svrl.xml")).TrimEnd('\n');
        Assert.Equal(expected, SchematronReport.SerializeSvrl(Detached(), cancellationToken: TestContext.Current.CancellationToken));
        using var stream = new MemoryStream();
        SchematronReport.SaveSvrl(Detached(), stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), stream.ToArray());
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Xemele.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root missing");
    }

    [Fact]
    public void EveryAuthoredFieldIsEscapedAndReferencesStayInDeclaredOrder()
    {
        var result = new SchematronResult(new ValidationResult(true, []), Reserved, Reserved,
            new Dictionary<string, string> { [Reserved] = Reserved }, [new(1, Reserved, Reserved)],
            [new(8, 1, 1, Reserved, "focus")],
            [new(SchematronFindingKind.SuccessfulReport,Reserved,Reserved,Reserved,1,8,Reserved,Reserved,Reserved,Reserved,
                default,"focus",default,Reserved,[new(Reserved,Reserved),new("last","")],[new(Reserved,Reserved),new("last","")])]);
        var xml = SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken);
        var root = XElement.Parse(xml);
        Assert.Equal(Reserved, (string?)root.Attribute("phase"));
        Assert.Equal(Reserved, (string?)root.Attribute("title"));
        var ns = root.Element(Ns + "ns-prefix-in-attribute-values")!;
        Assert.Equal(Reserved, (string?)ns.Attribute("prefix"));
        Assert.Equal(Reserved, (string?)ns.Attribute("uri"));
        var pattern = root.Element(Ns + "active-pattern")!;
        Assert.Equal(Reserved, (string?)pattern.Attribute("id"));
        Assert.Equal(Reserved, (string?)pattern.Attribute("name"));
        Assert.Equal(Reserved, (string?)root.Element(Ns + "fired-rule")!.Attribute("context"));
        var finding = root.Element(Ns + "successful-report")!;
        foreach (var attribute in new[] { "test", "location", "id", "role", "flag" })
        {
            Assert.Equal(Reserved, (string?)finding.Attribute(attribute));
        }

        Assert.Equal(Reserved, finding.Element(Ns + "text")!.Value);
        foreach (var kind in new[] { "diagnostic", "property" })
        {
            var rows = finding.Elements(Ns + (kind + "-reference")).ToArray();
            Assert.Equal([Reserved, "last"], rows.Select(r => (string?)r.Attribute(kind)));
            Assert.Equal([Reserved, ""], rows.Select(r => r.Value));
        }
        using var output = new MemoryStream();
        SchematronReport.SaveSvrl(result, output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(xml), output.ToArray());
        Assert.DoesNotContain("<?xml", xml);
        Assert.False(output.ToArray().AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(0xfffe)]
    [InlineData(0xffff)]
    [InlineData(0xd800)]
    [InlineData(0xdc00)]
    public void IllegalScalarsRejectInTextAndAttributesOnBothOutputs(int scalar)
    {
        // Build malformed UTF-16 at runtime: metadata strings replace lone surrogates.
        string illegal = new((char)scalar, 1);
        foreach (var result in new[]{Single(illegal),Single(title:illegal),Single(phase:illegal),
            Single(namespaces:new Dictionary<string,string>{{illegal,"ok"}}),Single(namespaces:new Dictionary<string,string>{{"ok",illegal}})})
        {
            Assert.Throws<ArgumentException>(() => SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken));
            using var stream = new TrackingStream();
            Assert.Throws<ArgumentException>(() => SchematronReport.SaveSvrl(result, stream, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(stream.Disposed);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void InvalidSurrogateCannotBypassAnyEmittedField(int field)
    {
        string Bad(int index, string good = "") => field == index ? new string((char)0xd800, 1) + "x" : good;
        var result = new SchematronResult(new ValidationResult(true, []), Bad(0, "#ALL"), Bad(1),
            new Dictionary<string, string> { { Bad(2, "prefix"), Bad(3, "uri") } }, [new(1, Bad(4), Bad(5))],
            [new(8, 1, 1, Bad(6, "same"), "focus")],
            [new(SchematronFindingKind.SuccessfulReport,Bad(9),Bad(10),Bad(11),1,8,Bad(4),Bad(6,"same"),Bad(7,"test"),Bad(8,"location"),
                default,"focus",default,Bad(12),[new(Bad(13,"d"),Bad(14))],[new(Bad(15,"p"),Bad(16))])]);
        Assert.Throws<ArgumentException>(() => SchematronReport.SerializeSvrl(result, TestContext.Current.CancellationToken));
        using var output = new TrackingStream();
        Assert.Throws<ArgumentException>(() => SchematronReport.SaveSvrl(result, output, TestContext.Current.CancellationToken));
        Assert.False(output.Disposed);
    }

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(4095)]
    [InlineData(8191)]
    public void AstralAndEscapeBoundaryParity(int length)
    {
        var text = new string('a', length) + "😀]]>&\r" + new string('b', length) + "😀";
        var xml = SchematronReport.SerializeSvrl(Single(text), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(text, XElement.Parse(xml).Element(Ns + "successful-report")!.Element(Ns + "text")!.Value);
        using var stream = new MemoryStream();
        SchematronReport.SaveSvrl(Single(text), stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(xml), stream.ToArray());
    }

    [Fact]
    public void PrefixSortingUsesOrdinalOrderAcrossCultures()
    {
        var result = Single(namespaces: new Dictionary<string, string> { { "ı", "4" }, { "I", "1" }, { "i", "2" }, { "İ", "3" }, { "", "0" } });
        string? previous = null;
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "tr-TR", "ja-JP" })
            {
                CultureInfo.CurrentCulture = new(name);
                var xml = SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(["", "I", "i", "İ", "ı"], XElement.Parse(xml).Elements(Ns + "ns-prefix-in-attribute-values").Select(e => (string?)e.Attribute("prefix")));
                if (previous is not null)
                {
                    Assert.Equal(previous, xml);
                }

                previous = xml;
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void CompleteAndIncompleteStatusAddsNoExtraProfile(bool complete, bool valid)
    {
        var findings = valid || !complete ? [] : new[] { Finding(8, "fail", true) };
        var diagnostics = valid ? [] : new[] { new XmlDiagnostic("TEST", complete ? XmlDiagnosticSeverity.Error : XmlDiagnosticSeverity.Fatal, "message", default) };
        var result = new SchematronResult(new ValidationResult(complete, diagnostics), "#ALL", null, new Dictionary<string, string>(), [new(1, "", null)], [new(8, 1, 1, "same", "focus")], findings);
        var root = XElement.Parse(SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(["phase"], root.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => a.Name.LocalName));
        Assert.Equal(valid || !complete ? 2 : 3, root.Elements().Count());
    }

    [Fact]
    public void ConcurrentRepeatabilityAndDetachedUseAfterDocumentMutation()
    {
        var schema = SchematronSchema.Compile("<schema xmlns='http://purl.oclc.org/dsdl/schematron'><title>Title</title><pattern><rule context='item' subject='@code'><report test='true()'>hi <name/>:<value-of select='@code'/></report></rule></pattern></schema>");
        var document = XmlDocument.Parse("<root><item code='😀'/><item code='x'/></root>", cancellationToken: TestContext.Current.CancellationToken);
        var result = schema.Validate(document);
        var expected = SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken);
        document.RootElement.SetTextContent("removed");
        Parallel.For(0, 32, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken }, _ => { Assert.Equal(expected, SchematronReport.SerializeSvrl(result, cancellationToken: TestContext.Current.CancellationToken)); using var output = new MemoryStream(); SchematronReport.SaveSvrl(result, output, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(Encoding.UTF8.GetBytes(expected), output.ToArray()); });
        var root = XElement.Parse(expected);
        Assert.Equal("Title", (string?)root.Attribute("title"));
        Assert.Equal(["hi item:😀", "hi item:x"], root.Elements(Ns + "successful-report").Select(e => e.Element(Ns + "text")!.Value));
        Assert.All(result.Findings, f => Assert.NotEqual(f.ContextLocation, f.Location));
    }

    [Fact]
    public void ArgumentGuardsAndEntryCancellationDoNotWriteOrDispose()
    {
        Assert.Throws<ArgumentNullException>(() => SchematronReport.SerializeSvrl(null!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => SchematronReport.SaveSvrl(null!, Stream.Null, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => SchematronReport.SaveSvrl(Single(), null!, cancellationToken: TestContext.Current.CancellationToken));
        using var readOnly = new MemoryStream([], false);
        Assert.Throws<ArgumentException>(() => SchematronReport.SaveSvrl(Single(), readOnly, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(readOnly.CanRead);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var output = new TrackingStream();
        Assert.Throws<OperationCanceledException>(() => SchematronReport.SerializeSvrl(Single(), cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => SchematronReport.SaveSvrl(Single(), output, cancelled.Token));
        Assert.Equal(0, output.Writes);
        Assert.False(output.Disposed);
    }

    [Fact]
    public void NonseekableStreamStartsAtCurrentPositionAndStaysOpen()
    {
        using var output = new TrackingStream();
        output.Write("prefix"u8);
        SchematronReport.SaveSvrl(Single("😀"), output, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(output.CanSeek);
        Assert.False(output.Disposed);
        Assert.Equal("prefix" + SchematronReport.SerializeSvrl(Single("😀"), cancellationToken: TestContext.Current.CancellationToken), Encoding.UTF8.GetString(output.Data.ToArray()));
        Assert.Equal(0, output.Flushes);
    }

    [Fact]
    public void WriteExceptionIdentitySurvivesAndOutputRemainsOpen()
    {
        var error = new IOException("caller");
        using var output = new TrackingStream { Error = error };
        Assert.Same(error, Assert.Throws<IOException>(() => SchematronReport.SaveSvrl(Single(new string('x', 20000)), output, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.False(output.Disposed);
    }

    [Fact]
    public void CallbackCancellationExceptionIdentitySurvives()
    {
        var error = new OperationCanceledException("caller");
        using var output = new TrackingStream { Error = error };
        Assert.Same(error, Assert.Throws<OperationCanceledException>(() => SchematronReport.SaveSvrl(Single(), output, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.False(output.Disposed);
    }

    [Fact]
    public void CancellationDuringLargeTextOutputPreventsFurtherWrites()
    {
        using var cancel = new CancellationTokenSource();
        using var output = new TrackingStream { AfterWrite = cancel.Cancel };
        var error = Assert.Throws<OperationCanceledException>(() => SchematronReport.SaveSvrl(Single(new string('x', 200000)), output, cancel.Token));
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(1, output.Writes);
        Assert.False(output.Disposed);
    }

    [Fact]
    public void CancellationDuringLargeReferenceCollectionPreventsFurtherWrites()
    {
        var rows = Enumerable.Range(0, 10000).Select(i => new SchematronDiagnosticReference("d", "message")).ToArray();
        var result = new SchematronResult(new ValidationResult(true, []), "#ALL", null, new Dictionary<string, string>(), [new(1, "", null)], [new(8, 1, 1, "same", "focus")], [Finding(8, "", diagnostics: rows)]);
        using var cancel = new CancellationTokenSource();
        using var output = new TrackingStream { AfterWrite = cancel.Cancel };
        Assert.Throws<OperationCanceledException>(() => SchematronReport.SaveSvrl(result, output, cancel.Token));
        Assert.Equal(1, output.Writes);
        Assert.False(output.Disposed);
    }

    [Fact]
    public void CancellationAfterFinalWriteStillThrows()
    {
        using var cancel = new CancellationTokenSource();
        using var output = new TrackingStream { AfterWrite = cancel.Cancel };
        Assert.Throws<OperationCanceledException>(() => SchematronReport.SaveSvrl(Single(), output, cancel.Token));
        Assert.Equal(1, output.Writes);
        Assert.False(output.Disposed);
    }

    private sealed class TrackingStream : Stream
    {
        internal MemoryStream Data { get; } = new(); internal bool Disposed; internal int Writes, Flushes;
        internal Exception? Error; internal Action? AfterWrite;
        public override bool CanRead => false; public override bool CanWrite => true; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position
        {
            get => throw new NotSupportedException(); set => throw new NotSupportedException();
        }
        public override void Flush() => Flushes++;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            if (Error is not null)
            {
                throw Error;
            }

            Data.Write(buffer);
            AfterWrite?.Invoke();
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing)
            {
                Data.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
