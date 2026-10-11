# Schematron compiler / evaluator join

`SchematronCompiler` implements the frozen internal `ISchematronCompiler` seam.
It publishes only immutable M4 expressions and owned model/collection snapshots;
source nodes, traversal scopes, scratch lists and resource loaders stay in the
compilation operation. There is no public facade or evaluator placeholder.

Selected profile: ISO/IEC 19757-3:2016, namespace
`http://purl.oclc.org/dsdl/schematron`, absent queryBinding or exact `xslt`,
evaluated directly by M4 XPath 1.0. `sch:ns` supplies the XPath prefix map;
XML default/prefix bindings do not implicitly become XPath bindings.

Pattern `documents` is a SCH.QueryBinding rejection: its external `document()`
semantics are outside the frozen direct-M4 profile. Content-valued `let` is also
SCH.QueryBinding: ISO2016 section 5.4.5 defines a value, and XSLT1 section 11.2
constructs a result-tree template; it is not an annotation to ignore or invalid
ISO syntax. The frozen profile supplies typed XPath expression lets and excludes
XSLT templates/instructions. Ordinary foreign annotations are ignored; XSLT
executable syntax is rejected. Primary anchors: ISO2016 Annex C/section 5.4.5,
https://www.iso.org/standard/55982.html and
https://www.w3.org/TR/1999/REC-xslt-19991116#variable-values.

Each named phase that activates a concrete authored pattern is compile-checked
independently with schema/that-phase/pattern/rule lets. A name in an unrelated
phase cannot resolve a reference. Phase-only names valid for every activating
named phase are accepted, as required by the TwoPhaseLets frozen fixture.
`#ALL` supplies no named-phase lets; if that run actually evaluates a phase-only
missing variable, the evaluator must emit incomplete SCH.XPath. Universally
unresolved names and let dependency cycles are compile errors. In a shadowing
let initializer only a reference to its own expanded name can resolve the outer
binding; other locally declared names remain dependency edges. The evaluator
must evaluate each initializer before replacing the visible outer value. Thus
schema x=1, pattern x=$x+1 and rule x=$x+1 yields3, while global/self-without-outer
and local x=$y,y=$x cycles remain errors. This error timing
is the coordinator's bounded contract interpretation; the validator must prove
it with #ALL/#DEFAULT/named-phase and cross-run concurrency tests.

The evaluator must select concrete patterns in schema order (even if phase active
references are authored in a different order), preserve stable pattern/rule keys,
choose the first matching rule per node per pattern, and process matches in
document order. `ContextSelector` translates the XSLT1 child/attribute match
language to a document-root selector, preserving per-step predicate focus,
union, `/`, `//`, node tests and literal `id()` patterns. Bare name `id` remains a
normal relative name. `key()` requires unsupported XSLT key declarations and is
rejected in this direct-M4 profile. Pattern semantics anchor:
https://www.w3.org/TR/1999/REC-xslt-19991116#patterns.

Assertions/messages/subjects execute with the matched node's singleton focus;
selection-array position()/last() must not leak into it. Rule/check subjects are
separate expressions; a check subject overrides the rule subject and affects
reported location only. Dynamic diagnostic/property content uses the check's
full let scope, including inner shadowing. Formatting markup contributes its
text. Original source locations survive include/abstract/extends boundaries.
All event/findings/results must detach from instance node handles.

The compiler charges actual Schematron scalar scans, traversals, lookups,
expansion and reference analysis, plus retained pattern/rule/check/text-expression
components before persistence. M4 compilation keeps its independent finite
XPathCompileLimits; this compiler does not claim measured internal M4 parser/AST
operations. Evaluation must use the verified spent-work adapter in finally and
one aggregate schema budget, with cancellation and incomplete-state checks.

Verified owning-model repair `M8-schema-resource-base` supplies the concrete
Load(reference, effectiveBaseUri, operation) overload without changing the frozen
interface. The compiler passes final actual source URI as reference.Referrer,
effective inherited xml:base separately, and original href including fragment as
SystemId. The normative redirect/xml:base regression covers this distinction.
No shared edit is made by this worker.
