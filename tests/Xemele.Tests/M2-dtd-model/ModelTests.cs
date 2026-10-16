using System.Collections;
using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests.DtdModel;

public sealed class ModelTests
{
    private static readonly XmlLocation Location = new(new Uri("https://fixture.invalid/subset.dtd"), 7, 11, 123);
    private static DtdParticle Leaf(string? name = "a", DtdOccurrence occurrence = DtdOccurrence.Once) => new(DtdParticleKind.Name, name, occurrence, []);
    private static DtdAttributeDeclaration Attribute(IEnumerable<string> tokens, DtdDefaultKind kind = DtdDefaultKind.Value, string? value = " value ") =>
        new("p:root", "mode", DtdAttributeType.Enumeration, tokens, kind, value, true, Location);
    private static void ReadOnly<T>(IReadOnlyList<T> snapshot, T value)
    {
        var list = Assert.IsAssignableFrom<IList<T>>(snapshot);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list.Add(value));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Throws<NotSupportedException>(() => list.Insert(0, value));
        if (list.Count > 0)
        {
            Assert.Throws<NotSupportedException>(() => list[0] = value);
            Assert.Throws<NotSupportedException>(() => list.RemoveAt(0));
        }
        var untyped = Assert.IsAssignableFrom<IList>(snapshot);
        Assert.True(untyped.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => untyped.Add(value));
    }
    [Fact]
    public void ParticleSnapshotsRemainRecursivelyImmutableAfterSourceMutation()
    {
        var leaves = new[] { Leaf("first"), Leaf("second", DtdOccurrence.Optional) };
        var choice = new DtdParticle(DtdParticleKind.Choice, null, DtdOccurrence.OneOrMore, leaves);
        var outer = new List<DtdParticle> { choice, Leaf("last") };
        var sequence = new DtdParticle(DtdParticleKind.Sequence, null, DtdOccurrence.ZeroOrMore, outer);
        leaves[0] = Leaf("changed");
        outer.Clear();
        Assert.Equal(DtdParticleKind.Sequence, sequence.Kind);
        Assert.Null(sequence.Name);
        Assert.Equal(DtdOccurrence.ZeroOrMore, sequence.Occurrence);
        Assert.Equal(["first", "second"], sequence.Children[0].Children.Select(p => p.Name));
        Assert.Equal(DtdOccurrence.OneOrMore, sequence.Children[0].Occurrence);
        Assert.Equal(DtdOccurrence.Optional, sequence.Children[0].Children[1].Occurrence);
        Assert.Equal("last", sequence.Children[1].Name);
        ReadOnly(sequence.Children, Leaf());
        ReadOnly(choice.Children, Leaf());
        ReadOnly(leaves[1].Children, Leaf());
    }
    [Fact]
    public void TokenSnapshotPreservesOrderDuplicatesAndExactValues()
    {
        var tokens = new List<string> { "blue", "red", "blue", " unnormalized " };
        var declaration = Attribute(tokens);
        tokens[0] = "changed";
        tokens.Clear();
        Assert.Equal(["blue", "red", "blue", " unnormalized "], declaration.Tokens);
        Assert.Equal("p:root", declaration.ElementName);
        Assert.Equal("mode", declaration.Name);
        Assert.Equal(DtdAttributeType.Enumeration, declaration.Type);
        Assert.Equal(" value ", declaration.DefaultValue);
        Assert.Equal(DtdDefaultKind.Value, declaration.DefaultKind);
        Assert.True(declaration.IsExternal);
        Assert.Equal(Location, declaration.Location);
        ReadOnly(declaration.Tokens, "changed");
    }
    [Fact]
    public void AllModelTablesOwnOrderedSnapshotsAndPreserveDuplicates()
    {
        var first = new DtdElementDeclaration("root", Leaf(), false, Location);
        var duplicate = first with
        {
            IsExternal = true
        };
        var elements = new[] { first, duplicate };
        var attribute = Attribute(["x"]);
        var attributes = new List<DtdAttributeDeclaration> { attribute, attribute };
        var general = new DtdEntityDeclaration("same", false, "&other;\n", null, null, null, false, Location);
        var parameter = general with
        {
            IsParameter = true,
            ReplacementText = "parameter"
        };
        var generals = new[] { general, general };
        var parameters = new List<DtdEntityDeclaration> { parameter };
        var notation = new DtdNotationDeclaration("image", "image.sys", "public id", Location);
        var notations = new[] { notation, notation };
        var model = new Xemele.Parsing.Dtd.DtdModel(elements, attributes, generals, parameters, notations);
        elements[0] = duplicate;
        attributes.Clear();
        generals[0] = parameter;
        parameters.Clear();
        notations[0] = notation with
        {
            Name = "changed"
        };
        Assert.Equal([first, duplicate], model.Elements);
        Assert.Equal([attribute, attribute], model.Attributes);
        Assert.Equal([general, general], model.GeneralEntities);
        Assert.Equal([parameter], model.ParameterEntities);
        Assert.Equal([notation, notation], model.Notations);
        ReadOnly(model.Elements, first);
        ReadOnly(model.Attributes, attribute);
        ReadOnly(model.GeneralEntities, general);
        ReadOnly(model.ParameterEntities, parameter);
        ReadOnly(model.Notations, notation);
    }
    [Fact]
    public void OneShotEnumerablesAreConsumedExactlyOnceForEverySnapshot()
    {
        var child = new Once<DtdParticle>([Leaf()]);
        var particle = new DtdParticle(DtdParticleKind.Mixed, null, DtdOccurrence.ZeroOrMore, child);
        var tokens = new Once<string>(["a", "b"]);
        var attribute = Attribute(tokens);
        var elements = new Once<DtdElementDeclaration>([new("r", particle, false, Location)]);
        var attributes = new Once<DtdAttributeDeclaration>([attribute]);
        var generals = new Once<DtdEntityDeclaration>([new("g", false, "text", null, null, null, false, Location)]);
        var parameters = new Once<DtdEntityDeclaration>([new("p", true, "text", null, null, null, false, Location)]);
        var notations = new Once<DtdNotationDeclaration>([new("n", null, "public", Location)]);
        var model = new Xemele.Parsing.Dtd.DtdModel(elements, attributes, generals, parameters, notations);
        Assert.Single(model.Elements);
        Assert.Single(model.Attributes);
        Assert.Single(model.GeneralEntities);
        Assert.Single(model.ParameterEntities);
        Assert.Single(model.Notations);
        Assert.Equal(2, attribute.Tokens.Count);
        Assert.Equal(1, child.Enumerations);
        Assert.Equal(1, tokens.Enumerations);
        Assert.Equal(1, elements.Enumerations);
        Assert.Equal(1, attributes.Enumerations);
        Assert.Equal(1, generals.Enumerations);
        Assert.Equal(1, parameters.Enumerations);
        Assert.Equal(1, notations.Enumerations);
    }
    [Fact]
    public void EmptySnapshotsAndAllParticleKindsAndOccurrencesArePreserved()
    {
        foreach (var kind in Enum.GetValues<DtdParticleKind>())
        {
            foreach (var occurrence in Enum.GetValues<DtdOccurrence>())
            {
                var particle = new DtdParticle(kind, null, occurrence, []);
                Assert.Equal(kind, particle.Kind);
                Assert.Equal(occurrence, particle.Occurrence);
                Assert.Empty(particle.Children);
            }
        }

        var model = new Xemele.Parsing.Dtd.DtdModel([], [], [], [], []);
        Assert.Empty(model.Elements);
        Assert.Empty(model.Attributes);
        Assert.Empty(model.GeneralEntities);
        Assert.Empty(model.ParameterEntities);
        Assert.Empty(model.Notations);
        ReadOnly(model.Elements, new("r", Leaf(), false, default));
        ReadOnly(Attribute([]).Tokens, "x");
    }
    [Fact]
    public void EveryAttributeTypeAndDefaultKindRetainsSuppliedSemanticsWithoutNormalization()
    {
        foreach (var type in Enum.GetValues<DtdAttributeType>())
        {
            foreach (var kind in Enum.GetValues<DtdDefaultKind>())
            {
                foreach (var value in new string?[] { null, "", " \t&entity;\r\n " })
                {
                    var declaration = new DtdAttributeDeclaration("e", "a", type, ["z", "a"], kind, value, false, default);
                    Assert.Equal(type, declaration.Type);
                    Assert.Equal(kind, declaration.DefaultKind);
                    Assert.Equal(value, declaration.DefaultValue);
                    Assert.Equal(["z", "a"], declaration.Tokens);
                    Assert.False(declaration.IsExternal);
                    Assert.Equal(default, declaration.Location);
                }
            }
        }
    }
    [Fact]
    public void EntityAndNotationFormsRetainIdentifiersReplacementAndSourceOrigin()
    {
        var internalEntity = new DtdEntityDeclaration("internal", false, "\n&reference;", null, null, null, false, Location);
        var externalEntity = new DtdEntityDeclaration("external", true, null, "relative.dtd", "public id", null, true, Location);
        var unparsed = externalEntity with
        {
            Name = "image",
            IsParameter = false,
            NotationName = "png"
        };
        var model = new Xemele.Parsing.Dtd.DtdModel([], [], [internalEntity, unparsed], [externalEntity], [new("png", null, "notation public", Location)]);
        Assert.Equal("\n&reference;", model.GeneralEntities[0].ReplacementText);
        Assert.Null(model.GeneralEntities[0].SystemId);
        Assert.False(model.GeneralEntities[0].IsExternal);
        Assert.Equal("png", model.GeneralEntities[1].NotationName);
        Assert.Null(model.GeneralEntities[1].ReplacementText);
        Assert.Equal("relative.dtd", model.GeneralEntities[1].SystemId);
        Assert.Equal("public id", model.GeneralEntities[1].PublicId);
        Assert.True(model.ParameterEntities[0].IsParameter);
        Assert.True(model.ParameterEntities[0].IsExternal);
        Assert.Equal(Location, model.ParameterEntities[0].Location);
        Assert.Same(Location.SourceUri, model.ParameterEntities[0].Location.SourceUri);
        Assert.Equal("notation public", model.Notations[0].PublicId);
        Assert.Null(model.Notations[0].SystemId);
        Assert.Equal(Location, model.Notations[0].Location);
    }
    [Fact]
    public void RequiredNullCollectionsReportTheirExactParameter()
    {
        Assert.Equal("children", Assert.Throws<ArgumentNullException>(() => new DtdParticle(DtdParticleKind.Any, null, DtdOccurrence.Once, null!)).ParamName);
        Assert.Equal("tokens", Assert.Throws<ArgumentNullException>(() => Attribute(null!)).ParamName);
        Assert.Equal("elements", Assert.Throws<ArgumentNullException>(() => new Xemele.Parsing.Dtd.DtdModel(null!, [], [], [], [])).ParamName);
        Assert.Equal("attributes", Assert.Throws<ArgumentNullException>(() => new Xemele.Parsing.Dtd.DtdModel([], null!, [], [], [])).ParamName);
        Assert.Equal("generalEntities", Assert.Throws<ArgumentNullException>(() => new Xemele.Parsing.Dtd.DtdModel([], [], null!, [], [])).ParamName);
        Assert.Equal("parameterEntities", Assert.Throws<ArgumentNullException>(() => new Xemele.Parsing.Dtd.DtdModel([], [], [], null!, [])).ParamName);
        Assert.Equal("notations", Assert.Throws<ArgumentNullException>(() => new Xemele.Parsing.Dtd.DtdModel([], [], [], [], null!)).ParamName);
    }
    private sealed class Once<T>(T[] values) : IEnumerable<T>
    {
        public int Enumerations
        {
            get; private set;
        }
        public IEnumerator<T> GetEnumerator()
        {
            if (++Enumerations != 1)
            {
                throw new InvalidOperationException("Source enumerated more than once.");
            }

            foreach (var value in values)
            {
                yield return value;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
