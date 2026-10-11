# String and byte performance rules

Required reading before changing runtime parsing, tokenization, matching, tree storage, or serialization. Applies to `net10.0`; the [performance plan](performance.md) defines product targets. These are implementation requirements, not claims of measured performance.

## Ownership and allocation boundaries

- Process transient input using `ReadOnlySpan<byte>` / `ReadOnlySpan<char>` and slices. Avoid `Substring`, `Split`, `ToArray`, `ToString`, interpolation, LINQ, boxing, and captured delegates inside token loops when a borrowed view or direct comparison suffices. Any necessary allocation on such a path must have a documented owner/lifetime and allocation measurement.
- Keep byte input as bytes where the operation permits it. Recognize ASCII markup/literals without converting each token to a string. Decode when Unicode semantics or the public character callback require it. Do not round-trip strings through UTF-8 simply to share a helper without measuring the conversion cost.
- Allocate owned names/text only at explicit persistence boundaries: DOM storage, interning a previously unseen name, owned results, or diagnostics. Probe existing interned names using borrowed input where possible; do not allocate a temporary lookup string on every hit. Bound document-local tables.
- Preserve source-location accounting, Unicode validation, namespace rules, and normalization when bulk-scanning. Fast paths cannot skip checks required by the active grammar state.
- Borrowed data must not survive callback return, buffer refill, pool return, or its owning operation. `ReadOnlyMemory<T>` is a retainable view, not ownership of a rented buffer. Async state retains an owner plus offsets; recreate spans only while accessing valid storage. Avoid keeping a whole document buffer alive for a tiny retained value.

## SearchValues and bulk scanning

For repeatedly searching a fixed delimiter/character set, evaluate cached `System.Buffers.SearchValues<byte>` or `SearchValues<char>` with span search operations. Create the search object once per immutable set, normally in a `static readonly` field; never per token. Its setup cost is separate from the scan's allocation budget. [SearchValues documentation](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.searchvalues-1?view=net-10.0)

```csharp
private static readonly SearchValues<byte> AttributeStops =
    SearchValues.Create("\"&<\r\n"u8);

// Candidate delimiter scan for a double-quoted attribute, not a full validator.
int stop = remaining.IndexOfAny(AttributeStops);
```

Use state-specific sets and preserve the handling of every character at the returned position. Compare cached SearchValues against a simple loop, `IndexOf`, and small-set `IndexOfAny` overloads. A tiny set or short token may favor the simpler operation. Prefer SearchValues where representative benchmarks demonstrate a benefit; record a measured reason when retaining a simpler path. Do not add custom SIMD before evaluating the runtime primitives.

## Immutable literals and tables

Use UTF-8 literals for fixed byte tokens and constant `ReadOnlySpan<T>` properties for suitable immutable tables:

```csharp
private static ReadOnlySpan<byte> DeclarationPrefix => "<?xml"u8;
private static ReadOnlySpan<byte> Utf8Preamble => [0xEF, 0xBB, 0xBF];
```

