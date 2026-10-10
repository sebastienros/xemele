using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;

var result = args.Select(path =>
{
    string full = Path.GetFullPath(path);
    var assembly = Assembly.LoadFile(full);
    return new { path = full, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant(),
        sourceVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        runtime = Environment.Version.ToString(), architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        coreLibrary = typeof(object).Assembly.Location,
        coreLibrarySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(object).Assembly.Location))).ToLowerInvariant(),
        serverGc = System.Runtime.GCSettings.IsServerGC, latencyMode = System.Runtime.GCSettings.LatencyMode.ToString() };
});
Console.WriteLine(JsonSerializer.Serialize(result));
