using System.Text.Json;
using Xemele;
using Xemele.Validation;
using Schema = Xemele.Validation.XmlSchema;

var root = Path.GetFullPath("tests/fixtures/M8-xsd-type-relations");
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
var observations = new List<object>();
int failures = 0;
foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray())
{
    string path = Path.Combine(root, item.GetProperty("schemas")[0].GetString()!);
    bool expected = item.GetProperty("expected").GetString() == "valid";
    bool instance = item.GetProperty("kind").GetString() == "instance";
    string? source = instance ? Path.Combine(root, item.GetProperty("instance").GetString()!) : null;
    bool accepted = false, complete = false;
    string[] diagnostics = [];
    try
    {
        using var input = File.OpenRead(path);
        var schema = Schema.Compile(input, new()
        {
            BaseUri = new Uri(path),
            Resolver = new FixtureResolver(root)
        });
        if (source is null)
        {
            accepted = true;
            complete = true;
        }
        else
        {
            using var xml = File.OpenRead(source);
            var result = schema.Validate(xml);
            accepted = result.IsValid;
            complete = result.IsComplete;
            diagnostics = result.Diagnostics.Select(d => d.Code + ":" + d.Message).ToArray();
        }
    }
    catch (SchemaCompileException error) { diagnostics = error.Diagnostics.Select(d => d.Code + ":" + d.Message).ToArray(); }
    bool oracle = false;
    string? oracleError = null;
    try
    {
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = new LocalResolver(root) };
        foreach (var schema in item.GetProperty("schemas").EnumerateArray())
            schemas.Add(null, Path.Combine(root, schema.GetString()!));
        schemas.Compile();
        if (source is not null)
        {
            var settings = new System.Xml.XmlReaderSettings { Schemas = schemas, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
            using var reader = System.Xml.XmlReader.Create(source, settings);
            while (reader.Read())
            {
            }
        }
        oracle = true;
    }
    catch (System.Xml.Schema.XmlSchemaException error) { oracleError = error.Message; }
    observations.Add(new
    {
        group = item.GetProperty("group").GetString(),
        id = item.GetProperty("id").GetString(),
        kind = instance ? "instance" : "schema",
        expected,
        accepted,
        complete,
        diagnostics,
        oracle,
        oracleError
    });
    // Schema rejection is a semantic failure only with a language diagnostic;
    // instance failure requires completion, never a quota/resource refusal.
    bool semantic = !accepted && diagnostics.Length > 0 && diagnostics.All(d => !d.StartsWith("SCHEMA.", StringComparison.Ordinal));
    if (accepted != expected || instance && !complete || !instance && !expected && !semantic)
        failures++;
}
Console.WriteLine(JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
if (!args.Contains("--observe") && failures != 0) throw new InvalidOperationException($"{failures} original outcomes differ from metadata.");

sealed class FixtureResolver(string root) : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = request.Uri.LocalPath;
        if (!request.Uri.IsFile || !Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;
        return new(File.OpenRead(path), request.Uri);
    }
}
sealed class LocalResolver(string root) : System.Xml.XmlResolver
{
    public override System.Net.ICredentials? Credentials
    {
        set
        {
        }
    }
    public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (!absoluteUri.IsFile || !Path.GetFullPath(absoluteUri.LocalPath).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new System.Xml.XmlException("Fixture resolver denied outside resource.");
        return File.OpenRead(absoluteUri.LocalPath);
    }
}
