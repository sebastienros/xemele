// Owned immutable declaration snapshots. Grammar validation and precedence belong to the parser.
namespace Xemele.Parsing.Dtd;
internal enum DtdSubsetKind { Internal, External }
internal enum DtdAttributeType { CData, Id, IdRef, IdRefs, Entity, Entities, NmToken, NmTokens, Enumeration, Notation }
internal enum DtdDefaultKind { Implied, Required, Fixed, Value }
internal enum DtdParticleKind { Empty, Any, Name, Sequence, Choice, Mixed }
internal enum DtdOccurrence { Once, Optional, ZeroOrMore, OneOrMore }
internal sealed class DtdParticle
{
    public DtdParticleKind Kind { get; }
    public string? Name { get; }
    public DtdOccurrence Occurrence { get; }
    public IReadOnlyList<DtdParticle> Children { get; }
    public DtdParticle(DtdParticleKind kind, string? name, DtdOccurrence occurrence, IEnumerable<DtdParticle> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Kind = kind; Name = name; Occurrence = occurrence;
        Children = Array.AsReadOnly(children.ToArray());
    }
}
internal sealed record DtdElementDeclaration(string Name, DtdParticle Content, bool IsExternal, XmlLocation Location);
internal sealed class DtdAttributeDeclaration
{
    public string ElementName { get; }
    public string Name { get; }
    public DtdAttributeType Type { get; }
    public IReadOnlyList<string> Tokens { get; }
    public DtdDefaultKind DefaultKind { get; }
    public string? DefaultValue { get; }
    public bool IsExternal { get; }
    public XmlLocation Location { get; }
    public DtdAttributeDeclaration(string elementName, string name, DtdAttributeType type, IEnumerable<string> tokens,
        DtdDefaultKind defaultKind, string? defaultValue, bool isExternal, XmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ElementName = elementName; Name = name; Type = type; Tokens = Array.AsReadOnly(tokens.ToArray());
        DefaultKind = defaultKind; DefaultValue = defaultValue; IsExternal = isExternal; Location = location;
    }
}
internal sealed record DtdEntityDeclaration(string Name, bool IsParameter, string? ReplacementText,
    string? SystemId, string? PublicId, string? NotationName, bool IsExternal, XmlLocation Location);
internal sealed record DtdNotationDeclaration(string Name, string? SystemId, string? PublicId, XmlLocation Location);
internal sealed class DtdModel
{
    public IReadOnlyList<DtdElementDeclaration> Elements { get; }
    public IReadOnlyList<DtdAttributeDeclaration> Attributes { get; }
    public IReadOnlyList<DtdEntityDeclaration> GeneralEntities { get; }
    public IReadOnlyList<DtdEntityDeclaration> ParameterEntities { get; }
    public IReadOnlyList<DtdNotationDeclaration> Notations { get; }
    public DtdModel(IEnumerable<DtdElementDeclaration> elements, IEnumerable<DtdAttributeDeclaration> attributes,
        IEnumerable<DtdEntityDeclaration> generalEntities, IEnumerable<DtdEntityDeclaration> parameterEntities,
        IEnumerable<DtdNotationDeclaration> notations)
    {
        ArgumentNullException.ThrowIfNull(elements); ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(generalEntities); ArgumentNullException.ThrowIfNull(parameterEntities);
        ArgumentNullException.ThrowIfNull(notations);
        Elements = Array.AsReadOnly(elements.ToArray()); Attributes = Array.AsReadOnly(attributes.ToArray());
        GeneralEntities = Array.AsReadOnly(generalEntities.ToArray()); ParameterEntities = Array.AsReadOnly(parameterEntities.ToArray());
        Notations = Array.AsReadOnly(notations.ToArray());
    }
}
