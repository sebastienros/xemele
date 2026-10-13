# M9 release freeze

The release retains the managed net10.0 product integrated at
6657e8e0ab0f34602a5040a04ee0d8fa197f70b1. No public additions or renames
are required by M9. `release-inputs.json` identifies all 55 API fragments,
1,430 member declarations, exact reference revisions, and the retained local
M8 package/manifest. The existing XML, streaming, XPath, DTD/canonicalization,
catalog/XInclude/adapters/CLI, HTML/CSS and three schema profiles remain the
capability inventory. Module inventories under tests/corpora and the frozen M8
inventory supply detailed scope. Their historical planned labels are design
metadata; actual shipped status comes from verified task evidence and compiled
API checks, never from an upstream parity claim.

Required release lanes, with their original acceptance unchanged:

| Lane | Required result |
| --- | --- |
| M9-api | Actual compiled signatures vs all approved fragments; compatibility with first preview; exception/nullability/ownership review and runnable migration examples. |
| M9-performance | The exact targets and protocol in docs/performance.md; every primary/reference pair and confidence interval; allocations, retained bytes and RSS separate. |
| M9-distribution | Deterministic packages, license/notices, Source Link, fresh package consumption and trim/NativeAOT execution on required platforms/architectures. |
| M9-robustness | Applicable full corpora, differential explanations, cancellation/resource/streaming cases, fixed fuzz replays, coverage and sustained campaigns in docs/testing.md. |
| M9-gate | Integrated combination, all earlier gates, complete advertised inventory and zero unresolved mandatory release blockers. |

Platform requirements are Windows, Linux and macOS correctness, with actual x64
and arm64 execution before release. No new six-cell OS-by-architecture matrix
is invented: each OS and both architectures must be represented, with package,
trimming and AOT smoke results identifying their actual RID and limitations.
Compile-only cross-target publication supplies no execution credit. Current
local execution is macOS arm64 only.

Performance retains DOM >=0.80x each native/Rust and >=1.00x BCL, forward read
>=1.00x BCL, serialization >=0.90x each native/Rust, XPath >=0.80x each native/Rust,
retained DOM <=0.75x BCL, streaming <=8 MiB at1 GiB under fixed bounds, zero
steady concrete traversal allocations, and primary fixture floor0.50x unless
explicitly accepted. Confirmed >10% primary regressions block release; >5%
changes need investigation/repeat confidence evidence. Timing release evidence
requires a stable dedicated machine. Local shared-host timing is informational.
All benchmark classes retain [MemoryDiagnoser, ShortRunJob], with no job CLI flag.
Allocation evidence needs no PARK, host lease or exclusivity; meaningful counters,
controls, identities and finite cleanup remain required. No foreign project
communication or synchronization is permitted.

Robustness retains the90% core parser/decoder branch-coverage target (documented
exclusions/review),24-hour coverage-guided campaign per major parser/validator,
progress before EOF, and bounded1 GiB streaming. Short deterministic mutation
runs are useful smoke evidence but cannot satisfy a24-hour campaign. Reuse valid
source-applicable prior evidence; repeat only changed or invalid workloads.

The155 exact human-approved XSD original nonpasses remain144false/11null with
zero original/gate pass credit; annotZ002 remains true. Separate advertised
application profiles and reference limitations never alter original results.
Existing exact-ID/hash approvals are preserved, not broadened. New exclusions,
failed thresholds or unavailable checks require a concrete documented human
release decision; missing evidence stays outstanding. No unsupported parity,
publication, or stable-release verification is inferred from M8 completion.

Raw generated outputs are transient under the current human artifact policy.
Keep concise actual outcomes, commands, identities, confidence tables and limits;
inspect then delete raw logs/results. Retain necessary unpublished packages and
manifests. No remote push or publication is authorized.
