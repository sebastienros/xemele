using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Dtd;

static void Require(bool value) { if (!value) throw new InvalidOperationException("Version probe oracle failed."); }
static object Identity(Assembly assembly) => new { Name = assembly.GetName().Name, Version = assembly.GetName().Version?.ToString(), FullVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, Sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
if (args.Length == 0 || args[0] == "--oracle")
{
    foreach (string version in new[] { "1.0", "1.1", "1.7", "1.00", "1.10", "1." + new string('9', 4096) })
    {
        using var core = new XmlParserCore(new()); var sink = new Sink(version);
        string xml = $"<?xml version='{version}'?><r/>";
        var result = core.Append(xml, [], true, sink, default);
        Require(result.Step == ScanStep.Completed && result.Consumed == xml.Length && sink.Callbacks == 6);
        using var framer = new XmlTextDeclarationFramer(new());
        var framed = framer.Append($"<?xml version='{version}' encoding='UTF-8'?>", [], true, default);
        Require(framed.Step == TextDeclarationStep.Declared && framed.Declaration?.Version == version && framed.Declaration?.EncodingName == "UTF-8");
        framer.Dispose(); Require(framed.Declaration?.Version == version);
        Require(XmlDocument.Parse(xml).RootElement.LocalName == "r");
    }
    Console.WriteLine("Passed 6 exact-version document/callback/owned-framing/public-DOM oracles.");
}
else if (args[0] == "--boundary-oracle") { Measure(false, false, false); MeasureOrdinary(false, false, false); Console.WriteLine("Passed long-version measured-boundary callback/pause/completion/owned-retention oracle; counters disabled."); }
else if (args[0] == "--runtime-identity") Console.WriteLine(JsonSerializer.Serialize(RuntimePayload.Capture()));
else if (args[0] == "--file-identity") Console.WriteLine(JsonSerializer.Serialize(Identity(Assembly.LoadFile(Path.GetFullPath(args[1])))));
else if (args[0] == "--identity") Console.WriteLine(JsonSerializer.Serialize(new { Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString() }));
else if (args[0] == "--allocation")
{
    bool positive = args.Length == 2 && args[1] == "positive";
    // Separate process warmup exercises the exact methods; no forced GC or JIT changes.
    for (int i = 0; i < 24; i++) { Measure(false, false, true, false, i); MeasureOrdinary(false, false, true, false, i); }
    Measure(positive, true); MeasureOrdinary(positive, true);
}
else throw new ArgumentException("Unknown mode.");

static void Measure(bool positive, bool print, bool count = true, bool enforceAllocations = true, int? warmIndex = null)
{
    var evidence = new ProbeEvidence("long", positive, count, enforceAllocations, warmIndex);
    Sink? sink = null;
    try
    {
        string version = "1." + new string('9', 60000);
        string prefix = "<?xml version='" + version[..50002];
        string suffix = version[50002..] + "'?>";
        string frameSuffix = version[50002..];
        sink = new Sink(version) { PauseDeclaration = true };
        using var core = new XmlParserCore(new() { Limits = new() { TokenCharacters = null } });
        evidence.Boundary = "core-prefix";
        var warm = core.Append(prefix, [], false, sink, default);
        evidence.Consumed = warm.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)warm.Step;
        evidence.Check(warm.Step == ScanStep.NeedInput && warm.Consumed == prefix.Length && sink.Callbacks == 1, "long-functional-1");
        evidence.Boundary = "core-workload";
        evidence.IntervalActive = count;
        long before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
        var scanned = core.Append(suffix, [], false, sink, default);
        object? escaped = positive ? new byte[37] : null;
        long coreBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        evidence.IntervalActive = false;
        evidence.CoreBytes = coreBytes;
        evidence.Consumed = scanned.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)scanned.Step;
        evidence.Check(scanned.Step == ScanStep.Paused && scanned.Consumed == suffix.Length && scanned.Diagnostic is null && sink.Callbacks == 2, "long-functional-2");
        evidence.Boundary = "core-completion";
        var complete = core.Append("<r/>", [], true, sink, default);
        evidence.Consumed = complete.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)complete.Step;
        evidence.Check(complete.Step == ScanStep.Completed && complete.Consumed == 4 && sink.Callbacks == 6, "long-functional-3");
        core.Dispose();
        using var framer = new XmlTextDeclarationFramer(new() { TokenCharacters = null });
        warm = default;
        evidence.Boundary = "framing-prefix";
        var framedWarm = framer.Append(prefix, [], false, default);
        evidence.Consumed = framedWarm.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)framedWarm.Step;
        evidence.Check(framedWarm.Step == TextDeclarationStep.NeedInput && framedWarm.Consumed == prefix.Length, "long-functional-4");
        evidence.Boundary = "framing-workload";
        evidence.IntervalActive = count;
        before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
        var digits = framer.Append(frameSuffix, [], false, default);
        escaped = positive ? new byte[37] : escaped;
        long digitBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        evidence.IntervalActive = false;
        evidence.DigitBytes = digitBytes;
        evidence.Consumed = digits.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)digits.Step;
        evidence.Check(digits.Step == TextDeclarationStep.NeedInput && digits.Consumed == frameSuffix.Length && digits.NextLocation == new XmlLocation(null, 1, prefix.Length + frameSuffix.Length + 1, null), "long-functional-5");
        evidence.Boundary = "framing-workload";
        evidence.IntervalActive = count;
        before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
        var owned = framer.Append("' encoding='UTF-8'?>", [], true, default);
        long ownedBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        evidence.IntervalActive = false;
        evidence.OwnedBytes = ownedBytes;
        evidence.Consumed = owned.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)owned.Step;
        evidence.Check(owned.Step == TextDeclarationStep.Declared && owned.Consumed == 20 && owned.Declaration?.Version == version && owned.Declaration?.EncodingName == "UTF-8", "long-functional-6");
        framer.Dispose(); evidence.Check(owned.Declaration?.Version == version, "long-functional-7");
        if (print) Console.WriteLine(JsonSerializer.Serialize(new { Mode = positive ? "positive" : "real", CoreBytes = coreBytes, DigitBytes = digitBytes, OwnedBytes = ownedBytes, VersionLength = version.Length, WarmCoreConsumed = prefix.Length, MeasuredCoreConsumed = scanned.Consumed, WarmFrameConsumed = framedWarm.Consumed, MeasuredFrameConsumed = digits.Consumed, CoreCallbacks = sink.Callbacks, RetainedVersion = owned.Declaration?.Version == version, Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString(), ProcessId = Environment.ProcessId }));


        evidence.Check(!positive || ((byte[])escaped!).Length == 37, "escaping-positive-wrapper");
        evidence.AllocationChecks();
        GC.KeepAlive(escaped);
    }
    catch (Exception error) { if (sink is not null) evidence.Callbacks = sink.Callbacks; evidence.Report(error); throw; }
}

