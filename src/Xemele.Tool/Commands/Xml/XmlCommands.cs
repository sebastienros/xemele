using System.Text;
using Xemele.Serialization;
using Xemele.Validation;
using Xemele.XPath;
namespace Xemele.Tool;
internal static class XmlCommands
{
    internal static int Run(string command, string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: xemele " + command + " [--input PATH] [--output PATH] [--base-uri URI] [--allow-root DIRECTORY] [--catalog PATH]");
            Console.WriteLine(command switch { "xpath" => "Required: --expression EXPR", "validate-dtd" => "Required: --dtd PATH", "catalog" => "Required: --catalog PATH --uri URI; optional: --system-id ID --public-id ID", "canonicalize" => "Optional: --exclusive --with-comments --allow-dtd", _ => "Optional: --allow-dtd" });
            return 0;
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        CancellationToken token = cancellation.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            var invocation = XmlInvocation.Parse(command, args);
            var local = new LocalResolver(invocation.Get("--allow-root"));
            Uri? source = invocation.Get("--base-uri") is { } baseText ? new Uri(baseText) : invocation.Get("--input") is { } inputPath ? InvocationUri(inputPath) : null;
            XmlResourceResolver resolver = local;
            XmlCatalog? catalog = null;
            if (invocation.Get("--catalog") is { } catalogPath)
            {
                string text = ReadText(catalogPath);
                catalog = XmlCatalog.Parse(text, new() { BaseUri = InvocationUri(catalogPath), Resolver = local }, token);
                resolver = new CatalogResolver(catalog, local);
            }
            using var staged = new MemoryStream(); // Owned result: no rejected operation publishes partial output.
            if (command == "catalog")
            {
                var request = new XmlResourceRequest(new Uri(invocation.Get("--uri")!), source, invocation.Get("--system-id") is not null || invocation.Get("--public-id") is not null ? XmlResourceKind.ExternalEntity : XmlResourceKind.XIncludeXml)
                { SystemId = invocation.Get("--system-id"), PublicId = invocation.Get("--public-id") };
                Uri mapped = catalog!.Resolve(request, token) ?? throw new RejectionException("No catalog mapping.");
                WriteText(staged, mapped.AbsoluteUri + "\n");
            }
            else
            {
                var options = new XmlParseOptions { DtdProcessing = invocation.Has("--allow-dtd") ? XmlDtdProcessing.Parse : XmlDtdProcessing.Prohibit, Resolver = resolver };
                XmlDocument document;
                Stream input = OpenInput(invocation.Get("--input"));
                try { document = XmlDocument.Load(input, options, token); }
                finally { if (invocation.Get("--input") is not null) input.Dispose(); }
                switch (command)
                {
                    case "validate-dtd":
                        var validation = DtdSchema.Compile(ReadText(invocation.Get("--dtd")!), resolver, cancellationToken: token).Validate(document, new() { CancellationToken = token });
                        if (!validation.IsValid || !validation.IsComplete) throw new RejectionException(string.Join("; ", validation.Diagnostics.Select(d => d.Message)));
                        break;
                    case "canonicalize":
                        XmlCanonicalizer.Canonicalize(document, staged, new() { Mode = invocation.Has("--exclusive") ? XmlCanonicalizationMode.Exclusive10 : XmlCanonicalizationMode.Inclusive10, IncludeComments = invocation.Has("--with-comments"), CancellationToken = token });
                        break;
                    case "xpath":
                        var value = XPathExpression.Compile(invocation.Get("--expression")!, cancellationToken: token).Evaluate(document.DocumentNode, new() { CancellationToken = token });
                        if (value.Kind == XPathValueKind.NodeSet)
                            foreach (var item in value.AsNodeSet()) WriteText(staged, SerializeItem(item) + "\n");
                        else WriteText(staged, value.AsString() + "\n");
                        break;
                    case "xinclude":
                        var result = XIncludeProcessor.Process(document, new() { BaseUri = source, Resolver = resolver, ParseOptions = options }, token);
                        if (!result.Success) throw new RejectionException(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
                        XmlSerializer.Serialize(result.Document!, staged, new() { OmitXmlDeclaration = true });
                        break;
                    default: XmlSerializer.Serialize(document, staged, new() { Indent = command == "format", OmitXmlDeclaration = true }); break;
                }
            }
            Publish(staged, invocation.Get("--output"));
            return 0;
        }
        catch (InvocationException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        catch (XmlParseException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (XPathCompileException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (XPathEvaluationException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (RejectionException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (OperationCanceledException) { Console.Error.WriteLine("Operation canceled."); return 3; }
        catch (Exception ex) { Console.Error.WriteLine("Internal failure: " + ex.Message); return 3; }
        finally { Console.CancelKeyPress -= cancel; }
    }
    private static Uri InvocationUri(string path)
    {
        try { return new Uri(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { throw new InvocationException(ex.Message); }
    }
    private static Stream OpenInput(string? path)
    {
        if (path is null) return Console.OpenStandardInput();
        try { return new GuardedFileInput(File.OpenRead(path), true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
    }
    private static string ReadText(string path)
    {
        try { return File.ReadAllText(path, new UTF8Encoding(false, true)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
        catch (DecoderFallbackException ex) { throw new RejectionException(ex.Message); }
    }
    private static void WriteText(Stream stream, string text) { using var writer = new StreamWriter(stream, new UTF8Encoding(false, true), leaveOpen: true); writer.Write(text); }
    private static string SerializeItem(XPathItem item)
    {
        if (item.Kind == XPathItemKind.Root) return XmlSerializer.Serialize(item.Node!.Value.OwnerDocument, new XmlSaveOptions() { OmitXmlDeclaration = true });
        if (item.Kind == XPathItemKind.Element)
        {
            var doc = new XmlDocument(); doc.DocumentNode.AppendChild(doc.ImportNode(item.Node!.Value));
            return XmlSerializer.Serialize(doc, new XmlSaveOptions() { OmitXmlDeclaration = true, NamespaceHandling = XmlNamespaceHandling.Repair });
        }
        if (item.Kind == XPathItemKind.Comment) return "<!--" + item.StringValue + "-->";
        if (item.Kind == XPathItemKind.ProcessingInstruction) return "<?" + item.Name + (item.StringValue.Length == 0 ? "" : " " + item.StringValue) + "?>";
        string escaped = item.StringValue.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("\r", "&#xD;", StringComparison.Ordinal);
        if (item.Kind is XPathItemKind.Attribute or XPathItemKind.Namespace) escaped = escaped.Replace("\t", "&#x9;", StringComparison.Ordinal).Replace("\n", "&#xA;", StringComparison.Ordinal);
        if (item.Kind == XPathItemKind.Attribute) return item.Name + "=\"" + escaped + "\"";
        if (item.Kind == XPathItemKind.Namespace) return "xmlns" + (item.LocalName.Length == 0 ? "" : ":" + item.LocalName) + "=\"" + escaped + "\"";
        return escaped;
    }
    private static void Publish(MemoryStream staged, string? path)
    {
        staged.Position = 0;
        if (path is null) { staged.CopyTo(Console.OpenStandardOutput()); return; }
        string? temporary = null;
        try
        {
            string full = Path.GetFullPath(path);
            temporary = Path.Combine(Path.GetDirectoryName(full)!, ".xemele-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) staged.CopyTo(output);
            File.Move(temporary, full, true); temporary = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
        finally { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
    }
}
