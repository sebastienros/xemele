# Internal XSD compiler handoff

The deliverable is `XmlSchemaCompiler : IXsdCompiler` and the exact frozen XSD
compiler declaration family, not the public `XmlSchema` facade. A compilation
owns its builders, namespace snapshots and resource loader. Published graphs
retain only immutable declarations, particles, contexts, locations and the
sealed definition registry. No source node/document or executable placeholder
library is retained. `Find` returns null until the facade supplies real libraries.

Composition registers declarations before imports, caches by final source URI
and effective target namespace, rejects include/redefine cycles, and allows
bounded mutually importing namespace component sets. Chameleon includes keep
separate component identities for each adopting namespace. Redefine preserves
internal preceding-version names independently of authored names and binds only
the designated base/group self-reference in the appropriate symbol space.
Recursive content type references remain edges into the replacement graph.

Dense local element/attribute IDs remain distinct from global expanded-name
maps. Anonymous and old redefine versions have stable internal names and are
marked Anonymous; they cannot become authored xsi:type candidates. Occurrences
are BigInteger; null maximum is unbounded. Block and final sets remain separate.
Attribute declaration constraints and local binding overrides keep their own
namespace contexts. Identity name tests use collision-free canonical prefixes
and one owned common context, preserving selector and each field's actual local
namespace bindings even when the same authored prefix has conflicting meanings.

## Deferred finalization obligations

Compilation checks XML/XSD structural grammar, namespace/component references,
composition identity, declaration collisions, derivation cycles/final controls,
substitution affiliation/closure and all-group occurrence constraints. The
following checks intentionally require the later verified joins and must finish
**before any public facade is published**, including unused declarations:

- Datatype lexical/value parsing, regex/facet definition consistency, typed
  default/fixed constraints and overrides, ID-related attribute restrictions,
  NOTATION value membership and ENTITY context checks.
- Content UPA, element declaration consistency, particle restriction/extension
  validity and effective attribute-use/wildcard restrictions.
- Identity XPath grammar/program construction and instance identity matching.

Zero-self-reference model/attribute-group redefine restrictions retain BOTH old
and new expanded programs as anonymous `XsdTypeDefinition` carriers linked by
Restriction/BaseType. Content/facet finalization must validate **all carriers**,
including those originating in unreferenced groups. The old/new obligation is
not lost when raw source/group builders are discarded. Self-reference versions
are expanded against the immediately preceding component.

## Checks and allocation boundary

`compiler.golden.txt` is the owning compiler-family subset of the unchanged
frozen `Xsd.golden.txt`. The verified M8-model extractor checks its exact internal
signatures (257 members). The public fragment is an empty additive
`xemele-api-v1` file because this lane adds no public entry point. The XML-only
project reuses the verified M8-model prerequisite input list and appends only
this compiler's source; its real consumer traverses an owned order/item graph.

The allocation-only probe follows the verified M8-model driver: two sealed
lookup primitives; owned parser baseline, graph construction, combined parsing,
external composition and invalid-schema diagnostics. It consumes every result,
separates cold setup and retained graph observations, warms 32 times, records 64
operation distributions and includes a positive allocation control. It does
not benchmark throughput or introduce speculative token matching optimizations.
The `--control` path disables all allocation/retention counters and timers.
Measurement requires the coordinator's explicit prepared-command lease.

Final runtime source is `d5e2a1c0f15b75cbfbb627ad5ea765e69e5b12bf`.
The verified source-location repair is merged; graph locations copy actual local
node or default/fixed/value attribute positions through guarded scope helpers.
Character input retains scalar coordinates without byte offsets; external byte
input retains final redirected URI and original offsets; stale source coordinates
remain unknown. The initial location-test build mistakenly used ColumnNumber;
its three compile errors were corrected to LinePosition before the passing build.

At this source revision: Release builds have zero warnings/errors; 72 owned,
20 affected model and 10 source-location cases pass (102 total); the prior safe
resolver selection passes 42. The compiler family matches 257 frozen members,
the additive public union matches 1393, and the XML-only real consumer passes.
Planning validates 176 tasks and whitespace checks pass. The prepared seven-route
control passes with counters disabled; elevated supervisor preflight verifies 24
identities and process-inspection capability before any child measurement starts.
`preparation.json` freezes the command/environment/input/binary/source identities;
`measure.py` is adapted from the verified M8-source-locations bounded supervisor.
The proposed lease is one allocation-only child, 30 seconds maximum, no retries
or builds. Final bounded source review accepted the final source. The sole granted allocation
run passed; lifecycle RELEASE and durable ACK preceded raw inspection. Lookup
primitives observed zero bytes; explicit owned allocations and retained-memory
limits are summarized in allocations.md. Task is implemented on this branch;
coordinator integrated verification remains. No corpus or release conformance
claim follows from these focused checks.
