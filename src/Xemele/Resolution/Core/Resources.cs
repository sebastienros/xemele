namespace Xemele;

/// <summary>Identifies the purpose of an external resource request.</summary>
public enum XmlResourceKind
{
    /// <summary>The external subset of a document type definition.</summary>
    ExternalSubset = 0,
    /// <summary>An external parsed or parameter entity.</summary>
    ExternalEntity = 1,
    /// <summary>An XML catalog document.</summary>
    Catalog = 2,
    /// <summary>An XInclude resource parsed as XML.</summary>
    XIncludeXml = 3,
    /// <summary>An XInclude resource included as text.</summary>
    XIncludeText = 4,
    /// <summary>A schema document.</summary>
    Schema = 5
}

/// <summary>Describes an external resource to be resolved.</summary>
/// <param name="Uri">The requested resource URI.</param>
/// <param name="Referrer">The URI of the referring resource, when known.</param>
/// <param name="Kind">The purpose of the request.</param>
public sealed record XmlResourceRequest(Uri Uri, Uri? Referrer, XmlResourceKind Kind)
{
    /// <summary>Gets the original system identifier, when available.</summary>
    public string? SystemId
    {
        get; init;
    }
    /// <summary>Gets the original public identifier, when available.</summary>
    public string? PublicId
    {
        get; init;
    }
}

/// <summary>Controls access to external resources and supplies their owned content.</summary>
public abstract class XmlResourceResolver
{
    /// <summary>Resolves an external resource synchronously.</summary>
    /// <param name="request">The resource to resolve.</param>
    /// <param name="cancellationToken">A token for canceling resolution.</param>
    /// <returns>An owned resource for the caller to dispose, or <see langword="null"/> if resolution is denied.</returns>
    public abstract XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken);

    /// <summary>Resolves a resource with an explicit outcome.</summary>
    /// <param name="request">The resource to resolve.</param>
    /// <param name="cancellationToken">A token for canceling resolution.</param>
    /// <returns>The resolution outcome, including any owned resource.</returns>
    /// <remarks>The default implementation calls <see cref="Resolve"/> and maps a null resource to a denied outcome.</remarks>
    public virtual XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resource = Resolve(request, cancellationToken);
        return resource is null ? XmlResourceResolution.Denied : XmlResourceResolution.FromResource(resource);
    }

    /// <summary>Resolves an external resource asynchronously.</summary>
    /// <param name="request">The resource to resolve.</param>
    /// <param name="cancellationToken">A token for canceling resolution.</param>
    /// <returns>An owned resource for the caller to dispose, or <see langword="null"/> if resolution is denied.</returns>
    /// <remarks>The default implementation calls the synchronous resolver.</remarks>
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
    /// <summary>Gets the readable content stream owned by this resource.</summary>
    public Stream Content
    {
        get;
    }
    /// <summary>Gets the final absolute URI used as the content's source and base URI.</summary>
    public Uri SourceUri
    {
        get;
    }

    /// <summary>Creates a resource that takes ownership of a readable stream.</summary>
    /// <param name="content">The readable stream to dispose with this resource.</param>
    /// <param name="sourceUri">The final absolute source URI.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable or the URI is relative.</exception>
    public XmlResolvedResource(Stream content, Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceUri);
        if (!content.CanRead)
        {
            throw new ArgumentException("A readable stream is required.", nameof(content));
        }

        if (!sourceUri.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute source URI is required.", nameof(sourceUri));
        }

        Content = content;
        SourceUri = sourceUri;
    }

    /// <summary>Disposes the owned content stream once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            Content.Dispose();
        }
    }

    /// <summary>Asynchronously disposes the owned content stream once.</summary>
    /// <returns>A task representing content disposal.</returns>
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref disposed, 1) == 0 ? Content.DisposeAsync() : default;
}
