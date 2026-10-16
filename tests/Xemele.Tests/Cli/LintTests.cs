using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xemele.Html;
using Xemele.Tool;
using Xunit;

namespace Xemele.Tests.Cli;

public sealed class LintTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "xemele-lint-" + Guid.NewGuid().ToString("N"));

    private string FilePath(string name, string content)
    {
        var path = Path.Combine(directory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private static (int Code, string Output, string Error) Run(string[] args, byte[]? input = null)
    {
        using var stream = new MemoryStream(input ?? []);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var code = LintCommands.Run(args, TestContext.Current.CancellationToken, stream, output, error);
        Assert.True(stream.CanRead);
        return (code, output.ToString(), error.ToString());
    }

    [Theory]
    [InlineData("<root/>")]
    [InlineData("<?xml version='1.0'?><root>\n<child/>\n</root>")]
    public void CleanStdinIsSilent(string xml)
    {
        Assert.Equal((0, "", ""), Run([], Encoding.UTF8.GetBytes(xml)));
    }

    [Theory]
    [InlineData("<root>")]
    [InlineData("<root>\n<a>\n<b>")]
    [InlineData("<root a='1' a='2'/>")]
    [InlineData("<root>\n<child broken></root>")]
    [InlineData("<root>&unknown;</root>")]
    [InlineData("<root/><other/>")]
    [InlineData("")]
    public void XmlOutputMatchesOrderedParserDiagnostics(string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        var diagnostics = XmlDocument.ParseWithDiagnostics(bytes, new() { Recover = true },
            TestContext.Current.CancellationToken).Diagnostics;
        Assert.NotEmpty(diagnostics);
        var expected = string.Concat(diagnostics.OrderBy(d => d.Location.LineNumber).ThenBy(d => d.Location.LinePosition)
            .Select(d => $"(stdin):{d.Location.LineNumber}: {d.Code} {d.Message.Replace("\r", "\\r").Replace("\n", "\\n")}{Environment.NewLine}"));
        var result = Run([], bytes);
        Assert.Equal((1, expected, ""), result);
    }

    [Fact]
    public void HtmlDiagnosticsUseMdlOutputShapeAndNormalizedLineNumbers()
    {
        var result = Run(["--type", "html"], Encoding.UTF8.GetBytes("<!doctype html>\r\n<p a=one a=two>text</p>"));
        Assert.Equal((1, "(stdin):2: HTML.duplicate-attribute duplicate attribute" + Environment.NewLine, ""), result);
    }

    [Theory]
    [InlineData("<!doctype html><script>const a = '<p a=one a=two>'; &unknown;</script>")]
    [InlineData("<!doctype html><style>a { content: '&unknown;'; }</style>")]
    [InlineData("<!doctype html><svg><![CDATA[&unknown;<p a=one a=two>]]></svg>")]
    [InlineData("<!doctype html><math><![CDATA[&unknown;]]></math>")]
    [InlineData("<!doctype html><title>&amp; title</title><p>text")]
    public void HtmlUsesTreeBuilderTokenizerContext(string html)
    {
        Assert.Equal((0, "", ""), Run(["--type", "html"], Encoding.UTF8.GetBytes(html)));
    }

    [Theory]
    [InlineData("<p a='unterminated", "eof-in-tag")]
    [InlineData("<!--unterminated", "eof-in-comment")]
    [InlineData("<p>&unknown;</p>", "unknown-named-character-reference")]
    [InlineData("<p>&#0;</p>", "null-character-reference")]
    [InlineData("<![CDATA[not foreign]]>", "cdata-in-html-content")]
    public void HtmlReportsExistingTokenizerErrors(string html, string code)
    {
        var result = Run(["--type", "html"], Encoding.UTF8.GetBytes(html));
        Assert.Equal(1, result.Code);
        Assert.Contains("HTML." + code + " ", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData("document.html", "HTML.")]
    [InlineData("document.HTM", "HTML.")]
    [InlineData("document.xml", "XML.")]
    [InlineData("document.xhtml", "XML.")]
    [InlineData("document.txt", "XML.")]
    public void AutoSelectsParserByExtension(string name, string prefix)
    {
        var path = FilePath(name, "<p a='one' a='two'/>");
        var result = Run([path]);
        Assert.Equal(1, result.Code);
        Assert.StartsWith(path + ":1: " + prefix, result.Output);
        Assert.Empty(result.Error);
        Assert.Equal("<p a='one' a='two'/>", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("document.html", "xml", "XML.")]
    [InlineData("document.xml", "html", "HTML.")]
    public void TypeOverridesFileExtension(string name, string type, string prefix)
    {
        var path = FilePath(name, "<p a='one' a='two'/>");
        var result = Run(["--type", type, path]);
        Assert.Equal(1, result.Code);
        Assert.StartsWith(path + ":1: " + prefix, result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void DirectoriesRecurseSortIncludeHiddenFilesAndDeduplicatePaths()
    {
        var last = FilePath("nested/z.HTML", "<p a=one a=two>");
        var first = FilePath(".hidden.xml", "<r>");
        var middle = FilePath("a.xml", "<r>");
        FilePath("ignored.txt", "<r>");
        var result = Run([directory, middle, Path.Combine(directory, "nested", "..", "a.xml")]);
        Assert.Equal(1, result.Code);
        Assert.Equal(3, result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.True(result.Output.IndexOf(first, StringComparison.Ordinal) < result.Output.IndexOf(middle, StringComparison.Ordinal));
        Assert.True(result.Output.IndexOf(middle, StringComparison.Ordinal) < result.Output.IndexOf(last, StringComparison.Ordinal));
        Assert.DoesNotContain("ignored.txt", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(".xml")]
    [InlineData(".xhtml")]
    [InlineData(".svg")]
    [InlineData(".xsd")]
    [InlineData(".rng")]
    [InlineData(".sch")]
    [InlineData(".xsl")]
    [InlineData(".xslt")]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void DirectoryDiscoveryIncludesDocumentExtensions(string extension)
    {
        var path = FilePath("input" + extension, "<r a='1' a='2'/>");
        var result = Run([directory]);
        Assert.Equal(1, result.Code);
        Assert.Contains(path + ":1: ", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void EmptyDirectoryDoesNotReadStdin()
    {
        Directory.CreateDirectory(directory);
        Assert.Equal((0, "", ""), Run([directory], "<broken>"u8.ToArray()));
    }

    [Fact]
    public void DirectoryDiscoveryDoesNotFollowSymlinks()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symlinks requires Windows privileges.");
        var target = FilePath("target/input.xml", "<r>");
        Directory.CreateSymbolicLink(Path.Combine(directory, "target", "loop"), directory);
        File.CreateSymbolicLink(Path.Combine(directory, "linked.xml"), target);
        var result = Run([directory]);
        Assert.Equal(1, result.Code);
        Assert.Single(result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith(target + ":1: ", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void FileFailuresDoNotHideFindingsOrStopLaterInputs()
    {
        var missing = Path.Combine(directory, "missing.xml");
        var first = FilePath("first.xml", "<r>");
        var last = FilePath("last.xml", "<r>");
        var result = Run([first, missing, last]);
        Assert.Equal(2, result.Code);
        Assert.Contains(first + ":1: XML.", result.Output);
        Assert.Contains(last + ":1: XML.", result.Output);
        Assert.Contains("LINT.File: " + missing + ": ", result.Error);
        Assert.DoesNotContain("\n   at ", result.Error);
    }

    [Fact]
    public void StdinCanBeMixedWithFileInputsOnce()
    {
        var path = FilePath("input.xml", "<r>");
        var result = Run([path, "-"], "<s>"u8.ToArray());
        Assert.Equal(1, result.Code);
        Assert.StartsWith(path + ":1: XML.", result.Output);
        Assert.Contains("(stdin):1: XML.", result.Output);
        Assert.Empty(result.Error);
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return [new[] { "--input", "file.xml" }];
        yield return [new[] { "--type" }];
        yield return [new[] { "--type", "markdown" }];
        yield return [new[] { "--type", "xml", "--type", "html" }];
        yield return [new[] { "--allow-dtd", "--allow-dtd" }];
        yield return [new[] { "--type", "html", "--allow-dtd" }];
        yield return [new[] { "-", "-" }];
        yield return [new[] { "" }];
        yield return [new[] { " " }];
    }

    [Theory, MemberData(nameof(InvalidArguments))]
    public void InvalidArgumentsAreInvocationErrorsBeforeReading(string[] args)
    {
        var result = Run(args, "<r>"u8.ToArray());
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
        Assert.StartsWith("LINT.Invocation: ", result.Error);
    }

    [Fact]
    public void DoubleDashPreservesOptionLikePaths()
    {
        Assert.Equal(["--type", "-h"], LintInvocation.Parse(["--", "--type", "-h"]).Inputs);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpIsSuccessfulWithoutReadingStdin(string option)
    {
        var result = Run([option], "<broken>"u8.ToArray());
        Assert.Equal(0, result.Code);
        Assert.StartsWith("Usage: xemele lint ", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DtdSyntaxRequiresExplicitOptIn(bool allow)
    {
        var result = Run(allow ? ["--allow-dtd"] : [],
            "<!DOCTYPE r [<!ENTITY local 'value'>]><r>&local;</r>"u8.ToArray());
        Assert.Equal(allow ? 0 : 1, result.Code);
        Assert.Empty(result.Error);
        Assert.Equal(allow, result.Output.Length == 0);
    }

    [Fact]
    public void AllowDtdDoesNotGrantExternalResourceAccess()
    {
        var secret = FilePath("secret.txt", "private");
        var xml = $"<!DOCTYPE r [<!ENTITY secret SYSTEM '{new Uri(secret).AbsoluteUri}'>]><r>&secret;</r>";
        var result = Run(["--allow-dtd"], Encoding.UTF8.GetBytes(xml));
        Assert.Equal(1, result.Code);
        Assert.Contains("XML.", result.Output);
        Assert.DoesNotContain("private", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void XmlByteInputRetainsEncodingDetection()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("<?xml version='1.0' encoding='utf-16'?><r>text</r>")).ToArray();
        Assert.Equal((0, "", ""), Run([], bytes));
    }

    [Fact]
    public void InvalidXmlEncodingIsAFinding()
    {
        var result = Run([], [.. "<r>"u8, 0xFF, .. "</r>"u8]);
        Assert.Equal(1, result.Code);
        Assert.Contains("INPUT.InvalidEncoding ", result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HtmlDiagnosticHookPreservesByteDecodingAndRecoveryTrees(bool utf16)
    {
        var html = "<!doctype html>\r\n<p a=one a=two>\u00e9</p>";
        var bytes = utf16
            ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(html)).ToArray()
            : Encoding.Latin1.GetBytes(html);
        var diagnostics = new List<(string Code, XmlLocation Location)>();
        using var input = new MemoryStream(bytes);
        var document = Html5Parser.Load(input, new(), TestContext.Current.CancellationToken,
            (code, location) => diagnostics.Add((code, location)));
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("duplicate-attribute", diagnostic.Code);
        Assert.Equal(2, diagnostic.Location.LineNumber);
        Assert.NotNull(diagnostic.Location.ByteOffset);
        Assert.True(input.CanRead);
        Assert.Equal(HtmlSerializer.Serialize(Html5Parser.Parse(bytes)), HtmlSerializer.Serialize(document));
        Assert.Contains("\u00e9", document.RootElement.GetTextContent());
        Assert.Equal(1, Run(["--type", "html"], bytes).Code);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("html")]
    public void QuotasAreFindingsRatherThanInternalFailures(string type)
    {
        var text = string.Concat(Enumerable.Repeat("<div>", 257));
        var result = Run(["--type", type], Encoding.UTF8.GetBytes(text));
        Assert.Equal(1, result.Code);
        Assert.NotEmpty(result.Output);
        Assert.Empty(result.Error);
    }

    [Theory]
    [InlineData("xml", "XML.DiagnosticLimit", 127)]
    [InlineData("xml", "XML.DiagnosticLimit", 128)]
    [InlineData("xml", "XML.DiagnosticLimit", 129)]
    [InlineData("html", "HTML.Limit", 127)]
    [InlineData("html", "HTML.Limit", 128)]
    [InlineData("html", "HTML.Limit", 129)]
    public void DiagnosticOutputIsBoundedWithExplicitLimitNotice(string type, string code, int count)
    {
        var text = "<r>" + string.Concat(Enumerable.Repeat("<p a='1' a='2'/>", count)) + "</r>";
        var result = Run(["--type", type], Encoding.UTF8.GetBytes(text));
        Assert.Equal(1, result.Code);
        Assert.Equal(Math.Min(count, 128), result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(count >= 128, result.Output.Contains(code + " ", StringComparison.Ordinal));
        Assert.Empty(result.Error);
    }

    [Fact]
    public void HtmlQuotaAfterDiagnosticCapStillKeepsBoundedOutput()
    {
        var text = string.Concat(Enumerable.Repeat("<p a=one a=two></p>", 200)) +
            string.Concat(Enumerable.Repeat("<div>", 257));
        var result = Run(["--type", "html"], Encoding.UTF8.GetBytes(text));
        Assert.Equal(1, result.Code);
        Assert.Equal(128, result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("HTML.Limit HTML depth quota exceeded.", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void PreCanceledRunDoesNotReadInput()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new FailureStream(new InvalidOperationException("must not read"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(3, LintCommands.Run([], cancellation.Token, input, output, error));
        Assert.Empty(output.ToString());
        Assert.StartsWith("LINT.Canceled: ", error.ToString());
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("html")]
    public void CancellationDuringReadIsNotReportedAsAFinding(string type)
    {
        using var input = new FailureStream(new OperationCanceledException());
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(3, LintCommands.Run(["--type", type], TestContext.Current.CancellationToken, input, output, error));
        Assert.Empty(output.ToString());
        Assert.StartsWith("LINT.Canceled: ", error.ToString());
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData(false, 2, "LINT.File: ")]
    [InlineData(true, 3, "LINT.Internal: ")]
    public void InputFailuresAreExplicitAndLeaveBorrowedStreamOpen(bool unexpected, int expected, string prefix)
    {
        using var input = new FailureStream(unexpected ? new InvalidOperationException("failure") : new IOException("failure"));
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(expected, LintCommands.Run([], TestContext.Current.CancellationToken, input, output, error));
        Assert.Empty(output.ToString());
        Assert.StartsWith(prefix, error.ToString());
        Assert.True(input.CanRead);
    }

    public static IEnumerable<object[]> ProcessCases()
    {
        yield return [new[] { "--help" }, "", 0, "lint", ""];
        yield return [new[] { "lint", "--help" }, "", 0, "Usage: xemele lint ", ""];
        yield return [new[] { "lint" }, "<r/>", 0, "", ""];
        yield return [new[] { "lint" }, "<r>", 1, "(stdin):1: XML.", ""];
        yield return [new[] { "lint", "--type", "html" }, "<p a=one a=two>", 1, "(stdin):1: HTML.duplicate-attribute ", ""];
        yield return [new[] { "lint", "--unknown" }, "", 2, "", "LINT.Invocation: "];
    }

    [Theory, MemberData(nameof(ProcessCases))]
    public async Task RealCliRegistersLintAndUsesExpectedStreamsAndExitCodes(string[] args, string input,
        int expectedCode, string expectedOutput, string expectedError)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(typeof(LintInvocation).Assembly.Location);
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellation.Token);
            var error = process.StandardError.ReadToEndAsync(cancellation.Token);
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellation.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(cancellation.Token);
            Assert.Equal(expectedCode, process.ExitCode);
            var actualOutput = await output;
            var actualError = await error;
            if (expectedOutput.Length == 0)
            {
                Assert.Empty(actualOutput);
            }
            else
            {
                Assert.Contains(expectedOutput, actualOutput);
            }

            if (expectedError.Length == 0)
            {
                Assert.Empty(actualError);
            }
            else
            {
                Assert.StartsWith(expectedError, actualError);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FailureStream(Exception exception) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw exception;
        public override int Read(Span<byte> buffer) => throw exception;
    }
}
