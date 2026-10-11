# Navigation implementation

`XPathNavigator` implements the frozen internal `IXPathNavigator`; there are no
public additions. `docs/api/M4-navigation.txt` therefore contains the schema
header and zero member rows. Compiled preservation uses the verified model's
API checker, comparing all 893 members (720 earlier + 173 model).

Snapshot capture requires exclusive caller access. It iteratively visits the
connected document, coalesces Text/CData runs under first-physical-member identity,
omits empty runs, and captures immutable item metadata through the internal
constructor. It never calls public unbudgeted factories. It captures parents,
subtree end indexes, namespace scope, actual attribute slots, xml:lang (including
empty stop declarations), and declared lexical DTD ID membership. No external
resource fetch or evaluator/facade is needed. Incomplete provenance rejects entry.

Every item preflights input count, aggregate five-field UTF-16 character count,
each individual string limit, and name capacity before creating item/name/text
storage. Descendant and logical-run text lengths are checked before `string.Create`;
copying charges at most 256 code units per chunk. Omitted physical text still
charges visits. Namespace shadow resolution scans live ancestors during capture
without an unbounded scratch dictionary. Namespace/attribute sections use ordinal
insertion sort with individually charged comparisons/moves. Dictionary keys have
no variable-length namespace prefix: namespaces resolve by captured owner plus a
budgeted ordinal scan, avoiding an uninterruptible prefix hash during lookup.

Axes scan captured entries and intervals, charging every candidate; reverse axes
scan backwards. Attribute/namespace parents are owners and following includes
owner children. Node-set growth checks precede append; normalization checks
incoming count, rebinds captured metadata, deduplicates/sorts indexes with bounded
charged shifts, and owns an immutable read-only result. ID scans return each
matching element once in order, accept XML-S-separated tokens, and do not infer
id/xml:id. Query operations never read live topology or values.

Cold snapshot/owned axis/normalization storage are intentionally allocated. List
and dictionary backing capacities remain O(input items/result count), with runtime
geometric capacity overhead; limits govern logical counts before growth. Repeated
aggregate descendant text, scope resolution, sorting and full axis scans can be
quadratic, explicitly bounded by the shared operation quota. No speculative
index/cache optimization is introduced. Warm rebinding, order, metadata and lang
reads reuse snapshot storage. Actual10.0.11 performance is recorded in performance.md after a bounded exclusive
coordinator lease; exact warmed borrowed primitive groups measured0B while cold
and owned snapshot/result costs are separately positive. Earlier10.0.0 provenance
failure and noisy retained-memory estimates are preserved with their limitations.

The standalone sample builds as an explicit `Xemele.Tests` friend assembly because
navigation is internal. The fresh package consumer repeats that seam proof in an
isolated net10.0 project/cache; this is not a new public facade. Public facade and
expression evaluation belong to later lanes. Differential tests use the pinned
.NET XPathNavigator for membership/order across every context and thirteen axes;
namespace/attribute ordering compares identities because the frozen ordinal order
is a permitted implementation choice. DTD ID inference and quotas have dedicated
expected-value tests rather than inappropriate reference equivalence.
