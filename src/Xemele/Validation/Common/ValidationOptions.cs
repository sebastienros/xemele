namespace Xemele.Validation;

public sealed record ValidationOptions
{
    public XmlResourceLimits Limits { get; init; } = new();
    public long? MaxOperations { get; init; } = 10_000_000;
    public CancellationToken CancellationToken { get; init; }
}
