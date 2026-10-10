using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
using Xemele.DtdGrammarJoin;
using Xemele.Parsing.Dtd;
if (args.Contains("--correctness"))
{
    for (int i = 0; i < 10; i++)
    {
        var (model, defaults) = Join.Run();
        if (model.Elements.Count != 3 || model.Attributes.Count != 2 || model.GeneralEntities.Count != 1 || model.ParameterEntities.Count != 0 || model.Notations.Count != 0 ||
            !model.Elements.Select(e => e.Name).SequenceEqual(new[] { "r", "a", "b" }) || model.Elements[0].Content.Kind != DtdParticleKind.Sequence ||
            model.Elements[0].Content.Children.Count != 2 || model.Elements[0].Content.Children[0].Name != "a" || model.Elements[0].Content.Children[1].Name != "b" ||
            model.Elements[0].Content.Children[1].Occurrence != DtdOccurrence.Optional || model.Elements[0].IsExternal || !model.Elements[1].IsExternal ||
            model.Elements[1].Content.Kind != DtdParticleKind.Empty || model.Elements[2].Content.Kind != DtdParticleKind.Mixed ||
            model.GeneralEntities[0].Name != "e" || model.GeneralEntities[0].ReplacementText != "中😀" || model.GeneralEntities[0].IsExternal ||
            model.Attributes[0].Name != "local" || model.Attributes[0].DefaultValue != " x  y " || model.Attributes[0].Type != DtdAttributeType.NmTokens || model.Attributes[0].IsExternal ||
            model.Attributes[1].Name != "inherited" || model.Attributes[1].DefaultValue != "&e;&#9;" || model.Attributes[1].Type != DtdAttributeType.CData || !model.Attributes[1].IsExternal ||
            !defaults.SequenceEqual(new[] { "x y", "中😀\t" })) throw new Exception("Authored full composition oracle.");
        if (!((string[])new DtdGrammarJoinBenchmarks().EffectiveDefaults()).SequenceEqual(new[] { "x y", "中😀\t" })) throw new Exception("Timed method oracle.");
    }
    Console.WriteLine("10 authored full composition/model/default oracles and 10 timed-method output oracles passed."); return;
}
BenchmarkSwitcher.FromAssembly(typeof(DtdGrammarJoinBenchmarks).Assembly).Run(args);
