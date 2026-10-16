using System.Text.Json;
using Xunit;

namespace Xemele.Tests;

public sealed class FramingAllocationProtocolTests
{
    [Fact]
    public void ReportSchemaAndProvenanceRejectMalformedEvidence()
    {
        var actual = FramingAllocationChild.CurrentProvenance();
        var request = new FramingAllocationChild.Request(1, "real", Guid.NewGuid().ToString("N"),
            Environment.ProcessId, "schema-control", "unused.xml", "unused.json", actual);
        var report = new FramingAllocationChild.Report(1, "real", request.Token, Environment.ProcessId, actual,
            1000, 1000, 0, 0, 32000, 32000, 1, 32007, 1, 64007, "Declared", 14, 1, 64021, null, 0);
        var json = JsonSerializer.Serialize(report);
        Assert.Equal(report, FramingAllocationChild.ParseAndValidateReport(json, request, Environment.ProcessId, actual));
        Assert.Throws<JsonException>(() => FramingAllocationChild.ParseAndValidateReport("{broken", request, Environment.ProcessId, actual));
        Assert.Throws<JsonException>(() => FramingAllocationChild.ParseAndValidateReport(json[..^1] + ",\"Unexpected\":true}", request, Environment.ProcessId, actual));
        var missing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        missing.Remove("AllocatedBytes");
        Assert.Throws<JsonException>(() => FramingAllocationChild.ParseAndValidateReport(JsonSerializer.Serialize(missing), request, Environment.ProcessId, actual));
        foreach (var invalid in new[] {
            report with { ProcessId = -1 }, report with { Token = "wrong" }, report with { Mode = "positive" },
            report with { Actual = actual with { HelperIlSha256 = "wrong" } }, report with { Actual = actual with { NativeRuntimeSha256 = "wrong" } },
            report with { Actual = actual with { RuntimeConfigSha256 = "wrong" } }, report with { Actual = actual with { ServerGc = !actual.ServerGc } },
            report with { MeasuredConsumed = 31 }, report with { FinalColumn = 64020 }, report with { AllocatedBytes = 1 },
            report with { Actual = actual with { NativeGcSha256 = "wrong" } }, report with { Actual = actual with { GcConfiguration = "{}" } },
            report with { Actual = actual with { TestVersion = "1.0.0+" + new string('0', 40) } },
            report with { Actual = actual with { ProductVersion = "1.0.0+" + new string('0', 40) } },
            report with { DiagnosticCode = "XML.Syntax" }, report with { EscapedObjectLength = 37 } })
        {
            Assert.Throws<InvalidOperationException>(() => FramingAllocationChild.ParseAndValidateReport(JsonSerializer.Serialize(invalid), request, Environment.ProcessId, actual));
        }
    }
}
