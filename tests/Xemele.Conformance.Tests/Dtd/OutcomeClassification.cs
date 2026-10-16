namespace Xemele.Conformance.Tests.Dtd;

internal static class OutcomeClassification
{
    internal static string ParseException(IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            if (code is "XML.ResourceDenied" or "XML.ResourceCycle" or "DTD.ExternalInput")
            {
                return "infrastructure";
            }

            if (code.Contains("Limit", StringComparison.Ordinal) || code.Contains("Incomplete", StringComparison.Ordinal))
            {
                return "incomplete";
            }
        }
        return "rejected";
    }
    internal static bool Pass(string? scope, IReadOnlyList<string> expected, string outcome) =>
        expected.Contains(outcome) || scope == "associated-dtd" && expected.Contains("invalid") && outcome == "rejected";
}
