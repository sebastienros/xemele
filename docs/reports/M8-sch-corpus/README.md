# M8 Schematron corpus

The branch integrates 50 original upstream testcases (51 schema variants),
19 authored complete-result cases and 96 BCL XPath differential cases. No
applicable selected input is missing. This is the declared ISO2016/direct-M4
profile corpus, not a claim of complete normative ISO or full upstream conformance.

Run from the repository root:

```sh
python3 docs/reports/M8-sch-corpus/checks.py
```

The script uses the installed SDK10.0.400/runtime10.0.11 and the existing offline
NuGet cache. It builds the real product/conformance/differential projects, executes
90 conformance and96 differential test cases, then reuses the verified reporting
check script for42 owned reporting tests and504 affected predecessor tests (these
suite counts overlap), frozen reporting3/evaluator9/compiler154 members, public1403
members, and regular/genuine XML-only consumers. It validates planning and Git
whitespace. No benchmark or allocation counter runs. Raw results/logs belong only
in ignored artifacts folders; durable outcomes belong in the task record.

## Independent expectations

Original upstream inputs are kept verbatim with SHA256 per testcase. `XElement`
extracts the schema/document/secondary include resources with namespace scopes
and whitespace preserved. The conformance harness reuses earlier corpus byte
integrity/path containment checks, runs every schema variant, evaluates the
original upstream SVRL predicates using independent BCL XPath and separately
checks frozen result validity, completeness and UTF8/string parity.

The authored generator specifies expectations before product execution. Its
19 cases compare all retained finding fields except exact source coordinates,
active-pattern/fired-event keys/order, phases, namespaces, flags/roles/null IDs,
subject and context locations, messages and references. Each case exercises all
three compile overloads and all three validation overloads (171 result comparisons),
complete XML Infoset comparison and UTF8 byte parity, and detached serialization
after instance mutation. Source locations are checked as known, or unknown for
empty subjects. Prefix spelling and attribute order are canonicalized; element,
attribute and text values remain exact. Eleven alternate binding strings and
eight negative schema cases have explicit diagnostic-code expectations.

The independent differential evaluates32 XPath1 expressions against three XML
inputs with `XPathDocument`/`XPathNavigator`, singleton assertion focus and a
separate location algorithm. It checks findings, dynamic messages/diagnostics/
properties and complete expected SVRL. Initial run agreed on95/96 cases. The
remaining Unicode length case is an actual reference limitation: BCL10.0.11
counts the supplementary scalar twice (5), whereas XPath1 section3.6 and M4 count
it once (4). Both observed reference and normative product values are asserted;
96 passing test cases must not be reported as96 differential agreements.

## Upstream distinctions

Reviewed applicability is38 ordinary cases,3 let-oracle disagreements,1 name
oracle disagreement,1 frozen output representation difference and7 unsupported
execution cases/8 variants. All remain executed. `applicability.json` records the
original outcome and reason per case. Three conflicting let expectations are
reconciled against ISO2016 parent scoping and upstream `let-scope-pattern-01`.
The original name-path predicate expects the attribute value; the standard/frozen
binding returns its name. The diagnostic-language predicate expects nested styled
markup absent from the frozen flat-text SVRL representation. The two original
SVRL predicates fail explicitly; there is no upstream output pass claim for them.

Eleven original "invalid" fixtures are report-only: nine applicable and two
unsupported subordinate-document cases. Applicable reports produce findings and
warnings with `IsValid=true`; failed assertions invalidate results. The harness
asserts both axes independently. Unsupported documents/content-valued lets/XSLT
execution produce `SCH.QueryBinding`, not silent skips.

The original cross-pattern abstract-rule fixture revealed a real compiler defect,
not an oracle disagreement: ISO2016 section5.4.12 restricts extends targets to the
same pattern. At source checkpoint9cd1dd86 the strict run was88/89, with only
`rule-abstract-02` failing because compilation accepted it. Root owns the narrow
repair and integrated verification. No corpus worker runtime edit is permitted.

Early harness issues are not passing evidence: an incorrect `Context` property
name and missing explicit test cancellation tokens caused a build failure; a
nested formatting/name-in-diagnostic authored fixture violated the frozen RNC and
was corrected to legal text/value-of syntax. The provisional upstream pass before
independent review is superseded by the strict original rule-scope oracle. Initial
95/96 BCL agreement and the exact supplementary-length mismatch remain visible.

The final upstream validity/kind oracle is independently specified from original
outcomes and the nine applicable report-only fixture IDs. A swapped failed-assert
witness must be rejected for every one of those IDs. The earlier result-derived
validity check was removed following independent review.
