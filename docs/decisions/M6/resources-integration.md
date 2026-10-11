# M6 freeze: independent resource and integration lanes

This is a contract, not evidence that M6 runtime features already exist. Exact
nonpackaged declarations and representative usage are in
`tests/Contracts/Fixtures/M6/{Catalog,XInclude,Interop,Cli}.cs.fixture` and their
independent projects. Extern members have no implementation and constructors are also extern; none
are invoked. Compiler warnings about nonnative extern declarations are suppressed
only in these nonpackaged projects. Every executable demonstration uses only integrated M1–M5 behavior.
No fixture enters the shipped assembly. These declarations replace stale
`Document` proposals with the implemented `XmlDocument` identity.

## Resource-kind repair and common operation boundary

Before runtime lanes start, append `Catalog = 2`, `XIncludeXml = 3`, and
`XIncludeText = 4` to `XmlResourceKind` in
`src/Xemele/Resolution/Core/Resources.cs`. Preserve `ExternalSubset = 0` and
`ExternalEntity = 1`. M2 validates enum membership; numeric casts do not confer
support. This contract task cannot edit that file. The coordinator must arrange
an ordered owning repair, its API baseline, and undefined-kind regression tests.
Earlier declaration fixtures remain unchanged.

Every operation validates and snapshots options and positive finite defaults.
Null shared quotas retain their explicit unlimited meaning; depth-specific
integer quotas must be positive. No ambient resolver, file/network access,
process-wide cache, or hidden Task.Run is introduced. Absolute final SourceUri
is the identity for nested bases and active cycles, including redirect aliases.
Requests have absolute Uri and null/absolute Referrer. The resolver's nullable
refusal is a structured resource denial; its thrown exceptions and cancellation
propagate unchanged, including callback-thrown XmlParseException. Invalid options
throw argument exceptions before acquisition. An acquired resource is disposed
exactly once; async uses DisposeAsync. Cleanup on an exceptional path preserves
the original exception rather than replacing it with a disposal exception.

Catalog and inclusion each share one operation budget over their entire graph:
Limits.ExternalResources counts every acquisition attempt, including denials,
repeated references, delegates, next catalogs and nested includes;
Limits.ExternalBytes counts aggregate bytes read, not one budget per
file. Limits.DocumentCharacters/ExpandedCharacters cover aggregate decoded and expanded
characters; external bytes cannot reset. Depth, InternedNames, TokenCharacters,
AttributesPerElement and Diagnostics apply to parsing and result construction. Exactly-at-limit works;
checked one-over fails. No fallback resets counters or catches quota violations.
Catalog loading has additional MaxCatalogDepth=32 and MaxCatalogDocuments=128
(distinct final resource identities), and inclusion Limits.InclusionDepth=32. Each
sibling acquisition counts even when the same source was previously completed.
Active identities leave the stack in finally; reuse outside an active chain is
permitted. Source locations and bounded module-prefixed diagnostic snapshots are
owned. This task performs no allocation/timing workload; runtime owners provide
applicable performance evidence under the coordinator lease.

## Catalog lane

Profile: OASIS XML Catalogs 1.0, namespace
`urn:oasis:names:tc:entity:xmlns:xml:catalog`. Entries: catalog, group, system,
public, uri, rewriteSystem, rewriteURI, delegateSystem, delegatePublic,
delegateURI and nextCatalog, with scoped xml:base/prefer. Root must be catalog.
Unsupported catalog-namespace entries (including 1.1 suffix entries) are rejected
with `CATALOG.Unsupported`; extension elements in other namespaces are ignored.
Catalog XML uses strict XML 1.0 with DTD prohibited. No fetch is granted by a URI
mapping. Relative targets require an absolute inherited base.

`XmlCatalog.Parse(xml, options, token)` eagerly constructs an immutable complete
graph through options.Resolver, using kind Catalog. No later Resolve call does
I/O. Null resolver refuses needed delegate/next acquisition. Initial preference
is public; a group/catalog prefer overrides for its descendants only. Public IDs
collapse XML whitespace; URN public-ID input uses the standard publicid decoding.
SystemId presence determines entity routing: when lexical SystemId is null,
entity lookup is public-only and no system/rewriteSystem/delegateSystem candidates
are tested, even though request.Uri is mandatory. This makes prefer=system public
lookup without a system ID reachable. When SystemId is present, use that lexical
key (never substitute request.Uri). URI keys use absolute request.Uri. Catalog entries retain document order after
group flattening with inherited base/preference.

