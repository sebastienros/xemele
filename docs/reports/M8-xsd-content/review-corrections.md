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

## Effective-empty expectation correction and separate owner blocker

The 101/102 result above remains historical evidence at `f098c72f`. Further
review corrected two test expectations, rather than weakening the content-kind
rules. [Structures 2E §3.4.2, content type clause 3.2.1](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#Complex_Type_Definition_details)
requires an extension with empty effective content to inherit the base content
type, including when the authored `complexContent mixed` is false. Missing
particles and empty authored sequences in these cases therefore extend the
mixed base legally. The original nonempty element-only extension remains
invalid and its existing rejection test is unchanged.

Owned test/preparation correction `05a08eaac946552cd1885a779d18d13d8b04021c`
replaces those two negatives with positive
`EffectiveEmptyExtensionInheritsMixedBaseContent` cases. Each first asserts
the declaration compiler's `ContentKind.Mixed`, then runs the actual content
program and empty-sequence cursor acceptance. The wildcard-union negative now
wraps the complete declaration/compiler-content operation, allowing either
owner to reject it without requiring a particular error stage.

Actual .NET 10.0.11 run on the existing verified compiler: 102 owned cases,
99 passed, 3 failed, zero errors/skips/not-run. The new positives fail before
content compilation because the declaration graph incorrectly says `Empty`
(no particle) or `ElementOnly` (empty sequences); the existing lost-intermediate
wildcard negative remains the third failure. Artifacts:
`artifacts/M8-xsd-content/effective-empty.xml` and `effective-empty.log`.
Release build completed with zero warnings/errors; planning validation checks
176 tasks and `git diff --check` passes. No measurements occurred.

Separate outside-ownership repair: the ordered declaration-compiler owner must
map effective-empty extension content according to §3.4.2 before retaining the
graph, with correct inherited content kind and particle. This request is
distinct from preserving/rejecting intermediate wildcard algebra. The root's
private wildcard repair is still under independent review and was not consumed.
Content runtime source remains unchanged at `f098c72f`; task-record writes
remain root-owned. Preparation now requires both corrected positives and the
lost-intermediate negative to pass in the complete focused selection.

## Verified owner-repair consumption

The coordinator verified both declaration-compiler repairs on main
`6de242de053c9933ba1a0f3506f360a42dbfd1ba` and returned task-record ownership.
Worker merge `ccb384811d47099f931854c7b2a79c962d4ba2d1` consumes those verified
dependencies. Content runtime `f098c72f` and corrected tests `05a08eaa` remain
byte-for-byte unchanged. All 102 owned cases now pass, including all three
previous failures and the original nonempty extension negative.

Actual broadened `check.py` run: 243 focused cases (including 11 wildcard-owner
and 28 effective-content-owner regressions), plus 42 approved safe resolver
cases, 285 passed with zero errors/failures/skips/not-run on .NET 10.0.11 Arm64.
Public comparison matches 1,400 members and frozen comparison matches nine;
the actual XML-only compiler/content consumer passes. Release builds report
zero warnings/errors; planning validates 179 tasks and the diff check passes.
Artifacts are `artifacts/M8-xsd-content/correctness.xml`, `resolver.xml` and
`verified-check.log`. Historical failing artifacts above are preserved.

Both benchmark payloads rebuild successfully. The refreshed counter/timer-free
controls consume all ten probe routes and five BDN semantic methods; discovery
confirms exactly five MemoryDiagnoser ShortRun InProcessNoEmit cases. No actual
allocation or timing run occurred. Preparation requires all 243 focused cases,
both corrected positives and the retained negative to pass before freezing.
Immutable content-source closure review and a fresh root/all-peer grant are
still required before measurement acceptance.
