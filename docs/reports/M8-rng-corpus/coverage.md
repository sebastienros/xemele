# Required XML profile coverage

All 385 original XML specification cases and all seven numeric historical regressions are applicable. No feature exclusion was added. The schema outcomes (accepted/rejected) and independent instance outcomes (valid/invalid) remain separate in the manifests. Compact syntax alone is outside the declared syntax profile.

| Required feature family | Representative immutable Jing case IDs |
| --- | --- |
| Arbitrary name classes | jing-006, jing-007, jing-004 |
| Recursive definitions and grammar scope | jing-002, jing-014, jing-015, jing-003 |
| Interleave and attribute ordering | jing-029, jing-017 |
| List/text/builtin datatypes | jing-034, jing-003, jing-008 |
| External composition | jing-044, jing-045 |
| Simplification | jing-133, jing-035, jing-031, jing-032 |
| RNG datatype facets | jing-041 |

Schemas include the original syntactic, simplification, name-class overlap, contextual, string-sequence, attribute and interleave restrictions. The supplementary seventeen authored schemas retain four facade-finalization rejection predicates and add lexical/value/facet checks absent from the original suite. Twelve historical-suite plus three supplemental libxml2 limitations are exact-ID/hash/spec-classified; none counts as agreement.

The corpus executes all 409 schemas through string, parsed DOM and caller UTF-8 streams, and all 626 instance predicates through the 3×3 compile/validate transport matrix. Streams begin at a nonzero position, return one byte per read, and remain open. DOM schema and instance serialization is unchanged. Seven boundary checks distinguish complete semantic invalidity from incomplete syntax/quota/recovered inputs and check retained byte/scalar/source URI locations. Existing owned RNG unit checks cover internal ContentRevision mutation detection, resolver redirects/cancellation/quotas and graph finalization.

Corpus verification adds no runtime hot path and requires no allocation counter or timing campaign. Normal and XML-only existing public consumer checks demonstrate use without a later milestone.
