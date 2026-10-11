# Catalog profile and measurement boundaries

The runtime implements the frozen Xemele OASIS XML Catalogs 1.0 profile. Loading
creates an immutable eager graph using only the caller's resolver. Lookups never
acquire resources. Exact and already-normalized public identifier lookups return
stored immutable URI references; rewrite results and public identifier/URN
normalization create owned results. CatalogResolver transfers its fallback result
without changing resource ownership or final SourceUri.

The task-owned probe compares a straightforward linear exact match against the
catalog's exact lookup at 8 and 128 entries, measures misses and owned graph
construction, and consumes every result. Fixtures and requests are prepared
outside timing. Every benchmark has MemoryDiagnoser and ShortRunJob. No CLI job
flags are used. The prepared NoEmit suite has eight cases and never builds child
programs. The separate probe distinguishes cold fixture/first graph, positive
allocation control, warmed exact/miss/public/URI matching, owned rewrite/public
normalization, owned graph construction, and inclusive retained heap over 100
graphs. Cold/static effects and retained heap are not steady matching budgets.

Borrowed ordinal comparisons are the straightforward implementation. No fixed
multi-delimiter SearchValues scan or generated/hash vocabulary matcher exists in
catalog lookup; ordinal equality and prefix comparisons are sufficient. XML
recognition and decoding remain the verified parser's responsibility. Timing is
informational on this arm64 host; no x64 or release portability claim is made.

Measurement scripts are adapted narrowly from M5-dtd-instance-provenance support.
They pin SDK 10.0.400, actual runtime 10.0.11, disable tiering/PGO/ReadyToRun, freeze
source/configuration/fixtures/generated outputs/binaries/SDK/runtime/host and
effective environment, bound exact phases, kill only owned process groups, check
for surviving children, and perform final identity checking in finally. They
require an explicit coordinator grant; --exclusive-grant alone is not authority.
Only summary tables and analysis are committed after raw-result inspection.

OASIS XML Catalogs 1.0 section 7.1.1 treats a supplied normalized public identifier
that differs from a SystemId publicid URN as an error. Catalog lookup uses strict
CATALOG.Syntax rejection; no optional recovery API is introduced. Equal or absent
PublicId unwraps the URN into public-only lookup. URI-kind lookup ignores these
entity identifiers. The coordinator verified the official search extract at
https://www.oasis-open.org/committees/entity/specs/cs-entity-xml-catalogs-1.0.html;
full page retrieval returned HTTP 520, so no full-page retrieval is claimed.

Native quota composition uses the integrated M6-parser-budget dependency. The
character root uses XmlParserCore plus DomSink; children use ByteDomAdapter with
the same OperationBudget and final SourceUri. Native scalar, reference expansion,
name, token and depth accounting is authoritative. A separate transport budget
counts every acquisition attempt and capped byte read, including the one-byte
EOF probe, while parser-side transport quotas are null to avoid double charging.
There is no duplicate decoding, lexical reference scan, or manual scalar count.
Catalog/group traversal uses an explicit stack, preserving document order,
scoped bases/preferences and cancellation even with a larger permitted XML depth.

The full effective child environment is hashed opaquely. SHLVL is explicitly
stabilized in the plan so a sole-command shell launch and a multi-command capture
cannot change the manifest. --identity-control executes the identical initial and
final child identity launches without allocation or benchmark phases; an unknown
environment variable must still cause rejection. Controls are performed before
requesting an exclusive measurement lease.

Graph loading and lookup also use explicit frames. Loading retains original
reference acquisition order, active fragment-free final identities, scoped
entry traversal and native budget accounting. It fully parses and disposes each
child stream before acquiring its descendants; no stream is retained by graph
frames. Immutable graph nodes reference children by integer index. Lookup uses
32 stack-allocated value frames, with an operation-local pooled array for a deeper
graph; the first pool miss is separately measured, and warmed traversal must be
zero allocation. No recursive loader/delegate/next call remains. Cancellation
checks cover each sibling enqueue and stable merge-sort comparison/copy; delegate
ties retain document order. Four 4,000-resource acyclic chain cases exercise
next/system/public/URI delegation with raised depth/document caps and explicitly
null shared quotas, both successful exact depth and structured one-over rejection.
