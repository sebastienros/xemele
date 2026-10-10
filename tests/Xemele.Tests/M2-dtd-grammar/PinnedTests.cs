using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.Parsing.Dtd;
using Xunit;
namespace Xemele.Tests;
public sealed class DtdGrammarPinnedTests
{
    public static IEnumerable<object[]> Resources()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "tests/fixtures/M2-dtd-grammar/pinned.json")));
        foreach (var row in doc.RootElement.GetProperty("cases").EnumerateArray())
            yield return [row.GetProperty("resource").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("bytes").GetInt32(),
                row.GetProperty("valid").GetBoolean(), row.GetProperty("oracle").GetString()!];
    }
    [Theory, MemberData(nameof(Resources))]
    public void PinnedExternalSubsetEveryShortSplitWithAuthoredModel(string path, string hash, int length, bool valid, string oracle)
    {
        string cache = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
        byte[] bytes = File.ReadAllBytes(Path.Combine(cache, path));
        Assert.Equal(length, bytes.Length); Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        string text = new UTF8Encoding(false, true).GetString(bytes);
        for (int split = 0; split <= text.Length; split++)
        {
            using var parser = new DtdParser(DtdSubsetKind.External, new(), new Uri("https://www.w3.org/XML/Test/" + path));
            var first = parser.Append(text.AsSpan(0, split), [], false, TestContext.Current.CancellationToken);
            var last = first.Step == DtdParseStep.Rejected ? first : parser.Append(text.AsSpan(split), [], true, TestContext.Current.CancellationToken);
            Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, last.Step);
            if (!valid) { Assert.NotNull(last.Diagnostic); Assert.Null(last.Entity); continue; }
            Assert.Null(last.Diagnostic); Assert.Null(last.Entity); Model(parser.Model, oracle);
        }
    }
    private static void Model(DtdModel model, string oracle)
    {
        // Expected declarations are authored from the pinned source, not a second parser.
        Assert.Empty(model.Notations);
        if (oracle == "p31pass1") { Assert.Empty(model.Elements); Assert.Empty(model.Attributes); Assert.Empty(model.GeneralEntities); Assert.Empty(model.ParameterEntities); return; }
        var element = Assert.Single(model.Elements); Assert.True(element.IsExternal); Assert.Equal(DtdOccurrence.Once, element.Content.Occurrence); Assert.Null(element.Content.Name); Assert.Empty(element.Content.Children);
        Assert.Equal(oracle.StartsWith("p", StringComparison.Ordinal) ? "doc" : oracle == "ibm32v01" ? "animal" : "root", element.Name);
        Assert.Equal(oracle == "pe00" ? DtdParticleKind.Mixed : DtdParticleKind.Empty, element.Content.Kind);
        if (oracle == "pe00")
        {
            Assert.Empty(model.Attributes); var parameter = Assert.Single(model.ParameterEntities);
            Assert.Equal("pub", parameter.Name); Assert.Equal("Éditions Gallimard", parameter.ReplacementText); Assert.True(parameter.IsParameter); Assert.True(parameter.IsExternal);
            Assert.Equal(new[] { "rights", "book" }, model.GeneralEntities.Select(e => e.Name));
            Assert.Equal("All rights reserved", model.GeneralEntities[0].ReplacementText);
            Assert.Equal("La Peste: Albert Camus,\n© 1947 Éditions Gallimard. &rights;", model.GeneralEntities[1].ReplacementText);
            return;
        }
        Assert.Empty(model.GeneralEntities);
        if (oracle == "pe01")
        {
            var parameter = Assert.Single(model.ParameterEntities); Assert.Equal("bad-pe", parameter.Name); Assert.Equal("pe01.ent", parameter.SystemId); Assert.Null(parameter.ReplacementText);
            Assert.Equal(2, model.Attributes.Count); var literal = model.Attributes[0];
            Assert.Equal("something", literal.Name); Assert.Equal(DtdAttributeType.CData, literal.Type); Assert.Equal("%not-pe;", literal.DefaultValue);
            var enumeration = model.Attributes[1]; Assert.Equal("evil", enumeration.Name); Assert.Equal(DtdAttributeType.Enumeration, enumeration.Type);
            Assert.Equal(new[] { "doctor", "scott" }, enumeration.Tokens); Assert.Equal("doctor", enumeration.DefaultValue);
        }
        else
        {
            Assert.Empty(model.ParameterEntities);
            if (oracle == "ibm32v01")
            { var a = Assert.Single(model.Attributes); Assert.Equal("animal", a.ElementName); Assert.Equal("color", a.Name); Assert.Equal(DtdAttributeType.CData, a.Type); Assert.Equal(DtdDefaultKind.Fixed, a.DefaultKind); Assert.Equal("yellow", a.DefaultValue); Assert.Empty(a.Tokens); Assert.True(a.IsExternal); }
            else Assert.Empty(model.Attributes);
        }
    }
    private static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "docs/tasks/M2-dtd-grammar.json"))) return d.FullName;
        throw new InvalidOperationException("Repository root missing.");
    }
}
