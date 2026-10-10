using System.Reflection;
using System.Text.Json;
using Xemele;
using Xemele.Benchmarks;

string directory = Path.Combine(Path.GetTempPath(), "xemele-capture-control-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    string survivor = Path.Combine(directory, "survivor.marker"), code = Path.Combine(directory, "fixture.notcs");
    File.WriteAllText(survivor, "owned sibling"); File.WriteAllText(code, "independent generated input");
    string valid = Assembly.GetExecutingAssembly().Location, product = typeof(XmlDocument).Assembly.Location;
    var complete = EntityBaseProvenance.Capture(directory, valid, directory, valid, product);
    if (complete.GetProperty("closure").GetProperty("survivor.marker").GetString() != EntityBaseProvenance.Hash(survivor))
        throw new Exception("Positive complete closure control failed.");
    CheckFailure(Path.Combine(directory, "missing.dll"), typeof(FileNotFoundException));
    string malformed = Path.Combine(directory, "malformed.dll"); File.WriteAllText(malformed, "invalid PE metadata");
    CheckFailure(malformed, typeof(BadImageFormatException));
    Console.WriteLine("Capture controls passed: complete closure; missing required entry; malformed required metadata; actual sibling/input hashes and exact primary metadata errors preserved; no engine/counters/timing.");

    void CheckFailure(string required, Type errorType)
    {
        var original = Console.Out; using var capture = new StringWriter(); Exception? failure = null;
        try
        {
            Console.SetOut(capture);
            try { EntityBaseProvenance.Capture(directory, required, directory, valid, product, "before"); }
            catch (Exception error) { failure = error; }
        }
        finally { Console.SetOut(original); }
        if (failure?.GetType() != errorType) throw new Exception("Exact primary capture error was not preserved.");
        string line = capture.ToString().Trim(); const string prefix = "ENTITY_BASE_PROVENANCE ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal) || line.Contains('\n')) throw new Exception("Exactly one partial provenance record required.");
        using var parsed = JsonDocument.Parse(line[prefix.Length..]); var proof = parsed.RootElement;
        if (proof.GetProperty("stage").GetString() != "before" || proof.GetProperty("captureComplete").GetBoolean() || proof.GetProperty("gatePassed").GetBoolean())
            throw new Exception("Partial capture incorrectly claimed successful gate.");
        var actual = proof.GetProperty("actual");
        if (actual.GetProperty("entry").ValueKind != JsonValueKind.Null || actual.GetProperty("driver").ValueKind != JsonValueKind.Object)
            throw new Exception("Missing metadata and surviving required metadata were not distinguished.");
        if (actual.GetProperty("closure").GetProperty("survivor.marker").GetString() != EntityBaseProvenance.Hash(survivor) ||
            actual.GetProperty("generatedInputs").GetProperty("fixture.notcs").GetString() != EntityBaseProvenance.Hash(code))
            throw new Exception("Actual sibling/generated input identities were lost.");
        var errors = proof.GetProperty("captureErrors").EnumerateArray().ToArray();
        if (errors.Length != 1 || errors[0].GetProperty("phase").GetString() != "metadata" || errors[0].GetProperty("field").GetString() != "entry" ||
            errors[0].GetProperty("path").GetString() != required || errors[0].GetProperty("type").GetString() != errorType.FullName)
            throw new Exception("Exact required metadata error identity was lost.");
        if (File.Exists(required) && actual.GetProperty("closure").GetProperty(Path.GetFileName(required)).GetString() != EntityBaseProvenance.Hash(required))
            throw new Exception("Malformed file's actual hash was lost before metadata refusal.");
    }
}
finally { Directory.Delete(directory, true); }
