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
