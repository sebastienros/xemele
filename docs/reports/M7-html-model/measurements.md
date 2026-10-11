# Prepared HTML model measurements

Measured revision `2ae7416e1d7eb96a453fbb5afdbefda3ffe02114`, prepared manifest
SHA256 `f9ae44ac95240605a71fa98123fd2d6685b073db2712162efb6675192a8f74a9`.
Durable exclusive lease `m7-html-model-2ae7416` granted at main
`f0838aeb1b46a9313f65efe126417698d15787e5`; release committed at
`8cfc3968557f86a235e5997f96c896eb4ffdda64` before raw inspection.
Exactly one `python3 -B docs/reports/M7-html-model/run_measurements.py --exclusive-grant`
completed: allocation, benchmark, initial identity, final identity and wrapper
exit 0; tracked identity/product groups killed/reaped/inspected, no children.
Exact-wrapper positive/unknown-environment-negative/final-positive controls passed.
Closure includes 3642 source/configuration/contract/task/corpus/generated inputs,
43 binaries and 3852 SDK/runtime files, host, full opaque effective environment,
per-key digests, explicit SHLVL=1 and retained measurement-session identity.

Actual SDK 10.0.400; explicit actual runtime .NET 10.0.11; macOS 15.8.1,
Apple M4 Pro Arm64. Tiering, PGO and ReadyToRun disabled. NoEmit prepared suite
performed no builds or generated child projects. ShortRun has one launch,
three warmups and three measurement iterations. Only this host is measured.

| Allocation domain | Numerator bytes | Denominator | Interpretation |
| --- | ---: | ---: | --- |
| Cold first named-table lookup | 35744 | 1 operation | Current-thread static table/first-access setup, excludes process startup |
| Positive allocating byte-array control | 1048 | 1 operation | Measurement detects allocation |
| Borrowed exact + longest/attribute + foreign probes | 0 | 4096 iterations per batch | 16 batches for each of 8 inputs; 128 batches, three calls/iteration |
| Existing node remove/reinsert | 0 | 4096 iterations | Prewarmed node/list capacity, no persistence |
| Uppercase/mixed query + unsuccessful removal | 0 | 4096 iterations | Explicitly prewarmed; 3 calls/iteration |
| Owned 32-element document graph | 2498400 | 100 documents | 24984 B/document; includes permanent nodes/views/lists/names/attributes |
| Owned attribute replacement snapshots | 4800 | 100 replacements | 48 B/replacement; immutable tree-owned record |
| Inclusive retained whole heap | 4794592 delta | 100 retained documents | Before 428648, after 5223240; includes array and runtime roots, not isolated bytes/document |

Borrowed cases: short amp hit, longest CounterClockwiseContourIntegral hit,
shared-prefix notin hit/notit miss, maximum-plus-length unknown, non-ASCII miss,
ambiguous amp= attribute candidate and empty span. Exact entries and all expected
scalar sequences independently pass the exhaustive authoritative 2231-entry test.
Foreign length-dispatched equality is the simple baseline; no delimiter scanner,
SearchValues, hash or custom SIMD is needed. No UTF-8 conversion is introduced.

| Exact input | Length-filtered ordinal linear baseline ns | Borrowed binary ns |
| --- | ---: | ---: |
| amp; | 330.78 | 25.77 |
| CounterClockwiseContourIntegral; | 28.94 | 24.88 |
| é; | 1533.06 | 19.13 |
| notin; | 1051.21 | 24.87 |
| notit; | 1757.91 | 25.20 |
| 33 z characters plus semicolon | 1463.16 | 26.93 |

All 12 BDN reports succeeded, MemoryDiagnoser reports no managed allocation for
both methods. Baseline data and corpus generation are outside timing; results are
consumed. Binary search bounds attacker-controlled candidate work logarithmically
without allocating or hashing. Timing is informational ShortRun evidence, with
wide 99.9% confidence intervals (especially the longest unknown binary case);
there is no release throughput, repeatability, x64 or complete HTML-engine claim.
Bounded longest-prefix/foreign lookup has direct allocation coverage; timing above
measures exact named lookup only. The future tokenizer/tree-builder/engine owners
must confirm composition. Owned graph and inclusive retained costs are deliberately
separate from borrowed primitive budgets. Raw logs/results remain ignored under
artifacts/M7-html-model and are not committed.
