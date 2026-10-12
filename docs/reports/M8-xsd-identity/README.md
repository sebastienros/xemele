# XSD identity engine

`XsdIdentityCompiler`, `XsdIdentityFactory` and `XsdIdentitySession` implement the
frozen internal hooks; no public XSD facade is introduced. Compilation prepares
all constraints, including detached ones, validates names/IDs/references and
field arity, and owns paths/names. A prepared program belongs to exactly one
immutable compiled schema. Factory calls return fresh state without a global
cache. Real compiler graphs contain built-in definitions; independent consumers
inject the real registry using only non-reserved authored definitions.

Restricted selector/field syntax implements XSD1.0 unions, child/self steps,
optional leading descendant-or-self, QName/wildcard namespace tests and terminal
attribute fields. Equivalent explicit child, self::node(), attribute and leading
descendant-or-self::node() forms are supported. XML Unicode names use an owned per-scalar polled validator with the established
ValueSyntax classifications; malformed UTF-16 and colons reject. Axis lookahead
charges/polls every whitespace unit on successful and unsuccessful paths without
recharging that lookahead in cursor advancement. Namespace tests use definition bindings (the compiler's
collision-free canonical aliases), never instance prefixes/default namespaces.
Union alternatives selecting the same node contribute a single field event.
Descendant-self paths with no name step also select text/comment/PI/CDATA nodes;
bounded DOM scans reject those non-element selector/non-simple field targets
instead of losing them at the element-only hook boundary.

Each document-order hook run stores only currently open scopes, pending selected
rows and per-scope typed tuples/tables. Effective attributes, including defaults,
arrive as typed events. Final element content arrives before its scope closes.
Invalid/untyped/complex/multiple fields produce errors rather than disappearing.
Unique/keyref absent or nil fields are unqualified; key requires every field and
rejects element fields assessed under a nillable declaration even when nonnil.
Typed comparison delegates to the verified datatype core's ValueEquals, preserving
primitive families, derived values, QName identity, union selection, ordered lists
and NaN rules. No lexical comparison or reparse can substitute for a typed value.
Values must belong to the supplied verified datatype provider graph; the core's
provider-provenance exceptions propagate unchanged.

Tables propagate upward through every element, including wildcard scopes. Only
eligible own key/unique tables contribute. Conflicting inherited tuples from
different nodes are suppressed; an own tuple has precedence over inherited
conflicts. A repeated tuple for the same selected node does not conflict.
References resolve after descendants and own key tables, strictly within the
owning subtree; ancestor/sibling tables never repair a child reference. Complete
requires all scopes closed and marks the session terminal. Source mutation,
foreign models/nodes, skipped/repeated elements, repeated attributes and changes
of the shared operation reject as explicit misuse.

One operation charges every compile scan, scope traversal, name comparison,
field match, tuple construction/comparison and table growth before work/growth.
Retained logical components use ChargeComponents; depth and diagnostics have
independent limits. Finite default work also bounds runtime memory and quadratic
linear-table comparisons. No new table-entry/byte quota or unlimited-work memory
promise is invented. The no-operation factory signature necessarily makes fresh
session object setup a separate owned boundary; subsequent state is charged at
hooks. Cancellation and caller exceptions propagate. A stopped schema operation
uses the existing internal SchemaCompileException stop convention; the future
facade/evaluator must retain its operation's fatal/incomplete result.

Primary semantic reference: W3C XML Schema1.0 Structures Second Edition,
[sections3.11.4–3.11.6](https://www.w3.org/TR/xmlschema-1/#Identity-constraint_Definitions).
Focused assertions are authored independently of runtime outcomes. These hooks
are an internal lane demonstration, not a public-validator/corpus conformance
claim. The facade must always supply a real prepared identity program, including
zero-constraint schemas, and deliver all effective attributes/typed element
values and wildcard scopes through one operation/provider graph.

`check.py` builds/runs149 owned plus546 relevant predecessor tests, separately
42 safe resolver cases, exact24 frozen declarations, unchanged1403 public member
union, and a real normal plus genuinely source-linked XML-only consumer. Planning
and whitespace are checked. The XML-only project omits HTML/CSS/XSLT/facade/
instance-evaluator and content engine sources. `identity.golden.txt` is a literal
subset of the unchanged frozen fixture, not an emitted runtime baseline.

Historical setup: an early xUnit1051 analyzer error was fixed by forwarding the
active test cancellation token. The first executable owned run119 had44passing,
75failing: the driver attempted to re-register built-in definitions. Filtering
reserved built-ins fixed that driver setup; the next119 passed. An invocation of
the previously built assembly before that analyzer fix discovered0 cases; it
was never treated as a passing test. Raw initial output remains transient.

The allocation-only probe has nine independently consumed routes: short hit/miss,
typed equality, long multilingual namespace hit/miss, preparation, valid/invalid
full hook runs and fresh initialized sessions. Counters/timers/retention reads
are completely disabled under --control. Each measurement route warms32 times
then records64 synchronous per-thread observations; cold fixture setup, positive
control and whole-heap retention for32 programs/32 initialized sessions are
separate. No throughput/latency or BDN allocation-dash claim is made; there is no
optimization or BDN class. Running --measure needs a fresh bounded coordinator
lease and reviewed fixed identities. Review and allocation evidence remain
pending; this branch is not implemented/verified yet.
