using System.Text.Json;
using System.Text.Json.Serialization;
using Xemele.CorpusInfrastructure;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine("Offline corpus infrastructure: import|provision <repository> <profile> [cache-directory]");
    Console.WriteLine("Imports pinned local bundles; provisioning uses only verified cached bytes. No parser execution.");
    return 0;
}
if (args.Length is < 3 or > 4 || args[0] is not ("import" or "provision"))
{
    Console.Error.WriteLine("Expected import|provision <repository> <profile> [cache-directory].");
    return 2;
}
try
{
    var root = Path.GetFullPath(args[1]);
    var cache = args.Length == 4 ? Path.GetFullPath(args[3]) : Path.Combine(root, "artifacts", "corpora");
    var manager = new CorpusProvisioner(root, cache);
    var manifests = CorpusManifest.Discover(root);
    if (args[0] == "import")
        foreach (var manifest in manifests) manager.ImportSource(manifest);
    var reports = manifests.Select(m => manager.Provision(m, new HashSet<string>(StringComparer.Ordinal) { args[2] })).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = 1, corpora = reports.Length, total = reports.Sum(r => r.Total),
        provisioned = reports.Sum(r => r.Provisioned), missingCache = reports.Sum(r => r.MissingCache),
        corruptCache = reports.Sum(r => r.CorruptCache), unsupportedProfile = reports.Sum(r => r.UnsupportedProfile),
        excluded = reports.Sum(r => r.Excluded), infrastructureFailure = reports.Sum(r => r.InfrastructureFailure),
        parserCasesExecuted = 0, standardsPassed = 0, reports
    }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
    return reports.Length > 0 && reports.All(r => r.Successful) ? 0 : 1;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or FormatException)
{
    Console.Error.WriteLine("Corpus infrastructure error: " + error.Message);
    return 1;
}
