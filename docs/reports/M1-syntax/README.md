# M1 syntax runtime

`XmlParserCore` and its event ABI are internal in `Xemele.Parsing.Syntax`.
They use only verified M1 common primitives; no input decoder, DOM, serializer,
System.Xml parser, native grammar, public placeholders or peer runtime is linked.
The test-only BCL oracle is in owned tests and is never runtime delegation.
The 100 generated documents compare concatenated text/CDATA and element counts;
a separate every-split/pause oracle compares resolved element/attribute names,
namespace declaration order/values, normalized attribute values and PI target/body.

## Operation and ownership

One core operation owns its options/budget snapshot, scalar location tracker,
lexical scratch and parallel locations, attribute descriptors, intern strings,
open-element stack and namespace bindings. Input spans are never retained.
Views borrow owned scratch/intern strings only through callback return. Sinks
must copy at persistence boundaries. Generic sinks are classes, resume must use
the identical instance, and reentrancy/concurrency reject. Callback exceptions,
including XmlParseException, and cancellation propagate unchanged and fault the
operation. Syntax/quota/DTD failures carry one owned result diagnostic.

Tags are fully validated before callbacks. Explicit namespace declarations are
reported in source order, then ordinary attributes in source order, then content.
Resolution sees all declarations on the tag; ordinary unprefixed attributes have
no default namespace. Expanded duplicates, reserved bindings and unbound prefixes
reject. Empty elements emit one end event without synthetic text.

The callback cursor advances before invocation, including callbacks that pause.
Accepted tag data drains before more source is consumed. EndDocument can pause;
its next empty final resume checks cancellation and completes without replay.
The final flag is sticky. Append counts submitted UTF-16 units, while quotas and
locations count source/normalized/expanded scalars according to their contracts.
Raw character offsets are null. Byte mode validates mappings incrementally along accepted units (one preflight
unit per resume), rechecks caller-owned mutable tails, and requires
CompleteByteSource exactly once before final Append; EOF uses that explicit length.

Text, comments, CDATA and PI bodies stream as source arrives. A fixed eight-unit
fragment buffer handles one scalar and up to two withheld delimiter characters.
No document or final text token is buffered to manufacture streaming progress.
A CR is emitted as LF immediately; the following literal LF is suppressed while
both source scalars charge DocumentCharacters. A high surrogate carries only its
unit/location until its low half arrives. Both mapped halves must agree.

## Quota and allocation boundaries

DocumentCharacters charges each validated raw source scalar, including both CR
and LF. ExpandedCharacters charges one scalar per predefined/numeric reference,
including attribute references. TokenCharacters charges normalized lexical scalars
including markup delimiters; streamed text references charge their lexical spelling.
A completed logical token releases its token counter. Tags/declarations retain
bounded scratch until callbacks drain; text never retains the complete token.
AttributesPerElement counts namespace declarations. Depth is simultaneous open
elements, including an empty element until its end callback. InternedNames counts
distinct owned local/prefix/namespace and PI target strings. Encoding labels borrow
declaration scratch and are not namespace/name interning. Common capacity limits
still apply when a quota is explicitly unlimited. Location overflow is structured
XML.LocationLimit. Cancellation precedes each callback/scalar and bounded inner
work, including 4096-unit name comparisons/scans and 256-step name/attribute loops;
borrowed dictionary hashing is a runtime operation over bounded token scratch.

Setup allocates the core, immutable options/budget snapshot and initial scratch:
128 UTF-16 units, 128 XmlLocation entries, eight attribute descriptors and eight
fragment units. Lists grow at first scopes/interns. Scratch/descriptor growth and
new distinct names/URIs are explicit ownership allocations. Already interned names
are probed with Dictionary's ordinal alternate span lookup, without temporary
strings. The runtime retains its collision defenses, and InternedNames bounds
owned keys before allocation. Tag namespace/duplicate checks are bounded by
attribute and depth limits. Dispose drops dictionary capacity, list capacity,
references and scratch owners. Test-only colliding initial buckets force and
verify the runtime's defensive comparer transition on the pinned runtime.

The warm target excludes setup, new unique names, scratch growth, owned diagnostics
and sink persistence. Focused tests measure repeated tag/namespace/attribute/reference/
CRLF/astral dispatch with a nonallocating sink. Long text tests observe progress
before finalization and verify lexical scratch stays at its initial size.

## Verification and performance

Run `python3 docs/reports/M1-syntax/verify.py` using SDK 10.0.400 on PATH with
DOTNET_CLI_USE_MSBUILD_SERVER=0. It preserves peer reports and models, records raw
output under ignored artifacts/M1-syntax/checks.txt, compares the unchanged common
public surface, compiles all approved M1 models/protocol probes, compares nine
actual internal syntax types with their model, runs the independent sample, packs
the runtime and records separate cold/warm/retained allocation boundaries.
No empty docs/api/M1-syntax.txt or public syntax type is invented.

The owned BenchmarkDotNet project filename and AssemblyName both match
Xemele.SyntaxBenchmarks. Its linked benchmark source is excluded from accidental
nested-probe globs. `--allocation` measures allocation/retention without latency
timing. Timing requires the coordinator's explicit exclusive host window:

```
dotnet run -c Release --project docs/reports/M1-syntax/Xemele.SyntaxBenchmarks.csproj --no-build -- --filter '*' --artifacts artifacts/M1-syntax/bdn
```

ShortRun separates warm repeated grammar, cold complete character/sink parsing,
and structured rejection. A semantics-equal delimiter oracle compares a simple
loop, IndexOfAny and cached SearchValues for short/long hit/miss, dense markup and
multilingual data. Corpus creation/static setup occur outside measurement. The
core currently retains scalar dispatch to preserve callback/source positions; no
custom SIMD or hashing is used. Timings remain provisional on the measured host;
M1 DOM owns later decoder/tree composition evidence, and cross-architecture release
confirmation remains required. Results and precise applicability will be recorded
after checks and the exclusive timing window actually complete.
