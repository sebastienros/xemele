namespace Xemele.Validation;

// Translate only a returned quota refusal; caller-thrown exceptions pass through.
// This temporary adapter is used at core's throwing composite/retype boundary.
internal sealed class DatatypeStructuredOperation : ISchemaOperation
{
    private readonly ISchemaOperation inner;
    internal DatatypeStructuredOperation(ISchemaOperation inner)
    {
        if (!inner.Charge(1, default))
        {
            throw new StoppedException();
        }

        if (!inner.ChargeComponents(1, default))
        {
            throw new StoppedException();
        }

        this.inner = inner;
    }
    internal sealed class StoppedException : Exception;
    public XmlResourceLimits Limits => inner.Limits;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public bool IsComplete => inner.IsComplete;
    public long? RemainingWork => inner.RemainingWork;
    public bool Charge(long work, XmlLocation location)
    {
        if (!inner.Charge(work, location))
        {
            throw new StoppedException();
        }

        return true;
    }
    public bool ChargeComponents(int count, XmlLocation location)
    {
        if (!inner.ChargeComponents(count, location))
        {
            throw new StoppedException();
        }

        return true;
    }
    public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
    public void Refuse(string code, string message, XmlLocation location) => inner.Refuse(code, message, location);
}
