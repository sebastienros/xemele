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

## Exact M6 freeze

[M6 resource and integration decisions](../decisions/M6/resources-integration.md)
replace the earlier additive proposals. Exact independently compiled declaration
fixtures live in `tests/Contracts/Fixtures/M6/`. Catalog/XInclude use Xemele's
implemented XmlDocument and M2 resolver contracts. Four standalone projects link
only their own lane declarations and integrated runtime; no runtime stubs or
later-milestone dependencies are delivered by the contract task.

The decision freezes Catalogs 1.0 precedence/delegation/rewrite/next behavior,
immutable eager graph loading, aggregate quotas and final-URI cycles; XInclude's
failure-atomic new-document result, async ownership, XML/text/fallback/fixups and
shorthand/element() subset; BCL reader positioning/disposal and read-only navigator
snapshot semantics; seven XML CLI commands, explicit resource grants, exit codes
and the existing static partial registry for AOT.

Before runtime lanes start, an ordered owning repair must add Catalog=2,
XIncludeXml=3 and XIncludeText=4 to XmlResourceKind while retaining earlier values.
This task does not edit its outside-owned source or earlier fixture enums. Each
lane owns its later public API baseline and executable behavior tests; actual
AOT/package composition belongs to M6 samples/gate, not declaration compilation.
