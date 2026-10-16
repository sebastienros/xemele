# Architecture

Xemele is a managed XML/HTML toolkit targeting `net10.0`. The
[module specifications](specs/README.md) define detailed API and behavior
contracts; source and executable tests establish actual behavior.

## API and ownership

The package and root namespace are `Xemele`. Public APIs follow .NET naming,
including `Xml`, `Html`, `Dtd`, `Uri`, and the `Async` suffix. Namespace
qualification or aliases distinguish names that overlap BCL types.

`XmlDocument` owns an arena of nodes, attributes, and namespace declarations.
`readonly struct XmlNode` handles retain document identity and a stable,
never-reused slot. Default handles represent missing navigation. Detachment
preserves identity; cross-document insertion requires explicit import.
Names are interned per document, and persistent values own their text instead
of retaining caller input or rented decoding buffers.

HTML has its own `HtmlDocument`/`HtmlNode` representation so WHATWG names,
comments, control characters, template contents, and foreign content do not
weaken XML invariants.

Documents support concurrent reading only without mutation. Parser contexts
and readers are single-consumer. Compiled queries and schemas are immutable
and reusable with independent evaluation state.

## Parsing and streaming

```text
string / character span / encoded bytes / Stream
                       |
           decoding + source locations
                       |
        resumable XML scanner and grammar
                       |
          borrowed semantic notifications
            /           |           \
     DOM builder    SAX callbacks    pull reader
```

The diagram describes shared semantics, not allocated event objects. Bounded
incremental decoding feeds character grammar. Parser state can suspend inside
tokens and resume without replaying accepted events. SAX and pull input do not
build a hidden DOM or retain all input. Large text may arrive in chunks.

Borrowed spans expire on callback return or the next reader operation. Persistent
values require copying. Asynchronous I/O retains owners and offsets, not spans
across awaits; it does not use Task.Run as an I/O substitute. Caller streams stay
open, while operations dispose resources transferred by resolvers exactly once.

HTML5 uses a separate tokenizer and tree-construction algorithm. Its callbacks
are lexical tokens, not a traversal of the corrected tree. Legacy libxml2-style
HTML recovery is a distinct entry point and profile.

## Errors and resources

Strict XML factories throw structured parse exceptions. Diagnostic-returning
factories expose completeness and well-formedness separately; recovery never
labels repaired input as originally well formed. Argument, I/O, cancellation,
and callback failures retain their exception behavior.

Options and finite resource limits are snapshotted at operation entry. DTD
processing is prohibited by default. External loading requires an explicit
resolver; enabling syntax grants no filesystem or network access. Resolution,
catalogs, inclusion, and schemas share explicit ownership and aggregate-budget
rules, without ambient global loaders or caches.

## Query, validation, and output

XPath expressions compile once and evaluate against explicit namespaces,
variables, functions, limits, and cancellation. Results distinguish node sets,
strings, numbers, and booleans, with owned snapshot membership and values.
CSS selectors operate over both XML and HTML with graph-specific name matching.

Schema compilation is separate from instance validation. Compiled schemas are
immutable; diagnostics, identity tables, and work ledgers belong to a run.
Validation does not mutate instances or publish a PSVI. Schema datatypes and
regular expressions follow their specifications, not assumed CLR equivalence.

XML/HTML serializers and the forward XML writer preserve their separate output
rules. Canonical XML produces exact UTF-8 bytes according to the selected W3C
algorithm; signing is outside its scope.

## Repository layout

`src/` contains the runtime and CLI. `tests/` contains behavioral, conformance,
differential, contract, fuzz, and package-consumer checks. `samples/` holds focused
executable examples, `benchmarks/` reproducible workloads, and `tools/` corpus,
reference-runner, API, and packaging utilities. `docs/specs/` is the product
contract reference. Generated outputs belong under ignored `artifacts/`.

The SDK, dependencies, and compiler settings are centrally pinned. Builds use
nullable annotations, analyzers, deterministic output, XML API documentation,
and warnings as errors. The CLI uses static partial registration for AOT.
Runtime dependencies remain separate from test and reference-engine tooling.
