# PI target NCName repair

Product implementation: `d3eb4c8b15fa38f981d7fab5c903020e6d6f9067`.
Verifier-only continuation: `37a0e4f88fc73ee0bcdfdd608a2b86996ee4ce5e`.
Independent coordinator source review approved d3eb4c8; no API changes or blockers.

Namespaces in XML 1.0 section 7 (https://www.w3.org/TR/xml-names/#Conformance)
requires NCName for XML Name tokens outside element/attribute names. PI targets,
entity names and notation names therefore cannot contain colons. The core now
rejects colons for non-QName contexts; unknown legal NCName references still report
XML.UndeclaredEntity. Element/attribute QName handling is unchanged. Tree factory
and serializer use their existing NCName validators. Validation still checks legal
Unicode scalars, reserved case-insensitive xml targets and PI data delimiters.
The two erroneous old colon-positive examples now use a-b and p-q; four serializer
colon-negative rows and eight new focused tests were added. No DOM guard, API
fragment, shared build file, corpus fixture/classification or peer report changed.

The private pinned rmt-ns10-042 file was independently SHA256 verified against
2ff7553efa457354141c5f61a7560112f22b528203ecd052d298da9eebfaa8e2 and its relevant
PI/root tokens confirmed. original042.json records that check. The owned synthetic
source `<?a:b bogus?><foo/>` rejects as XML.Syntax under both Xemele and the
namespace-aware BCL. No upstream fixture is redistributed here. BCL is used for
colon rejection and BMP positives; it is not the oracle for Fifth Edition
supplementary names, which the product accepts through U+EFFFF.

## Actual checks and limitations

At d3eb4c8, Release solution build passed with zero warnings/errors. Focused PI8,
DOM44, Input10, Syntax70, Tree14, builder11 and Serializer50 passed, no skips.
At unchanged product/verifier-only37a0e4f, focused persistence8 and namespace21
also passed. Full suite is 300 tests (288 baseline +8 new tests +4 theory rows).
Three actual full runs are preserved: first299/300 failed, standalone retry300/300
passed, verifier restart299/300 failed again. Both failures were the unchanged
DomTests.WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings probe, expected
0 B and actual216 B. Do not infer a cause or claim repeatable full-suite success.
No threshold, peer test or runtime change was made to suppress it. Coordinator
fresh integration must recheck this probe and route a repair if needed.

`python3 -B docs/reports/M1-pi-targets/verify.py --contracts-only --audit-base
502bca77b5bc91e88829276d21c5dff473efc9a9` passed at37a0e4f: Foundation11,
foundation139/173/8, all approved M1 models and protocol38, exact runtime public
222 union and canonical API fragments, internal Input41 and Syntax9types/80rows,
source sample and freshly packed NuGet-only sample with fresh isolated package
cache/local source, independent DOM sample/oracle, serialization allocation
oracle, build regressions16, planning regressions14 and planning validation108.
Committed/staged/working/untracked owned paths and whitespace were audited.
Raw records retain exact commands, revisions and outputs; checks.txt summarizes
the independent continuation rather than presenting a failed full verifier as
passing. Initial restricted restore NU1900 and xUnit2031 analyzer failures are
preserved; network restore and corrected assertions resolved them.

## Allocation evidence

The new repeated borrowed PI regression warmed100 iterations and measured10000
iterations of ASCII/xml-like/BMP/supplementary targets: 0 B managed allocation.
Existing Syntax streaming/borrowed dispatch checks also passed. Existing cached
three-scanner oracle: static initialization2072 B; short-hit/long-miss/long-hit/
multilingual1000 repetitions0 B. Serializer cold setup+first stream12712 B;
warm stream1392 B/op for16 and1000000 characters; owned string1528/4011735 B/op;
temporary owner not retained. DOM cold complete byte469096 B, warm byte468536 B,
warm char426664 B, retained GC estimate229782 B/document (owned storage/setup,
not an allocation-free DOM claim). Existing peer name-hit probe results remain
as described above; they are not erased by the PI0 B result.

No scan algorithm, scanner workload, owned-storage model or allocation boundary
changed. No benchmark timings were run and no new throughput claim is made.
Prior integrated Arm64 timings remain historical informational evidence; x64
release confirmation remains with the release owner. Stream output retains its
existing partial-output behavior: each PI target is validated before its own
PI delimiter, not through a new document-wide atomic preflight.

Coordinator handoff: combined verification remains unresolved. Task JSON is
untouched and remains in_progress; no implemented/verified claim. All checks
are parked after evidence checkpoint. Coordinator will serialize a narrow
DOM allocation-probe repair, verify it, renew the PI claim/integrate the verified
probe, then request a focused combined PI recheck.
