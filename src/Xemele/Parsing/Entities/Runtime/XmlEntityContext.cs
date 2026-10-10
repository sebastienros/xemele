using Xemele.Parsing.Dtd;

namespace Xemele.Parsing.Entities;

// The operation owns these indices; model entries are immutable declaration
// snapshots. Probes borrow the lexical name and never create a lookup string.
internal sealed class XmlEntityContext : IXmlEntityContext
{
    private readonly Dictionary<string, DtdEntityDeclaration> general = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<DtdAttributeDeclaration>> attributes = new(StringComparer.Ordinal);
    public DtdModel Model { get; }
    internal IReadOnlyDictionary<string, DtdEntityDeclaration> GeneralEntities => general;

    internal XmlEntityContext(DtdModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();
        Model = model;
        foreach (var declaration in model.GeneralEntities)
        { CheckName(declaration.Name, cancellationToken); general.TryAdd(declaration.Name, declaration); }
        var groups = new Dictionary<string, AttributeGroup>(StringComparer.Ordinal);
        foreach (var declaration in model.Attributes)
        {
            CheckName(declaration.ElementName, cancellationToken); CheckName(declaration.Name, cancellationToken);
            if (!groups.TryGetValue(declaration.ElementName, out var group))
                groups.Add(declaration.ElementName, group = new());
            if (group.Names.Add(declaration.Name)) group.Declarations.Add(declaration);
        }
        foreach (var group in groups)
        { cancellationToken.ThrowIfCancellationRequested(); attributes.Add(group.Key, group.Value.Declarations.AsReadOnly()); }
    }

    public bool TryGetGeneralEntity(ReadOnlySpan<char> name, out DtdEntityDeclaration? declaration) =>
        general.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out declaration);

    public IReadOnlyList<DtdAttributeDeclaration> GetAttributes(ReadOnlySpan<char> elementName) =>
        attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(elementName, out var result) ? result : Array.Empty<DtdAttributeDeclaration>();

    // The internal subset has precedence over the external subset. Each source
    // model is ordered, so retaining the first matching declaration implements
    // first-declaration wins without conflating general and parameter entities.
    internal static DtdModel Merge(DtdModel internalSubset, DtdModel externalSubset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(internalSubset);
        ArgumentNullException.ThrowIfNull(externalSubset);
        cancellationToken.ThrowIfCancellationRequested();
        return new(
            MergeByName(internalSubset.Elements, externalSubset.Elements, static d => d.Name, cancellationToken),
            MergeAttributes(internalSubset.Attributes, externalSubset.Attributes, cancellationToken),
            MergeByName(internalSubset.GeneralEntities, externalSubset.GeneralEntities, static d => d.Name, cancellationToken),
            MergeByName(internalSubset.ParameterEntities, externalSubset.ParameterEntities, static d => d.Name, cancellationToken),
            MergeByName(internalSubset.Notations, externalSubset.Notations, static d => d.Name, cancellationToken));
    }

    private static List<T> MergeByName<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, string> name, CancellationToken cancellation)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<T>();
        foreach (var declaration in first)
        { CheckName(name(declaration), cancellation); if (seen.Add(name(declaration))) result.Add(declaration); }
        foreach (var declaration in second)
        { CheckName(name(declaration), cancellation); if (seen.Add(name(declaration))) result.Add(declaration); }
        return result;
    }

    private static List<DtdAttributeDeclaration> MergeAttributes(IReadOnlyList<DtdAttributeDeclaration> first,
        IReadOnlyList<DtdAttributeDeclaration> second, CancellationToken cancellation)
    {
        var seen = new HashSet<(string Element, string Attribute)>();
        var result = new List<DtdAttributeDeclaration>();
        foreach (var declaration in first)
        { CheckName(declaration.ElementName, cancellation); CheckName(declaration.Name, cancellation); if (seen.Add((declaration.ElementName, declaration.Name))) result.Add(declaration); }
        foreach (var declaration in second)
        { CheckName(declaration.ElementName, cancellation); CheckName(declaration.Name, cancellation); if (seen.Add((declaration.ElementName, declaration.Name))) result.Add(declaration); }
        return result;
    }
    private sealed class AttributeGroup
    {
        internal readonly HashSet<string> Names = new(StringComparer.Ordinal);
        internal readonly List<DtdAttributeDeclaration> Declarations = [];
    }

    private static void CheckName(string name, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        for (int i = 0; i < name.Length; i += Math.Min(4096, name.Length - i))
            cancellation.ThrowIfCancellationRequested();
    }
}