Entity lookup with SystemId (ExternalSubset/ExternalEntity) tries exact system first, then
longest rewriteSystem prefix, then matching delegateSystem catalogs in descending
prefix length, document order for equal lengths. A delegated lookup discards
public ID. If no eligible system rule applies, exact public then delegatePublic
are eligible without a system ID, or under scoped prefer=public. Exact duplicate
keys use the first entry. Rewrite ties use the first entry. Public delegation
uses normalized prefixes, longest-first, and discards system ID. Delegation is
terminal for that identifier path: once eligible delegates exist, their misses
return no match rather than proceeding to current nextCatalog entries.
If no applicable delegation and no mapping exists, nextCatalog graphs are tried
in document order with the original identifiers. URI kinds use exact uri,
longest rewriteURI, delegateURI (same ordering/terminal behavior), then nextCatalog.
Entity lookups do not accidentally fall through to URI entries.

`Resolve(request, token)` returns an absolute mapped URI or null, checking entry
cancellation and bounded traversal. `CatalogResolver(catalog, fallback)` requires
both nonnull. It calls the fallback exactly once with request.Uri replaced by the
mapping when found, otherwise the original request; preserves Referrer, Kind,
lexical identifiers and operation token. A mapped resource's null result does not
retry the original address. Sync/async use their corresponding fallback methods
and transfer its returned ownership unchanged. Parse graph cycles are fatal
`CATALOG.Cycle`, limits `CATALOG.Limit`, malformed catalogs `CATALOG.Syntax`, denied
children `CATALOG.ResourceDenied`; syntax/denial/limit failures use
XmlParseException with the bounded diagnostic snapshot. Catalog resolution itself
never opens mapped resources.

