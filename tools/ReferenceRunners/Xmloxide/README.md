# Pinned xmloxide reference process

Reference infrastructure only: the Xemele runtime does not depend on Rust or this
runner. Run `python3 tools/ReferenceRunners/Xmloxide/build.py` from any directory
with Rust 1.95.0 installed (`rustup toolchain install 1.95.0 --profile minimal`).
The script uses the exact Git dependency commit and `cargo build --release --locked`.
Use `--offline` after provisioning the Cargo cache. Cargo.lock records every
registry checksum and the immutable engine revision. No host xmloxide package is
used. Downloaded source and compiler outputs are ignored caches; no vendor tree
is checked in. Start `artifacts/M0-references-xmloxide/target/release/xemele-xmloxide-reference`
(`.exe` on Windows). The verifier currently validates macOS arm64 only.

Rust profile options are explicit in Cargo.toml; engine default features disabled,
no engine features enabled. build.rs embeds actual rustc version, Cargo target and
repository revision. build.py refuses ambient Rust flags/wrappers/profile overrides,
verifies Cargo's actual engine source commit and clean tracked source, and records
resolved feature/dependency/license inventory, source tree and license/lock SHA256
in `artifacts/M0-references-xmloxide/provenance.json`. Cargo's `.cargo-ok` checkout
marker is the sole permitted untracked source-cache file. Dependency identities
and checksums live in the committed lock, not an unpinned system package.

Protocol: integrated v1 UTF-8 JSONL `xml.events`, `xml-1.0` profile. Unknown additive
fields are ignored, unsupported versions/operations/profiles/options/encoding hints
fail explicitly. Supports XML auto encoding through the actual pinned decoder,
or strict UTF-8 hint; invalid raw JSON wire UTF-8 exits 2 with stderr and no response
for that line. XML payload decoding errors are parse rejection. Stdout carries
only responses; framing/response bounds are measured in UTF-16 code units as in
v1. Request storage has an additional conservative 4MiB raw-byte cap; oversized
lines are drained and subsequent lines recover. Every quota is runner-failure/limit,
never an XML conformance verdict. Panic/I/O/nonzero process exits are infrastructure
failures for the integrated client.

Actual engine DOM nodes become expanded start/end/text events; namespaces omitted
from attribute events, attributes sorted by UTF-16 ordinal namespace/local name,
CDATA/text coalesced across ignored comments/PI, empty elements emit start and end.
EntityRef children are traversed, rather than duplicating their stored text value.
No synthetic engine output or upstream parser correction is made.

Supported resources: absolute-URI external general entities declared in the
internal subset, using a request-owned ParseOptions.entity_resolver closure.
Only mapped bytes are returned. No filesystem/network/default resolver is called.
Resolver bytes use the actual pinned decoder. Unmapped absolute file/network/URN
references fail resource-denied. URL canonicalization uses locked url 2.5.8;
relative URIs fail explicitly because the upstream callback has no base URI.
External DTD subsets and parameter-entity DTD declarations are unsupported and
return runner-failure/protocol. External subset recognition uses actual engine
DocumentType nodes; raw prolog-only lexical inspection is needed for parameter
entities because upstream reserializes the DTD and drops their declarations.
Inspection respects comments, PIs and quoted strings; it never scans document
text/CDATA for DOCTYPE substrings. BCL/libxml2 are needed for unsupported resources.
No fallback, source patch, unsafe code or global resolver mutation exists.

Safety configuration: recover=false, no_blanks=false, maximum depth256, name50000
bytes, entity-reference count10000. The pinned engine also enforces fixed nesting
and amplification guards. Exact pinned diagnostic patterns categorize these as
limit failures (arbitrary message substrings are not used). Decoded XML and emitted
text are capped at1048576 UTF-16 code units; response framing provides a separate
attribute/event-output bound. Upstream ParseOptions' attribute-count/text-length/
attribute-length fields are not enforced in this pinned parser: the runner does
not falsely claim they work. Caller timeouts/process isolation remain necessary
for untrusted reference workloads, as for other out-of-process engines.

Known pinned-engine differences (not corrected or hidden): literal CRLF in an
attribute becomes two spaces versus BCL one; CDATA retains CRLF versus BCL LF;
initial prolog whitespace is dropped (later misc whitespace may collapse to LF).
The verifier asserts these actual divergences separately. The known protocol XML
fixture and supported comparison fixtures still match BCL. This task does not
establish general XML conformance or parser performance.

Provenance: https://github.com/jonwiggins/xmloxide commit
555055f1ea6bf9944488fd96415b6f03b5b43696 (package0.5.0), MIT license copied in
UPSTREAM-LICENSE.txt. Dependency license/source inventory and registry checksums
are recorded as described above. Upstream source used here is reference-only;
no imported source is added to runtime/package paths.
