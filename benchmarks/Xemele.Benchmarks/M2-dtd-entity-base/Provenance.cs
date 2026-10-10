using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Xemele.Benchmarks;

internal static class EntityBaseProvenance
{
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // PE metadata probing never executes an independently built child during
    // preparation. Actual child Gate below reads its actual loaded assembly paths.
    internal static JsonElement Metadata(string path)
    {
        path = Path.GetFullPath(path);
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        string? version = null;
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (member.Parent.Kind != HandleKind.TypeReference) continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
            if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyInformationalVersionAttribute") continue;
            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) throw new InvalidDataException("Invalid assembly attribute prolog.");
            version = blob.ReadSerializedString();
        }
        return JsonSerializer.SerializeToElement(new { path, informationalVersion = version, mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid), sha256 = Hash(path) });
    }

    internal static SortedDictionary<string, string> Files(string root, bool recursive)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            result.Add(Path.GetRelativePath(root, path), Hash(path));
        return result;
    }

    internal static JsonElement Capture(string directory, string entry, string project, string driver, string product)
        => JsonSerializer.SerializeToElement(new
        {
            directory = Path.GetFullPath(directory), projectDirectory = Path.GetFullPath(project),
            entry = Metadata(entry), driver = Metadata(driver), product = Metadata(product), runtime = Metadata(typeof(object).Assembly.Location),
            closure = Files(directory, true), generatedInputs = Files(project, false)
        });

    internal static void Gate(string stage)
    {
        string? manifest = Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PREPARATION");
        string? approvedHash = Environment.GetEnvironmentVariable("XEMELE_ENTITY_BASE_PREPARATION_SHA256");
        if (manifest is null || approvedHash is null) throw new InvalidOperationException("Approved independent preparation manifest required before workload.");
        string actualHash = Hash(manifest);
        if (actualHash != approvedHash)
        {
            Console.WriteLine("ENTITY_BASE_GATE_FAILURE " + JsonSerializer.Serialize(new { stage, reason = "manifest-hash", expected = approvedHash, actual = actualHash }));
            throw new InvalidDataException("Preparation manifest changed before workload.");
        }
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        var expected = document.RootElement.GetProperty("generated").GetProperty("proof");
        var actual = Capture(AppContext.BaseDirectory, Assembly.GetEntryAssembly()!.Location,
            expected.GetProperty("projectDirectory").GetString()!, typeof(EntityBaseBenchmarks).Assembly.Location, typeof(XmlDocument).Assembly.Location);
        var differences = new List<object>();
        var compiledInputs = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        string repository = document.RootElement.GetProperty("repositoryRoot").GetString()!;
        foreach (var property in document.RootElement.GetProperty("generated").GetProperty("compiledInputs").EnumerateObject())
        {
            string path = Path.IsPathRooted(property.Name) ? property.Name : Path.Combine(repository, property.Name);
            string? hash = File.Exists(path) ? Hash(path) : null;
            compiledInputs.Add(property.Name, hash);
            string wanted = property.Value.GetProperty("sha256").GetString()!;
            if (hash != wanted) differences.Add(new { field = "compiledInput", file = property.Name, expected = wanted, actual = hash });
        }
        foreach (string field in new[] { "directory", "projectDirectory", "entry", "driver", "product", "runtime", "closure", "generatedInputs" })
            if (!Equal(expected.GetProperty(field), actual.GetProperty(field)))
                differences.Add(new { field, expected = expected.GetProperty(field), actual = actual.GetProperty(field) });
        Console.WriteLine("ENTITY_BASE_PROVENANCE " + JsonSerializer.Serialize(new
        {
            stage, process = Environment.ProcessId, manifestSha256 = actualHash,
            gatePassed = differences.Count == 0, actual, compiledInputs, differences
        }));
        if (differences.Count != 0) throw new InvalidDataException("Independent generated child provenance gate failed before workload.");
    }

    private static bool Equal(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        if (left.ValueKind == JsonValueKind.String) return left.GetString() == right.GetString();
        if (left.ValueKind == JsonValueKind.Object)
        {
            var properties = left.EnumerateObject().ToArray();
            return properties.Length == right.EnumerateObject().Count() && properties.All(p => right.TryGetProperty(p.Name, out var value) && Equal(p.Value, value));
        }
        return left.GetRawText() == right.GetRawText();
    }
}
