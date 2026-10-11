# Bounded source review correction batch

The read-only review of `fe30b556e3222a3fe5607535a407acf39599493c`
identified four blockers. Corrections dispatch derivation by `ContentKind` before
particle normalization; preserve the original wildcard range for every child
of NSRecurseCheckCardinality; separate mathematical namespace matching from
XSD 1.0 intensional subset/representability; and reserve owned counter overflow
construction before using charged, polled integer arithmetic/comparison.
Seeded UInt64 overflow and 4096-bit counters prove component refusal, exact
construction limits and cancellation without astronomical instance sequences.

Actual correction run: 102 owned cases, 101 passed, one failed, zero skipped.
The remaining failure is deliberately retained as a required negative:
`NonExpressibleIntermediateMustBeRejectedBeforeCompilerDiscardsIt`.
The current compiler accepts this principal schema (abbreviated namespace
bindings are fully present in the owned test):

```xml
<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
           targetNamespace="urn:t" xmlns:t="urn:t" xmlns:u="urn:u">
  <xs:import namespace="urn:u" schemaLocation="urn:import"/>
  <xs:attributeGroup name="local"><xs:anyAttribute namespace="##other"/></xs:attributeGroup>
  <xs:complexType name="T">
    <xs:attributeGroup ref="t:local"/>
    <xs:attributeGroup ref="u:foreign"/>
    <xs:anyAttribute namespace="urn:v"/>
  </xs:complexType>
</xs:schema>
```

The resolver supplies `urn:u` with an attributeGroup `foreign` containing
`anyAttribute namespace="##other"`. The first intersection negates two different
namespace names and is not expressible under [Structures 2E Attribute Wildcard
Intersection clause 5](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#cos-aw-intersect).
The later finite intersection erases that intermediate. The retained graph is
explicitly asserted as `T.AnyAttribute = { Negated=false, Namespaces=["urn:v"] }`;
ordinary groups and their operand sequence are not retained. The content lane
cannot infer the invalid discarded step from this valid-looking output.

Required outside-ownership repair: the ordered owner of
`src/Xemele/Validation/Xsd/Compilation/Compiler.cs` must reject non-expressible
operands/results at every private Union/Intersection step before discarding
operands. Thread actual `At(s,node/body)` into those private calls for provenance.
Keep the compiler independent of content; no public/frozen signature change is
needed. The request was sent to the authorized coordinator. No shared source
was edited, no case excluded, no acceptance criterion marked complete.

After the ordered repair is integrated and verified, update the worker from that
verified revision and rerun the unchanged required negative, all focused checks,
frozen/public API comparison and real XML-only consumer. Measurements and the
final implemented status remain pending.
