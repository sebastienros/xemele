using Xemele.Html;

namespace Xemele.Tool;

internal static class LintCommands
{
    internal static int Run(string[] args, CancellationToken cancellationToken = default,
        Stream? borrowedInput = null, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            output.WriteLine("Usage: xemele lint [--type auto|xml|html] [--allow-dtd] [--] [FILE|DIRECTORY|- ...]");
            output.WriteLine("Without paths (or with -), read stdin. Directories are scanned recursively.");
            output.WriteLine("Auto selects HTML for .html/.htm files, XML otherwise (including stdin).");
            output.WriteLine("Findings: path:line: CODE message on stdout; invocation/failure messages on stderr.");
            output.WriteLine("Exit codes: 0 clean; 1 findings; 2 invocation/file failure; 3 canceled/internal failure.");
            return 0;
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void cancel(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += cancel;
        try
        {
            var token = cancellation.Token;
            token.ThrowIfCancellationRequested();
            var invocation = LintInvocation.Parse(args);
            var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var code = 0;
            foreach (var path in invocation.Inputs)
            {
                token.ThrowIfCancellationRequested();
                if (path == "-")
                {
                    code = Math.Max(code, Lint(path, invocation, borrowedInput ?? Console.OpenStandardInput(), output, error, token));
                    continue;
                }

                string[] files;
                try
                {
                    files = Directory.Exists(path) ? Discover(path, token) : [path];
                }
                catch (Exception ex) when (IsFileFailure(ex))
                {
                    FileError(path, ex, error);
                    code = 2;
                    continue;
                }

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    Stream input;
                    try
                    {
                        if (!seen.Add(Path.GetFullPath(file)))
                        {
                            continue;
                        }

                        input = File.OpenRead(file);
                    }
                    catch (Exception ex) when (IsFileFailure(ex))
                    {
                        FileError(file, ex, error);
                        code = 2;
                        continue;
                    }

                    using (input)
                    {
                        code = Math.Max(code, Lint(file, invocation, input, output, error, token));
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            return code;
        }
        catch (InvocationException ex) { error.WriteLine("LINT.Invocation: " + SingleLine(ex.Message)); return 2; }
        catch (OperationCanceledException) { error.WriteLine("LINT.Canceled: Operation canceled."); return 3; }
        catch (Exception ex) { error.WriteLine("LINT.Internal: " + SingleLine(ex.Message)); return 3; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static string[] Discover(string path, CancellationToken token)
    {
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        }))
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetExtension(file).ToLowerInvariant() is ".xml" or ".xhtml" or ".svg" or
                ".xsd" or ".rng" or ".sch" or ".xsl" or ".xslt" or ".html" or ".htm")
            {
                files.Add(file);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return [.. files];
    }

    private static int Lint(string path, LintInvocation invocation, Stream input, TextWriter output,
        TextWriter error, CancellationToken token)
    {
        var diagnostics = new List<XmlDiagnostic>();
        var html = invocation.Type == "html" ||
            invocation.Type == "auto" && Path.GetExtension(path).ToLowerInvariant() is ".html" or ".htm";
        try
        {
            if (html)
            {
                var options = new Html5ParseOptions();
                Html5Parser.Load(input, options, token, (code, location) =>
                {
                    // Reserve the final bounded slot for an explicit truncation notice.
                    diagnostics.Add(diagnostics.Count == options.Limits.Diagnostics - 1
                        ? new("HTML.Limit", XmlDiagnosticSeverity.Error, "HTML diagnostic limit reached; further errors may be omitted.", location)
                        : new("HTML." + code, XmlDiagnosticSeverity.Error, code.Replace('-', ' '), location));
                });
            }
            else
            {
                var result = XmlDocument.LoadWithDiagnostics(input, new()
                {
                    Recover = true,
                    DtdProcessing = invocation.AllowDtd ? XmlDtdProcessing.Parse : XmlDtdProcessing.Prohibit
                }, token);
                diagnostics.AddRange(result.Diagnostics);
            }
        }
        catch (XmlParseException ex) { diagnostics.AddRange(ex.Diagnostics); }
        catch (InvalidOperationException ex) when (html && HtmlCommands.IsQuota(ex.Message))
        {
            var diagnostic = new XmlDiagnostic("HTML.Limit", XmlDiagnosticSeverity.Fatal, ex.Message, default);
            if (diagnostics.Count == new XmlResourceLimits().Diagnostics)
            {
                diagnostics[^1] = diagnostic;
            }
            else
            {
                diagnostics.Add(diagnostic);
            }
        }
        catch (IOException ex)
        {
            FileError(path, ex, error);
            return 2;
        }

        foreach (var diagnostic in diagnostics.OrderBy(d => d.Location.LineNumber).ThenBy(d => d.Location.LinePosition))
        {
            token.ThrowIfCancellationRequested();
            output.WriteLine(FormattableString.Invariant(
                $"{SingleLine(path == "-" ? "(stdin)" : path)}:{diagnostic.Location.LineNumber}: {diagnostic.Code} {SingleLine(diagnostic.Message)}"));
        }

        return diagnostics.Count == 0 ? 0 : 1;
    }

    private static bool IsFileFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static void FileError(string path, Exception ex, TextWriter error) =>
        error.WriteLine($"LINT.File: {SingleLine(path == "-" ? "(stdin)" : path)}: {SingleLine(ex.Message)}");

    private static string SingleLine(string text) => text.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