static void MeasureOrdinary(bool positive, bool print, bool count = true, bool enforceAllocations = true, int? warmIndex = null)
{
    var evidence = new ProbeEvidence("ordinary", positive, count, enforceAllocations, warmIndex);
    Sink? sink = null;
    try
    {
        sink = new Sink("1.0") { PauseDeclaration = true };
        using var core = new XmlParserCore(new());
        evidence.Boundary = "core-prefix";
        var initial = core.Append("<?xml version='", [], false, sink, default);
        evidence.Consumed = initial.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)initial.Step;
        evidence.Check(initial.Step == ScanStep.NeedInput && initial.Consumed == 15 && sink.Callbacks == 1, "ordinary-functional-1");
        evidence.Boundary = "core-workload";
        evidence.IntervalActive = count;
        long before = count ? GC.GetAllocatedBytesForCurrentThread() : 0;
        var declaration = core.Append("1.0'?>", [], false, sink, default);
        object? escaped = positive ? new byte[37] : null;
        long coreBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        evidence.IntervalActive = false;
        evidence.CoreBytes = coreBytes;
        evidence.Consumed = declaration.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)declaration.Step;
        evidence.Check(declaration.Step == ScanStep.Paused && declaration.Consumed == 6 && sink.Callbacks == 2, "ordinary-functional-2");
        evidence.Boundary = "core-completion";
        var complete = core.Append("<r/>", [], true, sink, default);
        evidence.Consumed = complete.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)complete.Step;
        evidence.Check(complete.Step == ScanStep.Completed && complete.Consumed == 4 && sink.Callbacks == 6, "ordinary-functional-3");
        core.Dispose();
        using var framer = new XmlTextDeclarationFramer(new());
        evidence.Boundary = "framing-prefix";
        var prefix = framer.Append("<?xml version='1", [], false, default);
        evidence.Consumed = prefix.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)prefix.Step;
        evidence.Check(prefix.Step == TextDeclarationStep.NeedInput && prefix.Consumed == 16, "ordinary-functional-4");
        evidence.Boundary = "framing-workload";
        evidence.IntervalActive = count;
        before = count ? GC.GetAllocatedBytesForCurrentThread() : 0;
        var owned = framer.Append(".0' encoding='UTF-8'?>", [], true, default);
        escaped = positive ? new byte[37] : escaped;
        long ownedBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
        evidence.IntervalActive = false;
        evidence.OwnedBytes = ownedBytes;
        evidence.Consumed = owned.Consumed; evidence.Callbacks = sink.Callbacks; evidence.Outcome = (int)owned.Step;
        evidence.Check(owned.Step == TextDeclarationStep.Declared && owned.Consumed == 22 && owned.Declaration?.Version == "1.0" && owned.Declaration?.EncodingName == "UTF-8", "ordinary-functional-5");
        framer.Dispose(); evidence.Check(owned.Declaration?.Version == "1.0", "ordinary-functional-6");
        if (print) Console.WriteLine(JsonSerializer.Serialize(new { Mode = positive ? "ordinary-positive" : "ordinary-real", CoreBytes = coreBytes, OwnedBytes = ownedBytes, Callbacks = sink.Callbacks, Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString(), ProcessId = Environment.ProcessId }));

        evidence.Check(!positive || ((byte[])escaped!).Length == 37, "escaping-positive-wrapper");
        evidence.AllocationChecks();
        GC.KeepAlive(escaped);
    }
    catch (Exception error) { if (sink is not null) evidence.Callbacks = sink.Callbacks; evidence.Report(error); throw; }
}

