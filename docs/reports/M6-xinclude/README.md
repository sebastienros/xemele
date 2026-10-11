# M6 XInclude implementation and measurement boundary

The module builds private captured-source and output documents. It snapshots
input ID identity before expansion, recognizes XML and text resources, expands
nested selections and eligible fallbacks, and carries source bases/languages and
namespace bindings across selected-root boundaries. Structured rejection never
exposes the working document. Callback, stream, cancellation and cleanup errors
remain exceptions. No catalog or peer branch is required.

All allocations here have explicit operation/result owners: captured tree and ID
index, imported DOM slots/name tables, paths/cycle stack, parser/decoder scratch,
text decoding buffer and final string, resolver wrappers, diagnostic snapshots.
This module introduces no borrowed primitive advertised as allocation-free and
no SearchValues/custom scanning optimization. Pointer grammar uses direct span
comparisons; owned ID strings/child arrays persist through asynchronous expansion.
End-to-end comparisons therefore report owned DOM/resource setup and results,
not a zero-allocation parser primitive. Fixture construction occurs before timing.

The independent sample covers public XML inclusion and asynchronous usage.
Its optional measurement reports cold construction, warmed sync and warmed true
async allocations using process-wide counters, and a separate retained-one-result
GC delta; a byte-array positive control checks the counter. The retained delta
is a host observation, not a precise graph-size guarantee. BenchmarkDotNet's six
ShortRun MemoryDiagnoser cases cover short text, multilingual long text, nested
XML, selection, real async inclusion, and denial/fallback. NoEmit runs in the already pinned process; discovery
checks effective ShortRun warmup/iteration/launch counts and toolchain. No CLI job
flags or child builds are used.

Measurements require the coordinator's exact bounded lease, a fresh park from
other workers, complete freeze of source/generated/config/binary/runtime/SDK and
effective environment, and durable release before inspecting raw outputs. Only
summary tables and analysis will be committed. Raw output is inspected before
cleanup. Cross-architecture release validation remains milestone/release work.
