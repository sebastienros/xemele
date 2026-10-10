# Bounded offline resource resolution

Actual public API is `docs/api/M2-resolver.txt`: 29 additive members in Xemele,
including XmlResourceKind, lexical request identifiers, nullable resolver refusal,
mandatory operation tokens and Stream/Uri resource construction. The independent
Resolver model has 114 members: Common81 already shipped, resolver29 implemented
here, XmlDtdProcessing4 deferred to M2-entities. No future option/model API is
shipped and no DTD/parser composition is required by this deliverable.

`Xemele.Resolution.Core.XmlResolutionSession` is internal sequential composition.
Use one session throughout a parse, optionally supplying the existing shared
OperationBudget. Open/OpenAsync accept absolute requests plus a source location;
CreateRequest explicitly resolves lexical system IDs against a declaration's final
URI. Success owns a handle; denial owns a diagnostic; resolver exceptions retain
identity. Parent cursors/handles stay caller-owned and explicit. SourceUri on each
handle is the final absolute URI, used for locations/nested referrers; cycle keys
are ordinal AbsoluteUri excluding fragments. Inactive repeats are allowed and
charged again; redirect cycles and unreadable returns are counted then disposed.

Read/ReadAsync borrow a destination only through return and request <=4096 bytes.
Remaining aggregate allowance bounds each read; exact quota permits only a
single-byte EOF probe. EOF succeeds, an extra byte increments BytesRead then
returns Count=0 with XML.ExternalBytesLimit, never decoded/persisted by consumers.
OperationBudget retains accepted charges only, while BytesRead includes the
terminal rejected probe. No counter refund occurs at source disposal. An exhausted
open allowance is checked before invoking user code. Quota failure makes the
session terminal. Empty reads do no I/O; caller cancellation is checked even then.
XML.ResourceDenied, XML.ResourceCycle, XML.ExternalResourcesLimit and
XML.ExternalBytesLimit are owned diagnostic codes.

Returned wrappers transfer ownership; failed acquisitions stay resolver-owned.
A handle/session disposes every owned wrapper exactly once, including async
rejection/cancellation. Wrapper sync/async disposal share an atomic claim. Sessions
reject reentrancy and work after disposal. They are sequential, not concurrent.
Stream/cancellation exceptions propagate; secondary cleanup errors do not replace
the primary exception. Disposal without another error still reports its first
failure after attempting all handles. Primary caller streams are not passed to
this session: deliberate non-owning adapters are required if a resolver grants
caller-owned content. No resolver, file access, network fetch, cache or global
state is installed implicitly.

## Verification

Pin SDK10.0.400/.NET10.0.11 via DOTNET_ROOT and PATH; set
DOTNET_CLI_USE_MSBUILD_SERVER=0. Run `python3 -B docs/reports/M2-resolver/verify.py
--report-prefix UNIQUE`. `--audit-base` supports integration audit roots, with no
worker-branch name requirement; default is the exact coordinated claim2e3d601.
Sources must be committed. Per-attempt durable summaries/compressed command logs
and XML results live here; raw output/packages/reference copies live in ignored
artifacts/M2-resolver. Fresh complete BCL output is copied before any solution
build; all242 reference records must match the full source build/hash, and the
pinned DLL must remain unchanged. Independent model ABI, full actual API union,
M1 model/protocol/foundation preservation, full actual runtime/conformance/
differential tests, fresh local package consumer, planning14/validate and all
committed/staged/working/untracked ownership/diff audits are required.

Benchmarks are prepared/discovered by the verifier. Execute timings only after
an explicit coordinator exclusive-host grant. ResolverBenchmarks compares direct
and bounded borrowed reads at 1/4096-byte chunks, async completion and operation
setup/open/read/disposal; MemoryDiagnoser and ShortRunJob are on the class.
Warm sync read has a 0 B allocation regression assertion. Sample records first
session allocation, warm setup and controlled1000-session GC retained deltas;
GC deltas are noisy and async per-thread counters are not process evidence.
No parser performance claim, search/token optimization, cross-architecture,
Windows/Linux or AOT claim follows from these provisional resource microbenchmarks.
