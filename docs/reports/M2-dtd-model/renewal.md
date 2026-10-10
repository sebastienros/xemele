# Renewal after verified resolver measurement repair

Coordinator renewed exactly M2-dtd-model at claim
`f9ce78b1e1eb46f6726865c5f435365ab321b23b`, base
`8af65f385654c382f201e1ad8c32f9be2cc3bb87`. The prerequisite
M2-resolver-allocation-probe is verified at integrated revision
`4e558709ba99879b22a892d17912dd8204e5baea`; M2-contracts remains verified
at `ab2fcabaf3cc984901f484c6fce1069c385e8c2f`. All three exact revisions are
ancestors of combined source `21e6c36ad7d2c01e43412f330f6a989c4a76cd6f`.

The worker merged the exact claim locally, resolving only its task JSON. Main's
owner/branch/base/dependencies/status/integration fields were retained together
with all prior model acceptance/evidence/implementation/handoff information.
No other conflict occurred. The production model, focused tests, standalone
sample, extracted approved model and verifier are byte-for-byte unchanged from
independently source-reviewed `e020db32fec509f122f0ffec37135ca2ee06724f`:

```sh
git diff --exit-code e020db32fec509f122f0ffec37135ca2ee06724f 21e6c36ad7d2c01e43412f330f6a989c4a76cd6f -- src/Xemele/Parsing/Dtd/Model tests/Xemele.Tests/M2-dtd-model samples/M2-dtd-model docs/reports/M2-dtd-model/Approved docs/reports/M2-dtd-model/verify.py
```

The existing verifier already supports `--audit-base`; no verifier source change
was needed. Its audit against the renewed claim includes all preexisting owned
branch files while excluding verified coordinator/prerequisite changes carried
by that claim. Earlier CS2001 preparation and first direct Resolver4080B failures
remain in the task evidence and `first/` reports.

Fresh combined check command, executed on the committed combined source:

```sh
export DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet'
export PATH="$DOTNET_ROOT:$PATH"
python3 -B docs/reports/M2-dtd-model/verify.py --report-prefix renewed --audit-base f9ce78b1e1eb46f6726865c5f435365ab321b23b --runtime-count 306
```

The unique `renewed/` report records the actual outcome; this renewal is a combined
verification after a verified prerequisite change, not a retry of the unchanged
initial cumulative candidate. No new feature scope, production edits, benchmark
hold, test exclusions or runtime setting changes were introduced.
