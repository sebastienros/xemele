using System.Numerics;
using System.Collections.ObjectModel;
namespace Xemele.Validation;
internal enum XsdDerivation { None, Restriction, Extension, List, Union }
internal enum XsdElementBlock { Substitution, Restriction, Extension }
internal enum XsdTypeBlock { Restriction, Extension }
internal enum XsdContentKind { Empty, Simple, ElementOnly, Mixed }
internal enum XsdProcessContents { Strict, Lax, Skip }
internal enum XsdAttributeUse { Optional, Required, Prohibited }
internal enum XsdCompositor { Sequence, Choice, All }
internal enum XsdIdentityKind { Key, Unique, KeyRef }
internal sealed record XsdWildcard(bool Negated, IReadOnlyList<string> Namespaces, XsdProcessContents ProcessContents)
{
    private IReadOnlyList<string> namespaces = SchemaSnapshots.List(Namespaces);
    public IReadOnlyList<string> Namespaces { get => namespaces; init => namespaces = SchemaSnapshots.List(value); }
}
internal abstract record XsdTerm;
internal sealed record XsdElementTerm(int DeclarationId) : XsdTerm;
internal sealed record XsdWildcardTerm(XsdWildcard Wildcard) : XsdTerm;
internal sealed record XsdGroupTerm(XsdCompositor Compositor, IReadOnlyList<XsdParticle> Particles) : XsdTerm
{
    private IReadOnlyList<XsdParticle> particles = SchemaSnapshots.List(Particles);
    public IReadOnlyList<XsdParticle> Particles { get => particles; init => particles = SchemaSnapshots.List(value); }
}
internal sealed record XsdParticle(XsdTerm Term, BigInteger MinOccurs, BigInteger? MaxOccurs, XmlLocation Location);
internal sealed record XsdAttributeDeclaration(int Id, SchemaName Name, SchemaName Type, string? DefaultValue, string? FixedValue, SchemaNamespaceContext Context, XmlLocation Location);
internal sealed record XsdAttributeBinding(int DeclarationId, XsdAttributeUse Use, string? DefaultValue, string? FixedValue,
    SchemaNamespaceContext Context, XmlLocation Location);
