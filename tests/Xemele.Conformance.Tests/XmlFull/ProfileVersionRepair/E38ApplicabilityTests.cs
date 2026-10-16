using System.Text.Json;
using Xunit;

namespace Xemele.Conformance.Tests.XmlFull.ProfileVersionRepair;

public sealed class E38ApplicabilityTests
{
    [Fact]
    public void HistoricalE38PurposeDoesNotOverrideFifthEditionVersionGrammar()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root missing");
        using var ledger = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tests/corpora/xml/m2-applicability.json")));
        var row = Assert.Single(ledger.RootElement.GetProperty("cases").EnumerateArray(), c => c.GetProperty("id").GetString() == "xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-38");
        Assert.Equal("not-wf", row.GetProperty("sourceType").GetString());
        Assert.Equal("XML1.0-errata2e", row.GetProperty("sourceRecommendation").GetString());
        Assert.Equal("accepted", Assert.Single(row.GetProperty("expectedOutcomes").EnumerateArray()).GetString());
        Assert.Equal("superseded-e38-version", row.GetProperty("rule").GetString());
        Assert.StartsWith("XML 1.0 document refers to 1.1 entity Source sections: E38.", row.GetProperty("rationale").GetString()!, StringComparison.Ordinal);
        Assert.Contains("https://www.w3.org/XML/xml-V10-4e-errata#E10", row.GetProperty("normative").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("b6be7039a5dfeaad8eaad16eb3976c754888985bdd598b518a81a0f9547e3cf6", row.GetProperty("inputSha256").GetString());
        var version = Assert.Single(row.GetProperty("versionEvidence").EnumerateArray());
        Assert.Equal("1.1", version.GetProperty("version").GetString());
        Assert.Equal("028229ed9dda8481d7996ef03a1e17300255abef2bf082011312f09b28623eef", version.GetProperty("sha256").GetString());
        Assert.Equal("xmlconf/eduni/errata-2e/E38.ent", Assert.Single(row.GetProperty("offlinePaths").EnumerateArray()).GetString());
        Assert.Empty(row.GetProperty("scalarEvidence").EnumerateArray());
        Assert.Equal("not-executed", row.GetProperty("productStatus").GetString()); // Ledger is a classification contract, not execution evidence.
    }
}
