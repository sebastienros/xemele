namespace Xemele.Streaming;

public sealed record XmlSaxCompletion;

public static partial class XmlSaxParser
{
    private const int InputChunkSize = 4096;
    public static void Parse(string xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: true);
        try
        {
            operation.Pump.SupplyCharacters(xml.AsMemory(), true);
            operation.Drain();
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
    }

    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(string xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: true);
        try
        {
            operation.Pump.SupplyCharacters(xml.AsMemory(), true);
            operation.Drain();
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
    }

    public static void Parse(ReadOnlySpan<byte> xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        try
        {
            // The pump may retain memory until NeedInput: copy only one bounded
            // chunk, never the whole borrowed input. No pool ownership escapes.
            byte[] scratch = new byte[InputChunkSize];
            int offset = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(scratch.Length, xml.Length - offset);
                xml.Slice(offset, count).CopyTo(scratch);
                offset += count;
                operation.Pump.Supply(scratch.AsMemory(0, count), offset == xml.Length);
            } while (operation.Drain() == StreamingStep.NeedInput);
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
    }

    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(ReadOnlySpan<byte> xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        try
        {
            // The pump may retain memory until NeedInput: copy only one bounded
            // chunk, never the whole borrowed input. No pool ownership escapes.
            byte[] scratch = new byte[InputChunkSize];
            int offset = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(scratch.Length, xml.Length - offset);
                xml.Slice(offset, count).CopyTo(scratch);
                offset += count;
                operation.Pump.Supply(scratch.AsMemory(0, count), offset == xml.Length);
            } while (operation.Drain() == StreamingStep.NeedInput);
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
    }

    public static void Parse(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead) throw new ArgumentException("Stream must be readable.", nameof(input));
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        try
        {
            byte[] scratch = new byte[InputChunkSize];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = input.Read(scratch.AsSpan());
                operation.Pump.Supply(scratch.AsMemory(0, count), count == 0);
                if (operation.Drain() != StreamingStep.NeedInput) break;
            }
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
    }

    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead) throw new ArgumentException("Stream must be readable.", nameof(input));
        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        try
        {
            byte[] scratch = new byte[InputChunkSize];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = input.Read(scratch.AsSpan());
                operation.Pump.Supply(scratch.AsMemory(0, count), count == 0);
                if (operation.Drain() != StreamingStep.NeedInput) break;
            }
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
    }

    private static void RejectRecovery(XmlParseOptions? options)
    {
        if (options?.Recover == true) throw new ArgumentException("Strict SAX parsing does not admit recovery.", nameof(options));
    }

    private sealed class SaxOperation : IDisposable
    {
        internal readonly XmlStreamingPump Pump;
        private readonly HandlerSink sink;
        private readonly CancellationToken cancellation;
        private readonly bool recover;
        private StreamingStep step;
        internal bool Failed;
        internal SaxOperation(IXmlSaxHandler handler, XmlParseOptions? options, CancellationToken cancellation, bool characterInput)
        {
            options ??= new();
            // Validate before allocating parser state or exposing any effects.
            ArgumentNullException.ThrowIfNull(options.Limits);
            options.Limits.Validate();
            cancellation.ThrowIfCancellationRequested();
            recover = options.Recover;
            this.cancellation = cancellation;
            Pump = new(options, characterInput: characterInput);
            sink = new(handler);
        }
        internal StreamingStep Drain()
        {
            do { step = Pump.Advance(sink, cancellation); } while (step == StreamingStep.Paused);
            if (step == StreamingStep.NeedResource) throw new InvalidOperationException("Synchronous SAX requires synchronous resource processing.");
            return step;
        }
        internal void FinishStrict()
        {
            if (step == StreamingStep.Rejected) throw new XmlParseException(Pump.Diagnostics);
        }
        internal XmlParseResult<XmlSaxCompletion> Result()
        {
            bool complete = step == StreamingStep.Completed;
            return new(complete || recover ? new XmlSaxCompletion() : null, complete, complete && Pump.IsWellFormed, Pump.Diagnostics);
        }
        public void Dispose()
        {
            if (!Failed) { Pump.Dispose(); return; }
            // Cleanup cannot replace the original callback/I/O/cancellation failure.
            try { Pump.Dispose(); } catch { }
        }
    }

    internal sealed class HandlerSink(IXmlSaxHandler handler) : IXmlStreamingSink
    {
        public bool StartDocument(XmlLocation location) { handler.StartDocument(); return true; }
        public bool EndDocument(XmlLocation location) { handler.EndDocument(); return true; }
        public bool StartElement(XmlStartElement element) { handler.StartElement(element); return true; }
        public bool EndElement(XmlEndElement element) { handler.EndElement(element); return true; }
        public bool Text(ReadOnlySpan<char> text, XmlLocation location) { handler.Text(text); return true; }
        public bool Comment(ReadOnlySpan<char> text, XmlLocation location) { handler.Comment(text); return true; }
        public bool ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data, XmlLocation location) { handler.ProcessingInstruction(target, data); return true; }
        public bool StartCData(XmlLocation location) { handler.StartCData(); return true; }
        public bool EndCData(XmlLocation location) { handler.EndCData(); return true; }
        public bool Declaration(XmlDeclaration declaration) { handler.Declaration(declaration); return true; }
        public bool StartDtd(XmlDocumentTypeView documentType) { handler.StartDtd(documentType); return true; }
        public bool DtdDeclaration(XmlDtdDeclaration declaration) { handler.DtdDeclaration(declaration); return true; }
        public bool EndDtd(XmlLocation location) { handler.EndDtd(); return true; }
        public bool SkippedEntity(XmlSkippedEntity entity) { handler.SkippedEntity(entity); return true; }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) { handler.IncompleteAttributeDefault(declaration); return true; }
        public bool Diagnostic(XmlDiagnostic diagnostic) { handler.Diagnostic(diagnostic); return true; }
    }
}
