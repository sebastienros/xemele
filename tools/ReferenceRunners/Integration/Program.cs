using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xemele.TestUtilities.References;

if (args.Length != 4) throw new ArgumentException("Expected repository, dotnet executable, report directory and source revision.");
string repository = Path.GetFullPath(args[0]), dotnet = args[1], report = Path.GetFullPath(args[2]), revision = args[3];
Directory.CreateDirectory(report);
string bridge = Path.Combine(repository, "artifacts/M0-references-libxml2/native-build/libxemele_libxml2.dylib");
Environment.SetEnvironmentVariable("XEMELE_LIBXML2_BRIDGE", bridge);
string configuration = ReferenceProcess.BuildConfiguration(AppContext.BaseDirectory);
EngineProcess[] engines = [
    new("System.Xml", dotnet, [Path.Combine(repository, $"tools/ReferenceRunners/Bcl/bin/{configuration}/net10.0/BclReference.dll")]),
    new("libxml2", dotnet, [Path.Combine(repository, $"tools/ReferenceRunners/Libxml2/bin/{configuration}/net10.0/Libxml2Reference.dll")]),
    new("xmloxide", Path.Combine(repository, "artifacts/M0-references-xmloxide/target/release/xemele-xmloxide-reference"), [])
];
using var wire = new StreamWriter(Path.Combine(report, "managed-responses.jsonl"), false, new UTF8Encoding(false));
var checks = new List<string>();
int responses = 0;
const string known = "<p:r xmlns:p='urn:p' z='2' a='1'>a\r\nb<![CDATA[c]]>&amp;<p:e/></p:r>";
const string expected = """
[{"kind":"start","namespaceUri":"urn:p","localName":"r","attributes":[{"namespaceUri":"","localName":"a","value":"1"},{"namespaceUri":"","localName":"z","value":"2"}]},{"kind":"text","value":"a\nbc&"},{"kind":"start","namespaceUri":"urn:p","localName":"e","attributes":[]},{"kind":"end","namespaceUri":"urn:p","localName":"e"},{"kind":"end","namespaceUri":"urn:p","localName":"r"}]
""";
foreach (var engine in engines)
{
    var result = await Run(engine, "known", known);
    Outcome(result, "success");
    EqualEvents(result, expected);
    var metadata = result.GetProperty("engine");
    Require(metadata.GetProperty("name").GetString() == engine.Name, "Actual engine name");
    string build = metadata.GetProperty("build").GetString()!;
    Require(build.Contains(revision, StringComparison.Ordinal), "Actual runner source revision");
    if (engine.Name == "libxml2")
    {
        Require(metadata.GetProperty("version").GetString() == "2.15.4", "Pinned libxml2 version");
        using var provenance = JsonDocument.Parse(build[(build.IndexOf(';') + 1)..]);
        Require(provenance.RootElement.GetProperty("commit").GetString() == "96498992efa48d52b0e8b83058bd88dbdaf153c1", "Pinned libxml2 commit");
        using var actual = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(bridge)!, "provenance.json")));
        Require(JsonNode.DeepEquals(JsonNode.Parse(provenance.RootElement.GetRawText()), JsonNode.Parse(actual.RootElement.GetRawText())), "Actual libxml2 build provenance");
    }
    else if (engine.Name == "xmloxide")
    {
        Require(metadata.GetProperty("version").GetString() == "0.5.0+555055f1ea6bf9944488fd96415b6f03b5b43696", "Pinned xmloxide commit/version");
        Require(metadata.GetProperty("runtime").GetString()!.Contains("rustc 1.95.0", StringComparison.Ordinal), "Pinned Rust toolchain");
    }
    else
    {
        Require(metadata.GetProperty("runtime").GetString() == System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, "Actual BCL runtime");
        Require(metadata.GetProperty("version").GetString() == typeof(System.Xml.XmlReader).Assembly.GetName().Version!.ToString(), "Actual BCL version");
    }
    Record(engine.Name + ": independent known events and actual metadata");
    Outcome(await Run(engine, "invalid-xml", "<r>"), "parse-rejected", "xml");
    foreach (var mutation in new (string Name, Action<JsonObject> Change)[] {
        ("version", r => r["protocolVersion"] = 2), ("operation", r => r["operation"] = "unknown"),
        ("base64", r => r["inputBase64"] = "invalid!"), ("options", r => r["options"] = new JsonObject { ["unknown"] = true }),
        ("profile", r => r["profile"] = "xml-1.1"), ("hint", r => r["encodingHint"] = "latin1"),
        ("relative-resource", r => r["offlineResources"] = new JsonObject { ["relative"] = "AA==" }) })
        Outcome(await Run(engine, "invalid-" + mutation.Name, "<r/>", mutation.Change), "runner-failure", "protocol");
    Record(engine.Name + ": XML rejection distinct from seven invalid requests");
    foreach (string uri in new[] { "https://example.invalid/entity.xml", new Uri(Path.Combine(repository, "global.json")).AbsoluteUri })
    {
        string xml = "<!DOCTYPE r [<!ENTITY e SYSTEM '" + uri + "'>]><r>&e;</r>";
        var allowed = await Run(engine, "mapped-" + new Uri(uri).Scheme, xml, r => r["offlineResources"] = new JsonObject { [uri] = Bytes("offline") });
        Outcome(allowed, "success");
        Require(allowed.GetProperty("events")[1].GetProperty("value").GetString() == "offline", "Mapped bytes used");
        Outcome(await Run(engine, "denied-" + new Uri(uri).Scheme, xml), "runner-failure", "resource-denied");
    }
    Record(engine.Name + ": actual file/network URI map and denial");
    var envelope = await Run(engine, "document-whitespace", " \n<r/>\n");
    if (engine.Name == "System.Xml")
    {
        Outcome(envelope, "success");
        Require(envelope.GetProperty("events")[0].GetProperty("value").GetString() == " \n", "Independent leading whitespace");
        Require(envelope.GetProperty("events")[3].GetProperty("value").GetString() == "\n", "Independent trailing whitespace");
    }
    else
    {
        Outcome(envelope, "runner-failure", "protocol");
        Require(envelope.GetProperty("diagnostics")[0].GetProperty("message").GetString()!.Contains("normalization", StringComparison.OrdinalIgnoreCase), "Explicit normalization capability");
    }
    string dtdUri = "https://example.invalid/root.dtd";
    var dtd = await Run(engine, "external-dtd", "<!DOCTYPE r SYSTEM '" + dtdUri + "'><r>&hello;</r>",
        r => r["offlineResources"] = new JsonObject { [dtdUri] = Bytes("<!ENTITY hello 'offline'>") });
    if (engine.Name == "xmloxide") Outcome(dtd, "runner-failure", "protocol");
    else { Outcome(dtd, "success"); Require(dtd.GetProperty("events")[1].GetProperty("value").GetString() == "offline", "External DTD bytes used"); }
    var crlf = await Run(engine, "literal-crlf", "<r a='x\r\ny'><![CDATA[x\r\ny]]></r>");
    Outcome(crlf, "success");
    Require(crlf.GetProperty("events")[0].GetProperty("attributes")[0].GetProperty("value").GetString() == (engine.Name == "xmloxide" ? "x  y" : "x y"), "Preserved actual attribute newline behavior");
    Require(crlf.GetProperty("events")[1].GetProperty("value").GetString() == (engine.Name == "xmloxide" ? "x\r\ny" : "x\ny"), "Preserved actual CDATA newline behavior");
    var iri = await Run(engine, "namespace-iri", "<r xmlns='urn:é'/>");
    Outcome(iri, engine.Name == "libxml2" ? "parse-rejected" : "success");
    Record(engine.Name + ": explicit capabilities and genuine newline/namespace differences");
}
string fixture = Path.Combine(repository, "docs/reports/M0-references-protocol/child_fixture.py");
foreach (string mode in new[] { "exit", "invalid", "invalid-utf8-json-string", "identity", "extra-line", "unterminated", "flood", "stderr-flood", "event-number", "failure-events" })
{
    await Failure(() => ReferenceProcess.ExecuteAsync("python3", [fixture, mode], Request("process-" + mode, "<r/>").ToJsonString(), TimeSpan.FromSeconds(5)), mode);
}
Record("Ten malformed/exit/overflow child modes remain infrastructure failures");
foreach (bool cancellation in new[] { false, true })
{
    string pidFile = Path.GetTempFileName();
    try
    {
        using var cancel = new CancellationTokenSource();
        if (cancellation) cancel.CancelAfter(TimeSpan.FromSeconds(1));
        var task = ReferenceProcess.ExecuteAsync("python3", [fixture, "sleep", pidFile], Request("cleanup", "<r/>").ToJsonString(),
            TimeSpan.FromSeconds(cancellation ? 10 : 1), cancel.Token);
        if (cancellation)
        {
            try { await task; throw new InvalidOperationException("Cancellation expected"); }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        }
        else await Failure(() => task, "timeout", "timed out");
        int pid = int.Parse(await File.ReadAllTextAsync(pidFile), System.Globalization.CultureInfo.InvariantCulture);
        try { using var child = Process.GetProcessById(pid); throw new InvalidOperationException("Child survived cleanup"); }
        catch (ArgumentException) { }
        Record(cancellation ? "Cancellation propagates after child reaped" : "Timeout is infrastructure failure after child reaped");
    }
    finally { File.Delete(pidFile); }
}
Record($"PASS: {responses} real engine responses, 12 adversarial process checks; source {revision}; {System.Runtime.InteropServices.RuntimeInformation.OSDescription}; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
await File.WriteAllLinesAsync(Path.Combine(report, "managed-checks.txt"), checks);

async Task<JsonElement> Run(EngineProcess engine, string id, string xml, Action<JsonObject>? change = null)
{
    var request = Request(id, xml);
    change?.Invoke(request);
    string response = await ReferenceProcess.ExecuteAsync(engine.Executable, engine.Arguments, request.ToJsonString(), TimeSpan.FromSeconds(15));
    await wire.WriteLineAsync(response);
    await wire.FlushAsync();
    responses++;
    using var document = JsonDocument.Parse(response);
    return document.RootElement.Clone();
}
static JsonObject Request(string id, string xml) => new() { ["protocolVersion"] = 1, ["caseId"] = id, ["operation"] = "xml.events", ["profile"] = "xml-1.0", ["inputBase64"] = Bytes(xml) };
static string Bytes(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Outcome(JsonElement response, string expectedOutcome, string? category = null)
{
    Require(response.GetProperty("outcome").GetString() == expectedOutcome, response.GetProperty("engine").GetProperty("name") + " " + response.GetProperty("caseId") + ": expected " + expectedOutcome + "; " + response);
    if (expectedOutcome != "success") Require(!response.TryGetProperty("events", out _), "Failure events omitted");
    if (category is not null) Require(response.GetProperty("diagnostics").EnumerateArray().Any(d => d.GetProperty("category").GetString() == category), "Expected diagnostic " + category);
}
static void EqualEvents(JsonElement response, string expectedEvents) => Require(JsonNode.DeepEquals(JsonNode.Parse(response.GetProperty("events").GetRawText()), JsonNode.Parse(expectedEvents)), "Independent expected events differ: " + response);
static async Task Failure(Func<Task<string>> action, string mode, string? message = null)
{
    try { await action(); throw new InvalidOperationException("Infrastructure failure expected: " + mode); }
    catch (ReferenceFailureException failure) { if (message is not null) Require(failure.Message.Contains(message, StringComparison.Ordinal), "Expected " + message); }
}
void Record(string message) { checks.Add(message); Console.WriteLine(message); }
sealed record EngineProcess(string Name, string Executable, string[] Arguments);
