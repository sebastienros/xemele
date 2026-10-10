using System.Security.Cryptography;
using System.Text.Json;

namespace Xemele.CorpusInfrastructure;

internal enum ProvisionOutcome { Provisioned, MissingCache, CorruptCache, UnsupportedProfile, Excluded, InfrastructureFailure }

internal sealed record ProvisionCase(string Path, string Classification, ProvisionOutcome Outcome, string Detail, string? CachePath);

internal sealed record ProvisionReport(string CorpusId, string Profile, string SourceUrl, string Revision,
    string SourceSha256, string License, string Notice, string AdapterVersion, IReadOnlyList<ProvisionCase> Cases)
{
    public int Total => Cases.Count;
    public int Provisioned => Count(ProvisionOutcome.Provisioned);
    public int MissingCache => Count(ProvisionOutcome.MissingCache);
    public int CorruptCache => Count(ProvisionOutcome.CorruptCache);
    public int UnsupportedProfile => Count(ProvisionOutcome.UnsupportedProfile);
    public int Excluded => Count(ProvisionOutcome.Excluded);
    public int InfrastructureFailure => Count(ProvisionOutcome.InfrastructureFailure);
    public int ParserCasesExecuted => 0;
    public int StandardsPassed => 0;
    public bool Successful => Cases.All(c => c.Outcome == ProvisionOutcome.Provisioned);
    private int Count(ProvisionOutcome outcome) => Cases.Count(c => c.Outcome == outcome);
}

// Offline infrastructure only. No parser is invoked and no conformance result is inferred.
internal sealed class CorpusProvisioner(string repository, string cacheDirectory)
{
    internal string SourceCachePath(CorpusManifest manifest) =>
        CachePath("sources", manifest.Sha256 + ".json");

    internal void ImportSource(CorpusManifest manifest)
    {
        if (manifest.SourcePath is null) throw new InvalidDataException("Offline import requires sourcePath.");
        var source = ContainedPath.Relative(repository, manifest.Directory, manifest.SourcePath);
        var bytes = File.ReadAllBytes(source);
        Verify(bytes, manifest.Sha256);
        ReadBundle(bytes, manifest); // Validate adapter and all fixtures before caching.
        WriteVerified(SourceCachePath(manifest), bytes, manifest.Sha256);
    }

    internal ProvisionReport Provision(CorpusManifest manifest, IReadOnlySet<string> supportedProfiles)
    {
        var cases = new List<ProvisionCase>();
        Dictionary<string, byte[]>? bundle = null;
        ProvisionOutcome? failure = null;
        string detail = "";
        if (supportedProfiles.Contains(manifest.Profile) && manifest.Fixtures.Any(f => f.Exclusion is null))
        {
            try
            {
                var source = SourceCachePath(manifest);
                if (Directory.Exists(source)) throw new IOException("Source cache payload is a directory, not a regular file.");
                if (!File.Exists(source))
                {
                    failure = ProvisionOutcome.MissingCache;
                    detail = "Source payload is absent; offline provisioning performs no download.";
                }
                else
                {
                    var bytes = File.ReadAllBytes(source);
                    Verify(bytes, manifest.Sha256);
                    bundle = ReadBundle(bytes, manifest);
                }
            }
            catch (InvalidDataException error) { failure = ProvisionOutcome.CorruptCache; detail = error.Message; }
            catch (JsonException error) { failure = ProvisionOutcome.CorruptCache; detail = error.Message; }
            catch (FormatException error) { failure = ProvisionOutcome.CorruptCache; detail = error.Message; }
            catch (Exception error) when (IsInfrastructure(error)) { failure = ProvisionOutcome.InfrastructureFailure; detail = error.Message; }
        }
        foreach (var fixture in manifest.Fixtures)
        {
            if (fixture.Exclusion is not null)
                cases.Add(new(fixture.Path, fixture.Classification, ProvisionOutcome.Excluded, fixture.Exclusion, null));
            else if (!supportedProfiles.Contains(manifest.Profile))
                cases.Add(new(fixture.Path, fixture.Classification, ProvisionOutcome.UnsupportedProfile, "Profile is not supported by the requested adapter.", null));
            else if (failure is not null)
                cases.Add(new(fixture.Path, fixture.Classification, failure.Value, detail, null));
            else
            {
                try
                {
                    var bytes = bundle![fixture.Path];
                    var target = CachePath("fixtures", manifest.Sha256, fixture.Sha256);
                    WriteVerified(target, bytes, fixture.Sha256);
                    cases.Add(new(fixture.Path, fixture.Classification, ProvisionOutcome.Provisioned, "Source and fixture SHA-256 verified; parser not executed.", target));
                }
                catch (Exception error) when (IsInfrastructure(error) || error is InvalidDataException)
                {
                    cases.Add(new(fixture.Path, fixture.Classification, ProvisionOutcome.InfrastructureFailure, error.Message, null));
                }
            }
        }
        return new(manifest.Id, manifest.Profile, manifest.SourceUrl, manifest.Revision,
            manifest.Sha256, manifest.License, manifest.Notice, manifest.AdapterVersion, cases);
    }

    private string CachePath(params string[] parts)
    {
        var cache = ContainedPath.Resolve(repository, cacheDirectory);
        return ContainedPath.Resolve(repository, Path.Combine([cache, .. parts]));
    }

    private static Dictionary<string, byte[]> ReadBundle(byte[] bytes, CorpusManifest manifest)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        JsonRules.Validate(root);
        JsonRules.Version(root);
        if (manifest.AdapterVersion != "xemele-offline-bundle-v1")
            throw new InvalidDataException("Unsupported source adapterVersion.");
        if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Bundle requires files array.");
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in files.EnumerateArray())
        {
            var path = JsonRules.Text(file, "path");
            if (!file.TryGetProperty("base64", out var encoded) || encoded.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Bundle requires base64 string bytes.");
            var content = Convert.FromBase64String(encoded.GetString()!);
            if (!result.TryAdd(path, content)) throw new InvalidDataException("Duplicate bundle path.");
        }
        if (result.Count != manifest.Fixtures.Count) throw new InvalidDataException("Bundle fixture inventory mismatch.");
        foreach (var fixture in manifest.Fixtures)
        {
            if (!result.TryGetValue(fixture.Path, out var content)) throw new InvalidDataException("Bundle fixture missing.");
            Verify(content, fixture.Sha256);
        }
        return result;
    }

    private static bool IsInfrastructure(Exception error) => error is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    internal static void Verify(byte[] bytes, string expected)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("SHA-256 checksum mismatch.");
    }

    private static void WriteVerified(string path, byte[] bytes, string expected)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            Verify(File.ReadAllBytes(temporary), expected);
            File.Move(temporary, path, overwrite: true);
            Verify(File.ReadAllBytes(path), expected);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
