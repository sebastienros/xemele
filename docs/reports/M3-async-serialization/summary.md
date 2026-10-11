# Async DOM serialization branch evidence

Runtime implementation: `17478155d8996c39bbd0e9f4b166bfc829c436f7`.
Final fixture/probe source: `7cee8bb2f2f154ff74bf5aeadacd0964e1f13a8a`.
Integrated prerequisites: claim `df540e28cacfb147d661829910b15225195c794a`, base
`ad8728b994bfb0089a2fbbb3f8e80c9e0bfad607`, verified writer integration
`080b518581f5e6439df81f2851de2a67d27d56a1`.

The additive Task APIs preserve the synchronous DOM serializer's behavior,
including complete mixed-content lookahead, namespace repair ordering, XML 1.0
regenerated declarations and omission of parsed DOCTYPE/standalone metadata.
Traversal has no depth-proportional call stack. Actual output uses the verified
internal WriterOutput async transport with an operation-owned rented byte buffer;
there is no full-output string, synchronous transport, explicit caller flush,
caller disposal, or Task.Run bridge. Namespace/scope/frame storage is owned by
the operation and grows with active depth and current element metadata.

122 unique relevant cases passed: 33 original async DOM cases, the additional
scratch-return case, 50 earlier serialization cases, 19 async writer cases and
19 sync writer cases (including its allocation regression outside any lease).
`correctness.xml` records the original 102; `lifecycle-preservation.xml` records
34 async DOM plus 19 sync writer while the added cleanup source was uncommitted;
`cleanup-final.xml` independently verifies that addition at the final committed
fixture revision. Passing earlier cases were preserved without broad repetition.
All Release builds completed with zero warnings/errors on SDK 10.0.400 and
.NET 10.0.11 Arm64. A first test build's synchronous xUnit assertion errors were
fixed before the implementation commit. Benchmark restore's unavailable audit
feed required process-local NuGetAudit=false; no shared build files changed.

The unchanged coordinator-provided approved API extractor matched 670 compiled
members against a temporary duplicate-checked union: existing 667 plus two methods
and the separate static class. The official inventory wrapper failed first on
missing offline `xmlconf/eduni/errata-2e/E14.dtd`; coordinator integration will
provision the exact corpus. The standalone sample compiled and executed both APIs
against an independent late-text indentation golden.

## Allocation and retained memory

`allocation.json` contains the actual default-runtime observation, accepted by
the coordinator after the initial wrapper failed its sandboxed ps cleanup check.
The probe child was waited/reaped but its return code was not printed: zero is
inferred from the complete passed JSON and the reviewed program return branch.
The parent wrapper's actual exit was 1; no benchmark ran in that initial lease.
The failure and elevated no-child check are preserved in
`measurement-harness-failure.json`. No allocation observation was repeated just
to repair this reporting problem.

- Actual borrowed ReadScalar closed-delegate calls: 48,000, zero managed bytes in
  both current-thread and precise processwide counters after documented warm-up.
  The allocating owned-copy positive control observed 512,000 bytes.
- Warm whole serialization including operation/scope setup: 138,400 bytes per
  100 documents for both 16-character and 65,536-character text inputs.
  This is 1,384 bytes/document, not a zero-allocation serialization claim.
- Whole-output string/byte positive control: 34,322,352 bytes per 100 large
  documents; results consumed.
- Genuine suspended async-only composition: 297,776 bytes per 20 documents,
  including transport and continuation costs, measured processwide.
- Actual rented byte array length 8192; largest observed write 2051 bytes in
  immediate and delayed transports. No output-sized scratch buffer.
- Retained heap delta 16,808 bytes after eight 8 MiB text serializations with
  fixture/DOM construction outside the comparison. A completed Task was kept
  alive while its caller document weak reference was collected after unwind.
- First whole-serializer call observation: 10,480 bytes. Fixture/reflection and
  primitive-owner initialization preceded it; this is not pristine cold-process
  initialization evidence or a promise about pool misses.

The scalar loop and bounded delimiter search retain M1's straightforward
implementation. No novel SearchValues/token-matching optimization or throughput
benefit is claimed. Invalid scalar/lexical/binding cases use the correctness
oracle and independent goldens. The standalone probe keeps its zero-allocation
primitive and positive-control gates for future regression checks.

## Benchmark composition

The separate BDN-only lease used the same frozen code/binaries and exactly one
CLI Dry InProcessEmitToolchain job: two methods times three corpora, six cases.
`benchmark-exits.json` records actual child exit 0, wrapper exit 0, scopes [6,6],
no timeout or invalid scope, and absence of the child process group. The fixed
cleanup wrapper prints the child exit before its group absence check; no external
ps is needed. The lease was released before results inspection.

The CSV/log reports preserve all MemoryDiagnoser results. Inclusive operation
allocations were sync 9.57 KiB long ASCII /13.79 KiB multilingual /13.79 KiB short,
and async 13.85 KiB for each corpus. These include setup/scope/framework/runner
initialization and are distinct from isolated primitive or steady operation
attribution. Timing and ratios are informational and unusable for comparative
speed/regression claims under the unrelated shared-host workload. Repeatable
x64/Arm64 release qualification remains with M9.

Frozen SHA256:

| Binary | SHA256 |
|---|---|
| Runtime | c7a9d2cfef0295a5cdee7124d10f9bef5424cb6bf301a0c4c296958edd87d86b |
| Probe | 0e3157676293afe83077b4df62ff6c639d7d56feaf0b0418a6e219e583007c7e |
| BDN | 55479378f8193ba78cdd8a63a4ae179a2fce07f1a0db0fd1fb964698c06ebd81 |
| Tests | 520ec12628668dc978f05791a036fc0d8108738ff72c2fa2312eb386a2198ef2 |

Planning validation passed all 147 tasks after implemented evidence was prepared.
Only this task's owned paths and JSON changed. Integration revision remains null.
