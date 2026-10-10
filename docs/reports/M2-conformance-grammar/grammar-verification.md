# Complete effective XML 1.0 grammar verification

The integrated prerequisites already implement the classified remaining runtime
capabilities. This branch adds independent expectations and executable coverage;
it makes no runtime, public API, shared build/package, classifier or corpus edit.
Historical reports (including `integration-summary.json`) remain unchanged.

`semantic_oracle.py` prepares expected models solely from original bytes, explicit
offline grants and authored XML 1.0 / Namespaces 1.0 rules. It never reads product
or reference responses. It is a bounded accepted-corpus model interpreter, not a
replacement validating parser: classification establishes rejection expectations;
unhandled model constructs and missing resources are preparation failures.
Fourteen authored controls check normalization, precedence, PE inclusion,
conditional status, relative-resource origin, namespace expansion, BOM identity,
unread declarations and symbolic/unknown identity rules.

The independently specified model covers expanded element names, sorted expanded
attribute names and normalized values, ordered start/end pairs and coalesced
text/CDATA. Exact primary declaration version/encoding/standalone, DOCTYPE name,
resource request origins and disposal counts, unresolved content identity/parent/
source/sequence/anchor invariants and symbolic default parts are also checked.
The grammar-owned driver explicitly projects applied incomplete members through
their symbolic sidecars; it never reads their guarded string values. No accepted
original applies such a member, but two retain incomplete unused/overridden
defaults. The authored runtime regression checks applied default identity and
loss refusal. Lexical comments/PI/prefixes/namespace declaration order and full
DTD model shapes retain their verified focused prerequisite oracles; they are
not erased or described as verified by the coalesced events projection.

All 2,585 originals retain their exact ID and byte hash. All 3,386 original files
are hash/length verified, and every external request is explicitly mapped in
memory with no online/filesystem fallback. Of 1,394 accepted originals, 409 also
agree with original-suite canonical output projections. Fifteen other usable
historical outputs have exact authored overrides bound to input/output hashes:
XML 1.1 NEL/LSEP normalization differs from XML 1.0 sections 2.11/3.3.3. Original
output bytes are preserved. Remaining accepted cases use the independent rule
interpreter; rejected cases use the verified normative applicability ledger.

Every accepted case is executed both with one-byte primary decoding and with
bulk decoding plus a pause after every callback, comparing identical models,
provenance, ownership and callback counts. Character split, supplementary name,
numeric/reference-origin whitespace, nonvalidating behavior, mandatory rejection
and symbolic loss-refusal regressions run separately. The new focused runtime
tests total 17; the oracle controls total 14.

All 771 System.Xml expectation disagreements are resolved by exact case ID/hash,
the normative applicability rule/rationale and independent accepted-model hash
where applicable. On its 644 mutual successes the reference also matches those
independent models. Reference rejection/name/version capabilities do not define
product expectations, and no product failure is recategorized or excluded. The
three original optional-error cases accepted by the reference and rejected by
the product remain permitted alternatives under their explicit ledger rows.

Reproduce with the pinned SDK 10.0.400 / runtime 10.0.11, `DOTNET_ROOT` and `PATH`
set to `/Users/sebastienros/Library/Application Support/dotnetup/dotnet`:

```sh
dotnet build samples/M2-conformance-grammar/Probe.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet build tools/ReferenceRunners/Bcl/BclReference.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
python3 -B docs/reports/M2-conformance-grammar/full_grammar.py <unique-report-name>
python3 -B -m unittest discover -s docs/reports/M2-conformance-grammar -p test_semantic_oracle.py
python3 -B docs/reports/M2-conformance-grammar/preserve_grammar.py <unique-preservation-report-name>
```

Reports record source, output/dependency and runtime hashes plus exact compilation
revisions. Results contain count/hash maps and disagreement facts, no raw corpus,
test XML, benchmark logs/CSV, packages or binaries. Two early report attempts
failed on report/preparation assumptions (omitted null JSON fields and symbolic
unused defaults); those are recorded honestly, corrected and covered by the
final passing execution. They were not product failures.

Existing integrated 2,157 focused correctness checks, 372 public API members
(82 additive), frozen shape/model checks, source/fresh-local-package consumer
checks and applicable allocation/benchmark observations retain their original
revisions. `preserve_grammar.py` proves their runtime/configuration/fixture/consumer/
workload input identity. The existing warm borrowed allocation observations are
0 B; setup, owned results, retained state and informational Arm64 timing remain
separate. A changed informational revision can change binary bytes; preservation
does not claim these earlier binaries were rebuilt or remeasured at grammar HEAD.
No new host measurement lease is needed for this test/report-only change. The
coordinator still integrates and verifies this branch, and the M2 gate owns
comprehensive combined and release-platform checks.
