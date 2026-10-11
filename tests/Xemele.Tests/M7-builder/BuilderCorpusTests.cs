using Xunit;
namespace Xemele.Tests.HtmlTreeConstruction;
public sealed class BuilderCorpusTests
{
    [Fact]
    public void EveryOriginalWptTreeFragmentVectorAndTransport()
    {
        string root = BuilderInputTests.FindRoot(); string report = Path.Combine(root, "artifacts/M7-builder/corpus-failures.json"); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        var result = TreeCorpus.Run(Path.Combine(root, "tests/fixtures/M7-builder/wpt-parsing/resources"), report);
        Assert.Equal(14316, result.Executions); Assert.Equal(1968, result.Originals); Assert.Equal(6, result.BrowserExecutions); Assert.Equal(6, result.NormativeConfigurations); Assert.Equal(0, result.Mismatches);
    }
}
