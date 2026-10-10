using System.Text;
using BenchmarkDotNet.Running;
using Xemele;
using Xemele.Benchmarks;
using Xemele.Parsing.Dtd;
if (args.Contains("--correctness"))
{
    int assertions = 0;
    foreach (int count in new[] { 1, 64 })
    {
        var b = new DtdParserBenchmarks { Declarations = count }; b.Setup();
        try
        {
            for (int i = 0; i < 10; i++)
            {
                Check(b.BorrowedScan() == "ascii中😀\r\n".Length, "Consumed.");
                Model((DtdModel)b.OwnedGrammar(), count);
                Model((DtdModel)b.ChunkedGrammar(), count);
            }
        } finally { b.Cleanup(); }
    }
    Console.WriteLine($"{assertions} independent full-model benchmark correctness assertions passed."); return;
    void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    void Model(DtdModel model, int count)
    {
        Check(model.Elements.Count == count && model.Attributes.Count == 2 * count && model.GeneralEntities.Count == 1 && model.ParameterEntities.Count == 1 && model.Notations.Count == 0, "Full declaration inventory.");
        int column = 1;
        for (int i = 0; i < count; i++)
        {
            string name = "e" + i;
            string elementText = "<!ELEMENT " + name + " (a|b)*>";
            string attrStart = "<!ATTLIST " + name + " ";
            const string firstAttribute = "a CDATA 'raw &later;' ";
            const string secondAttribute = "b (yes|no) 'yes'>";
            var element = model.Elements[i];
            Check(element.Name == name && !element.IsExternal && element.Location == new XmlLocation(null, 1, column, null), "Element name/origin/location.");
            var content = element.Content;
            Check(content.Kind == DtdParticleKind.Choice && content.Name is null && content.Occurrence == DtdOccurrence.ZeroOrMore && content.Children.Count == 2, "Root particle/occurrence.");
            for (int child = 0; child < 2; child++)
            {
                var particle = content.Children[child];
                Check(particle.Kind == DtdParticleKind.Name && particle.Name == (child == 0 ? "a" : "b") && particle.Occurrence == DtdOccurrence.Once && particle.Children.Count == 0, "Child particle/name/occurrence.");
            }
            var a = model.Attributes[i * 2];
            Check(a.ElementName == name && a.Name == "a" && a.Type == DtdAttributeType.CData && a.Tokens.Count == 0 && a.DefaultKind == DtdDefaultKind.Value && a.DefaultValue == "raw &later;" && !a.IsExternal && a.Location == new XmlLocation(null, 1, column + elementText.Length + attrStart.Length, null), "CDATA raw default and location.");
            var enumeration = model.Attributes[i * 2 + 1];
            Check(enumeration.ElementName == name && enumeration.Name == "b" && enumeration.Type == DtdAttributeType.Enumeration && enumeration.Tokens.SequenceEqual(new[] { "yes", "no" }) && enumeration.DefaultKind == DtdDefaultKind.Value && enumeration.DefaultValue == "yes" && !enumeration.IsExternal && enumeration.Location == new XmlLocation(null, 1, column + elementText.Length + attrStart.Length + firstAttribute.Length, null), "Enumeration/type/tokens/default/location.");
            column += elementText.Length + attrStart.Length + firstAttribute.Length + secondAttribute.Length;
        }
        var general = model.GeneralEntities[0];
        Check(general.Name == "later" && !general.IsParameter && general.ReplacementText == "中😀" && general.SystemId is null && general.PublicId is null && general.NotationName is null && !general.IsExternal && general.Location == new XmlLocation(null, 1, column, null), "General entity full declaration.");
        column += "<!ENTITY later '中😀'>".EnumerateRunes().Count();
        var parameter = model.ParameterEntities[0];
        Check(parameter.Name == "p" && parameter.IsParameter && parameter.ReplacementText == "<!NOTATION n SYSTEM &quot;x&quot;>" && parameter.SystemId is null && parameter.PublicId is null && parameter.NotationName is null && !parameter.IsExternal && parameter.Location == new XmlLocation(null, 1, column, null), "Parameter entity full declaration.");
    }
}
BenchmarkSwitcher.FromAssembly(typeof(DtdParserBenchmarks).Assembly).Run(args);
