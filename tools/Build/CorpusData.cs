using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Xemele.CorpusInfrastructure;

namespace Xemele.Build;

internal static class CorpusData
{
    private const string ArchiveHash = "9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f";
    private const string Source = "https://www.w3.org/XML/Test/xmlts20130923.tar.gz";
    private const int MaximumArchiveBytes = 2 * 1024 * 1024;
    internal static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    internal static async Task PrepareAsync(string root)
    {
        var directory = Path.Combine(root, "tests/corpora/xml");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.json")));
        if (manifest.RootElement.GetProperty("sourceUrl").GetString() != Source ||
            !manifest.RootElement.GetProperty("revision").GetString()!.EndsWith("archive-sha256:" + ArchiveHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("XML archive provenance differs from the reviewed pin.");
        }
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "inventory.json")));
        var files = new Dictionary<string, (string Hash, long Bytes)>(StringComparer.Ordinal);
        foreach (var record in inventory.RootElement.GetProperty("files").EnumerateArray())
        {
            var name = record.GetProperty("path").GetString()!;
            if (!name.StartsWith("xmlconf/", StringComparison.Ordinal) || name.Contains('\\') ||
                name.Split('/').Any(part => part is "" or "." or ".."))
            {
                throw new InvalidDataException("Unsafe corpus archive path: " + name);
            }
            ContainedPath.Relative(root, directory, name);
            files.Add(name, (JsonRules.Hash(record, "sha256"), record.GetProperty("bytes").GetInt64()));
        }
        if (files.Count != 3386 || files.Values.Any(file => file.Bytes < 0) || files.Values.Sum(file => file.Bytes) > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Unexpected XML inventory size.");
        }
        if (Verify(root, directory, files))
        {
            Console.WriteLine("Verified 3,386 pinned XML corpus files (cached).");
            return;
        }
        var cache = Path.Combine(root, "artifacts/test-data");
        Directory.CreateDirectory(cache);
        var archive = Path.Combine(cache, ArchiveHash + ".tar.gz");
        if (!File.Exists(archive))
        {
            var temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                using var response = await client.GetAsync(Source, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
                response.EnsureSuccessStatusCode();
                await using (var output = File.Create(temporary))
                await using (var input = await response.Content.ReadAsStreamAsync(cancel.Token))
                {
                    var buffer = new byte[65536];
                    var total = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, cancel.Token)) != 0)
                    {
                        total = checked(total + count);
                        if (total > MaximumArchiveBytes)
                        {
                            throw new InvalidDataException("XML archive exceeds its download limit.");
                        }
                        await output.WriteAsync(buffer.AsMemory(0, count), cancel.Token);
                    }
                }
                if (Hash(temporary) != ArchiveHash)
                {
                    throw new InvalidDataException("Downloaded XML archive SHA-256 mismatch.");
                }
                File.Move(temporary, archive, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        if (Hash(archive) != ArchiveHash)
        {
            throw new InvalidDataException("Cached XML archive SHA-256 mismatch: " + archive);
        }
        using var compressed = File.OpenRead(archive);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        var extracted = new HashSet<string>(StringComparer.Ordinal);
        long expanded = 0;
        int entries = 0;
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            expanded = checked(expanded + entry.Length);
            if (++entries > 10000 || expanded > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("XML archive exceeds its extraction limits.");
            }
            if (!files.TryGetValue(entry.Name, out var expected))
            {
                continue;
            }
            if (!extracted.Add(entry.Name))
            {
                throw new InvalidDataException("Duplicate corpus archive entry: " + entry.Name);
            }
            var bytes = ReadFixture(entry, expected.Bytes, expected.Hash);
            var destination = ContainedPath.Relative(root, directory, entry.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        if (extracted.Count != files.Count || !Verify(root, directory, files))
        {
            throw new InvalidDataException("Incomplete XML corpus archive.");
        }
        Console.WriteLine("Downloaded and verified 3,386 pinned XML corpus files.");
    }

    internal static byte[] ReadFixture(TarEntry entry, long length, string hash)
    {
        if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) ||
            entry.Length != length || entry.DataStream is null && length != 0)
        {
            throw new InvalidDataException("Invalid corpus archive entry: " + entry.Name);
        }
        using var payload = new MemoryStream();
        if (entry.DataStream is not null)
            entry.DataStream.CopyTo(payload);
        var bytes = payload.ToArray();
        if (bytes.LongLength != length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Corpus fixture SHA-256 mismatch: " + entry.Name);
        return bytes;
    }

    private static bool Verify(string root, string directory, Dictionary<string, (string Hash, long Bytes)> files)
    {
        var complete = true;
        foreach (var (name, expected) in files)
        {
            var path = ContainedPath.Relative(root, directory, name);
            if (!File.Exists(path))
            {
                complete = false;
            }
            else if (new FileInfo(path).Length != expected.Bytes || Hash(path) != expected.Hash)
            {
                throw new InvalidDataException("Corrupt corpus fixture: " + path);
            }
        }
        return complete;
    }
}