These patterns can use compiler-emitted static data instead of managed arrays. Prefer collection expressions for constant tables. A span return type alone does not prove allocation freedom: runtime-valued elements, mutable span results, and different element types/lowerings need inspection. `static readonly` values are not compile-time constants. For .NET 10, verify actual generated code instead of applying the article's older-runtime limitations to all primitive types. Avoid rebuilding constant bytes through `Encoding.GetBytes` or `ToArray`. Check allocation measurements and, for lowering-dependent optimizations, IL/JIT or AOT output. [Andrew Lock's article](https://andrewlock.net/removingbyte-array-allocations-in-dotnet-framework-using-readonlyspan-t/)

## Matching a fixed ASCII vocabulary

For known tokens (DTD keywords, encoding labels, HTML names, query keywords), compare spans directly. Baseline a length-dispatched `SequenceEqual` implementation before adding hashing. For larger hot vocabularies, evaluate build-time generated constants/matchers with byte and character paths, inspired by [StackExchange.Redis AsciiHash](https://github.com/StackExchange/StackExchange.Redis/blob/main/eng/StackExchange.Redis.Build/AsciiHash.md). Its useful techniques are length dispatch, precomputed token metadata, and hash filtering followed by equality. Adoption of a generator/package is a separate decision; this guide does not add a Redis dependency.

Always verify length and full equality after a hash match. A hash is not token identity. Keep matching rules explicit: XML names/keywords are case-sensitive; ASCII-insensitive matching is allowed only where the relevant HTML or encoding rule specifies it. Do not allocate upper/lower-case copies, fold punctuation with an unchecked bit trick, or treat Unicode case-insensitivity as ASCII equivalence.

Use a contiguous input slice when available. For a token crossing chunks, copy only a bounded candidate into scratch space and verify its end/delimiter; a known maximum plus overflow detection prevents accepting a truncated prefix. Precomputed maximum lengths can guide bounded buffer sizing. Never read beyond the valid span, regardless of padding or vector width.

Specialized fixed-token hashes are not automatically suitable for attacker-controlled intern/dictionary keys. Dynamic tables need collision resistance or bounded worst-case behavior and adversarial tests. Generated matchers stay internal, deterministic, and reproducible; test unknown tokens, shared prefixes, collision candidates, case variants, and non-ASCII input against a simple reference matcher.

## Scratch buffers and output

- Use a small, explicitly bounded stack buffer for short-lived scratch work. Never size stack allocation from unchecked input or repeatedly `stackalloc` inside a loop; include recursion/depth in the stack budget.
- For larger transient buffers, consider pooling with an explicit owner and `try/finally` return. Use only the initialized logical slice. Clear sensitive data when required; do not unnecessarily clear unrelated capacity. Pool misses and growth allocate and must be measured. Durable DOM storage must not rely on caller-managed pool returns.
- Avoid repeated concatenation and rescanning. Write escaped runs directly to a destination span, buffer writer, or buffered stream; size or grow buffers with checked arithmetic. Materialize a final string once when the API promises an owned string. Streaming output must not build the entire result first.
- Keep no-error paths free of formatted diagnostics. Benchmark invalid-input paths separately and bound their allocations. Do not introduce an allocation-saving optimization that increases retained memory without reporting that tradeoff.

## Evidence required for hot-path changes

The target for scan/match/slice primitives and borrowed callback dispatch is **0 B managed allocations per steady-state operation** after documented initialization, with bounded preallocated state. This does not claim that DOM creation, unique-name interning, I/O setup, or owned result construction is allocation-free. Account for each separately. Pool warm-up and static initialization cannot disappear from the report.

Use the task's owned benchmark path and record evidence in its task record:

1. Compare the straightforward baseline and proposed optimization with identical semantics on the pinned Release .NET 10 SDK/runtime. Consume results; keep fixture generation outside timing.
2. Cover short/long inputs, hits/misses and their positions, realistic delimiter density, ASCII/multilingual content, distinct/repeated names, chunk boundaries, and malformed inputs. Fixed-token tests include maximum length plus one and common prefixes.
3. Report throughput/latency and allocation bytes/op using BenchmarkDotNet memory diagnostics. Measure cold initialization, warm steady state, and retained memory separately. For async work, per-thread allocation counters alone are insufficient.
4. Include an end-to-end representative parser/query/output workload when it exists. Before it exists, label microbenchmark results provisional and schedule confirmation in the task owning composition. Record CPU/architecture, runtime settings, corpus, and raw results; validate hardware-sensitive claims on x64 and arm64 before release.
5. Keep a correctness oracle and focused allocation regressions for paths promised allocation-free. Include invalid Unicode, collision/unknown tokens, truncated input, pool ownership, and culture independence as applicable. Inspect generated code when a compiler/JIT lowering is the reason for the claimed benefit.
6. Benchmark classes should have the `[MemoryDiagnoser]` and `[ShortRunJob] attribute. 
7. Don't commit benchmark result logs and outputs, only summary tables and analysis.

A feature task touching these paths is not verified without its relevant allocation evidence. Noisy timing on shared CI is informational; use the performance plan's repeatability/regression rules. If an optimization does not produce a repeatable benefit, retain the simpler implementation and record the result. Apply these gates as each milestone is built, not only during M9.

## Approved M0 choices

[Foundation decisions](decisions/M0/foundation.md) record the integrated tree and
corrected input experiment revisions, memory/speed tradeoffs and allocation
boundaries. Character scanning plus bounded strict byte decoding, durable owned
DOM text, borrowed ordinal intern probes, cached state-specific multi-stop scans,
static immutable literals and length-dispatched equality are the baseline choices.
They do not replace per-task correctness/composition/measurement requirements.
The superseded original 62 input/rejection cases are not decision evidence.
