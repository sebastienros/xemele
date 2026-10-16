using System.Buffers;
namespace Xemele.Streaming;

public sealed partial class XmlReader : IAsyncDisposable
{
    /// <summary>Asynchronously advances to the next event, invalidating previously borrowed views.</summary>
    /// <param name="cancellationToken">A per-call token observed together with the reader's token.</param>
    /// <returns>True if an event is current; false after the final event.</returns>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="InvalidOperationException">The reader is faulted or another operation is active.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Either cancellation token was canceled.</exception>
    /// <remarks>A failed read faults the reader. The input stream remains open.</remarks>
    public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
        Enter();
        CancellationTokenSource? linked = null;
        try
        {
            current.Clear();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Reader is faulted.");
            }

            try
            {
                var token = this.cancellationToken;
                if (cancellationToken.CanBeCanceled)
                {
                    if (token.CanBeCanceled && token != cancellationToken)
                    {
                        linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken);
                        token = linked.Token;
                    }
                    else
                    {
                        token = cancellationToken;
                    }
                }
                token.ThrowIfCancellationRequested();
                if (completed)
                {
                    return false;
                }

                if (optimistic is not null)
                {
                    while (true)
                    {
                        switch (optimistic.Advance(current))
                        {
                            case OptimisticPullReader.Step.Token:
                                return true;
                            case OptimisticPullReader.Step.Completed:
                                completed = true;
                                return false;
                            case OptimisticPullReader.Step.NeedInput:
                                token.ThrowIfCancellationRequested();
                                await optimistic.ReadMoreFromAsync(input, token).ConfigureAwait(false);
                                break;
                            case OptimisticPullReader.Step.Declined:
                                FallbackToPump(token);
                                goto PumpLoop;
                        }
                    }
                }

            PumpLoop:
                inputBuffer ??= ArrayPool<byte>.Shared.Rent(InputBufferSize);
                pump!.SetAsyncResources(true);
                while (true)
                {
                    switch (await pump.AdvanceAsync(current, token).ConfigureAwait(false))
                    {
                        case StreamingStep.Paused:
                            if (current.Kind == XmlTokenKind.Diagnostic && pump.Diagnostic is { } terminal)
                            {
                                throw new XmlParseException([terminal]);
                            }

                            return true;
                        case StreamingStep.Completed:
                            completed = true;
                            return false;
                        case StreamingStep.Rejected:
                            throw new XmlParseException([pump.Diagnostic!]);
                        case StreamingStep.NeedInput:
                            token.ThrowIfCancellationRequested();
                            var count = await input.ReadAsync(inputBuffer.AsMemory(0, InputBufferSize), token).ConfigureAwait(false);
                            pump.Supply(inputBuffer.AsMemory(0, count), count == 0);
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected resource suspension.");
                    }
                }
            }
            catch
            {
                faulted = true;
                current.Clear();
                ReleaseBuffer();
                optimistic?.Dispose();
                optimistic = null;
                try
                {
                    if (pump is not null)
                    {
                        await pump.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch { }
                throw;
            }
        }
        finally { linked?.Dispose(); Exit(); }
    }

    /// <summary>Asynchronously releases parser state and invalidates current views, leaving the input stream open.</summary>
    /// <returns>A task representing parser cleanup.</returns>
    public async ValueTask DisposeAsync()
    {
        Enter();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current.Clear();
            ReleaseBuffer();
            optimistic?.Dispose();
            optimistic = null;
            try
            {
                if (pump is not null)
                {
                    await pump.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally { current.Release(); }
        }
        finally { Exit(); }
    }
}
