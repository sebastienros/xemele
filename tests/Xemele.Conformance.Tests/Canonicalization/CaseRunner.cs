using System.Text;
using System.Text.Json;
namespace Xemele.Conformance.Tests.Canonicalization;

internal sealed record ReferenceObservation(int Mode, bool Pass, string? Error);
internal sealed record CanonicalObservation(string Id, int Mode, bool Comments, bool Pass, string? Error, string? ActualBase64, IReadOnlyList<ReferenceObservation> References);
internal static class CaseRunner
{
    internal static CanonicalObservation Run(JsonElement c, Func<XmlDocument> parse, Func<XmlDocument, int, byte[]> product, Func<int, byte[]> reference)
    {
        var id = c.GetProperty("id").GetString()!;
        var mode = c.GetProperty("mode").GetInt32();
        var comments = c.TryGetProperty("comments", out var cv) && cv.GetBoolean();
        var phase = "parse";
        byte[]? actual = null;
        var references = new List<ReferenceObservation>();
        var pass = false;
        string? error = null;
        try
        {
            var document = parse();
            phase = "product";
            actual = product(document, mode);
            pass = actual.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(c.GetProperty("expected").GetString()!));
            phase = "reference";
            foreach (var entry in c.GetProperty("referenceModes").EnumerateArray())
            {
                var referenceMode = entry.GetInt32();
                try
                {
                    // Prefix lists are exclusive algorithm parameters. Cross-mode
                    // comparison uses the same mathematical input with that mode's options.
                    var compared = referenceMode == mode ? actual : product(document, referenceMode);
                    references.Add(new(referenceMode, compared.AsSpan().SequenceEqual(reference(referenceMode)), null));
                }
                catch (Exception e) { references.Add(new(referenceMode, false, e.GetType().Name + ":" + e.Message)); }
            }
        }
        catch (Exception e) { error = phase + ":" + e.GetType().Name + ":" + e.Message; }
        return new(id, mode, comments, pass && error is null && references.All(r => r.Pass), error, actual is null ? null : Convert.ToBase64String(actual), references);
    }
}
