# Provenance allocation and preparation evidence

The repair adds one document-local Int64 content revision and a source-completeness
boolean. The existing structural revision remains unchanged. Each XPath metadata
object adds one Int64; the XPathItem struct remains 56 bytes on this Arm64 host.
The revision is stored in the existing metadata object, with no new object type in
the product. Whole-document clone carries incomplete-source provenance.

The first exclusive sequence at prepared revision 390055f failed its primitive
exact-zero gate. Actual .NET 10.0.11/Arm64 output recorded the following inclusive
current-thread totals. SDK 10.0.400; inherited runtime defaults; 1024 warmup calls.
No bytes were subtracted or treated as a passing zero gate.

| Domain | Total bytes | Operations | Interpretation |
| --- | ---: | ---: | --- |
| Cold fixture setup | 12704 | 1 | DOM plus fixture initialization |
| Borrowed text value mutation | 152 | 65536 | Failed exact-zero gate; origin unattributed |
| Owned unprefixed attribute items | 4194304 | 65536 | 64 B/op, existing object plus new revision field |
| Owned namespace items | 4194568 | 65536 | 64 B/op plus inclusive 264 B residual, unattributed |
| Prior five-reference metadata shape | 3670016 | 65536 | 56 B/op; local layout surrogate, not prior full product call |
| Positive escaped 1024-byte array | 1048 | 1 | Allocation counter sensitivity control |
| Retained 4096 namespace items | 525368 heap delta | 4096 | Full GC heap-domain observation, not exact object-size accounting |

The retained array was allocated before the first forced GC snapshot; the delta
includes metadata and heap/GC effects while the array and fixture remain live.
The document itself is retained by item identities. No document-heap footprint
measurement was inferred from the field sizes. The revision remains monotonic
through repeated same-value assignments; source completeness does not recover
through mutation.

The allocation process persisted JSON then threw `Allocation controls failed`,
underlying phase exit -6, wrapper exit 250. Benchmark timing was never started.
Initial/final identity checks passed (148 source/config/generated entries,
43 binary entries, 3852 entire pinned SDK/runtime/host entries), no own-group
children remained. The coordinator durably released the lease before inspecting
raw output. Root and worker inspected actual JSON/log; raw remains transient.

A bounded harness-only correction separates the already observed owned/retained
phases from a primitive-only run: 32 identical warm and measured batches of 512
mutations (16384 operations each), distribution arrays preallocated before all
counters, with whole-loop inclusive counters including batch sampling/storage.
No threshold or subtraction is introduced. Exact launch settings now explicitly
set DOTNET_TieredCompilation=0, DOTNET_TieredPGO=0 and DOTNET_ReadyToRun=0. Changed
settings and batching do not identify the original residual's cause or erase it.

The counters-disabled primitive control passed on actual .NET 10.0.11, final
revision 32772 and measured consumption 402726912. Three declared BDN cases
remain [MemoryDiagnoser, ShortRunJob] with toolchain-only InProcessNoEmit, one
launch/three warmups/three iterations; no CLI job flags. Product source and binary
are unchanged by this correction; successful functional tests remain applicable.

Generated Release IL was inspected using the owned probe's --il mode. Both
AdvanceContentRevision and NotifyMutation contain only field/property calls,
integer operations and branches; no newobj or box. XmlNode.set_Value forwards to
SetValue, whose successful path constructs a value-type XmlTreeMutation (newobj
on that struct is value construction, not managed object allocation) and passes
it by readonly reference. TreeValidation.Text uses ReadOnlySpan and Rune decoding;
its reference-type newobj sites and SetValue/Value exception constructors are
only invalid-input branches. There is no box on these successful paths. This is
source/IL corroboration, not substitute evidence for a steady-state zero counter.

Timing on this shared Apple Arm64 host is informational. Broader hardware/AOT/
trimming/platform release gates remain M9 work. No runtime behavior changes or
performance comparisons across different ownership domains are claimed.

## Corrected exclusive sequence

Prepared revision 5c1fad88269bb7e02d98ca504c39a8551e99e122 and manifest
f5c23fa1a87b22cf6deb6f4ffa37674d0097fc9845d36972a18464003daaf1a1
covered the 148/43/3852 source/binary/full-SDK-runtime-host closure. Exactly one
bounded primitive-only process and one three-case BDN process ran. Both phase
exits, wrapper exit and initial/final identity exits were zero; own-group no-child
cleanup passed. The coordinator released durably before raw inspection. Product
source/binary was unchanged from the original successful functional checks.

| Corrected standalone scope | Inclusive bytes | Operations |
| --- | ---: | ---: |
| Cold fixture setup | 12664 | 1 |
| Warm mutations | 0 | 16384 |
| Measured mutations, including sampling/stores | 0 | 16384 |

All 32 warm and all 32 measured batch counters were zero. Warm/steady consumption
was 134291456/402726912, with final revision 32772. This establishes the scoped
steady primitive gate under the disclosed settings; original 152 B remains
unattributed. No ownership, positive or retained counter phases were repeated.

| BDN operation | Mean ns/op | Displayed allocation | Actual inclusive GC bytes | Actual operations | Derived B/op |
| --- | ---: | --- | ---: | ---: | ---: |
| Borrowed value mutation | 4.876 | dash | 0 | 134217728 | 0 |
| Owned unprefixed attribute item | 25.500 | 64 B | 2147553464 | 33554432 | 64.00208068 |
| Owned namespace item | 27.087 | 64 B | 2147552120 | 33554432 | 64.00204062 |

Actual runtime .NET 10.0.11 (10.0.1126.37416), Arm64 RyuJIT armv8.0-a,
concurrent workstation GC. Explicit tiered compilation/PGO/ReadyToRun disabled.
CPU/runtime/OS/full environment identity is frozen by the manifest. ShortRun
confidence intervals are broad: mutation ±1.643 ns, attribute ±8.143 ns and
namespace ±6.102 ns at 99.9%. Timing is informational. Inclusive GC byte numerators
are retained even when the display is a dash; small owned-domain residuals are
not subtracted. Prior 64 B and surrogate 56 B observations support the new Int64
metadata footprint. Retained heap delta stays a separate GC-domain observation.

Original failed preparation manifest SHA-256: feef635f2237f26b707a27a3b2285b3b9b3548a4e4cd93952d2cb516548bd66c.
Corrected benchmark execution elapsed 30.08 seconds (three successful reports).
Root independently inspected both failed and successful raw counters/logs, and
ACKed their exact summary and authorized removal only after durable evidence.
