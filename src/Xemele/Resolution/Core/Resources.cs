namespace Xemele;

public enum XmlResourceKind
{
    ExternalSubset = 0,
    ExternalEntity = 1,
    Catalog = 2,
    XIncludeXml = 3,
    XIncludeText = 4
}

public sealed record XmlResourceRequest(Uri Uri, Uri? Referrer, XmlResourceKind Kind)
{
    public string? SystemId { get; init; }
    public string? PublicId { get; init; }
}

public abstract class XmlResourceResolver
{
    public abstract XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken);

    public virtual ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Resolve(request, cancellationToken));
    }
}

/// <summary>Owns readable content at its final absolute source URI.</summary>
public sealed class XmlResolvedResource : IDisposable, IAsyncDisposable
{
    private int disposed;
    public Stream Content { get; }
    public Uri SourceUri { get; }

    public XmlResolvedResource(Stream content, Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!content.CanRead) throw new ArgumentException("A readable stream is required.", nameof(content));
        if (!sourceUri.IsAbsoluteUri) throw new ArgumentException("An absolute source URI is required.", nameof(sourceUri));
        Content = content;
        SourceUri = sourceUri;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) Content.Dispose();
    }

    public ValueTask DisposeAsync() => Interlocked.Exchange(ref disposed, 1) == 0 ? Content.DisposeAsync() : default;
}
