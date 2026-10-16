using System.Buffers;
namespace Xemele.Streaming;

public static partial class XmlSaxParser
{
    /// <summary>Asynchronously reads a strict XML document while delivering synchronous callbacks.</summary>
    /// <param name="input">The readable stream, which remains caller-owned and open.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token for canceling reads and parsing.</param>
    /// <returns>A task representing parsing and callback delivery.</returns>
    /// <exception cref="XmlParseException">The XML is not well-formed.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or recovery is enabled.</exception>
    public static async Task ParseAsync(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        RejectRecovery(options);
        var result = await ParseWithDiagnosticsAsync(input, handler, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsWellFormed)
        {
            throw new XmlParseException(result.Diagnostics);
        }
    }

    /// <summary>Asynchronously reads XML and returns diagnostics while delivering synchronous callbacks.</summary>
    /// <param name="input">The readable stream, which remains caller-owned and open.</param>
    /// <param name="handler">The recipient of borrowed parsing events.</param>
    /// <param name="options">Parsing and recovery options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling reads and parsing.</param>
    /// <returns>The completion marker, input-completeness and well-formedness flags, and accumulated diagnostics.</returns>
    public static async Task<XmlParseResult<XmlSaxCompletion>> ParseWithDiagnosticsAsync(Stream input, IXmlSaxHandler handler, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(handler);
        if (!input.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(input));
        }

        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var pump = new XmlStreamingPump(options, asyncResources: true);
        var scratch = ArrayPool<byte>.Shared.Rent(InputChunkSize);
        var failed = false;
        try
        {
            var sink = new HandlerSink(handler);
            StreamingStep step;
            while (true)
            {
                step = await pump.AdvanceAsync(sink, cancellationToken).ConfigureAwait(false);
                if (step == StreamingStep.Paused)
                {
                    continue;
                }

                if (step != StreamingStep.NeedInput)
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var count = await input.ReadAsync(scratch.AsMemory(0, InputChunkSize), cancellationToken).ConfigureAwait(false);
                pump.Supply(scratch.AsMemory(0, count), count == 0);
            }
            var complete = step == StreamingStep.Completed;
            return new(complete || options.Recover ? new XmlSaxCompletion() : null, complete, complete && pump.IsWellFormed, pump.Diagnostics);
        }
        catch { failed = true; throw; }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
            if (failed || pump.Diagnostic is not null)
            {
                try
                {
                    await pump.DisposeAsync().ConfigureAwait(false);
                }
                catch { }
            }
            else
            {
                await pump.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
