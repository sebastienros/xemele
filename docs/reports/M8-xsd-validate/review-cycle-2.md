# Second consolidated source correction batch

Runtime and regression source is committed at
`d712c03e6af25f3a3e3b8332d9cc80455e4d803f`. This batch was edited
under the external host PARK. It has not been built or executed. All earlier
tests remain present; no exclusions or skips were introduced.

`Evaluation/Evaluator.cs` defers required root/strict-wildcard assessment until
both element-declaration lookup and global xsi:type resolution have completed.
A named non-anonymous global type can provide declaration-free assessment.
Actual skip still propagates through the subtree, and every element opens its
identity scope. Lexical xsi:nil checking remains, while effective nil is enabled
only by an actual nillable element declaration. A declaration-free type-only
element therefore continues actual-type content validation even when its lexical
xsi:nil is true.

`Evaluation/Values.cs` preserves a complex edge's extension/restriction method
when checking the parent type's final set. Once that complex path reaches a
simple base, the remaining relation delegates to the existing bounded iterative
simple derivation evaluator, including union-member paths and the given
restriction block. Complex extension is not converted to simple restriction.

`AssessmentRouteTests.cs` adds sixteen independent expected-result cases:
declaration-free root and strict child with resolved int/invalid text/unresolved
type; type-only root/lax child with nil true and empty/valid integer content;
complex simple-content extension through a union member with extension and
restriction block negatives; and extension of a simple base with final
restriction, including direct-base restriction-block positive and extension-block
negative counterparts. These are unrun witnesses, not passing evidence.

The declared-decimal default `1` to selected int canonical-lexical negative
counterpart added at `da82339032a324d3935024b0a9705cc5a10285a3` is also
unrun. The canonical helper remains an owning-task join; no unverified helper or
frozen interface modification is present here.

Normative source used for review: [XSD 1.0 Structures, assessment and complex
type derivation](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#cos-ct-derived-ok).
The latest actual full selected run remains at
`8c4cabcdef573b1fe131fa9f1639ea396984c348`: 827 passed and one
canonical-default regression failed, out of 828. API and normal/XML-only consumer
checks at that source passed separately. Those results do not verify this batch.

Resume builds/tests only after explicit durable external cleanup RELEASE or
confirmed abort, then merge the coordinator's exact verified canonical-helper
integration before using it. Corrected-source review and fresh allocation evidence
remain required. The worker retains all task-owned writes until terminal handoff.