The precedence/profile source is [OASIS XML Catalogs 1.0, sections 6–7](https://www.oasis-open.org/committees/entity/specs/cs-entity-xml-catalogs-1.0.html).
The accessible search extract was checked; full-page retrieval returned HTTP 520.
Implementation review must retain the named 1.0 profile rather than assert 1.1
conformance. Ordering, budgets and eager graph construction above are explicit
Xemele decisions.

## XInclude lane

`Process` and `ProcessAsync` return an owned XIncludeResult. Success=true implies
a nonnull complete output Document; failure implies Document=null. Diagnostics
are immutable snapshots; IncludedResources counts successfully loaded include
resources, including text and nested resources, not catalogs or fallback nodes.
Failures can report successful acquisition count without exposing the working
tree. Import the complete input into a fresh private document, retaining required
namespace/ID metadata, and expand there. Input handles, content, metadata and
revision stamps remain unchanged on success, rejection, cancellation and resolver
exceptions. No in-place API. Reject unresolved references/incomplete attributes
as `XINCLUDE.Incomplete`. Caller excludes concurrent input mutation during capture.
The result cannot claim original validation/source-completeness provenance after
transformation; validation of the result is a separate operation.

Recognize only namespace `http://www.w3.org/2001/XInclude`. Default parse is xml;
text inserts one text node, never interprets markup, respects declared encoding
(M2 supported labels, default UTF-8), BOM consistency, XML-character validity and
line normalization. parse=xml uses strict ParseOptions (Recover=false), with
explicit DTD/Resolver options; syntax permission does not grant I/O. The inclusion
resolver comes from XIncludeOptions.Resolver; included entity resolver comes from
ParseOptions.Resolver and shares aggregate accounting. Validate both Limits records. For every shared
quota field, effective limits are the smaller finite value from XIncludeOptions.Limits
and ParseOptions.Limits; null is unlimited and never overrides a finite value.
Apply this effective snapshot to included parser inputs, output construction and
a single external resource/byte/expanded-character ledger shared by entities and
includes, including nested async paths. DTD defaulting does not reset that ledger.
Decode errors are resource
errors; cancellation and quotas are not. No unsupported encoding silently falls
back. Unsupported accept/accept-language negotiation is explicit
`XINCLUDE.Unsupported`, not silently discarded.

Resolve href against effective ancestor xml:base, initially options.BaseUri;
final returned SourceUri governs recursive bases and locations. Fragment in href
is fatal syntax; xpointer is separate. Empty/missing href is same-document XML
selection and requires xpointer; text with empty href is fatal. Same-document
selection does not invoke the resolver. Selection uses the pre-expansion captured
source; active identity includes final URI plus canonical selected element-child path
(or canonical root/item set), never object identity from a fresh parse. For input
without an absolute base use an operation-local source identity. Normalize ID and
element-path selections to the same canonical path. Disjoint same-resource
subtrees are legal; an active selection containing the include site or an active
ancestor selection is `XINCLUDE.Cycle`.
For different resources, import selected items then recursively expand them.
Default XML selection imports document element plus surrounding comments/PIs,
excluding declaration/DTD. Invalid top-level multiplicity/text is fatal
`XINCLUDE.Structure` rather than producing a malformed document.

Subset: one NCName shorthand, `element(id)`, `element(id/1/2)` and absolute
`element(/1/2)` with positive one-based element-child indexes (comments/text do
not count). Shorthand/ID use unique xml:id or verified DTD ID metadata, never an
arbitrary untyped attribute named id. Unmatched selection is a resource error;
ambiguous ID is fatal. XPath `xpointer()`, xmlns(), ranges, points, compound scheme
sequences and attribute/namespace selections are `XINCLUDE.Unsupported` and never
interpreted as a different expression.

One direct xi:fallback is allowed; multiple fallback, orphan fallback, bad parse,
invalid pointer grammar and include cycles are fatal. Fallback replaces include
with fallback children only on nullable resolver denial, internally detected
load/decode/XML-parse failure, or empty supported selection. Resolver exceptions,
I/O exceptions thrown by a caller resolver/stream, operation cancellation,
quota failures, bad options, unsupported features and structural fatal errors
propagate/reject without fallback. Fallback descendants are expanded normally;
unselected fallback is not traversed. Successful fallback adds an owned warning
`XINCLUDE.Fallback`; no fallback yields failure diagnostics
`XINCLUDE.ResourceDenied`, `XINCLUDE.ResourceError` or `XINCLUDE.Selection`.
Fatal codes also include Syntax, Cycle, Limit, Structure, Incomplete, Unsupported.
Process returns structured module rejection; no partial output escapes. Argument,
callback, caller I/O and cancellation exceptions propagate unchanged.

FixupBaseUris/FixupLanguage default true. Add/adjust xml:base on each inserted
root element to preserve its effective absolute source base; add xml:lang to
preserve differing inherited language, including empty string to clear language.
Namespace bindings required by imported names are preserved. With fixup=false,
that fixup is intentionally omitted; it does not change fetch-time source bases.
Async awaits resolver/stream operations, with cancellation at entry, each external
step and bounded traversal; no spans survive awaits.

The XML/text/fallback/fixup basis is [W3C XInclude 1.0 Second Edition, sections 3–4](https://www.w3.org/TR/xinclude/).
The child-sequence subset is [W3C XPointer element()](https://www.w3.org/TR/xptr-element/).
The finite supported subset and exception/result policy are Xemele decisions;
this is not a claim of full XPointer or XInclude conformance.

## BCL lane

Exact constructors: XemeleXmlReaderAdapter(Xemele.Streaming.XmlReader reader,
leaveOpen=true) and XemeleXPathNavigatorAdapter(XmlDocument document), namespace
Xemele.Interop. The reader is forward-only, starts Initial, maps accepted tokens
to BCL node types, reaches EndOfFile then Closed, and exposes depth with root=0.
Empty element produces one Element and no EndElement. Declaration maps to
XmlDeclaration; comments, PI, CDATA, text and whitespace remain their BCL kinds.
DTD start maps DocumentType; interior DTD framing/declaration tokens are consumed,
not invented BCL element events. Strict underlying failure propagates and sets
Error. Names/values/attributes are owned copies before the next underlying read.
NameTable interns exposed names only within the adapter.

Properties and methods are listed exactly in Interop.cs.fixture. GetAttribute
and string MoveToAttribute misses return null/false without moving; indexed
access outside [0,AttributeCount) throws ArgumentOutOfRangeException. xmlns
bindings appear as BCL attributes with the XMLNS namespace. Attributes preserve
source order, with namespace declarations in declaration order after ordinary
attributes (documented loss of original interleaving). MoveToFirst/NextAttribute
moves within the current element; MoveToElement restores the element. Attribute
Depth is element+1, ReadAttributeValue exposes one Text child at element+2 then
returns false. Read from an attribute/value advances to the next element-stream
event. AttributeCount/GetAttribute remain available while on an attribute/value.
ReadAsync delegates real underlying ReadAsync; overlap is InvalidOperationException.
LookupNamespace uses in-scope bindings including xml. CanResolveEntity=false and
ResolveEntity throws InvalidOperationException; expanded entity content remains
visible. No validation engine is added. Binary/chunk custom optimizations are not
promised; base BCL behavior applies. Close is idempotent; leaveOpen=false disposes
the underlying reader, true preserves it. Underlying reader always preserves
caller-owned primary stream. Disposed reader follows BCL Closed positioning.

Navigator captures an immutable read-only XPath data-model snapshot at creation,
root positioned at Document. Text/CDATA adjacency coalesces as prior M4 navigation;
attributes exclude xmlns; namespace axis includes in-scope bindings/xml and obeys
Local/ExcludeXml/All. MoveToId uses xml:id/verified typed IDs. Move failures preserve
position. Clone has independent cursor/shared snapshot. IsSamePosition/MoveTo
recognize only adapters sharing the same captured snapshot (clones), false for
unrelated snapshots. CanEdit=false; mutation methods throw NotSupportedException.
Reject incomplete input at construction. Inherited Select/Evaluate use BCL XPath
semantics over this adapter; Xemele's own XPath engine and its limits remain
separate. Namespace iteration order is deterministic ordinal prefix order.
BaseURI preserves captured effective base where present, otherwise empty string.
No schema-validation annotations, editable navigator or arbitrary external node
bridge is promised. Memory belongs to construction/snapshot, never borrowed spans.

## CLI lane

M0 internal partial CommandRegistry.RegisterXmlCommands registers explicit static
handlers from M6-owned partial files. Existing sorted registration/dispatch stays
unchanged. No assembly scanning, reflection, dynamic code, DI discovery, runtime
plugins or later HTML/schema dependencies. Later modules implement their existing
optional hooks. This is the AOT-safe registration contract; the runtime CLI owner
must verify an actual native publish/smoke, not infer it from this compilation.

Commands: parse, format, xpath, validate-dtd, canonicalize, catalog, xinclude.
Each accepts `--input PATH` or stdin when absent; `--output PATH` or stdout when
absent. parse emits compact XML; format indented XML; xpath additionally requires
`--expression EXPR`, emits scalar lexical value or each selected node serialized
in document order, one result per line. validate-dtd requires `--dtd PATH`, emits
no stdout on success. canonicalize defaults inclusive/no comments and accepts
`--exclusive`/`--with-comments`. catalog requires `--catalog PATH`, `--uri URI`,
optional `--system-id ID`/`--public-id ID`, outputs mapped absolute URI; no mapping
is invalid input. xinclude processes XML into serialized transformed document.
`--base-uri ABSOLUTE_URI`, `--allow-root DIRECTORY`, `--catalog PATH` and
`--allow-dtd` are explicit grants/options on applicable commands. Local access
is confined to allow-root including real-path symlink confinement; primary input,
output and explicitly named catalog/DTD are invocation files, not implied grants
for arbitrary nested references. No network grant in M6. Diagnostics to stderr,
UTF-8 without BOM output, deterministic invariant culture. Help is code 0.
Unknown/duplicate/inapplicable options and missing required args are code 2.

Codes: 0 success; 1 XML syntax/validation/query/resource/unsupported-input
rejection; 2 invocation errors; 3 unexpected internal failure. IOException on
explicit invocation files is code 2; resource-processing rejection is code 1;
unexpected resolver/internal exceptions are code 3. Cancellation is code 3 with
concise cancellation diagnostic. No stack trace unless an explicitly documented
debug option is later approved. Standard input/output remain open. Format and
include output must be buffered or staged so rejection writes no partial target;
file replacement is atomic after success. All commands demonstrate independent
prior-module use before samples compose them.
