using System.Text;
using System.Text.Json;
using Xemele.Validation;

namespace Xemele.Tool;

internal static class SchemaCommands
{
    internal static int Run(string command, string[] args, CancellationToken cancellationToken = default, Stream? standardInput = null)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: xemele " + command + " --schema PATH [--input PATH] [--schema-base-uri URI] [--allow-root DIRECTORY] [--catalog PATH] [--diagnostics text|json]");
            Console.WriteLine("Budgets: --compile-max-operations N --validation-max-operations N --compile-max-components N --compile-max-resources N --compile-max-resource-bytes N (positive finite integers)");
            if (command == "validate-schematron") Console.WriteLine("Optional: --phase ID --svrl PATH (use - for stdout)");
            return 0;
        }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        SchemaInvocation? invocation = null;
        Uri? inputUri = null;
        try
        {
            var token = cancellation.Token;
            token.ThrowIfCancellationRequested();
            invocation = SchemaInvocation.Parse(command, args);
            if (invocation.Get("--input") is { } inputPath) inputUri = FileUri(inputPath);
            var local = new LocalResolver(invocation.Get("--allow-root"));
            XmlResourceResolver resolver = local;
            if (invocation.Get("--catalog") is { } catalogPath)
            {
                using var catalogInput = OpenFile(catalogPath);
                using var reader = new StreamReader(catalogInput, new UTF8Encoding(false, true));
                var catalog = XmlCatalog.Parse(reader.ReadToEnd(), new() { BaseUri = FileUri(catalogPath), Resolver = local }, token);
                resolver = new CatalogResolver(catalog, local);
            }
            var compile = new SchemaCompileOptions
            {
                Resolver = resolver,
                BaseUri = invocation.Get("--schema-base-uri") is { } schemaBase ? new Uri(schemaBase) : FileUri(invocation.Get("--schema")!),
                MaxOperations = invocation.Number("--compile-max-operations", 10_000_000),
                MaxComponents = (int)invocation.Number("--compile-max-components", 100_000),
                Limits = new()
                {
                    ExternalResources = (int)invocation.Number("--compile-max-resources", 64),
                    ExternalBytes = invocation.Number("--compile-max-resource-bytes", 16_777_216)
                },
                CancellationToken = token
            };
            var validation = new ValidationOptions { MaxOperations = invocation.Number("--validation-max-operations", 10_000_000), CancellationToken = token };
            // Compile before acquiring the primary instance; both explicit files are invocation access.
            Func<Stream, ValidationResult> validate;
            SchematronResult? schematronResult = null;
            using (var schemaInput = OpenFile(invocation.Get("--schema")!))
            {
                switch (command)
                {
                    case "validate-rng":
                        var rng = RelaxNgSchema.Compile(schemaInput, compile);
                        validate = stream => rng.Validate(stream, validation);
                        break;
                    case "validate-xsd":
                        var xsd = XmlSchema.Compile(schemaInput, compile);
                        validate = stream => xsd.Validate(stream, validation);
                        break;
                    case "validate-schematron":
                        var schematron = SchematronSchema.Compile(schemaInput, new() { Schema = compile });
                        validate = stream => (schematronResult = schematron.Validate(stream, new() { Validation = validation, Phase = invocation.Get("--phase") })).Validation;
                        break;
                    default: throw new InvocationException("Unknown schema command: " + command);
                }
            }
            Stream input = invocation.Get("--input") is { } path ? OpenFile(path) : standardInput ?? Console.OpenStandardInput();
            ValidationResult result;
            try { result = validate(input); }
            finally { if (invocation.Get("--input") is not null) input.Dispose(); }
            token.ThrowIfCancellationRequested();
            // Invalid/incomplete operations never replace a requested report target.
            if (result.IsComplete && result.IsValid && invocation.Get("--svrl") is { } target)
            {
                using var staged = new MemoryStream();
                SchematronReport.SaveSvrl(schematronResult!, staged, token);
                Publish(staged, target, token);
            }
            Diagnostics(invocation, inputUri, result.IsComplete, result.IsValid, result.Diagnostics);
            return result.IsComplete && result.IsValid ? 0 : 1;
        }
        catch (SchemaCompileException ex) { Diagnostics(invocation, inputUri, false, false, ex.Diagnostics); return 1; }
        catch (XmlParseException ex) { Diagnostics(invocation, inputUri, false, false, ex.Diagnostics); return 1; }
        catch (RejectionException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (DecoderFallbackException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        catch (InvocationException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        catch (ArgumentException ex) when (command == "validate-schematron" && ex.ParamName == "phase") { Console.Error.WriteLine(ex.Message); return 2; }
        catch (OperationCanceledException) { Console.Error.WriteLine("Operation canceled."); return 3; }
        catch (Exception ex) { Console.Error.WriteLine("Internal failure: " + ex.Message); return 3; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static Uri FileUri(string path)
    {
        try { return new Uri(Path.GetFullPath(path)); }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
    }
    private static Stream OpenFile(string path)
    {
        try { return new GuardedFileInput(File.OpenRead(path), true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
    }
    private static void Diagnostics(SchemaInvocation? invocation, Uri? inputUri, bool complete, bool valid, IReadOnlyList<XmlDiagnostic> diagnostics)
    {
        if (invocation?.Get("--diagnostics") == "json")
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("inputUri", inputUri?.AbsoluteUri);
                writer.WriteBoolean("isComplete", complete); writer.WriteBoolean("isValid", valid);
                writer.WriteStartArray("diagnostics");
                foreach (var diagnostic in diagnostics)
                {
                    writer.WriteStartObject(); writer.WriteString("code", diagnostic.Code);
                    writer.WriteString("severity", diagnostic.Severity.ToString()); writer.WriteString("message", diagnostic.Message);
                    writer.WriteStartObject("location"); writer.WriteString("sourceUri", diagnostic.Location.SourceUri?.AbsoluteUri);
                    writer.WriteNumber("lineNumber", diagnostic.Location.LineNumber); writer.WriteNumber("linePosition", diagnostic.Location.LinePosition);
                    if (diagnostic.Location.ByteOffset is { } offset) writer.WriteNumber("byteOffset", offset); else writer.WriteNull("byteOffset");
                    writer.WriteEndObject(); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            Console.Error.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
        }
        else foreach (var diagnostic in diagnostics)
            Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} [source={diagnostic.Location.SourceUri?.AbsoluteUri ?? "<unknown>"}, line={diagnostic.Location.LineNumber}, column={diagnostic.Location.LinePosition}, byte={diagnostic.Location.ByteOffset?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<unknown>"}]");
    }
    private static void Publish(MemoryStream staged, string path, CancellationToken token)
    {
        staged.Position = 0;
        if (path == "-") { token.ThrowIfCancellationRequested(); staged.CopyTo(Console.OpenStandardOutput()); return; }
        string? temporary = null;
        try
        {
            string full = Path.GetFullPath(path);
            temporary = Path.Combine(Path.GetDirectoryName(full)!, ".xemele-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) staged.CopyTo(output);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, full, true);
            temporary = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw new InvocationException(ex.Message); }
        finally { if (temporary is not null) File.Delete(temporary); }
    }
}
