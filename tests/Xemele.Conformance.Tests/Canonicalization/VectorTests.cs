using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
namespace Xemele.Conformance.Tests.Canonicalization;
public sealed class VectorTests
{
    [Fact]
    public void AlgorithmSpecificVectorOptionsSelectionsAndExpectedBytesAreFrozen()
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Dtd.ApplicabilityTests.Root,"tests/corpora/c14n/vectors.json")));
        var rows=vectors.RootElement.EnumerateArray().ToArray();Assert.Equal(32,rows.Length);Assert.Equal(32,rows.Select(r=>r.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(new[]{0,1,2},rows.Select(r=>r.GetProperty("mode").GetInt32()).Distinct().Order().ToArray());
        foreach(var c in rows) Assert.Equal(c.GetProperty("expectedUtf8Sha256").GetString(),Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(c.GetProperty("expected").GetString()!))));
        var erratum=rows.Single(c=>c.GetProperty("id").GetString()=="w3c-c14n11-3.8-E11-01");Assert.Contains("bar/foo",erratum.GetProperty("expected").GetString());Assert.DoesNotContain("something/bar/foo",erratum.GetProperty("expected").GetString());
    }
}
