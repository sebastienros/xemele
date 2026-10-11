namespace Xemele.Streaming;

public static partial class XmlSaxParser
{
    public static async Task ParseAsync(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        var result = await ParseWithDiagnosticsAsync(input, handler, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsWellFormed) throw new XmlParseException(result.Diagnostics);
    }

    public static async Task<XmlParseResult<XmlSaxCompletion>> ParseWithDiagnosticsAsync(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead) throw new ArgumentException("Stream must be readable.", nameof(input));
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var pump = new XmlStreamingPump(options, asyncResources: true);
        bool failed = false;
        try
        {
            var sink = new HandlerSink(handler);
            byte[] scratch = new byte[InputChunkSize];
            StreamingStep step;
            while (true)
            {
                step = await pump.AdvanceAsync(sink, cancellationToken).ConfigureAwait(false);
                if (step == StreamingStep.Paused) continue;
                if (step != StreamingStep.NeedInput) break;
                cancellationToken.ThrowIfCancellationRequested();
                int count = await input.ReadAsync(scratch.AsMemory(), cancellationToken).ConfigureAwait(false);
                pump.Supply(scratch.AsMemory(0, count), count == 0);
            }
            bool complete = step == StreamingStep.Completed;
            return new(complete || options.Recover ? new XmlSaxCompletion() : null, complete, complete && pump.IsWellFormed, pump.Diagnostics);
        }
        catch { failed = true; throw; }
        finally
        {
            if (failed || pump.Diagnostic is not null) { try { await pump.DisposeAsync().ConfigureAwait(false); } catch { } }
            else await pump.DisposeAsync().ConfigureAwait(false);
        }
    }
}
