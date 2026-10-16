using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class ContextTests
{
    [Fact]
    public void InternalPrecedenceAppliesToDefaultExpansionWithSeparateEntityNamespaces()
    {
        var internalEntity = Entity("e", "inside");
        var parameter = internalEntity with
        {
            IsParameter = true,
            ReplacementText = "parameter"
        };
        var attribute = Attribute("r", "a", " &e;  tail ", DtdAttributeType.NmTokens, true);
        var model = XmlEntityContext.Merge(new([], [], [internalEntity], [parameter], []),
            new([], [attribute], [Entity("e", "outside") with { IsExternal = true }], [], []), TestContext.Current.CancellationToken);
        var context = new XmlEntityContext(model);
        var result = DtdValueProcessor.NormalizeAttributeDefault(attribute.DefaultValue!, attribute.Type,
            context.GeneralEntities, new(), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(result.Diagnostic);
        Assert.Equal("inside tail", result.Value);
        Assert.Same(internalEntity, Assert.Single(model.GeneralEntities));
        Assert.Same(parameter, Assert.Single(model.ParameterEntities));
        Assert.Same(attribute, Assert.Single(context.GetAttributes("r")));
        Assert.Empty(context.GetAttributes("missing"));
    }

    [Fact]
    public void DefaultsKeepFirstAttributePerLexicalElementAndAttributePair()
    {
        var first = Attribute("r", "a", "first");
        var second = Attribute("r", "a", "second");
        var other = Attribute("s", "a", "other");
        var context = new XmlEntityContext(XmlEntityContext.Merge(new([], [first], [], [], []),
            new([], [second, other], [], [], []), TestContext.Current.CancellationToken));
        Assert.Same(first, Assert.Single(context.GetAttributes("r")));
        Assert.Same(other, Assert.Single(context.GetAttributes("s")));
    }

    [Fact]
    public void BorrowedLookupsHaveNoWarmAllocationAndCannotBeMutated()
    {
        var entity = Entity("e", "owned");
        var context = new XmlEntityContext(new([], [Attribute("r", "a", "v")], [entity], [], []));
        var found = false;
        var attributes = 0;
        for (var i = 0; i < 10000; i++)
        {
            found = context.TryGetGeneralEntity("e", out _);
            attributes = context.GetAttributes("r").Count;
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            found = context.TryGetGeneralEntity("e", out _);
            attributes = context.GetAttributes("r").Count;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(found);
        Assert.Equal(1, attributes);
        Assert.Equal(0, allocated);
        Assert.Throws<NotSupportedException>(() => ((IList<DtdAttributeDeclaration>)context.GetAttributes("r")).Clear());
    }

    [Fact]
    public void NameAndExpansionLedgerSharesCountersAndRefusesBeforeMutation()
    {
        var operation = new OperationBudget(new()
        {
            Limits = new()
            {
                InternedNames = 2,
                EntityDepth = 1,
                ExpandedCharacters = 3
            }
        }, default);
        var retained = new Dictionary<string, string>(StringComparer.Ordinal) { ["core"] = "core" };
        Assert.True(operation.TryCharge(Quota.InternedNames, 1));
        IDtdBudget ledger = new XmlEntityBudget(operation, retained);
        Assert.Null(ledger.ChargeName("core", default));
        Assert.Null(ledger.ChargeName("dtd", default));
        Assert.NotNull(ledger.ChargeName("excess", default));
        Assert.False(retained.ContainsKey("excess"));
        Assert.Equal(2, operation.Count(Quota.InternedNames));
        Assert.Null(ledger.EnterEntity(default));
        Assert.NotNull(ledger.EnterEntity(default));
        Assert.Equal(1, operation.Count(Quota.EntityDepth));
        ledger.ExitEntity();
        Assert.Equal(0, operation.Count(Quota.EntityDepth));
        Assert.Null(ledger.ChargeExpandedCharacters(2, default));
        Assert.True(operation.TryCharge(Quota.ExpandedCharacters, 1));
        Assert.NotNull(ledger.ChargeExpandedCharacters(1, default));
        Assert.Equal(3, operation.Count(Quota.ExpandedCharacters));
    }

    private static DtdEntityDeclaration Entity(string name, string text) => new(name, false, text, null, null, null, false, default);
    private static DtdAttributeDeclaration Attribute(string element, string name, string value,
        DtdAttributeType type = DtdAttributeType.CData, bool external = false) => new(element, name, type, [], DtdDefaultKind.Value, value, external, default);
}
