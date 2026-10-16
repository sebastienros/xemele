# Testing and conformance strategy

## Acceptance model

A feature is complete when its contract and tests agree, not when a port compiles. Required gates are zero failures in supported conformance profiles, zero unexplained differential mismatches, and passing resource-limit regressions. Coverage percentages supplement these gates; target at least 90% branch coverage of the core parser/decoder, with documented reasons for exclusions and focused review of error paths.

Reports distinguish discovered, applicable, passed, failed, excluded-by-profile, and infrastructure-error counts. An unimplemented applicable case is a failure. Missing test data or a missing required reference runner is an infrastructure failure, never a silent skip or green run. Expected-failure records need a reason and stable tracking ID; they are not counted as passes.

## Test layers

| Layer | Cases and assertions |
| --- | --- |
| Unit | Character/name rules, decoding, token states, namespaces, entities, tree invariants, writer escaping, XPath conversions, datatype boundaries |
| API contract | Overloads, nullability, argument errors, disposal, stream ownership, cancellation, mutation and invalid handles, concurrent read behavior |
| Conformance | Imported standards corpora with upstream metadata and per-profile classification |
| Differential | Equivalent options against pinned reference engines, including libxml2 and appropriate BCL implementations |
| Property | Parse/serialize/parse semantic equivalence, clone/import equivalence, chunk-boundary invariance, DOM/SAX/pull event equivalence |
| Fuzz | Coverage-guided malformed bytes, encoding boundaries, parser state transitions, queries, validators, and mutation sequences |
| Security/resources | Entities, deep nesting, oversized tokens, hostile names, URI cycles, resource resolution, expensive queries/schema expressions |
| Integration | CLI exit codes/output, consuming NuGet packages, streams/files, trimming/AOT, all supported platforms |

Property generators must generate both valid and intentionally invalid documents. Round-trip comparison uses a semantic tree model with explicit CDATA/entity/whitespace policies; raw serialized string equality is only appropriate for byte-exact modes such as canonicalization.

## Unit-test case isolation

Represent independent inputs and configurations as xUnit theory rows rather than loops inside test methods, so each case is reported and can be rerun independently. Use `InlineData` for simple constant cases and `MemberData` for generated combinations, shared data, or complex values. For malformed Unicode, pass numeric UTF-16 code units and construct strings inside the test to avoid changing the input or collapsing distinct rows during discovery and serialization. Loops belong in data providers, setup, stream consumption, tree traversal, allocation measurements, and stateful sequences that together form one scenario.

## Corpus inventory

