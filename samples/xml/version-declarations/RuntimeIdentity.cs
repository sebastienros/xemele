using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

// Probe infrastructure only. No product hot path references this type.
internal static class RuntimePayload
{
    internal static object Capture()
    {
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        Assembly core = typeof(object).Assembly;
        string corePath = Path.GetFullPath(core.Location);
        string directory = Path.GetDirectoryName(corePath)!;
        string process = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown actual host path.");
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            files.Add(Path.GetRelativePath(directory, path), Hash(path));
        if (files.Count == 0 || !files.ContainsKey(Path.GetFileName(corePath)))
            throw new InvalidOperationException("Incomplete selected runtime payload.");
        return new
        {
            runtime = Environment.Version.ToString(),
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processPath = Path.GetFullPath(process),
            processSha256 = Hash(process),
            runtimeDirectory = directory,
            coreLib = new
            {
                path = corePath,
                fullName = core.FullName,
                version = core.GetName().Version?.ToString(),
                fullVersion = core.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                mvid = core.ManifestModule.ModuleVersionId,
                sha256 = Hash(corePath)
            },
            closure = files
        };
    }
}
