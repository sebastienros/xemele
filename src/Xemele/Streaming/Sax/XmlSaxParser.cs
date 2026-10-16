using System.Buffers;
namespace Xemele.Streaming;

/// <summary>A completion marker carried by SAX parse results that have a value.</summary>
public sealed record XmlSaxCompletion;

/// <summary>Parses XML into synchronous SAX callbacks without building a document tree.</summary>
/// <remarks>Callback spans and ref-struct views expire when each callback returns. Exceptions from callbacks, input, and cancellation propagate to the caller.</remarks>
public static partial class XmlSaxParser
{
    private const int InputChunkSize = 4096;
    /// <summary>Parses a character string as a strict XML document.</summary>
    /// <param name="xml">The XML characters to parse.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled.</exception>
    public static void Parse(string xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(handler);
        if (TryFast(xml, handler, options, cancellationToken))
        {
            return;
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: true);
        try
        {
            operation.Pump.SupplyCharacters(xml.AsMemory(), true);
            operation.Drain();
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
    }

    /// <summary>Parses XML characters and returns completion and diagnostic information.</summary>
    /// <param name="xml">The XML characters to parse.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing and recovery options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <returns>The completion marker, input-completeness and well-formedness flags, and accumulated diagnostics.</returns>
    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(string xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(handler);
        if (TryFast(xml, handler, options, cancellationToken))
        {
            return CompleteResult();
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: true);
        try
        {
            operation.Pump.SupplyCharacters(xml.AsMemory(), true);
            operation.Drain();
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
    }

    /// <summary>Parses encoded bytes as a strict XML document.</summary>
    /// <param name="xml">The encoded XML bytes, borrowed only for this call.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled.</exception>
    public static void Parse(ReadOnlySpan<byte> xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(handler);
        if (TryFast(xml, handler, options, cancellationToken))
        {
            return;
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        var scratch = ArrayPool<byte>.Shared.Rent(InputChunkSize);
        try
        {
            // The pump may retain memory until NeedInput: copy only one bounded
            // chunk, never the whole borrowed input. No pool ownership escapes.
            var offset = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(InputChunkSize, xml.Length - offset);
                xml.Slice(offset, count).CopyTo(scratch);
                offset += count;
                operation.Pump.Supply(scratch.AsMemory(0, count), offset == xml.Length);
            } while (operation.Drain() == StreamingStep.NeedInput);
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
        finally { ArrayPool<byte>.Shared.Return(scratch); }
    }

    /// <summary>Parses encoded XML bytes and returns completion and diagnostic information.</summary>
    /// <param name="xml">The encoded XML bytes, borrowed only for this call.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing and recovery options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <returns>The completion marker, input-completeness and well-formedness flags, and accumulated diagnostics.</returns>
    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(ReadOnlySpan<byte> xml, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (TryFast(xml, handler, options, cancellationToken))
        {
            return CompleteResult();
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        var scratch = ArrayPool<byte>.Shared.Rent(InputChunkSize);
        try
        {
            // The pump may retain memory until NeedInput: copy only one bounded
            // chunk, never the whole borrowed input. No pool ownership escapes.
            var offset = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(InputChunkSize, xml.Length - offset);
                xml.Slice(offset, count).CopyTo(scratch);
                offset += count;
                operation.Pump.Supply(scratch.AsMemory(0, count), offset == xml.Length);
            } while (operation.Drain() == StreamingStep.NeedInput);
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
        finally { ArrayPool<byte>.Shared.Return(scratch); }
    }

    /// <summary>Reads and parses a strict XML document without closing the input stream.</summary>
    /// <param name="input">The readable, caller-owned stream.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or recovery is enabled.</exception>
    public static void Parse(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(input));
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        var scratch = ArrayPool<byte>.Shared.Rent(InputChunkSize);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = input.Read(scratch.AsSpan(0, InputChunkSize));
                operation.Pump.Supply(scratch.AsMemory(0, count), count == 0);
                if (operation.Drain() != StreamingStep.NeedInput)
                {
                    break;
                }
            }
            operation.FinishStrict();
        }
        catch { operation.Failed = true; throw; }
        finally { ArrayPool<byte>.Shared.Return(scratch); }
    }

    /// <summary>Reads XML and returns completion and diagnostic information without closing the input stream.</summary>
    /// <param name="input">The readable, caller-owned stream.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing and recovery options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling parsing.</param>
    /// <returns>The completion marker, input-completeness and well-formedness flags, and accumulated diagnostics.</returns>
    public static XmlParseResult<XmlSaxCompletion> ParseWithDiagnostics(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(input));
        }

