using System.Text;
using SharpFuzz;
using Xemele;
using Xemele.Html;
using Xemele.Serialization;
using Xemele.Streaming;
using Xemele.Validation;
using Xemele.XPath;

// Multi-target fuzz driver: `TargetDriver <target>` reads one input from standard input under
// AFL (XEMELE_FUZZ_ENGINE=afl) or replays the files named after the target otherwise.
// Targets: html5, html-legacy, stream, xpath, xsd, rng, schematron, xinclude, serialize, writer.
//
// Multi-part inputs are split at NUL bytes (prepare_xml.py turns the "@@@@" marker lines of the
// committed seed files into NULs). Nothing here resolves from disk or network: every resource
// request is served from the input itself. Only exceptions that the public contract documents for
// malformed input are tolerated; anything else is a finding.
if (args.Length == 0 || !Targets.Map.TryGetValue(args[0], out var exercise))
{
    Console.Error.WriteLine("Usage: TargetDriver <" + string.Join("|", Targets.Map.Keys) + "> [replay files]");
    return 2;
}
if (Environment.GetEnvironmentVariable("XEMELE_FUZZ_ENGINE") == "afl")
{
    byte[] input = new byte[65537];
    Fuzzer.Run(stream =>
    {
        int length = 0, read;
        while (length < input.Length && (read = stream.Read(input, length, input.Length - length)) != 0)
            length += read;
        if (length <= 65536) exercise(input.AsSpan(0, length).ToArray());
    });
    return 0;
}
foreach (string path in args.Skip(1)) exercise(File.ReadAllBytes(path));
return 0;

static class Targets
{
    public static readonly Dictionary<string, Action<byte[]>> Map = new()
    {
        ["html5"] = Html5, ["html-legacy"] = HtmlLegacy, ["stream"] = Stream, ["xpath"] = XPathTarget,
        ["xsd"] = Xsd, ["rng"] = Rng, ["schematron"] = Schematron, ["xinclude"] = XInclude,
        ["serialize"] = Serialize, ["writer"] = Writer,
    };

    static XmlResourceLimits Limits() => new()
    {
        DocumentCharacters = 65536, Depth = 64, AttributesPerElement = 64, TokenCharacters = 65536,
        InternedNames = 4096, EntityDepth = 8, ExpandedCharacters = 131072, ExternalBytes = 65536,
        ExternalResources = 4, InclusionDepth = 4, Diagnostics = 16, EvaluationWork = 100000
    };

