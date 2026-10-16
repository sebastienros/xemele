namespace Xemele;

/// <summary>Configures XInclude expansion, resource access, and inherited-context fixups.</summary>
public sealed record XIncludeOptions
{
    /// <summary>Gets the absolute base URI of the input document, or null when unavailable.</summary>
    public Uri? BaseUri
    {
        get; init;
    }
    /// <summary>Gets the resolver for included XML and text resources.</summary>
    public XmlResourceResolver? Resolver
    {
        get; init;
    }
    /// <summary>Gets strict parsing options for included XML. Recovery must be disabled.</summary>
    public XmlParseOptions ParseOptions { get; init; } = new();
    /// <summary>Gets processing limits, combined with parsing limits by taking the stricter bounds.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets whether inserted elements receive xml:base fixups to preserve their base URI. Defaults to true.</summary>
    public bool FixupBaseUris { get; init; } = true;
    /// <summary>Gets whether inserted elements receive xml:lang fixups to preserve inherited language. Defaults to true.</summary>
    public bool FixupLanguage { get; init; } = true;
}

/// <summary>Contains the transformed document or diagnostics from a rejected XInclude operation.</summary>
public sealed class XIncludeResult
{
    internal XIncludeResult(XmlDocument? document, IEnumerable<XmlDiagnostic> diagnostics, int count)
    {
        Document = document;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        IncludedResources = count;
    }
    /// <summary>Gets the new transformed document, or null if processing was rejected.</summary>
    public XmlDocument? Document
    {
        get;
    }
    /// <summary>Gets whether a transformed document was produced.</summary>
    public bool Success => Document is not null;
    /// <summary>Gets the processing diagnostics captured by this result.</summary>
    public IReadOnlyList<XmlDiagnostic> Diagnostics
    {
        get;
    }
    /// <summary>Gets the number of included resources processed before completion or rejection.</summary>
    public int IncludedResources
    {
        get;
    }
}

/// <summary>Expands XInclude references into a new document without modifying the input document.</summary>
public static class XIncludeProcessor
{
    /// <summary>Processes XInclude references using synchronous resource access.</summary>
    /// <param name="document">The complete input document to transform.</param>
    /// <param name="options">Expansion options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling processing.</param>
    /// <returns>A transformed document and diagnostics, or diagnostics with no document on rejection.</returns>
    /// <exception cref="ArgumentException">The base URI is relative or parsing recovery is enabled.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static XIncludeResult Process(XmlDocument document, XIncludeOptions? options = null, CancellationToken cancellationToken = default) =>
        new Resolution.XInclude.IncludeOperation(document, options, cancellationToken, false).Run().GetAwaiter().GetResult();
    /// <summary>Processes XInclude references using asynchronous resource access.</summary>
    /// <param name="document">The complete input document to transform.</param>
    /// <param name="options">Expansion options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling processing.</param>
    /// <returns>A transformed document and diagnostics, or diagnostics with no document on rejection.</returns>
    /// <exception cref="ArgumentException">The base URI is relative or parsing recovery is enabled.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
    public static Task<XIncludeResult> ProcessAsync(XmlDocument document, XIncludeOptions? options = null, CancellationToken cancellationToken = default) =>
        new Resolution.XInclude.IncludeOperation(document, options, cancellationToken, true).Run().AsTask();
}
