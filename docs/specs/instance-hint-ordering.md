# Instance hint ordering without acquisition

The XSD 1.0 Second Edition profile checks Structures section 4.3.2 rule 4
independently of resource acquisition. No instance hint causes fetching.

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
affected correctness and API/consumer checks.
