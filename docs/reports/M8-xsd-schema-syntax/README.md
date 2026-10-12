# Schema syntax repair

Original fixtures are byte-preserving copies from W3C xsdtests revision `7bc3365c652a322f3d762021b3879eb92dae7e30`; provenance.json pins SHA256 and original metadata identities. `00COPYRIGHT` preserves the W3C notice and document-license reference. The only resolver targets are the three explicit imports in the owned test map; no network/filesystem fallback exists.

Baseline witness commit: `63b153d05` (full revision recorded in task evidence). On SDK10.0.400/runtime10.0.11 all three independent BCL oracles rejected; all three public facade rejection assertions failed because no exception was thrown (3 tests, 3 failures). No quota/resource failure masked those outcomes.

Standards: [Structures Second Edition, Appendix A and §3.11](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#coss-identity-constraint) requires the restricted lexical representation in addition to selector/field semantics; explicit child/attribute axes remain legal. [Datatypes §3.3.3](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#language) defines collapsed xs:language. Documentation wrappers now validate language without allocating a normalized value; payload nodes remain opaque. Global groups under schema/redefine reject local ref/minOccurs/maxOccurs attributes.

Intentional prior test identity transitions (no matching-engine change):
- XPathTests SelectorPathsAreUnionSets: descendant-or-self::node()/child::row moves to InvalidSelectorGrammarIsRejectedBeforePublication; .//child::row preserves the matching intent. row/self::node() moves to invalid selector coverage; existing row/. preserves that intent.
- XPathTests DescendantSelfFieldsRequireEverySelectedNodeToHaveASimpleType: descendant-or-self::node()/self::node() moves to InvalidFieldGrammarRejects; .//. and .//./. keep the simple-type semantic coverage.
- ParserBudgetTests ValidAxesHaveBoundedMidLookaheadCancellationWithNullCaps: self + whitespace + ::node()/row and descendant-or-self + whitespace + ::node()/row were wrongly marked legal. Their replacements are . + whitespace + /row and . + whitespace + //row. Child/attribute lookahead, null caps, call512 cancellation and empty diagnostics remain asserted.
- Direct selector/field self::node() rejection and literal-dot facade controls are additive.

Correctness command: `python3 docs/reports/M8-xsd-schema-syntax/check.py`. Allocation preparation/measurement is separate and requires the coordinator host lease; correctness never samples allocation counters.
