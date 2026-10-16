namespace Xemele;

public sealed partial class XmlDocument
{
    /// <summary>Asynchronously serializes this document to a stream, leaving the stream open.</summary>
    /// <param name="output">The writable destination stream, written at its current position.</param>
    /// <param name="options">The serialization options, or <see langword="null"/> for defaults.</param>
    /// <param name="cancellationToken">A token that can cancel traversal and asynchronous writes.</param>
    /// <returns>A task that completes when serialization has finished.</returns>
    /// <remarks>Output already written is not rolled back if serialization fails or is canceled.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The destination is not writable or the save options are invalid or unsupported.</exception>
    /// <exception cref="InvalidOperationException">The tree is not serializable, lacks exactly one root, or contains unresolved references.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public Task SaveAsync(Stream output, XmlSaveOptions? options = null, CancellationToken cancellationToken = default) =>
        XmlAsyncSerializer.SerializeAsync(this, output, options, cancellationToken);
}
