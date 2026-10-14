# Affine ASCII byte mappings

The retained implementation is at `e158264925539ab53b1683b3f8d145a979bd09c7`. The lane remains in progress: the latest complete 20-case ShortRun measures **1.879x BCL latency, 0.876x allocated bytes and 0.902x summed retained managed DOM heap**. Maximum time is 3.166x (feed-small); maximum allocation is 1.224x (SOAP). The latency target still fails. All default features remain enabled.

## What changed

The owned DOM byte decoder recognizes bounded ASCII runs with the runtime's byte-range search, converts a whole run with Ascii.ToUtf16, and counts physical CR/LF with a two-character search plus the existing scalar location tracker. ASCII CR and LF still map one byte to one UTF-16 unit, so the run can include line breaks. It returns the first original byte offset rather than writing one long offset per character.

Only validated declarations, supported single-byte/UTF-8 encodings and empty decoder carry permit this path. Non-ASCII, pending carry and UTF-16 input use the existing explicit mapping decoder. Detection, declaration validation, strict Unicode checks, first-excess locations and terminal/cancellation behavior remain intact.

The parser's internal ASCII entry accepts a proven starting offset. Private helpers calculate offset + index; ordinary callers retain ReadOnlySpan<long> mappings and their direct validation loop. No mapping view escapes a call, and persisted locations copy coordinates by value. Existing pooled offset storage remains for fallback. This reduces transient writes and fragmented conversion work; it does not remove durable source locations, validation or provenance. The experiment measures the combined path, not an isolated percentage of CPU spent writing offsets.

## Measurements and corrections

Pinned environment: Apple M4 Pro ARM64, macOS 15.8.1, SDK 10.0.400, runtime 10.0.11, BenchmarkDotNet 0.15.8, concurrent workstation GC; tiered compilation, PGO and ReadyToRun disabled. All results are local informational measurements on a shared desktop.

An initial mapping-wrapper prototype showed 10.6-14.3% gains in the earlier alternating harness. That harness loaded only the baseline in another assembly context. Binary fingerprints confirmed the intended baseline, but standalone default-context suites showed a smaller effect: 4.1% raw / 6.0% BCL-normalized improvement for that prototype. Those larger screening gains are not headline evidence. The paired harness now loads both variants consistently; standalone default-context runs remain primary.

The wrapper prototype also slowed character-input DOM screening by about 4-6% overall and 13% on SVG. The retained version restores span arguments and direct untrusted validation loops. Its four-fixture standalone screen is near baseline for character DOM (0.992x raw / 1.007x normalized), with no aggregate SAX slowdown (bytes 0.988x / 0.980x; chars 0.971x / 0.973x). These small screening differences are not release claims.

Against the preserved pre-change DLL in a separate default-context suite, the final complete matrix gives 0.9665x raw latency and 0.9753x BCL-normalized latency geomeans. Changes are not uniform: configuration improves about 15% and build 9%; the business fixture is slower in this window and remains an investigation. Compared with the older 1.89x checkpoint, the latest 1.88x BCL headline is only a small change. Multiple local windows and GC/process variability matter.

One intermediate full run had text warmups near 20 ms but measurements at 28-32 ms. An explicit two-case repeat returned 19.64 ms; the subsequent complete matrix measured 19.62 ms. The anomalous raw run is preserved and not silently replaced to claim parity.

| Fixture | Xemele ms | BCL ms | Time ratio | Allocation ratio |
| --- | ---: | ---: | ---: | ---: |
| feed-small | 0.053 | 0.017 | 3.17 | 1.02 |
| svg-small | 0.194 | 0.103 | 1.88 | 0.72 |
| deep-medium | 0.533 | 0.251 | 2.12 | 1.02 |
| entity-medium | 4.355 | 1.746 | 2.49 | 0.71 |
| xhtml-medium | 3.051 | 1.344 | 2.27 | 0.97 |
| build-medium | 3.601 | 2.445 | 1.47 | 0.66 |
| soap-large | 13.049 | 5.456 | 2.39 | 1.22 |
| configuration-large | 17.947 | 14.975 | 1.20 | 0.70 |
| business-large | 74.451 | 46.825 | 1.59 | 0.91 |
| text-large | 19.618 | 17.216 | 1.14 | 1.00 |

## Verification

- 10,803 unit, 920 conformance and 618 differential cases passed. New tests cover CRLF, representational line/column limits, byte anchors after Unicode and across decoder pages, and 0 B warmed ASCII decoding and mapped dispatch.
- All ten managed fixture digests in three held/released cycles, compacted-heap positive controls, independent rooted layouts and native payload/release controls passed. Retention uses graphEstimate = heldHeap - releasedHeap; forensic GetTotalMemory output is not the ratio. Totals: 72,935,800 bytes Xemele / 80,903,968 BCL.
- Five fresh 1 GiB streaming modes passed. Whole managed-heap peaks are 133,120–150,008 bytes against 8 MiB. A retained-buffer control reports about 9.5 MB and fails the oracle as expected. The runner intentionally exits 0 for successful negative detection; an orchestration expectation of exit 1 was corrected and the control rerun. All positive-mode checks had already passed.
- Streaming mutation control: 97 operations, zero failures.
- 1,430 extracted public API members remain byte-identical. The standard pipeline separately fails on the pre-existing header-only M8-datatypes API fragment; no standard-pipeline pass is claimed.

The native timing workers and corpus are unchanged. Their pinned earlier parse/lifecycle timings remain reference data in progress.md; native timings were not rerun for this checkpoint and are from another measurement window. Native memory sidecars were rerun. No native failure/provenance contract equivalence is implied.

## Reproduction and raw evidence

The runner project and tests are in the task-owned paths. Build Release with RuntimeFrameworkVersion=10.0.11. Set DOTNET_ROOT/PATH to the pinned SDK, XEMELE_PERF_ROOT to the worktree, DOTNET_TieredCompilation=0, DOTNET_TieredPGO=0 and DOTNET_ReadyToRun=0.

```text
dotnet --fx-version 10.0.11 docs/reports/M9-xml-performance/Runner/bin/Release/net10.0/XmlPerformanceRunner.dll bdn --iterationTime 250 --artifacts artifacts/M9-xml-performance/lean-qualified-bdn
python3 docs/reports/M9-xml-performance/default_context.py --before-dll artifacts/M9-xml-performance/before-affine/Xemele.dll
python3 docs/reports/M9-xml-performance/memory.py
```

Build MemoryWorker first. Streaming reproduction uses StreamingMemoryRunner with each of sax, sax-async, pull, pull-async and push, followed by 1073741824 and a JSON report path; negative uses 8192 and expects runner success plus withinLimit=false. The benchmark-only fingerprint command records version and private method sizes. The saved baseline DLL hash is `7485aef8fc0420be129c33a8048c4678ff0ed2cb932d895212dfe87d980f151f`; source version 6b277f48f has runtime identical to the 3b8fc9414 checkpoint.

Raw artifacts: lean-qualified-bdn/, affine-before-default/, affine-after-default/, affine-lean-text-repeat/, affine-probe-before.json, affine-probe-after.json, affine-lean-probe.json, memory-current/report.json, lean-stream-*.json and api-lean-qualified.txt. Failed/intermediate attempts remain ignored artifacts. Parser namespace/attribute work and representation costs remain optimization candidates; no profiler percentage is assigned to them.
