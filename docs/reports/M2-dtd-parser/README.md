# Incremental DTD parser

The approved internal parser/result/request declarations are implemented without
resolver, decoder, DOM or core dependencies. The common-only sample compiles the
verified model/framing/value helpers and the parser directly. Each supplied source
has an independent scalar cursor, mapping mode, byte EOF, final flag and active
name. External callers retain their parent suffix and resume it after child EOF.
Internal replacements are already declaration-processed: numeric-reference CR/LF
is not normalized again. Raw character sources normalize literal CR/CRLF once.

Owned declaration scratch consists of lexical token strings and a reusable
builder, bounded by normalized declaration TokenCharacters and checked UTF-16
capacity. Comments, PI data, ignored bodies and whitespace have no accumulated
body storage. No whole external source, borrowed character owner or mapping owner
survives Append. Strings/tokens are explicit scratch allocations; model names,
values, particles and snapshot arrays are durable output allocations. No custom
hash, SearchValues or SIMD optimization is introduced: scalar cursor, validation,
quota and state transitions remain the straightforward correctness baseline.

Defaults remain raw. Internal-subset completion checks references resolvable in
its known table without applying defaults or charging application expansion;
syntactically valid unresolved names defer to the caller's effective table.
Independently parsed external subsets defer that table-dependent check because
internal declarations can override every local external declaration. Application
uses the verified value normalizer against the caller's effective table. Model
merging, resolver opens/bytes, primary DOCTYPE framing and general-content entity
composition belong to later composing owners, not this parser.

The owned verifier preserves raw logs/counts/API inventory/reference provenance,
compares independently compiled approved vs actual parser members, runs current
cumulative checks and audits changes against the claim. Allocation probes separate
first use, warm no-persistence scan, owned output and retained-model estimates.
Benchmarks cover whole/chunked small/large grammar and warm borrowed scalar scans.
Timing requires the coordinator's exclusive grant; x64, Windows and AOT remain
unrun local limitations, and there is no full XML composition benchmark yet.
