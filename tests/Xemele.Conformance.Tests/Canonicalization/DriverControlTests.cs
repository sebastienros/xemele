using System.Text;
using System.Text.Json;
using Xunit;
namespace Xemele.Conformance.Tests.Canonicalization;

public sealed class DriverControlTests
{
    [Theory]
    [InlineData("parse")]
    [InlineData("product")]
    [InlineData("reference")]
    public void OneInjectedStageFailureLeavesEveryVectorSerialized(string phase)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Dtd.ApplicabilityTests.Root, "tests/corpora/c14n/vectors.json")));
        var document = new XmlDocument();
        var results = new List<CanonicalObservation>();
        var index = 0;
        foreach (var c in vectors.RootElement.EnumerateArray())
        {
            var inject = index++ == 0;
            results.Add(CaseRunner.Run(c, () => inject && phase == "parse" ? throw new IOException("injected-parse") : document,
                (_, _) => inject && phase == "product" ? throw new IOException("injected-product") : Encoding.UTF8.GetBytes(c.GetProperty("expected").GetString()!),
                _ => inject && phase == "reference" ? throw new IOException("injected-reference") : Encoding.UTF8.GetBytes(c.GetProperty("expected").GetString()!)));
        }
        Assert.Equal(32, results.Count);
        Assert.Single(results, r => !r.Pass);
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(results));
        Assert.Equal(32, serialized.RootElement.GetArrayLength());
    }
    [Theory]
    [InlineData("XML.ResourceDenied", "infrastructure")]
    [InlineData("DTD.ExternalInput", "infrastructure")]
    [InlineData("XML.ResourceCycle", "infrastructure")]
    [InlineData("DTD.IncompleteInput", "incomplete")]
    [InlineData("XML.ExternalBytesLimit", "incomplete")]
    public void RefusalsAndIncompleteNeverSatisfyInvalidity(string code, string expected)
    {
        var outcome = Dtd.OutcomeClassification.ParseException([code]);
        Assert.Equal(expected, outcome);
        Assert.False(Dtd.OutcomeClassification.Pass("associated-dtd", ["invalid"], outcome));
    }
}
