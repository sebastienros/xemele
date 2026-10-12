# M8 schema resource outcomes — accepted bounded design

Coordinator decision after independent contract and main source review. This is a compatible repair of required XSD 1.0 composition, not a profile exclusion. No runtime implementation or passing checks are implied by this document.

The first owner is `M8-schema-resource-outcomes`; the compiler owner is the later `M8-xsd-optional-composition`, after the existing component, recursive, type and particle repair queue. Preserve legacy resolver behavior, frozen `ISchemaResourceLoader`, earlier task evidence and all non-schema consumers.

```csharp
namespace Xemele;
public enum XmlResourceResolutionStatus { Denied = 0, Unavailable = 1, Resolved = 2 }
public readonly struct XmlResourceResolution
{
    public XmlResourceResolutionStatus Status { get; }
    public XmlResolvedResource? Resource { get; }
    public static XmlResourceResolution Denied { get; }
    public static XmlResourceResolution Unavailable { get; }
    public static XmlResourceResolution FromResource(XmlResolvedResource resource);
}
// Add to existing XmlResourceResolver; Resolve and ResolveAsync stay unchanged.
public virtual XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken);
```

Freeze and compile this exact additive surface in an isolated fixture before runtime implementation. Use private constructors and no setters: default is Denied/null; only FromResource(non-null) makes Resolved; Unavailable carries no resource. FromResource(null) throws ArgumentNullException. Do not expose arbitrary status/payload construction. The default virtual method checks cancellation, calls legacy Resolve exactly once with the identical request/token, maps null to Denied, and catches nothing. Avoid a post-acquisition cancellation throw in the adapter: the receiving loader must own and protect cleanup first.

The result is a non-disposable value. Constructing/copying it does not transfer ownership; returning Resolved across the resolver boundary transfers the existing resource wrapper once. Denied/Unavailable transfer no resource. Existing successful parsing, final URI, redirected base, quotas and exactly-once disposal remain intact, including preservation of a primary exception when disposal also fails.

Keep mandatory Load strict and add a concrete internal TryLoadOptional(reference, effectiveBaseUri, operation, out XmlDocument? document). Only explicit Unavailable returns false/null with a complete operation. Denied/default, missing resolver, legacy null and invalid base still refuse. No caller exception, parser failure, quota or cancellation becomes Unavailable. Reuse the existing acquisition/parsing logic; never recover after Refuse. Poll operation/cancellation before returning absence. Charge attempts through the shared operation; absence adds no successful external resource or bytes, fabricated document, successful cache entry or fresh allowance. Existing exhausted-resource capacity guard still precedes resolver invocation.

The later XSD owner uses optional acquisition for include/import/empty or annotation-only redefine. Substantive redefine still requires resolution, and missing referenced components fail normal schema checks. Resolved malformed/wrong-namespace documents remain errors. Do not change cycles, instance-hint fetching, async methods, DTD/entity/catalog/XInclude call sites, or other language composition rules.

Required proof: legacy null/resource/exception behavior and callback identity; default/factory invariants; optional versus mandatory unavailability and denial; cancellation after unavailable and after acquisition; exactly-once successful/failing stream cleanup; shared quota boundaries, principal/external parsing and redirected source/base URI; subsequent available dependencies; additive public API and unchanged frozen lane interfaces; normal and true XML-only consumers. Actual allocation evidence uses the existing prepared infrastructure and a fresh root lease, without a new throughput gate.

Sources: [include](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#src-include), [redefine](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#src-redefine), [import](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#src-import). The four original cases are anyURI_a002_1337.v, anyURI_a004_1339.i, schD7.v and schG8.v. The a004 original INVALID remains recorded; [W3C4126](https://www.w3.org/Bugs/Public/show_bug.cgi?id=4126) supports separately derived VALID. Corpus changes its authored-missing mapping only after both repairs are integrated and verified.
