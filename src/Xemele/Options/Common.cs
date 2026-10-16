namespace Xemele;

/// <summary>Specifies resource budgets for XML operations.</summary>
/// <remarks>Each limit must be positive, or <see langword="null"/> to disable that limit. Limits are snapshotted when an operation begins.</remarks>
public sealed record XmlResourceLimits
{
    /// <summary>Gets the maximum number of document characters; the default is 16,777,216.</summary>
    public long? DocumentCharacters { get; init; } = 16_777_216;
    /// <summary>Gets the maximum element nesting depth; the default is 256.</summary>
    public int? Depth { get; init; } = 256;
    /// <summary>Gets the maximum number of attributes per element; the default is 256.</summary>
    public int? AttributesPerElement { get; init; } = 256;
    /// <summary>Gets the maximum number of characters in a token; the default is 1,048,576.</summary>
    public int? TokenCharacters { get; init; } = 1_048_576;
    /// <summary>Gets the maximum number of distinct interned names; the default is 65,536.</summary>
    public int? InternedNames { get; init; } = 65_536;
    /// <summary>Gets the maximum entity expansion nesting depth; the default is 32.</summary>
    public int? EntityDepth { get; init; } = 32;
    /// <summary>Gets the maximum number of expanded entity characters; the default is 16,777,216.</summary>
    public long? ExpandedCharacters { get; init; } = 16_777_216;
    /// <summary>Gets the maximum cumulative number of external resource bytes; the default is 16,777,216.</summary>
    public long? ExternalBytes { get; init; } = 16_777_216;
    /// <summary>Gets the maximum number of external resources; the default is 64.</summary>
    public int? ExternalResources { get; init; } = 64;
    /// <summary>Gets the maximum inclusion nesting depth; the default is 32.</summary>
    public int? InclusionDepth { get; init; } = 32;
    /// <summary>Gets the maximum number of retained diagnostics; the default is 128.</summary>
    public int? Diagnostics { get; init; } = 128;
    /// <summary>Gets the maximum evaluation work budget; the default is 10,000,000 units.</summary>
    public long? EvaluationWork { get; init; } = 10_000_000;
    /// <summary>Checks that every enabled limit is positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An enabled limit is zero or negative.</exception>
    public void Validate()
    {
        if (DocumentCharacters is <= 0 || Depth is <= 0 || AttributesPerElement is <= 0 ||
            TokenCharacters is <= 0 || InternedNames is <= 0 || EntityDepth is <= 0 ||
            ExpandedCharacters is <= 0 || ExternalBytes is <= 0 || ExternalResources is <= 0 ||
            InclusionDepth is <= 0 || Diagnostics is <= 0 || EvaluationWork is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(XmlResourceLimits));
        }
    }
}
/// <summary>Configures XML parsing, resource resolution, recovery, and resource limits.</summary>
public sealed partial record XmlParseOptions
{
    /// <summary>Gets the resource limits applied to the parse operation.</summary>
    public XmlResourceLimits Limits { get; init; } = new();

}
