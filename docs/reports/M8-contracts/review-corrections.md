# Batched correction of 4c471b3 source review

The initial comparison results are preserved at their original revision. They
establish declaration compatibility; the following gaps required a new freeze.
No runtime implementation was requested or added.

| Finding | Frozen correction | Compiled representative consumer |
| --- | --- | --- |
| Phase lets | SchematronCompiledPhase contains Lets/ActivePatternKeys; selected phase scope sits between schema/pattern | Schematron/ReviewCases.cs.fixture TwoPhaseLets assigns threshold=1/2 in two phases using one shared pattern/test |
| Optional pattern IDs and detached order | Nullable authored Id plus stable pattern/rule/fired-event keys; finding joins by keys | Reporting/ReviewCases.cs.fixture two unnamed patterns, empty first findings, identical fired context, child subject; review-detached.svrl.xml freezes expected order/absent IDs |
| Substitution controls | Separate XsdElementBlock(Substitution/Restriction/Extension) and XsdTypeBlock(Restriction/Extension), distinct from Final | Xsd/ReviewCases.cs.fixture element substitution block and complex-type extension block |
| Local attribute constraint context | XsdAttributeBinding retains its own Context/Location | Xsd/ReviewCases.cs.fixture global/local p map to distinct URIs for QName p:item |
| Schema notation declarations | XsdCompiledSchema.Notations carries expanded-name declarations/public/system/context/location; NOTATION is schema-based, ENTITY uses retained DTD | Xsd/ReviewCases.cs.fixture declaration and lookup |
| Subject | Rule/check source/expression, check precedence, separate assertion context/reported subject locations | Schematron/ReviewCases.cs.fixture parent context/child subject and check child/@code override |
| Aggregate XPath work | RemainingWork plus exact proposed XmlNode/XPathItem accounting adapter; cap to min and charge actual count in finally, including failure | AccountingJoin.cs.fixture consumes real M4 XPathExpression through declaration-only future adapter |

The supporting Schematron project grammar is pinned to
655a641bb8fe21ec4fa7b1a82498c43bc70a3bf0, SHA-256
3acec1e3dc3b1941a23e19840282311bd6039739899b3fc73007dced1cf49c46.
It retains its included license in References/schematron.rnc. Offline checks
verify the digest and phase-let/optional-pattern-ID/subject productions.
sources.json distinguishes this supporting grammar from the normative selected
ISO/IEC 19757-3:2016 publication. The first source-pin attempt was denied DNS in
the sandbox; an authorized read succeeded. A whitespace-sensitive source check
then failed; whitespace normalization corrected it before the pin was saved.

All scenario functions are compiled and **not invoked** against M8 declaration
methods. The XML fixture is a declarative expected serialization, not output
from an unimplemented runtime serializer. Actual phase evaluation, detached
report serialization and accounting success/failure/exact-limit execution remain
required tests for their runtime owners. API controls additionally reject changing
nullable authored IDs or removing fired-event associations.

Required ordered work before M8-model: M8-resource-kind (Schema=5) and
M8-xpath-accounting (private M4 budget access/internal instance overloads plus
thin adapter). Contract fixtures do not claim either runtime addition is present.
The public M4 surface and earlier runtime sources are unchanged by this task.
