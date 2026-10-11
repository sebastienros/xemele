namespace Xemele.Html;

public static class Html5Tokenizer
{
    public static IReadOnlyList<HtmlToken> Tokenize(string html, HtmlTokenizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var result = new List<HtmlToken>();
        Tokenize(html, result.Add, options);
        return result.AsReadOnly();
    }

    public static void Tokenize(string html, Action<HtmlToken> callback, HtmlTokenizerOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();
        var tokenizer = new HtmlTokenizer(callback, options, cancellationToken);
        tokenizer.Feed(html.AsSpan());
        tokenizer.Complete();
    }
}