internal sealed class Sink(string version) : IXmlEventSink
{
    internal int Callbacks; internal bool PauseDeclaration;
    public bool StartDocument(XmlLocation location) { if (Callbacks++ != 0) throw new Exception("Document order."); return true; }
    public bool Declaration(XmlDeclarationView value)
    { if (Callbacks++ != 1 || !value.Version.SequenceEqual(version) || !value.EncodingName.IsEmpty || value.Standalone is not null || value.Location != new XmlLocation(null, 1, 1, null)) throw new Exception("Borrowed version oracle."); return !PauseDeclaration; }
    public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
    { if (Callbacks++ != 2 || !empty || !name.LocalName.SequenceEqual("r") || !name.NamespaceUri.IsEmpty || !name.Prefix.IsEmpty) throw new Exception("Root oracle."); return true; }
    public bool NamespaceDeclaration(XmlNamespaceView value) => throw new Exception("Unexpected namespace.");
    public bool Attribute(XmlAttributeView value) => throw new Exception("Unexpected attribute.");
    public bool StartElementContent() { if (Callbacks++ != 3) throw new Exception("Content order."); return true; }
    public bool EndElement(XmlNameView name, XmlLocation location) { if (Callbacks++ != 4 || !name.LocalName.SequenceEqual("r")) throw new Exception("End order."); return true; }
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => throw new Exception("Unexpected text.");
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => throw new Exception("Unexpected fragment.");
    public bool EndDocument(XmlLocation location) { if (Callbacks++ != 5) throw new Exception("EOF order."); return true; }
}
