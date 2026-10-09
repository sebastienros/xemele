# Resource and integration contracts — M2/M6

Resolver decision owner: `M2-contracts`; catalogs/inclusion/adapters/tool owner: `M6-contracts`.

## Resolver boundary, implemented in M2

```csharp
namespace Xemele.Resolution;
public readonly record struct XmlResourceRequest(
    Uri? BaseUri, string? SystemId, string? PublicId, XmlResourceKind Kind);
public abstract class XmlResourceResolver
{
    public abstract XmlResolvedResource Resolve(XmlResourceRequest request);
    public virtual ValueTask<XmlResolvedResource> ResolveAsync(
        XmlResourceRequest request, CancellationToken cancellationToken = default);
}
public sealed class XmlResolvedResource : IDisposable, IAsyncDisposable
{
    public XmlResolvedResource(Uri sourceUri, Stream content);
    public Uri SourceUri { get; }
    public Stream Content { get; }
}
```

Returned resources and their streams are owned/disposed by the requesting resolution session; refusal throws a categorized resolution exception. `SourceUri` is the final absolute resolved URI used for relative references and locations. Async default behavior is explicitly documented (it may synchronously resolve); built-in async-capable adapters perform real async I/O. No hidden `Task.Run`.

The bounded internal resolution session tracks resource count/bytes, nesting, cycles, cancellation, and diagnostics. User resolver code is an explicit trust boundary. Fixture resolvers are offline. The core never fetches a URI itself. DTD/external-entity processing is fully usable with a caller resolver before catalogs exist.

## M6 additive services

```csharp
public sealed class XmlCatalog
{
    public static XmlCatalog Parse(string xml);
    public Uri? Resolve(XmlResourceRequest request);
}
public sealed class CatalogResolver : XmlResourceResolver
{
    public CatalogResolver(XmlCatalog catalog, XmlResourceResolver fallback);
}
public static class XIncludeProcessor
{
    public static XIncludeResult Process(Document document, XIncludeOptions options);
}
```

M6 contracts fix catalog resolution precedence, delegation/rewrite/next-catalog support, cycle limits, inclusion options/result/async shapes and mutation/failure semantics. Proposal: process on an imported working tree and return a new document, preserving the original on failure; any in-place API must be explicitly named. Include XML/text, fallback, base/language fixup, relative resources, and shorthand/`element()` XPointer profiles. More schemes are reported unsupported, never silently ignored. Caller-supplied resolvers allow XInclude and catalog lanes to run independently.

BCL adapters are separate types in `Xemele.Interop`, consuming completed tree/reader APIs. `XemeleXmlReaderAdapter : System.Xml.XmlReader` supplies the required abstract members with documented differences; a navigator adapter is added only for the agreed XPath integration surface. These adapters do not replace the engines. Contract fixtures specify reader positioning, attributes, disposal, and unsupported capabilities.

The CLI adds parse/format/XPath/DTD-validation/canonicalization/catalog/XInclude commands using existing modules. XML-only CLI functionality builds before HTML/schemas. Later commands live in separate task-owned command files discovered by the M0 registry convention, with no edits to one giant switch. Exit codes distinguish success, invalid input, invocation error, and internal failure. Inputs/outputs and resolver grants are explicit; tests use local fixtures.

M6 parallel lanes: catalog, XInclude, BCL adapters, and XML CLI. A final samples/integration task composes them and runs package/AOT smoke checks. No lane requires later HTML or XSD APIs.
