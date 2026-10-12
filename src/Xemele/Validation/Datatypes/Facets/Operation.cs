namespace Xemele.Validation;

// Own only this run's adapter. The sentinel is exclusively a returned budget
// refusal; caller exceptions (including schema exceptions) retain provenance.
internal sealed class FacetOperation : ISchemaOperation
{
    private readonly ISchemaOperation inner;
    internal FacetOperation(ISchemaOperation inner)
    {
        if (!inner.Charge(1, default) || !inner.ChargeComponents(1, default)) throw new Stopped();
        this.inner = inner;
    }
    internal sealed class Stopped : Exception;
    public XmlResourceLimits Limits => inner.Limits;
    public CancellationToken CancellationToken => inner.CancellationToken;
    public bool IsComplete => inner.IsComplete;
    public long? RemainingWork => inner.RemainingWork;
    public bool Charge(long work, XmlLocation location)
    { if (!inner.Charge(work, location)) throw new Stopped(); return true; }
    public bool ChargeComponents(int count, XmlLocation location)
    { if (!inner.ChargeComponents(count, location)) throw new Stopped(); return true; }
    public bool AddDiagnostic(XmlDiagnostic diagnostic)
    { if (!inner.AddDiagnostic(diagnostic)) throw new Stopped(); return true; }
    public void Refuse(string code, string message, XmlLocation location)
    { inner.Refuse(code, message, location); if (!inner.IsComplete) throw new Stopped(); }
}
