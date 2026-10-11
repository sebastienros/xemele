using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
namespace Xemele.Conformance.Tests.Dtd;
public sealed class ApplicabilityTests
{
    internal static string Root {get {var p=new DirectoryInfo(AppContext.BaseDirectory);while(p is not null&&!File.Exists(Path.Combine(p.FullName,"global.json")))p=p.Parent;return p?.FullName??throw new InvalidOperationException("Repo root");}}
    [Fact]
    public void FrozenValidatingLedgerRetainsAllOriginalCasesAndResourceHashes()
    {
        string root=Root;using var prior=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"tests/corpora/xml/m2-applicability.json")));
        using var frozen=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"tests/corpora/dtd/applicability.json")));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"tests/corpora/xml/m2-applicability.json")))),frozen.RootElement.GetProperty("priorLedgerSha256").GetString());
        Assert.Equal(prior.RootElement.GetProperty("files").GetRawText(),frozen.RootElement.GetProperty("files").GetRawText());
        var originals=prior.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c=>c.GetProperty("id").GetString()!);
        var rows=frozen.RootElement.GetProperty("cases").EnumerateArray().ToArray();Assert.Equal(2585,rows.Length);Assert.Equal(3386,frozen.RootElement.GetProperty("files").GetArrayLength());
        Assert.Equal(976,rows.Count(c=>c.GetProperty("scope").GetString()=="associated-dtd"));
        foreach(var c in rows) {var o=originals[c.GetProperty("id").GetString()!];Assert.Equal(o.GetProperty("inputSha256").GetString(),c.GetProperty("inputSha256").GetString());Assert.NotEmpty(c.GetProperty("expected").EnumerateArray());}
    }
}
