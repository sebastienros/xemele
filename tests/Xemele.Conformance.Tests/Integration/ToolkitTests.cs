using Xemele.IntegrationSamples;
using Xunit;
namespace Xemele.Conformance.Tests.Integration;

public sealed class ToolkitTests
{
    private static string Corpus
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "tests/corpora/xinclude")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir?.FullName ?? throw new InvalidOperationException(), "tests/corpora");
        }
    }
    [Fact]
    public async Task PublicOfflineCompositionMatchesFrozenOutput() => Assert.Equal(File.ReadAllText(Path.Combine(Corpus, "xinclude/expected.txt")), await ToolkitScenario.RunAsync(Corpus, TestContext.Current.CancellationToken));
    [Fact]
    public async Task AggregateIncludeQuotaRejectsNestedResourceAndClosesOpenedSource()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ToolkitScenario.RunAsync(Corpus, TestContext.Current.CancellationToken, 1));
        Assert.StartsWith("Offline include failed:", error.Message);
    }
    [Fact]
    public async Task CancellationPrecedesResourceWork()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToolkitScenario.RunAsync(Corpus, canceled.Token));
    }
}
