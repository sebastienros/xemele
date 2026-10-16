using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdResolvedAvailability;

internal sealed class ResolvedFixture
{
    internal static readonly int[] Sizes = [8, 32, 64];
    internal static readonly string[] Routes = ["query-8", "query-32", "query-64", "compile-8", "compile-32", "compile-64"];
    private readonly string[] sources = new string[3];
    private readonly AvailabilityBuilder[] builders = new AvailabilityBuilder[3];
    private readonly Counted[] operations = new Counted[3];
    private readonly ComponentIdentity root = Node(0);
    private long consumed;
    internal long Consumed => consumed;
    internal long Inspections => operations.Sum(op => op.Work);
    internal long RetainedComponents => operations.Sum(op => op.Components);
    internal ResolvedFixture()
    {
        for (var i = 0; i < Sizes.Length; i++)
        {
            var text = new StringBuilder("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>");
            for (var j = 0; j < Sizes[i]; j++)
            {
                text.Append("<xs:element name='r").Append(j).Append("' type='xs:int'/>");
            }

            sources[i] = text.Append("</xs:schema>").ToString();
            operations[i] = new();
            builders[i] = Build(operations[i], Sizes[i]);
        }
    }
    internal static ComponentIdentity Node(int i) => ComponentAvailability.Simple(new("", "N" + i));
    internal static AvailabilityBuilder Build(Counted op, int size)
    {
        var builder = new AvailabilityBuilder(op);
        builder.Reference(Node(size), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "resolved"), default, true, DependencyUse.TypeAssessment);
        for (var i = 0; i < size; i++)
        {
            builder.Link(Node(i), Node(i + 1));
        }

        return builder;
    }
    internal object Run(int route)
    {
        if ((uint)route >= (uint)Routes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        consumed++;
        if (route < 3)
        {
            if (builders[route].Unavailable(root))
            {
                throw new InvalidOperationException("Resolved query unexpectedly unavailable.");
            }

            return builders[route];
        }
        return XmlSchema.Compile(sources[route - 3], Options());
    }
    internal void ConsumeSchema(object value, int route)
    {
        if (route < 3)
        {
            return;
        }

        var result = ((XmlSchema)value).Validate("<r0>7</r0>");
        if (!result.IsComplete || !result.IsValid)
        {
            throw new InvalidOperationException("Compiled result unusable.");
        }
    }
    internal static SchemaCompileOptions Options() => new()
    {
        MaxOperations = 100_000_000,
        MaxComponents = 1_000_000,
        Limits = new()
        {
            DocumentCharacters = 64 * 1024 * 1024,
            EvaluationWork = 100_000_000
        }
    };
    internal sealed class Counted : ISchemaOperation
    {
        internal long Work, Components, Polls;
        public XmlResourceLimits Limits { get; } = new() { EvaluationWork = null };
        public CancellationToken CancellationToken => default;
        public bool IsComplete => true;
        public long? RemainingWork => null;
        public bool Charge(long count, XmlLocation at)
        {
            Work = checked(Work + count);
            if (count == 0)
            {
                Polls++;
            }

            return true;
        }
        public bool ChargeComponents(int count, XmlLocation at)
        {
            Components = checked(Components + count);
            return true;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException("Unexpected diagnostic.");
        public void Refuse(string code, string message, XmlLocation at) => throw new InvalidOperationException("Unexpected refusal.");
    }
}
