using System.Text.Encodings.Web;
using System.Text.Json;
using Xemele.CorpusInfrastructure;

namespace Xemele.Build;

internal static class ApiFragments
{
    internal sealed record Entry(string declaration, string id, string member, string source, int line);
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static List<Entry> Read(string root)
    {
        var paths = Directory.GetFiles(Path.Combine(root, "docs/api"), "*.txt").Order(StringComparer.Ordinal).ToArray();
        var entries = new List<Entry>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            ContainedPath.Resolve(root, path);
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0] != "xemele-api-v1")
                throw new InvalidDataException("Unsupported API fragment format: " + path);
            string? previous = null;
            for (var index = 1; index < lines.Length; index++)
            {
                using var document = JsonDocument.Parse(lines[index]);
                var row = document.RootElement;
                JsonRules.Validate(row);
                var declaration = JsonRules.Text(row, "declaration");
                var id = JsonRules.Text(row, "id");
                var member = JsonRules.Text(row, "member");
                if (row.EnumerateObject().Count() != 3 || new[] { declaration, id, member }.Any(value => value.Contains('\n') || value.Contains('\r')) ||
                    member.Length < 3 || !new[] { "T:", "M:", "P:", "F:", "E:" }.Contains(member[..2]) || member.Any(char.IsWhiteSpace))
                    throw new InvalidDataException("Invalid API fragment entry: " + path);
                var canonical = JsonSerializer.Serialize(new { declaration, id, member }, Json);
                if (lines[index] != canonical)
                    throw new InvalidDataException("Noncanonical API JSON: " + path);
                if (!ids.Add(id) || !members.Add(member) || previous is not null && StringComparer.Ordinal.Compare(previous, id) >= 0)
                    throw new InvalidDataException("Duplicate, conflicting, or unordered API entry: " + id);
                entries.Add(new(declaration, id, member, Path.GetRelativePath(root, path).Replace('\\', '/'), index + 1));
                previous = id;
            }
        }
        if (entries.Count == 0)
            throw new InvalidDataException("Missing or empty API baseline aggregate.");
        return entries.OrderBy(entry => entry.member, StringComparer.Ordinal).ToList();
    }

    internal static string Compose(IEnumerable<Entry> entries) => "xemele-api-v1\n" +
        string.Concat(entries.Select(entry => JsonSerializer.Serialize(new
        {
            entry.declaration,
            id = entry.member,
            entry.member
        }, Json) + "\n"));
}
