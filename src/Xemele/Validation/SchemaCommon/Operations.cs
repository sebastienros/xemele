namespace Xemele.Validation;

internal sealed class SchemaCompileOperation : ISchemaOperation
{
    private readonly long? maximum;
    private readonly int componentMaximum, diagnosticMaximum;
    private long work;
    private int components;
    private List<XmlDiagnostic>? diagnostics;
    public XmlResourceLimits Limits { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsComplete { get; private set; } = true;
    public long? RemainingWork => maximum is long value ? value - work : null;

    internal SchemaCompileOperation(SchemaCompileOptions? options = null)
    {
        var snapshot = SchemaSnapshots.Compile(options);
        Limits = snapshot.Limits; CancellationToken = snapshot.CancellationToken;
        maximum = snapshot.MaxOperations is long a && Limits.EvaluationWork is long b ? Math.Min(a, b) : snapshot.MaxOperations ?? Limits.EvaluationWork;
        componentMaximum = snapshot.MaxComponents ?? int.MaxValue;
        diagnosticMaximum = Limits.Diagnostics ?? int.MaxValue;
    }

    public bool Charge(long amount, XmlLocation location)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return false;
        if (amount > long.MaxValue - work || (maximum is long cap && amount > cap - work))
        { Refuse("SCHEMA.Limit", "Schema work limit exceeded.", location); return false; }
        work += amount; return true;
    }

    public bool ChargeComponents(int count, XmlLocation location)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return false;
        if (count > componentMaximum - components)
        { Refuse("SCHEMA.Limit", "Schema component limit exceeded.", location); return false; }
        components += count; return true;
    }

    public bool AddDiagnostic(XmlDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic); CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return false;
        diagnostics ??= new();
        if (diagnostics.Count == diagnosticMaximum)
        { Refuse("SCHEMA.Limit", "Schema diagnostic limit exceeded.", diagnostic.Location); return false; }
        diagnostics.Add(diagnostic); return true;
    }

    public void Refuse(string code, string message, XmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(code); ArgumentNullException.ThrowIfNull(message);
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return;
        var terminal = new XmlDiagnostic(code, XmlDiagnosticSeverity.Fatal, message, location);
        diagnostics ??= new();
        if (diagnostics.Count == diagnosticMaximum) diagnostics[^1] = terminal; else diagnostics.Add(terminal);
        IsComplete = false;
    }

    internal IReadOnlyList<XmlDiagnostic> Diagnostics => SchemaSnapshots.List(diagnostics ?? (IEnumerable<XmlDiagnostic>)Array.Empty<XmlDiagnostic>());
    internal SchemaCompileException Exception() => new(Diagnostics);
}

// M5 owns its existing run ledger and terminal code. The adapter never modifies M5.
internal sealed class SchemaValidationOperation : ISchemaOperation
{
    private readonly ValidationOperation operation;
    private readonly long? maximum;
    private int components;
    public XmlResourceLimits Limits => operation.Limits;
    public CancellationToken CancellationToken => operation.CancellationToken;
    public bool IsComplete => operation.IsComplete;
    public long? RemainingWork => maximum is long cap ? cap - operation.Operations : null;

    internal SchemaValidationOperation(ValidationOptions? options = null)
    {
        options ??= new();
        operation = new(options);
        maximum = options.MaxOperations is long a && Limits.EvaluationWork is long b ? Math.Min(a, b) : options.MaxOperations ?? Limits.EvaluationWork;
    }
    public bool Charge(long work, XmlLocation location) => operation.ChargeOperations(work, location);
    public bool ChargeComponents(int count, XmlLocation location)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!Charge(0, location)) return false;
        if (count > int.MaxValue - components) { Refuse("VALIDATION.Limit", "Component counter exhausted.", location); return false; }
        components += count; return true;
    }
    public bool AddDiagnostic(XmlDiagnostic diagnostic) => operation.AddDiagnostic(diagnostic);
    public void Refuse(string code, string message, XmlLocation location) => operation.Refuse(code, message, location);
    internal ValidationResult Complete() => operation.Complete();
}
