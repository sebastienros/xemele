namespace Xemele.Html;

public static class Html5Parser
{
    public static HtmlDocument Parse(string html, Html5ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options));
        builder.Tokenizer.Feed(html.AsSpan()); builder.Tokenizer.Complete(); return builder.Document;
    }
    public static HtmlDocument Parse(ReadOnlySpan<byte> html, Html5ParseOptions? options = null)
    {
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options));
        HtmlByteInput.Parse(html, builder.Tokenizer); return builder.Document;
    }
    public static HtmlDocument Load(Stream stream, Html5ParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream); cancellationToken.ThrowIfCancellationRequested();
        var builder = new HtmlTreeBuilder(HtmlOperationSnapshots.Snapshot(options), cancellation: cancellationToken);
        HtmlByteInput.Load(stream, builder.Tokenizer, cancellationToken); return builder.Document;
    }
    public static HtmlNode ParseFragment(string html, HtmlFragmentContext context, Html5ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var settings = HtmlOperationSnapshots.Snapshot(options); var snapshot = HtmlOperationSnapshots.Snapshot(context);
        var builder = new HtmlTreeBuilder(settings, snapshot);
        builder.Tokenizer.Feed(html.AsSpan()); builder.Tokenizer.Complete(); return builder.FinishFragment();
    }
}