- **XML and namespaces:** import the [W3C XML conformance suite](https://www.w3.org/XML/Test/). Classify by XML edition, namespace edition, validity, and validating/nonvalidating mode. Run external-entity cases through an offline fixture resolver. Do not reuse another implementation's applicable-test count.
- **Upstream regression coverage:** inventory libxml2 test/result directories and other retained reference corpora at their pinned revisions. Port semantic assertions with attribution; don't translate harness bugs into requirements.
- **XPath 1.0:** combine pinned reference cases, specification examples, and an owned inventory covering all axes/operators/functions. Test namespace and attribute identity, document order, reverse-axis predicates, NaN/infinities, conversions, variables, and namespace context. The [XPath 1.0 specification](https://www.w3.org/TR/xpath-10/) defines the data model when references differ.
- **Canonicalization:** W3C examples/vectors for C14N 1.0, [C14N 1.1](https://www.w3.org/TR/xml-c14n11/), and Exclusive C14N, plus libxml2 regressions. Cover comments, inherited context, selected node sets, inclusive prefixes, and exact UTF-8 bytes.
- **HTML5:** use [html5lib-tests](https://github.com/html5lib/html5lib-tests) for applicable tokenizer/encoding/serializer fixtures and its linked [web-platform-tests parsing suite](https://github.com/web-platform-tests/wpt/tree/master/html/syntax/parsing) for tree construction. The html5lib repository now directs tree-construction maintenance to WPT. Adapt parser-only tests and explicitly classify browser/scripting-only cases.
- **Schema validation:** import the [W3C XML Schema test suite](https://www.w3.org/XML/2004/xml-schema-test-suite/) using metadata to select XSD 1.0 cases. Add libxml2 RELAX NG and Schematron cases, then select and pin authoritative additional suites for the declared profiles for each supported profile.
- **DTD/catalogs/XInclude/CSS:** upstream regressions plus specification-based owned cases. Inventory normative requirements missing from existing corpora.

Before import, record source URL, revision/archive hash, license/attribution, local mapping, and adapter version. Keep small minimized regressions in Git; download large corpora into an ignored cache with checksums. CI provisions the cache before tests and can run offline afterward. Changes to exclusions or expected outputs receive the same review as parser code.

### .NET-only build and test prerequisites

`dotnet build Xemele.slnx -c Release` builds the managed `tools/Build` utility and
automatically prepares test data. The first build downloads the immutable W3C XML
archive into `artifacts/test-data/`, checks its pinned SHA-256, and extracts exactly
the 3,386 inventoried regular files into the ignored `tests/corpora/xml/xmlconf/`
directory. Download time/size, expanded size, entry count, paths, lengths and
fixture hashes are bounded or validated. Existing corrupt archives or fixtures
fail explicitly rather than being silently replaced. Verified caches support
offline builds after NuGet restore. `XEMELE_CORPUS_RESOURCES` remains available
for tests using another already-provisioned corpus location.

`dotnet test --solution Xemele.slnx -c Release --no-build` includes corpus
infrastructure and foundation contract tests as well as the product suites.
The SDK pin requests 10.0.100 and rolls forward to the newest installed .NET 10
feature band. Test projects and the BCL reference runner use the SDK's default
runtime selection, without project-level runtime-version pins.
Allocation-test runtime settings are declared by test projects. Protocol failure
fixtures run as actual managed child processes, including malformed UTF-8,
overflow, timeout and cancellation controls.

`dotnet run --project tools/Build -c Release --no-build -- verify` validates
corpus manifests and fixture/source bytes, validates API fragments, and compares
the runtime and foundation fixtures with the approved compiled API extractor.
`... -- test` additionally verifies discovery, report contents and deliberate
failure exit codes. Neither command requires Python, Java, native compilers, or
Git command-line tools.

### Independent-reference maintenance

Normal RELAX NG tests consume compact recorded Jing/libxml2 outcomes, not native
binaries. Each snapshot pins engine revision/version and runner-source hash,
hashes every input inventory, schema, instance and resource, and must account for
every expected outcome. Product assertions still use upstream authoritative
expectations; the existing 15 libxml2 limitations remain explicit disagreements,
not agreements. Live BCL comparisons continue to execute on each test run.
The pinned canonicalization reference is restored as a test-only NuGet dependency,
not loaded from an assumed global cache path. Its existing NuGet vulnerability
warnings remain visible; these packages are not runtime product dependencies.

The weekly/manual `.github/workflows/test-data.yml` workflow installs the external
toolchains, verifies pinned upstream downloads, rebuilds independent engines, and
regenerates snapshots using `tools/Validation/reference_snapshots.py`. It executes
both live and recorded comparisons before proposing a draft PR for changes.
Raw build identities remain workflow artifacts. Snapshots are never generated
from Xemele output. Set `XEMELE_LIVE_REFERENCES=1` and the identity/Java variables
exported by `prepare_references.py` to run the live RNG checks manually.

The weekly refresh deliberately does not follow mutable "latest" URLs or advance
upstream pins, fixture expectations or limitation ledgers automatically. New
upstream revisions require reviewed provenance/inventory changes first. Legacy
Python discovery, signature-mutation and exhaustive CLI maintenance checks remain
in that workflow; native AOT release packaging remains a separate toolchain.

## Differential harness

Run isolated reference processes through a versioned input/output protocol. Inputs specify operation, bytes/encoding, parser options, resources, and limits. Outputs describe acceptance, diagnostics categories, tree/event/query results, or serialized bytes.

Tree comparison preserves expanded names, attribute values, namespace bindings where relevant, text, comments, processing instructions, document type, and ordering. It normalizes only differences documented for that test. SAX text segmentation can be normalized; dropping all whitespace or namespace declarations cannot be a universal normalization rule.

Compare diagnostic categories and source locations, not exact message strings. Compare recovery trees only within a named compatibility profile. Verify references actually exercised the requested options/features. Persist disagreements as minimized fixtures and classify them against the standard.

Native/Rust reference tools must run with resource/time limits and no ambient network access during corpus tests. The BCL is an additional reference only where behavior overlaps; it is never the sole oracle for new Xemele semantics.

## Adversarial and streaming cases

- Entity amplification, recursive entities, parameter entities, external DTD defaults, and expansion limits at/above boundaries.
- Deep nesting, huge attributes/text, millions of distinct names, malformed UTF sequences, surrogate boundaries, and numeric-reference overflow.
- Controlled checks proving that parse/validate/include cannot read a local file or access a network resource without a resolver grant.
- Catalog/XInclude/schema cycles, relative URI resolution, base URI propagation, and cumulative external byte budgets.
- XPath/pathological schema inputs, bounded recursion/work, and deterministic cancellation.
- Every byte split for short documents, including inside BOMs and UTF sequences; random multi-chunk partitions; empty chunks; finalization with incomplete tokens.
- Events produced before EOF and a 1 GiB generated input consumed without a DOM. Memory checks use fixed depth/name vocabulary/token bounds, so the measurement tests input-length independence.
- SAX delegate/handler equivalence, callback order, empty-element start/end pairs, namespace scope, defaulted attributes, CDATA/text boundaries, and optional unregistered callbacks.
- SAX borrowed-data lifetimes, sequential callbacks during async I/O, callback exception propagation, cancellation, rejected context reentrancy, and absence of successful end-document notifications after failure.

Select a coverage-guided .NET fuzz runner compatible with net10.0; keep the fuzz entry points callable independently of that tool. Commit seeds and minimized regressions. Run a bounded fuzz smoke job on changes, rotating targets nightly, and at least a 24-hour campaign per major parser/validator target before its first stable release. Zero findings is evidence, not a proof of safety.

## CI and release gates

Pull requests: Release build, analyzers/API checks, unit/property/regression tests, supported conformance cases, deterministic differential fixtures, and basic package consumption. Correctness matrix: Windows, Linux, macOS; include x64 and arm64 runners as available and verify both architectures before a release.

Nightly or manually dispatched runs: full differential corpora, fuzz campaigns, large-stream/resource tests, trimming/AOT samples, and controlled performance jobs. Do not make noisy shared-runner timing results hard merge gates.

Release: all applicable cases for every advertised profile pass, no unresolved reference disagreements, package/API docs validate, all fixed fuzz cases replay, required architecture checks pass, and the performance report is reviewed. Publish exact suite revisions and denominators alongside results.
