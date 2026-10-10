# Resource and integration contracts — M2/M6

Resolver decision owner: `M2-contracts`; catalogs/inclusion/adapters/tool owner: `M6-contracts`.

## Resolver boundary, implemented in M2

The exact nonpackaged declarations are
[Resolver.cs.fixture](../../tests/Contracts/Fixtures/M2/Resolver.cs.fixture).
They preserve the approved foundation M2 namespace Xemele and existing signatures:
XmlResourceRequest(Uri Uri, Uri? Referrer, XmlResourceKind Kind), nullable sync
Resolve(request, cancellationToken), virtual nullable ResolveAsync with the same
operation token, and XmlResolvedResource(Stream content, Uri sourceUri).
SystemId/PublicId are additive init-only lexical identifier properties on request.
This replaces the stale Resolution namespace/BaseUri proposal; no foundation
fixture or earlier runtime API changes.

Requests have an absolute resolved Uri and null/absolute Referrer. Relative
SystemId resolution occurs against the declaration's final source URI before
calling the resolver; no base means XML.ResourceDenied. PublicId never grants
implicit lookup. Resolve returning null means categorized structured denial;
resolver-thrown exceptions propagate unchanged. The async fallback may resolve
synchronously after checking cancellation; no hidden Task.Run. Real async adapters
arrive with their owning modules.

The returned readable stream/wrapper transfers ownership to the session. Final
absolute SourceUri controls nested bases, locations and active-cycle checks.
[M2 decisions](../decisions/M2/xml-resources.md) fix exactly-once disposal, invalid
return handling, count/byte quotas, cancellation, alias cycles, supported profiles
and bounded byte-limit EOF probing. Caller-owned primary streams remain open.
Parser grants never include an ambient file/network resolver. Fixture resolvers
are offline; DTD/external entities are usable with a caller resolver before
catalogs exist. These contracts are independent model evidence, not implemented
resource I/O or parser behavior.

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
