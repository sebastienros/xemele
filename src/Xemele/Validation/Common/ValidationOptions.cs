namespace Xemele.Validation;

/// <summary>Configures resource budgets and cancellation for one validation operation.</summary>
public sealed record ValidationOptions
{
    /// <summary>Gets the resource limits applied to validation and, when applicable, instance parsing.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets the positive work allowance, or <see langword="null"/> for no additional work cap.</summary>
    /// <remarks>The default is 10,000,000. When <see cref="XmlResourceLimits.EvaluationWork"/> is also set, the smaller allowance applies.</remarks>
    public long? MaxOperations { get; init; } = 10_000_000;
    /// <summary>Gets the token used to cancel the operation.</summary>
    public CancellationToken CancellationToken
    {
        get; init;
    }
}
