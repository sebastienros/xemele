using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M6XInclude;

[MemoryDiagnoser, ShortRunJob]
public class XIncludeBenchmarks
{
    private XmlDocument shortText = null!, longText = null!, nestedXml = null!, denied = null!, selectedXml = null!;
    private XIncludeOptions options = null!;
    [GlobalSetup] public void Setup()
    {
        const string xi = "http://www.w3.org/2001/XInclude";
        XmlDocument Input(string body) => XmlDocument.Parse($"<r xmlns:xi='{xi}'>{body}</r>");
        shortText = Input("<xi:include href='short' parse='text'/>");
        longText = Input("<xi:include href='long' parse='text'/>");
        nestedXml = Input("<xi:include href='outer'/>");
        selectedXml = Input("<xi:include href='selected' xpointer='element(/1/2)'/>");
        denied = Input("<xi:include href='missing'><xi:fallback><ok/></xi:fallback></xi:include>");
        options = new() { BaseUri = new("https://bench.test/source"), Resolver = new Resolver(new() {
            ["/short"] = "plain & text"u8.ToArray(), ["/long"] = System.Text.Encoding.UTF8.GetBytes(new string('é', 4096)),
            ["/outer"] = System.Text.Encoding.UTF8.GetBytes($"<a xmlns:xi='{xi}'><xi:include href='inner'/></a>"), ["/inner"] = "<b>nested</b>"u8.ToArray(), ["/selected"] = "<a><first/><second>selected</second></a>"u8.ToArray() }) };
    }
    [Benchmark] public XIncludeResult ShortOwnedText() => XIncludeProcessor.Process(shortText, options);
    [Benchmark] public XIncludeResult MultilingualOwnedText() => XIncludeProcessor.Process(longText, options);
    [Benchmark] public XIncludeResult NestedOwnedXml() => XIncludeProcessor.Process(nestedXml, options);
    [Benchmark] public XIncludeResult DenialOwnedFallback() => XIncludeProcessor.Process(denied, options);
    [Benchmark] public XIncludeResult SelectedOwnedXml() => XIncludeProcessor.Process(selectedXml, options);
    [Benchmark] public Task<XIncludeResult> AsyncOwnedXml() => XIncludeProcessor.ProcessAsync(nestedXml, options);
    private sealed class Resolver(Dictionary<string, byte[]> content) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return content.TryGetValue(request.Uri.AbsolutePath, out var bytes) ? new(new MemoryStream(bytes, false), request.Uri) : null; }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        { await Task.Yield(); return Resolve(request, cancellationToken); }
    }
}
