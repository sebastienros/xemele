# Tree allocation measurement repair

The original integrated framing check at `5109fe930fe45f7b485d4ed36c39b9c09b5ba625`
reported Tree traversal allocation 1064 B, expected exact zero, with 2094/2095
passing cases. Relevant raw history is copied byte-identically with SHA256 records.
Its cause remains unresolved. Verified counter controls and runtime issue evidence
support isolated instrumentation as a rationale, not causal proof for this failure.

The existing Fact invokes one real child then one positive child without retries.
Each child preserves the exact document/element/10-text-child/attribute/namespace
fixture, one warm 1000-loop Walk and one measured 1000-loop Walk. Both counters
and every traversal/namespace/attribute/borrowed intern operation share one
synchronous NoInlining helper. Both consumed sums must be 21000 and attribute
identity must hold. A positive child executes the identical work and additionally
escapes a 37-byte array on each iteration; the real child asserts exactly zero.
Setup, output, provenance and assertions occur outside the measured interval.
Default JIT/GC/test class parallelism remain unchanged. No subtraction, threshold,
extra warm-up or production edits are involved.

The task-owned child protocol derives from the verified PI design. It requires
strict required JSON, capability/parent/known arguments, owned canonical artifact
paths, current host/compiled full revision/test and product DLL/native runtime/
config/helper IL hashes, exact one-Fact XML and consumed-work reports. Deadlines
kill the process tree; raw stdout/stderr/request/report/XML/exit artifacts survive
failure. The verifier archives all raw children before parsing report JSON.

Run verify.py with explicit branch/audit base/counts and a fresh unique prefix.
It pins a fresh complete BCL output before the historical M1 solution build and
checks all 242 full-source/hash reference records, mandatory joint MTP/direct
suites, API/discovery/planning/ownership checks and bounded protocol negatives.
Diagnostics execute original/equivalent/actual-helper shapes once each with
meaningful consumed work; no historical cause or concurrent allocation guarantee
is inferred. All actual outcomes will be recorded separately at committed source.
