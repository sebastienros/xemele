# Instance hint ordering without acquisition

Frozen before runtime edits for `M8-xsd-instance-hint-ordering`, on the
coordinator's accepted bounded design and exact triage. The project's complete
XSD 1.0 Second Edition profile checks Structures §4.3.2 rule 4 independently
of instance resource acquisition. No instance hint causes fetching.

Each validation owns an ordinal namespace-use set. A start tag is atomic:
check every namespace/location pair in `xsi:schemaLocation`, and the empty
namespace for `xsi:noNamespaceSchemaLocation`, against preceding tags; then
record its physical element and attribute namespace names. Names on the same
tag do not make that tag's hints late, regardless of attribute order. Empty
namespace is a real key, including unqualified physical attributes. Declarations,
QName value prefixes and hint tokens/locations do not establish namespace use.
Repeated hints before use are timely; any hint after use is late even when its
location repeats. One ordering error is emitted per offending hint attribute
at its original source location. Existing lexical assessment and typed identity
hooks run exactly once and retain their behavior.

The explicit project interpretation tracks names and ordering throughout the
validation-root subtree, including skipped subtrees. This does not enable skipped
datatype, content, lexical, xsi:type or xsi:nil assessment. The interaction of
wildcard skip with global hint ordering has no independent W3C adjudication in
the supplied review: this corner is a project interpretation, not an upstream
conformance oracle.

Preserve all seven original INVALID expectations, original bytes and URI
provenance, source statuses and SHA256 pins from
`docs/orchestration/M8-xsd-instance-hint-ordering-triage.json` (upstream
`7bc3365c652a322f3d762021b3879eb92dae7e30`). Compile the first authored schema
for each namespace explicitly; later hints remain unfetched. Preserve actual
pre-repair failures and the adapter's prior unassessed limitation separately.
W3C4078 supports preserving these original expectations; W3C9158 is an unadopted
backport, not authority to change them.

Use borrowed spans with XML whitespace tokenization, charged scanning and
ordinal comparison, bounded polling even with null quotas, and run-owned table
growth charged before allocation. Borrow DOM namespace strings and retain no
hint token or location string. The additional ordering scan does not charge
DocumentCharacters twice. Preserve cancellation, document revision detection,
shared work and diagnostic limits. No shared static document state, instance
mutation, public/frozen interface changes, or Values.cs edits.

Evidence must distinguish complete semantic invalidity from refusal. Require
unchanged originals, independent same-tag/physical-use/non-use/skip/lexical/
recovery witnesses, source locations, no resolver calls, exact and one-short
limits, long-token/comparison cancellation, isolation, immutable documents,
affected correctness and API/consumer checks. Allocation evidence uses existing
infrastructure only under a separate fresh bounded root grant; preparation and
correctness exclude counters, heap probes, timing and WarmBorrowed methods.
