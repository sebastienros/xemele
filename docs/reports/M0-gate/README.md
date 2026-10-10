# Foundation checkpoint

Run `python3 -B docs/reports/M0-gate/verify.py --fetch-libxml2` with the pinned
.NET SDK 10.0.400 on PATH and explicitly provisioned native toolchains. Omit the
fetch flag when the exact libxml2 checkout already exists. Rust uses the locked
offline cache. `CMAKE` may name the exact CMake 3.31.6 executable. Native builds
assert Apple Clang 17, SDK 26.2, arm64/macOS target 15.0 and Rust 1.95.0.
Use `--output artifacts/M0-gate/rerun` for integration checks that preserve the
committed checkpoint. MTP requires local IPC permissions.

The verifier removes generated bin/obj outputs for all tracked managed projects,
checks integrated prerequisite ancestry, then runs clean bootstrap, actual MTP
and controlled failures, compiled contracts/API comparisons, focused offline
corpus checks, actual three-engine managed-client/process checks, and unchanged
tree/input correctness/allocation/ownership checks. It records each actual exit.
No nested full contract/corpus preservation suite repeats the bootstrap checks.
No new benchmark timings are taken.

`checkpoint/checkpoint.json` indexes snapshots by exact path, source revision,
Git blob and SHA-256. Approved decisions, declaration/usage fixtures, goldens,
corpus source/fixtures/license metadata and build/native configuration inputs are
copied under `checkpoint/archive/`. Historical experiment, ShortRun and native
evidence is indexed at its exact original path/blob/hash without rewriting it.
Fresh native provenance and independently expected real responses are retained
under `checkpoint/references/`. The verification transcript identifies the fixed
source revision; the later evidence commit records results without pretending
that its own hash was tested.

M0 ships no runtime parser or public APIs. Counts describe infrastructure or
nonpackaged declaration models, never product conformance. Reference whitespace
loss and xmloxide external DTD/parameter entity/relative resource-context gaps
remain explicit unsupported outcomes; xmloxide CRLF values and libxml2 namespace
IRI errors remain observed differences. None narrows Xemele requirements.
Normalizer coverage is limited to the approved foundation signature family;
future owners expand probes before adding uncovered APIs. Synthetic timing data
and architecture decisions remain provisional; composed XML workloads and
cross-architecture release measurements belong to later owners. Hosted CI on
Windows/Linux/macOS has not been executed by this local checkpoint.
