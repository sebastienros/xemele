# Roadmap and feature goals

Status: proposed, 2026-10-09. Implementation has not started.

## Mission

Create a managed C# implementation of the XML and HTML capabilities represented by libxml2 and xmloxide. Preserve familiar operations, parser modes, and data-model concepts while following .NET library conventions. Target `net10.0` only. Deliver correctness and diagnostics first, then optimize against reproducible baselines.

The implementation should own its parsing, tree, query, and validation engines. Native libraries and `System.Xml` serve as test and benchmark references; they are not the underlying implementation. Ordinary BCL services such as streams, encodings, collections, and Unicode primitives remain appropriate dependencies.

## Compatibility contract

1. Specifications define standards-mode behavior.
2. A pinned libxml2 build defines the legacy compatibility profile where standards leave behavior unspecified, particularly recovery and HTML parsing.
3. A pinned xmloxide revision provides additional API, feature, and regression references.
4. A mismatch between references is investigated and recorded; majority agreement is not proof of correctness.

Compatibility means documented operations and semantics, not C binary compatibility, identical numeric enum values, allocation callbacks, or exact error-message wording. No global initialization or cleanup is required. Safe defaults and managed ownership are intentional API differences.

Initial reference candidates, observed on 2026-10-09:

- [libxml2 v2.15.4](https://github.com/GNOME/libxml2/tree/v2.15.4), commit `96498992efa48d52b0e8b83058bd88dbdaf153c1`.
- [xmloxide](https://github.com/jonwiggins/xmloxide/tree/555055f1ea6bf9944488fd96415b6f03b5b43696), commit `555055f1ea6bf9944488fd96415b6f03b5b43696`.

M0 must build and record these references, including compile options and enabled features. A version label alone is insufficient to reproduce behavior. New upstream releases trigger reviewed baseline updates, not automatic changes to expected results.

The [libxml2 public-header inventory](https://gnome.pages.gitlab.gnome.org/libxml2/html/files.html) and [xmloxide module inventory](https://github.com/jonwiggins/xmloxide#module-overview) are inputs to the compatibility inventory. Upstream feature and pass-rate claims are not Xemele acceptance evidence.

## Goals and scope

| ID | Capability | Target and acceptance boundary |
| --- | --- | --- |
| G01 | XML parsing | XML 1.0 Fifth Edition and Namespaces 1.0 Third Edition; documents and explicit fragment mode; correct normalization, names, entities, and namespace constraints |
| G02 | Encodings | UTF-8 and UTF-16 first; BOM/declaration precedence and malformed-input rejection; later a documented compatibility table of additional encodings, without silent replacement |
| G03 | Tree | Document, elements, attributes, namespaces, text, CDATA, comments, processing instructions, document type, and entity-reference representation; navigation, mutation, import, clone, ID lookup |
| G04 | Parser interfaces | DOM, required SAX2-style callback API with C# delegates and reusable handlers, pull reader, true incremental push parser; synchronous and asynchronous stream entry points |
| G05 | Output | XML/HTML serialization, formatting, escaping, encoding, streaming writer, namespace repair policy; semantic round trips |
| G06 | XPath | Complete XPath 1.0 core, reusable compiled expressions, variables and namespace context, typed results, distinct attribute and namespace results |
| G07 | DTD | Internal/external subsets, parameter/general entities, defaults, attribute types, IDs/IDREFs, content models, and validation |
| G08 | Canonicalization | Canonical XML 1.0 and 1.1, Exclusive XML Canonicalization; comments options and node-set/subtree cases |
| G09 | External resources | Controlled resolution, OASIS XML Catalogs, XInclude 1.0; documented supported XPointer schemes |
| G10 | Legacy HTML | Behavior compatible with pinned libxml2 HTML parser, including recovery and serialization; distinct entry point from HTML5 |
| G11 | HTML5 | WHATWG tokenizer, tree construction, fragments, foreign content, templates, encoding, and serialization; tokenizer callbacks documented separately from final tree order |
| G12 | CSS selectors | Explicit selector inventory, compiled selectors, matching and selection; start with common selectors then expand toward pinned xmloxide coverage |
| G13 | RELAX NG | XML syntax and applicable datatype libraries; compact syntax is a separately tracked extension |
| G14 | XML Schema | XSD 1.0 Second Edition including datatypes, imports/includes, derivation, identity constraints, and validation diagnostics |
| G15 | Schematron | A declared ISO Schematron edition and query-binding profile, initially XPath 1.0; patterns, phases, diagnostics, and structured reports; no implicit XSLT2/XPath2 claim |
| G16 | .NET integration | Streams, spans, cancellation, async I/O, nullable annotations, trimming and NativeAOT smoke tests; adapters to BCL XML abstractions where useful |
| G17 | Tooling | `xemele` .NET tool with familiar parse, format, XPath, validation, HTML, and canonicalization operations; deterministic exit codes |
| G18 | Release quality | Conformance reports, reproducible performance reports, migration examples, API compatibility tracking, package validation, and security regressions |

Every inventory entry must have an upstream symbol/concept, proposed C# API, expected behavior, milestone, test IDs, and status: planned, implemented, verified, intentionally different, or deferred. This includes a review of libxml2 writer, pattern, regexp/automata, URI, XPointer, and diagnostic APIs so less visible modules do not disappear from scope. Exposed utility APIs require a concrete managed use case; internal needs can be implemented internally.

## Explicit extensions and exclusions

- XML 1.1, XPath 2.0/3.x, XSD 1.1, RELAX NG compact syntax, and broader XPointer schemes are later proposals. Selected XPath extension functions must use an explicit profile and never imply full XPath 2.0 support.
- CLR-object XML serialization is a later companion feature requiring its own contract and AOT design.
- Native C ABI exports, Python/WASM bindings, and direct porting of C memory/thread/module utilities are outside the initial managed release.
- XSLT is a separate project scope. Schematron support must therefore clearly state which query bindings are implemented directly.
- Browser DOM behavior, JavaScript execution, rendering, and byte-for-byte lexical round trips are outside this parser/toolkit scope.

These exclusions limit the initial contract, not the long-term ability to add compatible features.

## Implementation tracking

The authoritative work breakdown is in [individual task records](tasks/README.md). Each task records status, acceptance criteria, dependencies, source/test ownership, API outputs, and evidence. These plans do not imply implementation.

Run `python3 tools/planning/tasks.py status` for current progress or `python3 tools/planning/tasks.py ready` for executable work. Do not add live checkboxes to this overview; workers update only their task records.

| Milestone | Additive result | Implementation plan |
| --- | --- | --- |
| M0 | Build, contracts, corpus/reference infrastructure and measured architecture decisions | [Foundation](milestones/M0.md) |
| M1 | Mutable tree, strict XML subset, basic document serialization | [Strict XML](milestones/M1.md) |
| M2 | Complete XML/namespaces, DTD parsing, entities, encodings, recovery and bounded resolution | [Complete XML](milestones/M2.md) |
| M3 | SAX callbacks, pull/push, async transport and forward writer | [Streaming preview](milestones/M3.md) |
| M4 | XPath 1.0 compilation/evaluation and conformance | [XPath](milestones/M4.md) |
| M5 | DTD validation and canonicalization | [Validation/canonicalization](milestones/M5.md) |
| M6 | Catalogs, XInclude, BCL adapters and XML CLI | [XML beta](milestones/M6.md) |
| M7 | Legacy HTML, HTML5, fragments, token callbacks and CSS | [HTML/CSS](milestones/M7.md) |
| M8 | RELAX NG, XSD 1.0 and Schematron | [Schemas](milestones/M8.md) |
| M9 | API, performance, packaging and robustness evidence for stable release | [Release candidate](milestones/M9.md) |

Every milestone is independently buildable and usable with its predecessors. Unsupported later features are explicitly rejected, not represented by shipped stubs. Each gate preserves earlier tests and never depends on a higher-numbered milestone.

Implementation concurrency follows task dependencies. HTML work may start after M3 while XML query/integration work proceeds. Schema work can start after M5 and does not need HTML. Cumulative release gates remain ordered M0 through M9. Each milestone plan lists blockers and independent lanes; ownership validation catches unordered file collisions.

Each feature supplies its own tests, API baseline fragment and evidence. New grammar states include invalid-input cases; each fixed defect gets a minimized regression. Optimizations follow correctness and report both improvements and regressions. M9 findings create narrowly owned repair tasks rather than allowing broad concurrent edits.

The first executable task is [M0-build](tasks/M0-build.json). After it is integrated, CI, corpora, reference runners and the two architecture experiments can proceed independently. Contract decisions join the experiments before M1 implementation begins.
