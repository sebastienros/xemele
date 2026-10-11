# M7-tokenizer work evidence

Owned incremental scalar state machine and public owned-token string adapter.
The implementation keeps the current token and fixed-size reference/declaration
candidates, never the whole input. Callback tokens own immutable snapshots.
Input CR/CRLF preprocessing, split surrogates, scalar source locations and
nullable source-byte maps are retained across Feed calls. The tree builder can
change text state and CDATA allowance synchronously during token delivery.

Current WHATWG https://html.spec.whatwg.org/multipage/parsing.html retrieved
2026-10-11 SHA-256 `34e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf`
requires processing-instruction states (§13.2.5.72–76), entered unconditionally
from '?' in Tag open. Verified additive PI model `84dfa585` and metadata
`3608900` are merged; mandatory PI target/data/reconsume/EOF states now preserve
target case, literal data (including NUL), owned snapshots and source positions.

Current PI correctness checkpoint: pinned Release build has zero warnings/errors;
15 focused tests pass on actual runtime 10.0.11. All 6,823 original lexical
cases / 7,049 state vectors pass whole input, every two-chunk split and single
UTF16 units. Eighteen independently authored current PI witnesses pass all
boundaries. Error-code audit matches 7,046 original vectors; exactly three
`domjs.test` error arrays reflect former tag-open behavior. The immutable
`normative-discrepancies.json` retains their exact original inputs/arrays and
asserts the mandatory PI-open invalid-first-character error instead. No lexical
expectations or upstream resources are changed. Dedicated PI tests cover case,
raw NUL/references, supplementary scalar quotas, source locations and identical
callback exception propagation. Full compiled API comparison matches 1,206
members and rejects missing/added/changed controls; official discovery verifies
1,206 declarations and all 3,388 inherited fixture identities. Planning validates
167 records. Allocation/timing evidence remains pending review and root lease.

Pinned full fixture corpus and classification: tests/fixtures/M7-tokenizer/README.md.
All 7,049 tokenizer state vectors run with every chunk split. Parse errors remain
separate from token comparison. Encoding sniffing/byte decoding is parser
composition, not a string tokenizer API; its original resources remain pinned
for that owner and the final gate.

Performance status: no allocation or timing measurements have executed. Borrowed
lexical classification/references and owned callback/result persistence will be
measured separately only after independent review and the coordinator's exclusive
prepared durable lease, using the existing model freeze/wrapper lifecycle.

Actual pinned ordinary builds: SDK10.0.400, RuntimeFrameworkVersion10.0.11.
Actual test execution uses `dotnet --fx-version 10.0.11 ...Xemele.Tests.dll`.
Initial full corpus run fails at pending PI states as expected; no exclusion.

Independent checkpoint at `11f4d6f316f418a928c1ec2d0ce8136a211a1c24`:
ordinary Release build succeeded with zero warnings/errors; tokenizer-only tests
had 9 passing tests and 2 failing full-corpus checks due to pending PI behavior.
Later preparation adds scalar-batch/reference locations, bounded input diagnostics
and exhaustive numeric-digit oracle tests, plus full failure accounting. Current
corpus is 6,823 original tests / 7,049 state vectors. No filtering of failed PI
states is permitted. The separate code audit additionally exposed three
lookahead warning-order defects; their explicit delayed-warning repair is owned
here. Exact error-code order remains tested separately from lexical tokens.

Preparation (not measurements): full API comparison matched 1,203 compiled public
members with missing/added/changed negative controls rejected, tokenizer sample
ran successfully, and benchmark discovery verified 24 ShortRun NoEmit cases.
The immutable original XML cache was copied only to this worktree's 3,386 ignored
manifest-listed resources after coordinator authorization, with source and
copied SHA256 verification; no cache mutation/refetch/symlink. API inventory
then passed with bootstrap disabled and all 3,388 inherited fixture identities.

The probe separates zero-allocation borrowed numeric-digit/reference classification
from owned tokenizer setup, owned strings/tokens/attribute snapshots, callback
operation persistence, token-list persistence, and retained whole-heap snapshots.
Primitive comparisons use char.IsAsciiHexDigit as the straightforward oracle;
owned comparisons retain identical lexical results for contiguous versus
single-UTF16-unit transport chunks. No SearchValues or custom hashing optimization
is adopted in these state loops; scalar processing preserves every state's NUL,
source position and quota behavior. Performance results remain provisional single
osx-arm64 evidence until composition and cross-platform release verification.
Existing corrected model freeze/wrapper infrastructure is copied mechanically
with owned tokenizer paths and an explicit retained measurement session ID;
SHLVL=1, complete effective-environment SHA/per-key SHA, source/generated/binary,
SDK/runtime/host and all actual tokenizer/encoding/XML resources remain closed.
Controls/measurements await final verified PI code and coordinator review.
