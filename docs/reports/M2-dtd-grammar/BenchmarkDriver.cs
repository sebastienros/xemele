using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
using Xemele.DtdGrammarJoin;
using Xemele.Parsing.Dtd;
using Xemele;
if (args.Contains("--correctness"))
{
    for (int i = 0; i < 10; i++)
    {
        var (model, defaults) = Join.Run();
        Check(model.Elements.Count == 3 && model.Attributes.Count == 2 && model.GeneralEntities.Count == 1 && model.ParameterEntities.Count == 0 && model.Notations.Count == 0);
        Element(model.Elements[0], "r", false, 1); Element(model.Elements[1], "a", true, 41); Element(model.Elements[2], "b", true, 59);
        var sequence = model.Elements[0].Content;
        Particle(sequence, DtdParticleKind.Sequence, null, DtdOccurrence.Once, 2);
        Particle(sequence.Children[0], DtdParticleKind.Name, "a", DtdOccurrence.Once, 0);
        Particle(sequence.Children[1], DtdParticleKind.Name, "b", DtdOccurrence.Optional, 0);
        Particle(model.Elements[1].Content, DtdParticleKind.Empty, null, DtdOccurrence.Once, 0);
        Particle(model.Elements[2].Content, DtdParticleKind.Mixed, null, DtdOccurrence.Once, 0);
        Attribute(model.Attributes[0], "local", DtdAttributeType.NmTokens, " x  y ", false, 32);
        Attribute(model.Attributes[1], "inherited", DtdAttributeType.CData, "&e;&#9;", true, 93);
        var entity = model.GeneralEntities[0];
        Check(entity.Name == "e" && !entity.IsParameter && entity.ReplacementText == "中😀" && entity.SystemId is null && entity.PublicId is null &&
            entity.NotationName is null && !entity.IsExternal && entity.Location == new XmlLocation(null, 1, 56, null));
        Check(defaults.SequenceEqual(new[] { "x y", "中😀\t" }));
        if (!((string[])new DtdGrammarJoinBenchmarks().EffectiveDefaults()).SequenceEqual(new[] { "x y", "中😀\t" })) throw new Exception("Timed method oracle.");
    }
    Console.WriteLine("10 authored full composition/model/default oracles and 10 timed-method output oracles passed."); return;
    static void Check(bool condition) { if (!condition) throw new Exception("Authored full composition oracle."); }
    static void Element(DtdElementDeclaration element, string name, bool external, int column)
        => Check(element.Name == name && element.IsExternal == external && element.Location == new XmlLocation(null, 1, column, null));
    static void Particle(DtdParticle particle, DtdParticleKind kind, string? name, DtdOccurrence occurrence, int children)
        => Check(particle.Kind == kind && particle.Name == name && particle.Occurrence == occurrence && particle.Children.Count == children);
    static void Attribute(DtdAttributeDeclaration attribute, string name, DtdAttributeType type, string raw, bool external, int column)
        => Check(attribute.ElementName == "r" && attribute.Name == name && attribute.Type == type && attribute.Tokens.Count == 0 &&
            attribute.DefaultKind == DtdDefaultKind.Value && attribute.DefaultValue == raw && attribute.IsExternal == external && attribute.Location == new XmlLocation(null, 1, column, null));
}
BenchmarkSwitcher.FromAssembly(typeof(DtdGrammarJoinBenchmarks).Assembly).Run(args);
