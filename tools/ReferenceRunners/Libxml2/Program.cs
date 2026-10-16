using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xemele.ReferenceProtocol;

// The build manifest is adjacent to the pinned native bridge, never inferred from a host library.
string nativePath = Path.GetFullPath(Environment.GetEnvironmentVariable("XEMELE_LIBXML2_BRIDGE")
    ?? throw new InvalidOperationException("Set XEMELE_LIBXML2_BRIDGE to the task-built native bridge."));
NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, _, _) =>
    name == "xemele_libxml2" ? NativeLibrary.Load(nativePath) : IntPtr.Zero);
using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(nativePath)!, "provenance.json")));
if (manifest.RootElement.GetProperty("commit").GetString() != "96498992efa48d52b0e8b83058bd88dbdaf153c1"
    || Native.String(Native.xb_version()) != "21504")
    throw new InvalidOperationException("Native library does not match pinned libxml2.");
string nativeHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(nativePath)));
if (nativeHash != manifest.RootElement.GetProperty("bridgeSha256").GetString())
    throw new InvalidOperationException("Native bridge checksum differs from build provenance.");
var engine = new Engine("libxml2", "2.15.4", RuntimeInformation.FrameworkDescription,
    Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion
    + "; " + manifest.RootElement.GetRawText());
using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true), false);
using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false, true)) { AutoFlush = true };
while (true)
{
    var line = new StringBuilder();
    bool oversized = false;
    int ch;
    try
    {
        while ((ch = input.Read()) >= 0 && ch != '\n')
        {
            if (line.Length < Wire.MaximumLineCharacters)
                line.Append((char)ch);
            else
                oversized = true;
        }
        if (ch < 0 && line.Length == 0 && !oversized)
            break;
    }
    catch (DecoderFallbackException)
    {
        Console.Error.WriteLine("Invalid UTF-8 protocol input.");
        Environment.ExitCode = 2;
        break;
    }
    Request? request = null;
    Response response;
    try
    {
        if (oversized)
            throw new Failure("limit", "Request exceeds line limit.");
        request = JsonSerializer.Deserialize<Request>(line.ToString(), Wire.Json)
            ?? throw new ArgumentException("Null request.");
        if (request.ProtocolVersion != 1 || string.IsNullOrWhiteSpace(request.CaseId)
            || request.Operation != "xml.events" || request.Profile != "xml-1.0" || request.Options?.Count > 0)
            throw new ArgumentException("Unsupported version, identity, operation, profile or options.");
        if (request.EncodingHint is not null && request.EncodingHint != "utf-8")
            throw new ArgumentException("Only absent or utf-8 encoding hint is supported.");
        if (request.InputBase64 is null)
            throw new ArgumentException("Missing inputBase64.");
        byte[] bytes = Convert.FromBase64String(request.InputBase64);
        if (bytes.Length > 4_194_304)
            throw new Failure("limit", "XML input byte limit exceeded.");
        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long resourceBytes = 0;
        foreach (var pair in request.OfflineResources ?? [])
        {
            if (!Uri.TryCreate(pair.Key, UriKind.Absolute, out var uri))
                throw new ArgumentException("Offline keys must be absolute URIs.");
            byte[] value = Convert.FromBase64String(pair.Value);
            resourceBytes += value.Length;
            if (resourceBytes > 4_194_304)
                throw new Failure("limit", "Offline resource byte limit exceeded.");
            resources.Add(uri.AbsoluteUri, value);
        }
        if (request.EncodingHint == "utf-8")
            _ = new UTF8Encoding(false, true).GetCharCount(bytes);
        using var parser = new Parser(bytes, request.EncodingHint, resources);
        Event[] events = parser.Read();
        string? unsupported = DocumentEnvelope.UnsupportedReason(bytes, request.EncodingHint, parser.EncodingName);
        if (unsupported is not null)
            throw new Failure("protocol", "Unsupported normalization: " + unsupported);
        response = Reply("success", [], events);
    }
    catch (Rejected ex) { response = Reply("parse-rejected", [new("xml", ex.Message)]); }
    catch (DecoderFallbackException ex) { response = Reply("parse-rejected", [new("xml", ex.Message)]); }
    catch (Failure ex) { response = Reply("runner-failure", [new(ex.Category, ex.Message)]); }
    catch (Exception ex)
    {
        response = Reply("runner-failure", [new(ex is ArgumentException or JsonException or FormatException
        ? "protocol" : "infrastructure", ex.Message)]);
    }
    string serialized = JsonSerializer.Serialize(response, Wire.Json);
    if (serialized.Length > Wire.MaximumLineCharacters)
    {
        serialized = JsonSerializer.Serialize(Reply("runner-failure", [new("limit", "Response exceeds line limit.")]), Wire.Json);
        if (serialized.Length > Wire.MaximumLineCharacters)
            serialized = JsonSerializer.Serialize(new Response(1, null, null, engine, "runner-failure",
                [new("limit", "Identity cannot fit within response limit.")], null), Wire.Json);
    }
    output.WriteLine(serialized);
    Response Reply(string outcome, Diagnostic[] diagnostics, Event[]? events = null) =>
        new(1, request?.CaseId, request?.Operation, engine, outcome, diagnostics, events);
}

sealed class Failure(string category, string message) : Exception(message)
{
    public string Category { get; } = category;
}
sealed class Rejected(string message) : Exception(message);

