namespace Xemele;
public sealed record XmlResourceLimits
{
    public long? DocumentCharacters { get; init; } = 16_777_216;
    public int? Depth { get; init; } = 256;
    public int? AttributesPerElement { get; init; } = 256;
    public int? TokenCharacters { get; init; } = 1_048_576;
    public int? InternedNames { get; init; } = 65_536;
    public int? EntityDepth { get; init; } = 32;
    public long? ExpandedCharacters { get; init; } = 16_777_216;
    public long? ExternalBytes { get; init; } = 16_777_216;
    public int? ExternalResources { get; init; } = 64;
    public int? InclusionDepth { get; init; } = 32;
    public int? Diagnostics { get; init; } = 128;
    public long? EvaluationWork { get; init; } = 10_000_000;
    public void Validate()
    {
        if (DocumentCharacters is <= 0 || Depth is <= 0 || AttributesPerElement is <= 0 ||
            TokenCharacters is <= 0 || InternedNames is <= 0 || EntityDepth is <= 0 ||
            ExpandedCharacters is <= 0 || ExternalBytes is <= 0 || ExternalResources is <= 0 ||
            InclusionDepth is <= 0 || Diagnostics is <= 0 || EvaluationWork is <= 0)
            throw new ArgumentOutOfRangeException(nameof(XmlResourceLimits));
    }
}
public sealed partial record XmlParseOptions
{
    public XmlResourceLimits Limits { get; init; } = new();

}