internal sealed record XsdTypeDefinition(SchemaName Name, SchemaName? BaseType, XsdDerivation Derivation,
    bool Abstract, bool Anonymous, IReadOnlyList<XsdDerivation> Final, IReadOnlyList<XsdTypeBlock> Block, XsdContentKind ContentKind, SchemaName? SimpleType,
    XsdParticle? Particle, IReadOnlyList<XsdAttributeBinding> Attributes, XsdWildcard? AnyAttribute, XmlLocation Location)
{
    private IReadOnlyList<XsdDerivation> final = SchemaSnapshots.List(Final);
    public IReadOnlyList<XsdDerivation> Final { get => final; init => final = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdTypeBlock> block = SchemaSnapshots.List(Block);
    public IReadOnlyList<XsdTypeBlock> Block { get => block; init => block = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdAttributeBinding> attributes = SchemaSnapshots.List(Attributes);
    public IReadOnlyList<XsdAttributeBinding> Attributes { get => attributes; init => attributes = SchemaSnapshots.List(value); }
}
internal sealed record XsdElementDeclaration(int Id, SchemaName Name, SchemaName? Type, bool Abstract, bool Nillable,
    string? DefaultValue, string? FixedValue, SchemaName? SubstitutionGroup, IReadOnlyList<XsdElementBlock> Block,
    IReadOnlyList<XsdDerivation> Final, IReadOnlyList<int> IdentityConstraints, SchemaNamespaceContext Context, XmlLocation Location)
{
    private IReadOnlyList<XsdElementBlock> block = SchemaSnapshots.List(Block);
    public IReadOnlyList<XsdElementBlock> Block { get => block; init => block = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdDerivation> final = SchemaSnapshots.List(Final);
    public IReadOnlyList<XsdDerivation> Final { get => final; init => final = SchemaSnapshots.List(value); }
    private IReadOnlyList<int> identityConstraints = SchemaSnapshots.List(IdentityConstraints);
    public IReadOnlyList<int> IdentityConstraints { get => identityConstraints; init => identityConstraints = SchemaSnapshots.List(value); }
}
internal sealed record XsdIdentityConstraint(int Id, SchemaName Name, XsdIdentityKind Kind, string Selector,
    IReadOnlyList<string> Fields, SchemaName? Refer, SchemaNamespaceContext Context, XmlLocation Location)
{
    private IReadOnlyList<string> fields = SchemaSnapshots.List(Fields);
    public IReadOnlyList<string> Fields { get => fields; init => fields = SchemaSnapshots.List(value); }
}
internal sealed record XsdNotationDeclaration(SchemaName Name, string? PublicId, string? SystemId,
    SchemaNamespaceContext Context, XmlLocation Location);
internal sealed record XsdCompiledSchema(IReadOnlyList<XsdElementDeclaration> Elements,
    IReadOnlyList<XsdAttributeDeclaration> Attributes, IReadOnlyList<XsdTypeDefinition> Types,
    IReadOnlyList<XmlDatatypeDefinition> SimpleTypes, IReadOnlyList<XsdIdentityConstraint> Identities,
    IReadOnlyDictionary<SchemaName, XsdNotationDeclaration> Notations,
    IReadOnlyDictionary<SchemaName, int> GlobalElements, IReadOnlyDictionary<SchemaName, int> GlobalAttributes,
    IXmlDatatypeRegistry Datatypes)
{
    private IReadOnlyList<XsdElementDeclaration> elements = SchemaSnapshots.List(Elements);
    public IReadOnlyList<XsdElementDeclaration> Elements { get => elements; init => elements = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdAttributeDeclaration> attributes = SchemaSnapshots.List(Attributes);
    public IReadOnlyList<XsdAttributeDeclaration> Attributes { get => attributes; init => attributes = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdTypeDefinition> types = SchemaSnapshots.List(Types);
    public IReadOnlyList<XsdTypeDefinition> Types { get => types; init => types = SchemaSnapshots.List(value); }
    private IReadOnlyList<XmlDatatypeDefinition> simpleTypes = SchemaSnapshots.List(SimpleTypes);
    public IReadOnlyList<XmlDatatypeDefinition> SimpleTypes { get => simpleTypes; init => simpleTypes = SchemaSnapshots.List(value); }
    private IReadOnlyList<XsdIdentityConstraint> identities = SchemaSnapshots.List(Identities);
    public IReadOnlyList<XsdIdentityConstraint> Identities { get => identities; init => identities = SchemaSnapshots.List(value); }
    private IReadOnlyDictionary<SchemaName, XsdNotationDeclaration> notations = XsdSnapshots.Map(Notations);
    public IReadOnlyDictionary<SchemaName, XsdNotationDeclaration> Notations { get => notations; init => notations = XsdSnapshots.Map(value); }
    private IReadOnlyDictionary<SchemaName, int> globalElements = XsdSnapshots.Map(GlobalElements);
    public IReadOnlyDictionary<SchemaName, int> GlobalElements { get => globalElements; init => globalElements = XsdSnapshots.Map(value); }
    private IReadOnlyDictionary<SchemaName, int> globalAttributes = XsdSnapshots.Map(GlobalAttributes);
    public IReadOnlyDictionary<SchemaName, int> GlobalAttributes { get => globalAttributes; init => globalAttributes = XsdSnapshots.Map(value); }
}
internal interface IXsdCompiler
{
    XsdCompiledSchema Compile(XmlDocument schema, SchemaCompileOptions options, ISchemaOperation operation);
}

internal static class XsdSnapshots
{
    internal static IReadOnlyDictionary<SchemaName,T> Map<T>(IReadOnlyDictionary<SchemaName,T> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var owned = new Dictionary<SchemaName,T>();
        foreach (var (key,value) in input) { if (value is null) throw new ArgumentException("Null component.", nameof(input)); owned.Add(key,value); }
        return new ReadOnlyDictionary<SchemaName,T>(owned);
    }
}
