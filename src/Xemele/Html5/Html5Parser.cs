namespace Xemele.Html;

/// <summary>Parses HTML using HTML5 tokenization and tree-construction rules.</summary>
public static class Html5Parser
{
    /// <summary>Parses decoded HTML text into a new document.</summary>
    /// <param name="html">The decoded HTML input.</param>
    /// <param name="options">The parsing settings, or null for defaults.</param>
    /// <returns>An owned document with parser-selected quirks and scripting metadata.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    public static HtmlDocument Parse(string html, Html5ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options));
        try
        {
            builder.Tokenizer.Feed(html.AsSpan());
            builder.Tokenizer.Complete();
            return builder.Document;
        }
        finally
        {
            builder.DisposeBuffers();
        }
    }
    /// <summary>Sniffs and decodes HTML bytes into a new document.</summary>
    /// <param name="html">The encoded HTML input, borrowed only for this call.</param>
    /// <param name="options">The parsing settings, or null for defaults.</param>
    /// <returns>An owned document whose input encoding is selected from the BOM, initial metadata, or fallback.</returns>
    public static HtmlDocument Parse(ReadOnlySpan<byte> html, Html5ParseOptions? options = null)
    {
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options));
        try
        {
            HtmlByteInput.Parse(html, builder.Tokenizer);
            return builder.Document;
        }
        finally
        {
            builder.DisposeBuffers();
        }
    }
    /// <summary>Reads, sniffs, and parses HTML bytes from a stream without closing it.</summary>
    /// <param name="stream">The input stream, read from its current position.</param>
    /// <param name="options">The parsing settings, or null for defaults.</param>
    /// <param name="cancellationToken">The token used to cancel reading and parsing.</param>
    /// <returns>A new document containing the parsed HTML tree.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static HtmlDocument Load(Stream stream, Html5ParseOptions? options = null, CancellationToken cancellationToken = default)
        => Load(stream, options, cancellationToken, null);

    internal static HtmlDocument Load(Stream stream, Html5ParseOptions? options, CancellationToken cancellationToken,
        Action<string, XmlLocation>? parseError)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options), cancellation: cancellationToken, parseError: parseError);
        try
        {
            HtmlByteInput.Load(stream, builder.Tokenizer, cancellationToken);
            return builder.Document;
        }
        finally
        {
            builder.DisposeBuffers();
        }
    }
    /// <summary>Parses decoded HTML as a fragment using a snapshot of an element's parsing context.</summary>
    /// <param name="html">The decoded fragment input.</param>
    /// <param name="context">The context element, its ancestry, and the quirks mode used for fragment parsing.</param>
    /// <param name="options">The parsing settings, or null for defaults.</param>
    /// <returns>A fragment in a new owner document; the original context element is not modified.</returns>
    /// <exception cref="ArgumentNullException">The input, context, or context element is null.</exception>
    /// <exception cref="ArgumentException">The context node is not an element.</exception>
    public static HtmlNode ParseFragment(string html, HtmlFragmentContext context, Html5ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var settings = HtmlOperationSnapshots.Snapshot(options);
        var snapshot = HtmlOperationSnapshots.Snapshot(context);
        var builder = new HtmlTreeBuilder(settings, snapshot);
        try
        {
            builder.Tokenizer.Feed(html.AsSpan());
            builder.Tokenizer.Complete();
            return builder.FinishFragment();
        }
        finally
        {
            builder.DisposeBuffers();
        }
    }
}