    static XmlParseOptions XmlOptions(XmlResourceResolver? resolver = null) =>
        new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver, Limits = Limits() };

    // Splits at NUL bytes into exactly `count` parts; missing parts are empty.
    static byte[][] Split(byte[] data, int count)
    {
        var parts = new byte[count][];
        int start = 0;
        for (int i = 0; i < count; i++)
        {
            int end = i == count - 1 ? data.Length : Array.IndexOf(data, (byte)0, start) is var z and >= 0 ? z : data.Length;
            parts[i] = data[start..end];
            start = Math.Min(data.Length, end + 1);
        }
        return parts;
    }

    static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    // Serves the input-derived parts for every resource request; never touches the file system.
    sealed class MemoryResolver(byte[] xml, byte[] text) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] body = request.Kind == XmlResourceKind.XIncludeText ? text : xml;
            return new(new MemoryStream(body, writable: false), request.Uri);
        }
    }

    // Returns at most `size` bytes per read so that every transport boundary is exercised.
    sealed class ChunkStream(byte[] data, int size) : System.IO.Stream
    {
        int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(count, size), data.Length - position);
            Array.Copy(data, position, buffer, offset, n);
            position += n;
            return n;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    static void Walk(HtmlNode root)
    {
        var stack = new Stack<HtmlNode>();
        stack.Push(root);
        int visited = 0;
        while (stack.TryPop(out var node))
        {
            visited += node.LocalName.Length + node.Value.Length + node.Attributes.Count;
            foreach (var child in node.Children) stack.Push(child);
        }
        if (visited < 0) throw new InvalidOperationException();
    }

    static void CheckHtml(HtmlDocument document)
    {
        Walk(document.DocumentNode);
        _ = HtmlSerializer.Serialize(document, new HtmlSaveOptions { Mode = HtmlSerializationMode.Html5 });
        _ = HtmlSerializer.Serialize(document, new HtmlSaveOptions { Mode = HtmlSerializationMode.Legacy });
        _ = document.DocumentNode.GetTextContent();
    }

    static void Html5(byte[] data)
    {
        var options = new Html5ParseOptions { Limits = Limits(), ScriptingEnabled = data.Length > 0 && (data[0] & 1) == 1 };
        CheckHtml(Html5Parser.Parse(data, options));
        CheckHtml(Html5Parser.Parse(Text(data), options));
        // The first byte also picks a fragment context element.
        string[] contexts = ["div", "table", "tbody", "tr", "td", "select", "template", "textarea", "title", "script", "style", "html", "body", "svg", "math", "p"];
        var owner = new HtmlDocument(Limits());
        var context = owner.CreateElement(contexts[(data.Length > 0 ? data[0] : 0) % contexts.Length]);
        Walk(Html5Parser.ParseFragment(Text(data), new HtmlFragmentContext(context), options));
    }

    static void HtmlLegacy(byte[] data)
    {
        var options = new HtmlParseOptions { Limits = Limits() };
        CheckHtml(HtmlParser.Parse(data, options));
        CheckHtml(HtmlParser.Parse(Text(data), options));
        CheckHtml(HtmlParser.Load(new ChunkStream(data, 1 + (data.Length > 0 ? data[0] % 17 : 0)), options));
    }

    sealed class Touch : XmlSaxHandler
    {
        public long Total;
        public override void StartElement(XmlStartElement element)
        {
            Total += element.LocalName.Length + element.Prefix.Length + element.NamespaceUri.Length;
            foreach (var attribute in element.Attributes) Total += attribute.LocalName.Length + attribute.Value.Length;
            foreach (var declaration in element.NamespaceDeclarations) Total += declaration.NamespaceUri.Length;
        }
        public override void EndElement(XmlEndElement element) => Total += element.LocalName.Length;
        public override void Text(ReadOnlySpan<char> text) => Total += text.Length;
        public override void Comment(ReadOnlySpan<char> text) => Total += text.Length;
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data) => Total += target.Length + data.Length;
        public override void Diagnostic(XmlDiagnostic diagnostic) => Total += diagnostic.Message.Length;
    }

    static void Stream(byte[] data)
    {
        // The first byte is the transport chunk size; the rest is the document.
        int chunk = 1 + (data.Length > 0 ? data[0] % 61 : 0);
        byte[] body = data.Length > 0 ? data[1..] : data;
        var options = XmlOptions();
        try
        {
            using var reader = Xemele.Streaming.XmlReader.Create(new ChunkStream(body, chunk), options);
            long total = 0;
            while (reader.Read())
            {
                total += reader.ValueSpan.Length + reader.LocalName.Length + reader.Target.Length;
                foreach (var attribute in reader.Attributes) total += attribute.LocalName.Length + attribute.Value.Length;
            }
        }
        catch (XmlParseException) { }
        try { XmlSaxParser.Parse(body, new Touch(), options); } catch (XmlParseException) { }
        _ = XmlSaxParser.ParseWithDiagnostics(body, new Touch(), options);
        try { XmlSaxParser.ParseAsync(new ChunkStream(body, chunk), new Touch(), options).GetAwaiter().GetResult(); }
        catch (XmlParseException) { }
        try
        {
            using var push = new XmlPushParser(new Touch(), options);
            for (int i = 0; i < body.Length; i += chunk) push.Append(body.AsSpan(i, Math.Min(chunk, body.Length - i)));
            push.Complete();
        }
        catch (XmlParseException) { }
        try { XmlDocument.LoadAsync(new ChunkStream(body, chunk), options).GetAwaiter().GetResult(); }
        catch (XmlParseException) { }
    }

    static void XPathTarget(byte[] data)
    {
        var parts = Split(data, 2);
        var compile = new XPathCompileOptions
        {
            Namespaces = new Dictionary<string, string> { ["a"] = "urn:a", ["x"] = "urn:x", ["xml"] = "http://www.w3.org/XML/1998/namespace" }
        };
        XPathExpression expression;
        try { expression = XPathExpression.Compile(Text(parts[0]), compile); }
        catch (XPathCompileException) { return; }
        if (!XmlDocument.TryParse(parts[1], out var document, out _, XmlOptions()) || document is null)
            document = XmlDocument.Parse("<r xmlns:a='urn:a' a:k='1'><a:c>t</a:c><!--c--><?p d?><d/></r>");
        try { _ = expression.Evaluate(document.RootElement, new XPathContext { Limits = new() { MaxOperations = 100000 } }); }
        catch (XPathEvaluationException) { }
    }

    static SchemaCompileOptions CompileOptions(byte[] resource) => new()
    {
        Resolver = new MemoryResolver(resource, resource), BaseUri = new Uri("file:///fuzz/schema.xml"),
        Limits = Limits(), MaxOperations = 200000, MaxComponents = 5000
    };

    static ValidationOptions ValidateOptions() => new() { Limits = Limits(), MaxOperations = 200000 };

    static void Xsd(byte[] data)
    {
        var parts = Split(data, 3);
        XmlSchema schema;
        try { schema = XmlSchema.Compile(Text(parts[0]), CompileOptions(parts[2])); }
        catch (SchemaCompileException) { return; }
        try { _ = schema.Validate(Text(parts[1]), ValidateOptions()); } catch (XmlParseException) { }
    }

    static void Rng(byte[] data)
    {
        var parts = Split(data, 3);
        RelaxNgSchema schema;
        try { schema = RelaxNgSchema.Compile(Text(parts[0]), CompileOptions(parts[2])); }
        catch (SchemaCompileException) { return; }
        try { _ = schema.Validate(Text(parts[1]), ValidateOptions()); } catch (XmlParseException) { }
    }

    static void Schematron(byte[] data)
    {
        var parts = Split(data, 3);
        SchematronSchema schema;
        try
        {
            schema = SchematronSchema.Compile(Text(parts[0]), new SchematronCompileOptions
            {
                Schema = CompileOptions(parts[2]),
                XPathLimits = new() { MaxExpressionCharacters = 4096, MaxTokens = 2048, MaxAstNodes = 2048, MaxDepth = 64 }
            });
        }
        catch (SchemaCompileException) { return; }
        try
        {
            _ = schema.Validate(Text(parts[1]), new SchematronValidationOptions
            {
                Validation = ValidateOptions(), XPathLimits = new() { MaxOperations = 100000 }
            });
        }
        catch (XmlParseException) { }
    }

    static void XInclude(byte[] data)
    {
        // main document, XML resource, text resource, catalog
        var parts = Split(data, 4);
        var memory = new MemoryResolver(parts[1], parts[2]);
        XmlResourceResolver resolver = memory;
        try
        {
            var catalog = XmlCatalog.Parse(Text(parts[3]), new XmlCatalogOptions
            {
                BaseUri = new Uri("file:///fuzz/catalog.xml"), Resolver = memory, Limits = Limits()
            });
            _ = catalog.Resolve(new XmlResourceRequest(new Uri("file:///fuzz/doc.dtd"), null, XmlResourceKind.ExternalSubset)
            { PublicId = "-//Fuzz//DTD A//EN", SystemId = "http://fuzz.example/a.dtd" });
            resolver = new CatalogResolver(catalog, memory);
        }
        catch (XmlParseException) { }
        if (!XmlDocument.TryParse(parts[0], out var document, out _, XmlOptions()) || document is null) return;
        var result = XIncludeProcessor.Process(document, new XIncludeOptions
        {
            BaseUri = new Uri("file:///fuzz/main.xml"), Resolver = resolver, ParseOptions = XmlOptions(), Limits = Limits()
        });
        if (result.Success && result.Document is null) throw new InvalidOperationException("Successful XInclude lacks its document.");
    }

    static void Serialize(byte[] data)
    {
        // The first byte selects options; the rest is the document.
        byte flags = data.Length > 0 ? data[0] : (byte)0;
        byte[] body = data.Length > 0 ? data[1..] : data;
        if (!XmlDocument.TryParse(body, out var document, out _, XmlOptions()) || document is null) return;
        var save = new XmlSaveOptions
        {
            Indent = (flags & 1) != 0, OmitXmlDeclaration = (flags & 2) != 0,
            NamespaceHandling = (flags & 4) != 0 ? XmlNamespaceHandling.Repair : XmlNamespaceHandling.Strict
        };
        try { _ = XmlSerializer.Serialize(document, save); } catch (InvalidOperationException) when ((flags & 4) == 0) { }
        try { XmlAsyncSerializer.SerializeAsync(document, new MemoryStream(), save).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) when ((flags & 4) == 0) { }
        foreach (var mode in Enum.GetValues<XmlCanonicalizationMode>())
        {
            try
            {
                XmlCanonicalizer.Canonicalize(document, new MemoryStream(), new XmlCanonicalizationOptions
                {
                    Mode = mode, IncludeComments = (flags & 8) != 0, InclusivePrefixes = (flags & 16) != 0 && mode == XmlCanonicalizationMode.Exclusive10 ? ["a", "x"] : [],
                    Limits = Limits(), MaxOutputBytes = 1_048_576
                });
            }
            // docs/decisions/M5/validation-canonicalization.md: relative namespace names are rejected.
            catch (ArgumentException e) when (e.Message == "Relative namespace name.") { }
        }
    }

    // Interprets the input as a script of writer operations; sequencing and name violations the
    // writer documents (InvalidOperationException/ArgumentException) are expected, not findings.
    static void Writer(byte[] data)
    {
        int index = 0;
        byte Next() => index < data.Length ? data[index++] : (byte)0;
        string Str()
        {
            int length = Next() % 12;
            int take = Math.Min(length, data.Length - index);
            string s = Encoding.UTF8.GetString(data, index, Math.Max(take, 0));
            index += Math.Max(take, 0);
            return s;
        }
        byte flags = Next();
        var options = new XmlSaveOptions
        {
            Indent = (flags & 1) != 0, OmitXmlDeclaration = (flags & 2) != 0,
            NamespaceHandling = (flags & 4) != 0 ? XmlNamespaceHandling.Repair : XmlNamespaceHandling.Strict
        };
        using var output = new MemoryStream();
        try
        {
            using var writer = Xemele.XmlWriter.Create(output, options);
            for (int steps = 0; index < data.Length && steps < 256; steps++)
            {
                switch (Next() % 10)
                {
                    case 0: writer.WriteStartDocument(); break;
                    case 1: writer.WriteStartElement(Str(), Str(), Str()); break;
                    case 2: writer.WriteNamespace(Str(), Str()); break;
                    case 3: writer.WriteAttribute(Str(), Str(), Str(), Str()); break;
                    case 4: writer.WriteText(Str()); break;
                    case 5: writer.WriteCData(Str()); break;
                    case 6: writer.WriteComment(Str()); break;
                    case 7: writer.WriteProcessingInstruction(Str(), Str()); break;
                    case 8: writer.WriteEndElement(); break;
                    default: writer.WriteEndDocument(); break;
                }
            }
            writer.Flush();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { }
    }
}