sealed class Parser : IDisposable
{
    private GCHandle input;
    private readonly Native.Resolve resolver;
    private readonly Dictionary<string, (IntPtr Data, nuint Length)> resources = new(StringComparer.Ordinal);
    private IntPtr session;
    private string? loaderError;
    private long loadedBytes;
    public Parser(byte[] bytes, string? encoding, Dictionary<string, byte[]> mapped)
    {
        input = GCHandle.Alloc(bytes.Length == 0 ? new byte[1] : bytes, GCHandleType.Pinned);
        resolver = Resolve;
        try
        {
            foreach (var pair in mapped)
            {
                IntPtr data = Marshal.AllocHGlobal(Math.Max(pair.Value.Length, 1));
                Marshal.Copy(pair.Value, 0, data, pair.Value.Length);
                resources.Add(pair.Key, (data, (nuint)pair.Value.Length));
            }
            session = Native.xb_create(input.AddrOfPinnedObject(), bytes.Length, encoding, resolver, IntPtr.Zero);
            if (session == IntPtr.Zero)
                throw new Failure("infrastructure", "libxml2 reader allocation failed.");
        }
        catch { Dispose(); throw; }
    }
    private int Resolve(IntPtr _, IntPtr url, out IntPtr data, out nuint length)
    {
        data = IntPtr.Zero;
        length = 0;
        // Exceptions must never unwind through a C callback.
        try
        {
            string name = Native.String(url);
            if (!Uri.TryCreate(name, UriKind.Absolute, out var uri)
                || !resources.TryGetValue(uri.AbsoluteUri, out var value))
                return 0;
            loadedBytes += (long)value.Length;
            if (loadedBytes > 4_194_304)
            {
                loaderError = "limit";
                return 0;
            }
            data = value.Data;
            length = value.Length;
            return 1;
        }
        catch { loaderError = "infrastructure"; return 0; }
    }
    public string EncodingName => Native.String(Native.xb_encoding(session));
    public Event[] Read()
    {
        var events = new List<Event>();
        long characters = 0;
        int result;
        while ((result = Native.xb_next(session)) == 1)
        {
            if (Native.String(Native.xb_xml_version(session)) != "1.0")
                throw new Rejected("XML version is not 1.0.");
            int type = Native.xb_type(session);
            if (type == 1)
            {
                string ns = Native.String(Native.xb_ns(session)), name = Native.String(Native.xb_name(session));
                bool empty = Native.xb_empty(session) == 1;
                var attributes = new List<AttributeValue>();
                if (Native.xb_first_attr(session) == 1)
                {
                    do
                    {
                        string ans = Native.String(Native.xb_ns(session));
                        if (ans != "http://www.w3.org/2000/xmlns/")
                        {
                            string value = Native.String(Native.xb_value(session));
                            characters += value.Length;
                            attributes.Add(new(ans, Native.String(Native.xb_name(session)), value));
                        }
                    } while (Native.xb_next_attr(session) == 1);
                    _ = Native.xb_element(session);
                }
                events.Add(new("start", ns, name, Attributes: AttributeNormalization.Sort(attributes)));
                if (empty)
                    events.Add(new("end", ns, name));
            }
            else if (type == 15)
                events.Add(new("end", Native.String(Native.xb_ns(session)), Native.String(Native.xb_name(session))));
            else if (type is 3 or 4 or 13 or 14)
            {
                string value = Native.String(Native.xb_value(session));
                characters += value.Length;
                if (events.Count > 0 && events[^1].Kind == "text")
                    events[^1] = events[^1] with
                    {
                        Value = events[^1].Value + value
                    };
                else
                    events.Add(new("text", Value: value));
            }
            if (characters > 1_048_576 || events.Count > 131_072)
                throw new Failure("limit", "Normalized character or event limit exceeded.");
        }
        if (loaderError is not null)
            throw new Failure(loaderError, "Offline loader quota or callback failed.");
        if (Native.xb_denied(session) != 0)
            throw new Failure("resource-denied", "External resource absent from offline map.");
        if (result < 0)
        {
            string message = Native.String(Native.xb_error(session)).Trim();
            int code = Native.xb_error_code(session);
            if (code == 114)
                throw new Failure("limit", message); // XML_ERR_RESOURCE_LIMIT, pinned header.
            if (code == 1)
                throw new Failure("infrastructure", message); // XML_ERR_NO_MEMORY.
            if (code == 32)
                throw new Failure("protocol", "Unsupported XML encoding: " + message);
            throw new Rejected(message);
        }
        return events.ToArray();
    }
    public void Dispose()
    {
        if (session != IntPtr.Zero)
        {
            Native.xb_free(session);
            session = IntPtr.Zero;
        }
        foreach (var resource in resources.Values)
            Marshal.FreeHGlobal(resource.Data);
        resources.Clear();
        if (input.IsAllocated)
            input.Free();
        GC.KeepAlive(resolver);
    }
}

static class Native
{
    private const string Library = "xemele_libxml2";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int Resolve(IntPtr user, IntPtr url, out IntPtr data, out nuint length);
    internal static string String(IntPtr value) => Marshal.PtrToStringUTF8(value) ?? "";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_create(IntPtr bytes, int length, [MarshalAs(UnmanagedType.LPUTF8Str)] string? encoding, Resolve resolver, IntPtr user);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void xb_free(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_next(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_type(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_empty(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_first_attr(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_next_attr(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_element(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_ns(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_name(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_value(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_denied(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int xb_error_code(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_error(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_version();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_xml_version(IntPtr session);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr xb_encoding(IntPtr session);
}
