using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdMissingComponents;

internal sealed class MissingFixture
{
    internal static readonly string[] Routes = ["compile-complete", "compile-explicit", "compile-inferred", "compile-list", "compile-deferred-program", "validate-unrelated", "validate-required", "validate-xsi-program", "availability-resolved", "availability-missing-final", "availability-owner-absent"];
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private readonly string[] sources;
    private readonly XmlSchema schema, programSchema;
    private readonly XmlDocument good, bad, selected;
    private readonly ComponentAvailability availability, resolvedAvailability;
    private readonly ComponentIdentity owner;
    private readonly CounterOperation operation = new();
    private long consumed;
    internal MissingFixture()
    {
        sources = [Wrap("<xs:element name='good' type='xs:int'/>"),
            Wrap("<xs:element name='good' type='xs:int'/><xs:element name='bad' type='absent'/>"),
            Wrap("<xs:element name='bad' substitutionGroup='absent'/><xs:element name='good' type='xs:int'/>"),
            Wrap("<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:element name='bad' type='L'/><xs:element name='good' type='xs:int'/>"),
            Wrap("<xs:element name='h'/><xs:element name='m' substitutionGroup='absent'/><xs:complexType name='C'><xs:choice><xs:element ref='h'/><xs:element ref='m'/></xs:choice></xs:complexType><xs:element name='good' type='xs:int'/>")];
        schema = XmlSchema.Compile(sources[1]);
        programSchema = XmlSchema.Compile(sources[4]);
        good = XmlDocument.Parse("<good>7</good>");
        bad = XmlDocument.Parse("<bad/>");
        selected = XmlDocument.Parse("<unlisted xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='C'><h/></unlisted>");
        owner = ComponentAvailability.Simple(new("", "L"));
        var builder = new AvailabilityBuilder(operation);
        for (var i = 0; i < 64; i++)
        {
            builder.Reference(owner, ReferenceKind.ListItemType, SymbolSpace.Type, new("", "item" + i), default, i < 63, DependencyUse.TypeAssessment);
        }

        availability = builder.Seal();
        var resolved = new AvailabilityBuilder(operation);
        for (var i = 0; i < 64; i++)
        {
            resolved.Reference(owner, ReferenceKind.ListItemType, SymbolSpace.Type,
            new("", "item" + i), default, true, DependencyUse.TypeAssessment);
        }

        resolvedAvailability = resolved.Seal();
    }
    private static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal object Run(int route)
    {
        if ((uint)route >= (uint)Routes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        if (route < 5)
        {
            var compiled = XmlSchema.Compile(sources[route]);
            consumed++;
            return compiled;
        }
        if (route >= 8)
        {
            var required = route == 10 ? new ComponentIdentity(OwnerKind.SimpleType, new("", "unrelated"), null) : owner;
            var snapshot = route == 8 ? resolvedAvailability : availability;
            var before = operation.Work;
            var missing = snapshot.Missing(required, DependencyUse.TypeAssessment, operation, default);
            if ((missing is null) != (route != 9) || operation.Work - before != (route == 10 ? 64 : 128)
                || missing is not null && (missing.ReferenceId != 63 || missing.Owner != owner || missing.AuthoredName != new SchemaName("", "item63")))
            {
                throw new InvalidOperationException("Availability oracle failed.");
            }

            consumed += missing?.ReferenceId ?? 1;
            return (object?)missing ?? snapshot;
        }
        var result = route switch
        {
            5 => schema.Validate(good),
            6 => schema.Validate(bad),
            _ => programSchema.Validate(selected)
        };
        if (route == 5 ? !result.IsComplete || !result.IsValid : result.IsComplete || result.IsValid || result.Diagnostics.Count != 1 || result.Diagnostics[0].Code != "XSD.MissingComponent")
        {
            throw new InvalidOperationException("Result oracle failed.");
        }

        consumed += result.Diagnostics.Count + (result.IsValid ? 1 : 0);
        return result;
    }
    internal long Consumed => consumed;
    internal long Inspections => operation.Work;
    private sealed class CounterOperation : ISchemaOperation
    {
        internal long Work;
        private static readonly XmlResourceLimits limits = new() { EvaluationWork = null };
        public XmlResourceLimits Limits => limits;
        public CancellationToken CancellationToken => default;
        public bool IsComplete => true;
        public long? RemainingWork => null;
        public bool Charge(long count, XmlLocation location)
        {
            Work = checked(Work + count);
            return true;
        }
        public bool ChargeComponents(int count, XmlLocation location) => true;
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException("Unexpected diagnostic.");
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException("Unexpected refusal.");
    }
}
