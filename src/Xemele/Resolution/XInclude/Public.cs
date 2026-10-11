namespace Xemele;

public sealed record XIncludeOptions
{
    public Uri? BaseUri { get; init; }
    public XmlResourceResolver? Resolver { get; init; }
    public XmlParseOptions ParseOptions { get; init; } = new();
    public XmlResourceLimits Limits { get; init; } = new();
    public bool FixupBaseUris { get; init; } = true;
    public bool FixupLanguage { get; init; } = true;
}

public sealed class XIncludeResult
{
    internal XIncludeResult(XmlDocument? document, IEnumerable<XmlDiagnostic> diagnostics, int count)
    {
        Document = document;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        IncludedResources = count;
    }
    public XmlDocument? Document { get; }
    public bool Success => Document is not null;
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
    public int IncludedResources { get; }
}

public static class XIncludeProcessor
{
    public static XIncludeResult Process(XmlDocument document, XIncludeOptions? options = null, CancellationToken cancellationToken = default) =>
        new Resolution.XInclude.IncludeOperation(document, options, cancellationToken, false).Run().GetAwaiter().GetResult();
    public static Task<XIncludeResult> ProcessAsync(XmlDocument document, XIncludeOptions? options = null, CancellationToken cancellationToken = default) =>
        new Resolution.XInclude.IncludeOperation(document, options, cancellationToken, true).Run().AsTask();
}
