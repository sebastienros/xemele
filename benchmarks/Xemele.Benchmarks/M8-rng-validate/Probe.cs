using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Validation;

internal static class Probe
{
    private const string Ns = "http://relaxng.org/ns/structure/1.0";
    private const string Simple = "<element xmlns='" + Ns + "' name='r'><empty/></element>";
    private static object? sink;
    private sealed record Route(string Name, Func<object> Run, Func<object, bool> Check);
    private static Route[] Routes()
    {
        var simple = RelaxNgSchema.Compile(Simple); var valid = XmlDocument.Parse("<r/>"); var invalid = XmlDocument.Parse("<wrong/>");
        var text = RelaxNgSchema.Compile("<element xmlns='" + Ns + "' name='r'><value type='string'>é東京ab</value></element>");
        var fragmented = XmlDocument.Parse("<r>é<![CDATA[東京]]><!--ignored-->ab</r>");
        var typed = RelaxNgSchema.Compile("<element xmlns='" + Ns + "' name='r'><data datatypeLibrary='http://www.w3.org/2001/XMLSchema-datatypes' type='string'><param name='pattern'>a.*</param><param name='pattern'>.*b</param></data></element>");
        var lexical = XmlDocument.Parse("<r>ab</r>");
        var recursive = RelaxNgSchema.Compile("<grammar xmlns='" + Ns + "'><start><ref name='n'/></start><define name='n'><element name='n'><zeroOrMore><ref name='n'/></zeroOrMore></element></define></grammar>");
        var nested = XmlDocument.Parse("<n><n><n/></n><n/></n>");
        var list = RelaxNgSchema.Compile("<element xmlns='" + Ns + "' name='r'><list><oneOrMore><data datatypeLibrary='http://www.w3.org/2001/XMLSchema-datatypes' type='int'/></oneOrMore></list></element>");
        var tokens = XmlDocument.Parse("<r>1 02 -3 4</r>");
        var interleave = RelaxNgSchema.Compile("<element xmlns='" + Ns + "' name='r'><interleave><choice><element name='a'><value>x</value></element><element name='a'><value>y</value></element></choice><element name='b'><empty/></element></interleave></element>");
        var interleaved = XmlDocument.Parse("<r><b/><a>y</a></r>");
        static bool Valid(object value) => value is ValidationResult { IsValid: true, IsComplete: true };
        return [
            new("compile-owned-schema", () => RelaxNgSchema.Compile(Simple), value => ((RelaxNgSchema)value).Validate(valid).IsValid),
            new("dom-valid", () => simple.Validate(valid), Valid),
            new("dom-invalid-diagnostic", () => simple.Validate(invalid), value => value is ValidationResult { IsValid: false, IsComplete: true, Diagnostics.Count: 1 }),
            new("coalesced-multilingual-text", () => text.Validate(fragmented), Valid),
            new("datatype-regex", () => typed.Validate(lexical), Valid),
            new("recursive-dom", () => recursive.Validate(nested), Valid),
            new("list-values", () => list.Validate(tokens), Valid),
            new("ambiguous-interleave", () => interleave.Validate(interleaved), Valid),
            new("string-input-dom-and-result", () => simple.Validate("<r/>"), Valid),
            new("byte-stream-input-dom-and-result", () => { using var stream = new MemoryStream("<r/>"u8.ToArray()); return simple.Validate(stream); }, Valid)
        ];
    }
    private static object Execute(Route route)
    { var result = route.Run(); if (!route.Check(result)) throw new Exception("Incorrect route: " + route.Name); sink = result; return result; }
    public static void Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("--control" or "--measure")) throw new ArgumentException("Use --control|--measure output.json");
        bool measure = args[0] == "--measure";
        // The cold route executes before fixture/schema initialization. Its result
        // check occurs after the counter stop, never inside the measured interval.
        object? cold = null; long coldBytes = 0;
        if (measure) { long before = GC.GetAllocatedBytesForCurrentThread(); cold = RelaxNgSchema.Compile(Simple); coldBytes = GC.GetAllocatedBytesForCurrentThread() - before; }
        else cold = RelaxNgSchema.Compile(Simple);
        if (!((RelaxNgSchema)cold).Validate("<r/>").IsValid) throw new Exception("Cold route failed.");
        var routes = Routes(); var observations = new List<object>();
        foreach (var route in routes)
        {
            for (int i = 0; i < 32; i++) Execute(route);
            var raw = new long[64];
            for (int sample = 0; sample < raw.Length; sample++)
            {
                long before = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
                for (int operation = 0; operation < 16; operation++) sink = route.Run();
                raw[sample] = measure ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
                if (!route.Check(sink!)) throw new Exception("Measured result failed: " + route.Name);
            }
            if (measure) observations.Add(new { route.Name, OperationsPerSample = 16, Samples = raw.Length, RawByteTotals = raw, TotalOperations = 1024, TotalBytes = raw.Sum() });
        }
        long positive = 0;
        if (measure) { long before = GC.GetAllocatedBytesForCurrentThread(); sink = new byte[1024]; positive = GC.GetAllocatedBytesForCurrentThread() - before; if (positive < 1024) throw new Exception("Allocation positive control failed."); }
        else sink = new byte[1024];
        long retained = 0, rss = 0; var schemas = new RelaxNgSchema[16];
        if (measure)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long before = GC.GetTotalMemory(true);
            for (int i = 0; i < schemas.Length; i++) schemas[i] = RelaxNgSchema.Compile(Simple);
            retained = GC.GetTotalMemory(true) - before;
            using var process = Process.GetCurrentProcess(); rss = process.WorkingSet64;
        }
        else for (int i = 0; i < schemas.Length; i++) schemas[i] = RelaxNgSchema.Compile(Simple);
        GC.KeepAlive(schemas); GC.KeepAlive(cold); GC.KeepAlive(sink);
        File.WriteAllText(args[1], JsonSerializer.Serialize(new { Mode = args[0], CountersEnabled = measure, RouteCount = routes.Length, RouteNames = routes.Select(route => route.Name).ToArray(), ColdCompileBytes = coldBytes, PositiveControlBytes = positive, RetainedSchemas = schemas.Length, RetainedManagedBytesDelta = retained, RssBytes = rss, Observations = observations, Runtime = Environment.Version.ToString(), Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS: " + routes.Length + " route controls; counters=" + measure);
    }
}
