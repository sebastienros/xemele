using Xunit;

namespace Xemele.Tests.Infrastructure;

// Runner infrastructure only: this does not exercise product behavior or conformance.
public sealed class MtpRunnerSmokeTests
{
    [Fact]
    [Trait("Category", "Infrastructure")]
    public void RunnerExecutesAndPropagatesControlledFailure()
    {
        Assert.False(Environment.GetEnvironmentVariable("XEMELE_MTP_SMOKE_FAIL") == "1",
            "Intentional infrastructure failure used to verify the runner exit code.");
    }
}
