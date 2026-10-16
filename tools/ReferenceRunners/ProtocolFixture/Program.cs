using System.Text;
using System.Text.Json.Nodes;

var request = JsonNode.Parse(Console.ReadLine() ?? throw new InvalidDataException("Missing fixture request."))!;
if (args.Length > 1)
{
    File.WriteAllText(args[1], Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
var mode = args[0];
switch (mode)
{
    case "exit":
        Console.Error.Write("intentional child failure");
        return 7;
    case "sleep":
        Thread.Sleep(30000);
        return 0;
    case "flood":
        Console.Write(new string('x', 1100000));
        Console.Out.Flush();
        Thread.Sleep(30000);
        return 0;
    case "stderr-flood":
        Console.Error.Write(new string('x', 1100000));
        Console.Error.Flush();
        Thread.Sleep(30000);
        return 0;
    case "invalid":
        Console.WriteLine("invalid JSON");
        return 0;
    case "invalid-utf8":
        Console.OpenStandardOutput().Write([0xff, 0x0a]);
        return 0;
    case "extra-line":
        Console.Write("{}\n\n");
        return 0;
    case "unterminated":
        Console.Write("{}");
        return 0;
    case "identity":
        Console.WriteLine("""{"protocolVersion":1,"caseId":"wrong","operation":"xml.events","outcome":"success"}""");
        return 0;
}
var start = new JsonObject { ["kind"] = "start", ["namespaceUri"] = "", ["localName"] = "r", ["attributes"] = new JsonArray() };
var end = new JsonObject { ["kind"] = "end", ["namespaceUri"] = "", ["localName"] = "r" };
var text = new JsonObject { ["kind"] = "text", ["value"] = "" };
var attribute = new JsonObject { ["namespaceUri"] = "", ["localName"] = "a", ["value"] = "" };
var events = new JsonArray();
var response = new JsonObject
{
    ["protocolVersion"] = 1,
    ["caseId"] = request["caseId"]!.DeepClone(),
    ["operation"] = request["operation"]!.DeepClone(),
    ["engine"] = new JsonObject { ["name"] = "fixture", ["version"] = "1", ["runtime"] = ".NET", ["build"] = "test" },
    ["outcome"] = "success",
    ["diagnostics"] = new JsonArray(),
    ["events"] = events
};
switch (mode)
{
    case "version-overflow":
        response["protocolVersion"] = 2147483648L;
        break;
    case "version-exponent":
        response["protocolVersion"] = 1e100;
        break;
    case "diagnostic-number":
        response["diagnostics"] = new JsonArray(42);
        break;
    case "diagnostic-missing-category":
        response["diagnostics"] = new JsonArray(new JsonObject { ["message"] = "bad" });
        break;
    case "diagnostic-message-number":
        response["diagnostics"] = new JsonArray(new JsonObject { ["category"] = "xml", ["message"] = 42 });
        break;
    case "diagnostic-category-null":
        response["diagnostics"] = new JsonArray(new JsonObject { ["category"] = null, ["message"] = "bad" });
        break;
    case "event-number":
        events.Add(42);
        break;
    case "start-missing-names":
        events.Add(new JsonObject { ["kind"] = "start" });
        break;
    case "start-namespace-null":
        start["namespaceUri"] = null;
        events.Add(start);
        break;
    case "start-name-number":
        start["localName"] = 42;
        events.Add(start);
        break;
    case "start-attributes-missing":
        start.Remove("attributes");
        events.Add(start);
        break;
    case "attribute-number":
        start["attributes"] = new JsonArray(42);
        events.Add(start);
        break;
    case "attribute-missing-value":
        attribute.Remove("value");
        start["attributes"] = new JsonArray(attribute);
        events.Add(start);
        break;
    case "attribute-value-number":
        attribute["value"] = 42;
        start["attributes"] = new JsonArray(attribute);
        events.Add(start);
        break;
    case "text-missing-value":
        text.Remove("value");
        events.Add(text);
        break;
    case "text-value-number":
        text["value"] = 42;
        events.Add(text);
        break;
    case "end-missing-namespace":
        end.Remove("namespaceUri");
        events.Add(end);
        break;
    case "end-attributes":
        end["attributes"] = new JsonArray();
        events.Add(end);
        break;
    case "start-value":
        start["value"] = "bad";
        events.Add(start);
        break;
    case "text-namespace":
        text["namespaceUri"] = "";
        events.Add(text);
        break;
    case "unknown-kind":
        events.Add(new JsonObject { ["kind"] = "unknown" });
        break;
    case "failure-events":
        response["outcome"] = "parse-rejected";
        break;
    case "additive-fields":
        response["future"] = new JsonObject { ["optional"] = true };
        response["diagnostics"] = new JsonArray(new JsonObject { ["category"] = "note", ["message"] = "", ["future"] = 42 });
        start["future"] = true;
        attribute["future"] = 42;
        start["attributes"] = new JsonArray(attribute);
        text["future"] = 42;
        end["future"] = 42;
        events.Add(start);
        events.Add(text);
        events.Add(end);
        break;
    case "custom-payload":
        response.Remove("events");
        response["fixtureValue"] = "checked";
        break;
    case "custom-invalid":
        response.Remove("events");
        response["fixtureValue"] = 42;
        break;
    case "invalid-utf8-json-string":
        text["value"] = "invalid-marker";
        events.Add(text);
        break;
    default:
        throw new ArgumentException("Unknown child mode: " + mode);
}
var json = response.ToJsonString();
if (mode == "invalid-utf8-json-string")
{
    var marker = json.IndexOf("invalid-marker", StringComparison.Ordinal);
    using var output = Console.OpenStandardOutput();
    output.Write(Encoding.UTF8.GetBytes(json[..marker]));
    output.WriteByte(0xff);
    output.Write(Encoding.UTF8.GetBytes(json[(marker + "invalid-marker".Length)..] + "\n"));
}
else
{
    Console.WriteLine(json);
}
return 0;
