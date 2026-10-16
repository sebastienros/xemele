namespace Xemele.Html;

/// <summary>Tokenizes decoded HTML with HTML5 tokenization rules without constructing a tree.</summary>
public static class Html5Tokenizer
{
    /// <summary>Tokenizes the complete input and captures its tokens in emission order.</summary>
    /// <param name="html">The decoded HTML input.</param>
    /// <param name="options">The initial tokenizer state and limits, or null for defaults.</param>
    /// <returns>A read-only list of owned tokens, including the end-of-file token.</returns>
    /// <exception cref="ArgumentNullException">The input is null.</exception>
    public static IReadOnlyList<HtmlToken> Tokenize(string html, HtmlTokenizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var result = new List<HtmlToken>();
        Tokenize(html, result.Add, options);
        return result.AsReadOnly();
    }

    /// <summary>Tokenizes the input and synchronously invokes a callback for each owned token.</summary>
    /// <param name="html">The decoded HTML input.</param>
    /// <param name="callback">The callback, which may retain each token and whose exceptions propagate to the caller.</param>
    /// <param name="options">The initial tokenizer state and limits, or null for defaults.</param>
    /// <param name="cancellationToken">The token used to cancel tokenization.</param>
    /// <exception cref="ArgumentNullException">The input or callback is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static void Tokenize(string html, Action<HtmlToken> callback, HtmlTokenizerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();
        var tokenizer = new HtmlTokenizer(callback, options, cancellationToken);
        try
        {
            tokenizer.Feed(html.AsSpan());
            tokenizer.Complete();
        }
        finally
        {
            tokenizer.DisposeBuffers();
        }
    }
}