        using var operation = new SaxOperation(handler, options, cancellationToken, characterInput: false);
        var scratch = ArrayPool<byte>.Shared.Rent(InputChunkSize);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = input.Read(scratch.AsSpan(0, InputChunkSize));
                operation.Pump.Supply(scratch.AsMemory(0, count), count == 0);
                if (operation.Drain() != StreamingStep.NeedInput)
                {
                    break;
                }
            }
            return operation.Result();
        }
        catch { operation.Failed = true; throw; }
        finally { ArrayPool<byte>.Shared.Return(scratch); }
    }

    // In-memory strict input first goes through the whole-buffer parser, which declines (delivering nothing)
    // whenever the streaming parser must decide. Options are validated in the same order SaxOperation does.
    private static bool TryFast(string xml, IXmlSaxHandler handler, XmlParseOptions? options, CancellationToken cancellationToken)
    {
        options = ValidateForFast(options, cancellationToken);
        return OptimisticSax.TryParse(xml.AsSpan(), handler, options, cancellationToken);
    }

    private static bool TryFast(ReadOnlySpan<byte> xml, IXmlSaxHandler handler, XmlParseOptions? options, CancellationToken cancellationToken)
    {
        options = ValidateForFast(options, cancellationToken);
        return OptimisticSax.TryParse(xml, handler, options, cancellationToken);
    }

    private static XmlParseOptions ValidateForFast(XmlParseOptions? options, CancellationToken cancellationToken)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        return options;
    }

    private static XmlParseResult<XmlSaxCompletion> CompleteResult() => new(new XmlSaxCompletion(), true, true, []);

    private static void RejectRecovery(XmlParseOptions? options)
    {
        if (options?.Recover == true)
        {
            throw new ArgumentException("Strict SAX parsing does not admit recovery.", nameof(options));
        }
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
            do
            {
                step = Pump.Advance(sink, cancellation);
            } while (step == StreamingStep.Paused);
            if (step == StreamingStep.NeedResource)
            {
                throw new InvalidOperationException("Synchronous SAX requires synchronous resource processing.");
            }

            return step;
        }
        internal void FinishStrict()
        {
            if (step == StreamingStep.Rejected)
            {
                throw new XmlParseException(Pump.Diagnostics);
            }
        }
        internal XmlParseResult<XmlSaxCompletion> Result()
        {
            var complete = step == StreamingStep.Completed;
            return new(complete || recover ? new XmlSaxCompletion() : null, complete, complete && Pump.IsWellFormed, Pump.Diagnostics);
        }
        public void Dispose()
        {
            if (!Failed)
            {
                Pump.Dispose();
                return;
            }
            // Cleanup cannot replace the original callback/I/O/cancellation failure.
            try
            {
                Pump.Dispose();
            }
            catch { }
        }
    }

    internal sealed class HandlerSink(IXmlSaxHandler handler) : IXmlStreamingSink
    {
        public bool StartDocument(XmlLocation location)
        {
            handler.StartDocument();
            return true;
        }
        public bool EndDocument(XmlLocation location)
        {
            handler.EndDocument();
            return true;
        }
        public bool StartElement(XmlStartElement element)
        {
            handler.StartElement(element);
            return true;
        }
        public bool EndElement(XmlEndElement element)
        {
            handler.EndElement(element);
            return true;
        }
        public bool Text(ReadOnlySpan<char> text, XmlLocation location)
        {
            handler.Text(text);
            return true;
        }
        public bool Comment(ReadOnlySpan<char> text, XmlLocation location)
        {
            handler.Comment(text);
            return true;
        }
        public bool ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data, XmlLocation location)
        {
            handler.ProcessingInstruction(target, data);
            return true;
        }
        public bool StartCData(XmlLocation location)
        {
            handler.StartCData();
            return true;
        }
        public bool EndCData(XmlLocation location)
        {
            handler.EndCData();
            return true;
        }
        public bool Declaration(XmlDeclaration declaration)
        {
            handler.Declaration(declaration);
            return true;
        }
        public bool StartDtd(XmlDocumentTypeView documentType)
        {
            handler.StartDtd(documentType);
            return true;
        }
        public bool DtdDeclaration(XmlDtdDeclaration declaration)
        {
            handler.DtdDeclaration(declaration);
            return true;
        }
        public bool EndDtd(XmlLocation location)
        {
            handler.EndDtd();
            return true;
        }
        public bool SkippedEntity(XmlSkippedEntity entity)
        {
            handler.SkippedEntity(entity);
            return true;
        }
        public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration)
        {
            handler.IncompleteAttributeDefault(declaration);
            return true;
        }
        public bool Diagnostic(XmlDiagnostic diagnostic)
        {
            handler.Diagnostic(diagnostic);
            return true;
        }
    }
}
