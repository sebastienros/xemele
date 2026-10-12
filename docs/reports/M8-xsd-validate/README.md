# XSD core evaluator

The internal evaluator consumes prepared content programs, the executable facet
registry and a prepared identity program/factory. Every validation creates a
fresh charged operation, namespace snapshots, cursors, effective values and
document ID tables. Traversal is iterative; wildcard scopes remain observable by
identity hooks. Defaults are run-local values and never mutate the document.

This task exposes no public API. The later facade must check every retained
definition and constraint before publication, including unused anonymous and
old/new redefine carriers; validate typed fixed/default consistency with each
binding/declaration's captured context; reject forbidden ID attribute uses and
multiple ID attribute declarations per type; and finalize all NOTATION references
through base/item/member graphs, requiring effective enumeration and resolving
every authored enumeration QName against the sealed schema notation table.
Runtime checks here enforce contextual NOTATION/ENTITY and document ID consistency
on encountered values. Generic facet parsing does not imply those joins.

`check.py` is correctness/API/consumer validation only. Allocation evidence is
pending a separate prepared probe and exact durable coordinator grant. Validation
results, typed values, diagnostics, namespace maps, traversal frames and ID tables
are explicit owned per-run allocations; no zero-allocation or speed claim is made.
