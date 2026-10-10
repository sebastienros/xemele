# Strict DOM composition — current branch evidence

The seven frozen factories compose verified Input, Syntax, Tree and the linear
DOM builder. This source has no serialization dependency. The sink copies borrowed
names only on document-local intern misses, preserves attributes/declarations and
logical Text/CDATA/Comment/PI boundaries, and ignores document-level whitespace.
The byte adapter owns 4096 UTF-16 units and 4096 parallel Int64 offsets, plus a
six-unit declaration-recognition prefix. Stream transport owns an additional
4096-byte read buffer. Declaration framing submits at most one new original byte
per decoder call, draining detection carry before bulk body decoding. Declaration
validation occurs in the callback, or once with empty encoding on absence. Original
byte counts and UTF-16 counts remain separate; EOF is explicitly marked once.
Caller streams stay open and I/O/cancellation propagate unchanged. Structured
rejection returns no document and an owned diagnostic list.

At `15e099bd11e25a5fc2576059a39a6c707a45feef`, `verify.py` passed:

- Release solution build, 25 focused DOM cases, Input 10/Syntax 70/Tree 14/builder
  11/serialization 46 regressions and all 240 solution tests, no skips.
- Foundation 11 tests, explicitly rebuilt M0 M1/M2/ApiProbe and all M1 independent
  declaration models, and 38 protocol model assertions.
- Exact independent DOM model 199 declarations, serializer model 215, and their
  deduplicated canonical union of 222 matched against actual runtime output.
  The actual seven-factory API fragment matches the approved DOM declarations;
  mandatory discovery and compiled fragment comparison passed.
- Preserved internal Input 41-row contract from a disposable probe working
  directory; Syntax nine approved types and 80 declaration rows.
- Independent source sample and newly packed local-feed-only NuGet consumer with
  a fresh isolated package cache, with no project reference in the consumer.
- Owned benchmark driver build; planning validation and whitespace check.

The structure oracle covers all three input forms, six encoding/BOM/signature
combinations, stream read sizes 1–64, declaration precedence across every small
read size, malformed encodings/grammar/DTD, scalar and CRLF positions, original
byte EOF, exact quotas, stream ownership, I/O/cancellation identity and release of
mutated borrowed chunks. A test-only BCL oracle compares the declared supported
subset. The 12,000-depth case asserts exactly one builder attachment check per
node; 1024 attribute/declaration pairs cover wide construction. Incremental
adapter assertions observe built nodes before EOF. No full XML conformance claim
or later recovery/DTD/reader/async API is made.

The allocation sample measures the first complete byte parse before any parser
warm-up in that process; fixture generation is excluded. For 512 repeated children,
cold complete owned byte DOM allocated 469088 B, warm byte DOM 468528 B, and warm
character DOM 426656 B. These include setup, scratch and durable DOM output.
A 32-document full-GC retained-owner estimate was 229783 B/document, excluding the
shared input fixture. This is an estimate, not a precise object-layout guarantee.
The document arena intentionally retains all slots while any handle retains its
owner. Parser/sink temporary text and dictionaries belong to the operation.
Warm borrowed ordinal name hits allocated 0 B over 10000 calls after 10000 setup
calls; no allocation-free claim applies to owned output or setup.

Earlier attempts: unassigned test field warning failed the initial build; an
incorrect MTP option was passed to `dotnet run`; lone-surrogate attribute metadata
was replaced before the test, so invalid strings are now generated at runtime.
Sandboxed MTP IPC was denied and rerun with authorized escalation. The first full
verification stopped with 24/25 focused tests passing: a 100-call warm-up left a
24 B one-time allocation in the name-probe measurement. Isolating assertion
evaluation did not remove it; documented 10000-call warm-up produced 0 B. The raw
failed and passing known-revision attempts are compressed alongside this report.

## Blocking review and next action

Independent review found an unresolved bounded-cancellation problem: public tree
creation and current builder member APIs repeat `TreeValidation` scans without
operation cancellation. Long text/name/attribute/namespace persistence can thus
walk more than 4096 units without a cancellation check. All executed correctness
checks above passed, but this review blocks completion and timing. No benchmark
timing has run; the task remains unimplemented.

A narrow tree-owned repair is requested: trusted syntax-validated node/member
persistence that retains arena/name quotas, checked capacity, builder freshness,
versions and mutation notifications while removing duplicate grammar scans, or
equivalent cancellable validation with checks at bounded intervals. Public tree
validation remains unchanged. The DOM worker must consume only an integrated,
verified contract and then add cancellation regressions, rerun affected checks and
request an exclusive grant for the 16 owned measurement cases. Task JSON has not
been edited while the coordinator serializes prerequisite changes.
