using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xemele.CorpusInfrastructure;

internal sealed record CorpusFixture(string Path, string Sha256, string Classification, string? Exclusion);

internal sealed record CorpusManifest(
    string Id, string SourceUrl, string Revision, string Sha256, string License,
    string Notice, string AdapterVersion, string Profile, string? SourcePath,
    string Directory, IReadOnlyList<CorpusFixture> Fixtures)
{
    internal static IReadOnlyList<CorpusManifest> Discover(string repository)
    {
        var directory = ContainedPath.Resolve(repository, System.IO.Path.Combine(repository, "tests", "corpora"));
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        var manifests = new List<CorpusManifest>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(ContainedPath.Comparer);
        foreach (var module in System.IO.Directory.GetDirectories(directory).Order(StringComparer.Ordinal))
        {
            var path = System.IO.Path.Combine(module, "manifest.json");
            if (!File.Exists(path))
            {
                continue;
            }

            var manifest = Load(repository, path);
            if (!ids.Add(manifest.Id))
            {
                throw new InvalidDataException("Duplicate corpus id.");
            }

            foreach (var fixture in manifest.Fixtures)
            {
                if (!paths.Add(ContainedPath.Relative(repository, manifest.Directory, fixture.Path)))
                {
                    throw new InvalidDataException("Duplicate fixture path across manifests.");
                }
            }

            manifests.Add(manifest);
        }
        return manifests;
    }

    internal static CorpusManifest Load(string repository, string path)
    {
        path = ContainedPath.Resolve(repository, path);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        JsonRules.Validate(root);
        JsonRules.Version(root);
        var id = JsonRules.Text(root, "id");
        var url = JsonRules.Text(root, "sourceUrl");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("sourceUrl must be HTTP(S).");
        }

        var revision = JsonRules.Text(root, "revision");
        if (revision.ToLowerInvariant() is "main" or "master" or "latest" or "head")
        {
            throw new InvalidDataException("Source revision must be immutable.");
        }

        var directory = System.IO.Path.GetDirectoryName(path)!;
        var fixtures = new List<CorpusFixture>();
        var paths = new HashSet<string>(ContainedPath.Comparer);
        if (!root.TryGetProperty("fixtures", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Manifest requires fixtures array.");
        }

        foreach (var item in entries.EnumerateArray())
        {
            var relative = JsonRules.Text(item, "path");
            var actual = ContainedPath.Relative(repository, directory, relative);
            if (!paths.Add(actual))
            {
                throw new InvalidDataException("Duplicate fixture path.");
            }

            var classification = JsonRules.Text(item, "classification");
            if (classification is not ("accepted" or "rejected"))
            {
                throw new InvalidDataException("Fixture classification must be accepted or rejected.");
            }

            fixtures.Add(new(relative, JsonRules.Hash(item, "sha256"), classification,
                item.TryGetProperty("exclusion", out _) ? JsonRules.Text(item, "exclusion") : null));
        }
        if (fixtures.Count == 0)
        {
            throw new InvalidDataException("Empty fixture manifest.");
        }

        var source = root.TryGetProperty("sourcePath", out _) ? JsonRules.Text(root, "sourcePath") : null;
        if (source is not null)
        {
            ContainedPath.Relative(repository, directory, source);
        }

        return new(id, url, revision, JsonRules.Hash(root, "sha256"),
            JsonRules.Text(root, "license"), JsonRules.Text(root, "notice"),
            JsonRules.Text(root, "adapterVersion"), JsonRules.Text(root, "profile"), source, directory, fixtures);
    }
}

internal static partial class JsonRules
{
    internal static void Validate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate JSON field: {property.Name}");
                }

                Validate(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Validate(item);
            }
        }
    }

    internal static void Version(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var number) || number != 1)
        {
            throw new InvalidDataException("Unsupported schemaVersion; expected 1.");
        }
    }

    internal static string Text(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Missing metadata: {name}");
        }

        return value.GetString()!;
    }

    internal static string Hash(JsonElement root, string name)
    {
        var value = Text(root, name);
        if (!Sha256Pattern().IsMatch(value))
        {
            throw new InvalidDataException($"Invalid SHA-256: {name}");
        }

        return value;
    }

    [GeneratedRegex("\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}

internal static class ContainedPath
{
    internal static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static string Relative(string root, string directory, string path)
    {
        if (System.IO.Path.IsPathRooted(path))
        {
            throw new InvalidDataException("Expected relative path.");
        }

        return Resolve(root, System.IO.Path.Combine(directory, path));
    }

    internal static string Resolve(string root, string path)
    {
        root = Physical(System.IO.Path.GetFullPath(root));
        path = Physical(System.IO.Path.GetFullPath(path));
        var relative = System.IO.Path.GetRelativePath(root, path);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Path escapes repository.");
        }

        return path;
    }

    private static string Physical(string path)
    {
        var current = System.IO.Path.GetPathRoot(path)!;
        foreach (var part in path[current.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, part);
            FileSystemInfo entry = System.IO.Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is not null)
            {
                current = entry.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Unresolvable symbolic link.");
            }
        }
        return current;
    }
}
