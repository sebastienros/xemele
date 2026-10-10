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
else if (args[0] == "--file-identity") Console.WriteLine(JsonSerializer.Serialize(Identity(Assembly.LoadFile(Path.GetFullPath(args[1])))));
else if (args[0] == "--identity") Console.WriteLine(JsonSerializer.Serialize(new { Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString() }));
else if (args[0] == "--allocation")
{
    bool positive = args.Length == 2 && args[1] == "positive";
    // Separate process warmup exercises the exact methods; no forced GC or JIT changes.
    for (int i = 0; i < 24; i++) { Measure(false, false); MeasureOrdinary(false, false); }
    Measure(positive, true); MeasureOrdinary(positive, true);
}
else throw new ArgumentException("Unknown mode.");

static void Measure(bool positive, bool print, bool count = true)
{
    string version = "1." + new string('9', 60000);
    string prefix = "<?xml version='" + version[..50002];
    string suffix = version[50002..] + "'?>";
    string frameSuffix = version[50002..];
    var sink = new Sink(version) { PauseDeclaration = true };
    using var core = new XmlParserCore(new() { Limits = new() { TokenCharacters = null } });
    var warm = core.Append(prefix, [], false, sink, default);
    Require(warm.Step == ScanStep.NeedInput && warm.Consumed == prefix.Length && sink.Callbacks == 1);
    long before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
    var scanned = core.Append(suffix, [], false, sink, default);
    object? escaped = positive ? new byte[37] : null;
    long coreBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
    Require(scanned.Step == ScanStep.Paused && scanned.Consumed == suffix.Length && scanned.Diagnostic is null && sink.Callbacks == 2);
    var complete = core.Append("<r/>", [], true, sink, default);
    Require(complete.Step == ScanStep.Completed && complete.Consumed == 4 && sink.Callbacks == 6);
    core.Dispose();
    using var framer = new XmlTextDeclarationFramer(new() { TokenCharacters = null });
    warm = default;
    var framedWarm = framer.Append(prefix, [], false, default);
    Require(framedWarm.Step == TextDeclarationStep.NeedInput && framedWarm.Consumed == prefix.Length);
    before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
    var digits = framer.Append(frameSuffix, [], false, default);
    escaped = positive ? new byte[37] : escaped;
    long digitBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
    Require(digits.Step == TextDeclarationStep.NeedInput && digits.Consumed == frameSuffix.Length && digits.NextLocation == new XmlLocation(null, 1, prefix.Length + frameSuffix.Length + 1, null));
    before = (count ? GC.GetAllocatedBytesForCurrentThread() : 0);
    var owned = framer.Append("' encoding='UTF-8'?>", [], true, default);
    long ownedBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
    Require(owned.Step == TextDeclarationStep.Declared && owned.Consumed == 20 && owned.Declaration?.Version == version && owned.Declaration?.EncodingName == "UTF-8");
    framer.Dispose(); Require(owned.Declaration?.Version == version);
    if (print) Console.WriteLine(JsonSerializer.Serialize(new { Mode = positive ? "positive" : "real", CoreBytes = coreBytes, DigitBytes = digitBytes, OwnedBytes = ownedBytes, VersionLength = version.Length, WarmCoreConsumed = prefix.Length, MeasuredCoreConsumed = scanned.Consumed, WarmFrameConsumed = framedWarm.Consumed, MeasuredFrameConsumed = digits.Consumed, CoreCallbacks = sink.Callbacks, RetainedVersion = owned.Declaration?.Version == version, Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString(), ProcessId = Environment.ProcessId }));
    if (count) Require(positive ? coreBytes > 0 && digitBytes > 0 && ((byte[])escaped!).Length == 37 : coreBytes == 0 && digitBytes == 0);
    if (count) Require(ownedBytes >= 2 * version.Length);
    GC.KeepAlive(escaped);
}

static void MeasureOrdinary(bool positive, bool print, bool count = true)
{
    var sink = new Sink("1.0") { PauseDeclaration = true };
    using var core = new XmlParserCore(new());
    var initial = core.Append("<?xml version='", [], false, sink, default);
    Require(initial.Step == ScanStep.NeedInput && initial.Consumed == 15 && sink.Callbacks == 1);
    long before = count ? GC.GetAllocatedBytesForCurrentThread() : 0;
    var declaration = core.Append("1.0'?>", [], false, sink, default);
    object? escaped = positive ? new byte[37] : null;
    long coreBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
    Require(declaration.Step == ScanStep.Paused && declaration.Consumed == 6 && sink.Callbacks == 2);
    var complete = core.Append("<r/>", [], true, sink, default);
    Require(complete.Step == ScanStep.Completed && complete.Consumed == 4 && sink.Callbacks == 6);
    core.Dispose();
    using var framer = new XmlTextDeclarationFramer(new());
    var prefix = framer.Append("<?xml version='1", [], false, default);
    Require(prefix.Step == TextDeclarationStep.NeedInput && prefix.Consumed == 16);
    before = count ? GC.GetAllocatedBytesForCurrentThread() : 0;
    var owned = framer.Append(".0' encoding='UTF-8'?>", [], true, default);
    escaped = positive ? new byte[37] : escaped;
    long ownedBytes = (count ? GC.GetAllocatedBytesForCurrentThread() : 0) - before;
    Require(owned.Step == TextDeclarationStep.Declared && owned.Consumed == 22 && owned.Declaration?.Version == "1.0" && owned.Declaration?.EncodingName == "UTF-8");
    framer.Dispose(); Require(owned.Declaration?.Version == "1.0");
    if (print) Console.WriteLine(JsonSerializer.Serialize(new { Mode = positive ? "ordinary-positive" : "ordinary-real", CoreBytes = coreBytes, OwnedBytes = ownedBytes, Callbacks = sink.Callbacks, Product = Identity(typeof(XmlDocument).Assembly), Probe = Identity(Assembly.GetExecutingAssembly()), Runtime = Environment.Version.ToString(), ProcessId = Environment.ProcessId }));
    if (count) Require(positive ? coreBytes > 0 && ownedBytes > 32 && ((byte[])escaped!).Length == 37 : coreBytes == 0 && ownedBytes == 32);
    GC.KeepAlive(escaped);
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
