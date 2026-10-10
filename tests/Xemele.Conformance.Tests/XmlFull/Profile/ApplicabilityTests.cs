using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Xemele.Conformance.Tests.XmlFull.Profile;

public sealed class ApplicabilityTests
{
    private static string Root
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json"))) path = path.Parent;
            return path?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    [Fact]
    public void EveryOriginalCaseAndResourceHasAnExecutableEffectiveXml10Expectation()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"tests/corpora/xml/inventory.json")));
        using var ledger = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"tests/corpora/xml/m2-applicability.json")));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root,"tests/corpora/xml/inventory.json")))), ledger.RootElement.GetProperty("inventorySha256").GetString());
        Assert.Equal(inventory.RootElement.GetProperty("archiveSha256").GetString(),ledger.RootElement.GetProperty("archiveSha256").GetString());
        var originals = inventory.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c=>c.GetProperty("id").GetString()!,StringComparer.Ordinal);
        var cases = ledger.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(2585,cases.Length); Assert.Equal(2585,cases.Select(c=>c.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var c in cases)
        {
            var original = originals[c.GetProperty("id").GetString()!];
            foreach (string name in new[]{"inputPath","inputSha256"}) Assert.Equal(original.GetProperty(name).GetString(),c.GetProperty(name).GetString());
            var outcomes=c.GetProperty("expectedOutcomes").EnumerateArray().Select(o=>o.GetString()).ToArray();
            Assert.NotEmpty(outcomes); Assert.All(outcomes,o=>Assert.True(o is "accepted" or "rejected"));
            Assert.NotEmpty(c.GetProperty("rationale").GetString()!); Assert.NotEmpty(c.GetProperty("normative").EnumerateArray());
            Assert.NotEqual("unsupported-profile",c.GetProperty("rule").GetString());
            Assert.True(Uri.TryCreate(c.GetProperty("sourceUri").GetString(),UriKind.Absolute,out var source));
            Assert.StartsWith("https://xemele.invalid/corpus/",source!.AbsoluteUri,StringComparison.Ordinal);
        }
        var originalFiles=inventory.RootElement.GetProperty("files").EnumerateArray().ToArray();
        var files=ledger.RootElement.GetProperty("files").EnumerateArray().ToArray(); Assert.Equal(3386,files.Length);
        string resources=Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES")??"/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
        for(int i=0;i<files.Length;i++)
        {
            Assert.Equal(originalFiles[i].GetRawText(),files[i].GetRawText());
            Assert.Equal(files[i].GetProperty("bytes").GetInt32(),Read(resources,files[i].GetProperty("path").GetString()!,files[i].GetProperty("sha256").GetString()!).Length);
        }
    }
    internal static byte[] Read(string root,string relative,string hash)
    {
        if(Path.IsPathRooted(relative)||relative.Split('/','\\').Any(p=>p is ".." or ".")||relative.Contains('\\')) throw new InvalidDataException("Unsafe resource path.");
        root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)) throw new InvalidDataException("Outside root.");
        for(FileSystemInfo? entry=new FileInfo(path);entry is not null;entry=entry is FileInfo f?f.Directory:((DirectoryInfo)entry).Parent)
        {
            if(entry.LinkTarget is not null) throw new InvalidDataException("Symbolic resource.");
            if(entry.FullName==root) break;
        }
        byte[] bytes=File.ReadAllBytes(path);
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=hash) throw new InvalidDataException("Resource hash mismatch.");
        return bytes;
    }
    [Fact]
    public void MissingCorruptAbsoluteTraversalSiblingAndSymlinkAreInfrastructureFailures()
    {
        string root=Path.Combine(Path.GetTempPath(),"m2-profile-"+Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,"input");File.WriteAllText(path,"<r/>");
            string hash=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(4,Read(root,"input",hash).Length);
            Assert.Throws<FileNotFoundException>(()=>Read(root,"missing",hash));
            Assert.Throws<InvalidDataException>(()=>Read(root,"input",new string('0',64)));
            foreach(string invalid in new[]{path,"../input","./input",root+"-sibling/input","a\\input"}) Assert.Throws<InvalidDataException>(()=>Read(root,invalid,hash));
            File.CreateSymbolicLink(Path.Combine(root,"link"),path);
            Assert.Throws<InvalidDataException>(()=>Read(root,"link",hash));
            Directory.CreateSymbolicLink(Path.Combine(root,"linked-root"),root);
            Assert.Throws<InvalidDataException>(()=>Read(Path.Combine(root,"linked-root"),"input",hash));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void HistoricalAndCrossStandardPurposesNeverDisableEffectiveXml10Rules()
    {
        using var ledger=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root,"tests/corpora/xml/m2-applicability.json")));
        var cases=ledger.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c=>c.GetProperty("id").GetString()!);
        var version=cases["xmlconf/eduni/errata-4e/errata4e.xml#x-rmt-008b"];
        Assert.Equal("accepted",Assert.Single(version.GetProperty("expectedOutcomes").EnumerateArray()).GetString());
        Assert.Equal("accepted",Assert.Single(cases["xmlconf/eduni/xml-1.1/xml11.xml#rmt-013"].GetProperty("expectedOutcomes").EnumerateArray()).GetString());
        Assert.Equal("rejected",Assert.Single(cases["xmlconf/eduni/xml-1.1/xml11.xml#rmt-043"].GetProperty("expectedOutcomes").EnumerateArray()).GetString());
        Assert.Equal("rejected",Assert.Single(cases["xmlconf/eduni/namespaces/1.1/rmt-ns11.xml#rmt-ns11-003"].GetProperty("expectedOutcomes").EnumerateArray()).GetString());
    }
}
