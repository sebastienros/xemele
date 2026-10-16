namespace Xemele.Streaming;

/// <summary>Strict incremental byte parsing with synchronous borrowed callbacks.</summary>
public sealed class XmlPushParser : IDisposable
{
    private const int InputChunkSize = 4096;
    private XmlStreamingPump? pump;
    private HandlerSink? sink;
    // One owned transport chunk; caller input is never retained after Append.
    private byte[]? scratch;
    private readonly CancellationToken cancellation;
    private int gate;
    private State state;
    private enum State
    {
        Accepting, Completed, Faulted, Disposed
    }

    /// <summary>Creates a strict incremental parser that delivers callbacks synchronously.</summary>
    /// <param name="handler">The recipient of parsing events; borrowed views expire when each callback returns.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token observed during construction and parsing.</param>
    /// <exception cref="ArgumentNullException">The handler or resource limits are null.</exception>
    /// <exception cref="ArgumentException">Recovery is enabled.</exception>
    public XmlPushParser(IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new();
        if (options.Recover)
        {
            throw new ArgumentException("Push parsing does not admit recovery.", nameof(options));
        }

        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        cancellation = cancellationToken;
        pump = new(options);
        sink = new(handler);
        scratch = new byte[InputChunkSize];
    }

    /// <summary>Consumes a byte chunk and delivers all events available before more input is needed.</summary>
    /// <param name="input">Bytes to consume; no caller input is retained after this method returns.</param>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="InvalidOperationException">Parsing is completed, faulted, concurrent, or reentrant.</exception>
    /// <exception cref="ObjectDisposedException">The parser has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The parser's cancellation token was canceled.</exception>
    /// <remarks>Empty input can still deliver the initial document event. A parsing, callback, or cancellation failure faults the parser.</remarks>
    public void Append(ReadOnlySpan<byte> input)
    {
        Enter();
        try
        {
            CheckAccepting();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var offset = 0;
                // Empty input is work too: StartDocument is delivered on first use.
                do
                {
                    cancellation.ThrowIfCancellationRequested();
                    var count = Math.Min(InputChunkSize, input.Length - offset);
                    input.Slice(offset, count).CopyTo(scratch);
                    offset += count;
                    pump!.Supply(scratch.AsMemory(0, count), false);
                    if (Drain() != StreamingStep.NeedInput)
                    {
                        throw new InvalidOperationException("Push input requires NeedInput before EOF.");
                    }
                } while (offset < input.Length);
            }
            catch { Fault(); throw; }
        }
        finally { Exit(); }
    }

    /// <summary>Signals end of input and delivers the remaining events.</summary>
    /// <exception cref="XmlParseException">The final input is not a complete, well-formed document.</exception>
    /// <exception cref="InvalidOperationException">Parsing is completed, faulted, concurrent, or reentrant.</exception>
    /// <exception cref="ObjectDisposedException">The parser has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The parser's cancellation token was canceled.</exception>
    /// <remarks>Success releases parsing state and prevents further append or completion calls. Failure faults the parser.</remarks>
    public void Complete()
    {
        Enter();
        try
        {
            CheckAccepting();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                pump!.Supply(ReadOnlyMemory<byte>.Empty, true);
                if (Drain() != StreamingStep.Completed)
                {
                    throw new InvalidOperationException("Push EOF did not complete.");
                }

                state = State.Completed;
                Release();
            }
            catch { Fault(); throw; }
        }
        finally { Exit(); }
    }

    private StreamingStep Drain()
    {
        StreamingStep step;
        do
        {
            step = pump!.Advance(sink!, cancellation);
        } while (step == StreamingStep.Paused);
        if (step == StreamingStep.Rejected)
        {
            throw new XmlParseException(pump!.Diagnostics);
        }

        return step;
    }
    private void Fault()
    {
        state = State.Faulted;
        // A cleanup failure must never replace the original exception.
        try
        {
            Release();
        }
        catch { }
    }
    private void Release()
    {
        var owner = pump;
        pump = null;
        scratch = null;
        sink = null;
        owner?.Dispose();
    }
    private void Enter()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
        {
            throw new InvalidOperationException("Concurrent or reentrant push parsing.");
        }
    }
    private void Exit() => Volatile.Write(ref gate, 0);
    private void CheckAccepting()
    {
        ObjectDisposedException.ThrowIf(state == State.Disposed, this);
        if (state != State.Accepting)
        {
            throw new InvalidOperationException("Push parser is completed or faulted.");
        }
    }
    /// <summary>Releases parsing state without completing input or delivering a final document event.</summary>
    public void Dispose()
    {
        Enter();
        try
        {
            if (state == State.Disposed)
            {
                return;
            }

            state = State.Disposed;
            Release();
        }
        finally { Exit(); }
    }

    private sealed class HandlerSink(IXmlSaxHandler handler) : IXmlStreamingSink
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
