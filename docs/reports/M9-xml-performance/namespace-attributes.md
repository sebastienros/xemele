# Namespace and attribute experiments

The retained checkpoint before these experiments is e158264925539ab53b1683b3f8d145a979bd09c7. BCL parity remains unmet. These trials do not remove namespace, quota, recovery or source-location behavior.

## Calibration and initial cache

A four-entry inline prefix cache used a bounded bucket and mandatory exact ordinal matching. Additions and scope removal invalidated it, and recovery used the original lookup. Its tests passed 10,812 cases including 0 B warmed resolution, collisions, shadowing, entities and unknown/reserved prefixes. The initial balanced screening measured 1.0064x prior latency geomean. The cache was removed from the current experiment, but that tiny difference is not sufficient to establish a regression.

The balanced harness A/A control, using the same DLL for both variants, measured after/before fixture ratios from 0.8808 to 1.1069, geomean 0.9860. Those context/process/GC effects are not isolated here. Screening differences of only a few percent cannot establish adoption or rejection. Raw data: namespace-cache-paired.json and namespace-aa-control.json under artifacts/M9-xml-performance; the original prefix-cache patch is preserved there too.

## Active default namespace

The narrower trial maintains an index of the active default declaration. It updates when an empty-prefix declaration is added, and restores the previous default only when scope removal removes that declaration. Unprefixed elements read it directly; unprefixed attributes still have no namespace. Prefixed lookup and interning/error ordering remain unchanged. All binding mutations were inspected; other clears occur only during terminal disposal.

BCL v10.0.11 similarly stores the active default in its XML context and reads it directly for unprefixed elements. This supports the pattern, not a claim of equal performance. [ElementNamespaceLookup and declaration handling](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlTextReaderImpl.cs#L4630).

The trial passed 10,814 unit, 920 conformance and 618 differential cases, including namespace undeclaration, nested shadowing, entity scopes and 0 B warmed default resolution. The valid standalone suites measured after/before geomeans 0.9902x raw and 1.0415x BCL-normalized. They do not establish useful performance improvement. The default-index runtime and temporary tests were reverted; their patch is preserved in artifacts/M9-xml-performance/default-namespace-index.patch (SHA-256 673d951adadf89fc745fd3ef697fc197d2ac12f856068bd09b4807e0fa5b0447). No retained parser change or performance credit.

The first standalone helper run had an invalid baseline build: BenchmarkDotNet found duplicate generated project names, emitted NA and still returned process success. That attempt remains in default-namespace-context/. The helper now gives generated projects unique names and requires 20 non-NA rows before printing PASS. The corrected run is in default-namespace-context-fixed/.

## Duplicate-attribute filter prototype

BCL uses a small bitmask prefilter before potential duplicate checks and retains exact comparisons. [AddAttribute precheck](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlTextReaderImpl.cs#L7214). A benchmark-only 64-bit variation prepares distinct buckets, deliberate collisions, a true duplicate and equal local names in different URIs. Setup verifies both the result and intended bucket distribution. Count 4/16/64; no runtime parser change or measured gain is claimed yet. Source: benchmarks/Xemele.Benchmarks/M9-xml-performance/AttributeDuplicateFilters.cs.

The corrected default_context.py run completed 20 valid before and 20 valid after rows in default-namespace-context-fixed/. Original failed artifacts remain. Both namespace representations are out of the retained runtime.

## Attribute results and production decision

The 24 mixed-key microbenchmarks completed with all setup oracles and 0 B timed allocation. Distinct sets: pairwise/filter 3.80/3.71 ns at 4 attributes, 72.4/19.5 ns at 16, and 1194/72.0 ns at 64. Deliberate collisions retained exact checks and could be slower; equal local names in different URIs remained valid. These artificial distributions demonstrate the mechanism, not the frozen corpus.

A streaming XML attribute census found at most four ordinary attributes per frozen element: build 3, business/configuration 4, SVG/XHTML 2, others 0-1. On a common vocabulary, the cheaper first-character mask measured 1.83 ns versus 3.64 ns pairwise at count 4, with 0 B. At 16 and 64 it measured 38.8 and 625.7 ns, versus 70.7 and 1201 ns pairwise. Nine common-vocabulary cases completed.

The parser trial used the first-character mask only for at least three ordinary attributes, retained exact comparisons on collisions and original error ordering, and bypassed duplicate work for fewer than two ordinary attributes. All 10,810 unit cases passed, including exact expanded-name collisions and 0 B warmed dispatch. Nevertheless, valid standalone full before/after suites measured 1.0234x raw / 1.0100x BCL-normalized latency geomeans. No aggregate gain was established, so the runtime and temporary tests were reverted. No conformance/memory/streaming verification is credited to that rejected trial. Exact source patch: attribute-mask-trial.patch, SHA-256 `5e3a59d7c37939feaa55daf7a6fc6391e5edd8d647b2f1477eff74d5fba29e86`.

Raw evidence is under artifacts/M9-xml-performance: duplicate-filters-bdn/, duplicate-common-bdn/, attribute-mask-context/, namespace-cache-unit.log, default-ns-*-tests.log and attribute-mask-unit.log. These experiments leave retained runtime source at e1582649. The next useful step is better CPU attribution of the remaining parser/persistence cost, rather than inferring end-to-end gain from another tiny lookup.
