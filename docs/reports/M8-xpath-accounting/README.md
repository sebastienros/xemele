# XPath accounting delivery

Implementation: `88ce17fc956cb26de15ee3823fe5c766c80f1ddb`.
Prepared plan: `d766bef0af08e1077c1f7f35ddd525227c6ec28a`.
Branch: `codex/m8-xpath-accounting`; checkout:
`/Users/sebastienros/.codex/worktrees/956b/xemele`.
Base `3000efdc9d9c1768810ace6372ad52aa51be6bd2` and coordinated claim
`5b7730916dba401857e67f6c2705328e4ea7af1d` are ancestors; prerequisite
M8-contracts and M4-evaluate integration revisions are ancestors and verified.

## Behavior and contract

The two internal `XPathExpression.EvaluateWithAccounting` overloads initialize
the output to zero before entry guards and construct the existing XPath budget.
Their `finally` covers connectivity, dynamic context capture, navigator snapshot,
context/variable binding and evaluator execution. It copies actual successful
charges even after failure. Rejected attempted charges are excluded. The static
`XPathEvaluationAccounting` adapter forwards directly without catching exceptions.
Public Evaluate delegates to this same path; context-specific guard order,
document lookup order, option snapshots, results and exception provenance remain.
There is no reflection, alternate compiler/evaluator or later-milestone dependency.

`internal.golden.txt` freezes the exact five compiled internal join rows: the
static adapter type and its two methods, plus the two expression instance methods.
Access, static shape, nullable context options, out Int64, and nonoptional
parameters match the M8 fixtures and decision. The owned Roslyn extractor compares
all public metadata against the original facade linked with otherwise identical
integrated source. All **1,253 public members are unchanged**. Four deliberate
omission/signature corruption comparison controls reject. The coordinator's later
resource-kind integration adds its separate public member; this worker does not
rebase or edit that contract.

## Correctness and checks

At implementation revision `88ce17fc956cb26de15ee3823fe5c766c80f1ddb`, the explicit
.NET 10.0.11 test execution passed **368/368**, with no failed, skipped or unrun
cases: 353 existing M4 cases and 15 accounting cases. New tests cover both facade
context forms, attribute/namespace/text items, actual counts on successful runs
and in failure finally blocks, zero before budget creation, detached connectivity
charges, setup enumeration/callback identity, cancellation before/during setup and
dispatch, unchanged input/result/string limits and diagnostics, entry snapshots
during mutation, independent runs and exact/one-short shared allowances across
three different expressions. A test-local enclosing budget applies the frozen
minimum cap, refuses zero remaining before option construction, and debits actual
work on success and failure without hiding primary cancellation. It introduces
no schema model implementation.

Pinned CLI is `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`;
SDK 10.0.400, Release, compiler/build-server reuse disabled. Commands:

```text
<dotnet> build tests/Xemele.Tests -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
<dotnet> --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace 'Xemele.Tests.M4*' -namespace Xemele.Tests.M8XPathAccounting -noLogo -noColor -parallelMode none -result-xml artifacts/M8-xpath-accounting/correctness.xml
python3 docs/reports/M8-xpath-accounting/check_api.py --dotnet <dotnet> --baseline artifacts/M8-xpath-accounting/prepared/baseline-runtime/Xemele.dll
python3 tools/planning/tasks.py validate
git diff --check
```

The complete public/internal comparison and planning/whitespace checks passed at
88ce17f. A clean committed-tree Release build also passed at d766bef with zero
warnings/errors, followed by planning validation of 171 tasks and whitespace
checks. Runtime/test sources are identical between these two revisions; only the
measurement plan is added. The initial build at claim HEAD with preliminary dirty
tests failed with one struct-property assignment error and four xUnit cancellation
helper analyzer errors. Those tests were corrected without suppressions before
the passing source was committed. That failure is not passing evidence.

## Bounded owned allocation evidence

Exactly one coordinator-granted sequence, `M8-xpath-accounting-allocation-1`, ran:

```text
python3 docs/reports/M8-xpath-accounting/lease.py -- --measure
```

Main acknowledged PARK; the peer worker was terminal and source review accepted
88ce17f and its prepared plan. The wrapper selected .NET 10.0.11 explicitly,
checked 4,131 frozen source/generated/binary/SDK/runtime file mappings plus host
and effective runtime environment before/after, and bounded/reaped its own two
process groups. Both child exits and wrapper exit were zero, `no_children=true`.
The child sequence, cleanup and final identity check took 0.946231417 seconds
inside the 60-second grant. RELEASE was reported before raw inspection, and the
coordinator acknowledged durable release before inspection. The coordinator then
independently inspected both JSONs and accepted the disclosed evidence. This is
process lifecycle evidence, not throughput or global host-idle evidence.

Host: Arm64, macOS 15.8.1, 14 logical processors. Probe preparation builds and both
`--control` runs passed with allocation counters disabled. Inputs/expressions are
frozen in `measurement-plan.json`: a three-element XML document with ASCII and
`根😀`, scalar arithmetic, `/r/a` owned node-set output and Unicode string length.
The baseline links the exact original XPathExpression bytes from the integrated
base; all other runtime source is unchanged. Candidate public and internal calls
use the same parsed document, compiled expressions and null dynamic options.
Each case warms 16 operations then consumes 64 measured results synchronously.
Fixture parse/compile precedes warm counters; every Evaluate still owns its
document/dynamic-context snapshot, continuation/scratch state and results.

| Expression / context | Baseline public bytes / 64 | Candidate public bytes / 64 | Candidate internal bytes / 64 | Baseline/public B/op | Internal B/op | Actual internal work/op |
|---|---:|---:|---:|---:|---:|---:|
| Scalar / XmlNode | 532,480 | 532,480 | 532,480 | 8,320 | 8,320 | 434 |
| Scalar / XPathItem | 528,384 | 528,384 | 528,384 | 8,256 | 8,256 | 432 |
| Path / XmlNode | 637,440 | 637,440 | 637,440 | 9,960 | 9,960 | 514 |
| Path / XPathItem | 633,344 | 633,344 | 633,344 | 9,896 | 9,896 | 512 |
| Unicode / XmlNode | 771,584 | 771,584 | 772,136 | 12,056 | 12,064.625 | 508 |
| Unicode / XPathItem | 767,488 | 767,488 | 767,488 | 11,992 | 11,992 | 506 |

Every baseline and candidate public distribution consists of its table B/op
value repeated 64 times. Five internal distributions are also identical. Internal
Unicode XmlNode has **12,056 B × 63 and 12,608 B × 1**, with the extra 552 B at
zero-based sample index 27. Its median/mode match baseline; its actual aggregate
does not. No sample is discarded, no overhead is subtracted and no rerun was made.
Consumed totals are 448 for scalar, 192 for path and 128 for Unicode in every
route. Internal work totals are respectively 27,776/27,648, 32,896/32,768 and
32,512/32,384 for node/item. Counters are stable across all independent runs.

Both processes report cold fixture setup **32,112 B** and positive `byte[1024]`
control **1,048 B**. Cold setup includes parsing, compilation and static
initialization reached there; first-evaluation cold initialization is not isolated.
These whole-operation owned costs are positive and are not primitive zero gates.

Offline source/probe inspection finds no newly introduced allocation site: the
existing one-budget construction moves from Create into each equivalent entry
path; Create's existing context/navigator/evaluator construction remains; the
bridge's added state is an out Int64 and finally read; the static adapter only
forwards. Sample counters include Run, numeric/node-count oracle consumption and
distribution storage. Distribution arrays precede counters; row/JSON construction
follows them. The single extra 552-byte sample cannot be attributed from these
counters. This report makes no JIT-cause, exact all-sample parity, zero-allocation,
timing or cross-architecture claim. Unchanged primitive evidence remains with
M4-model/context/compiler/navigation/functions; this bridge changes none of those
implementations or their borrowed primitives. Later schema composition remains
its implementing task's responsibility.

Identity manifest SHA256:
`f7f48af6fda5ab89284fb8b365468e5627353ee335d06adcec984e790b0b45b0`.
Probe source SHA256:
`e14d7669aeb113b2fcaa6aad74165b8aa42678344b9dbff028cd3a9ace91ca1c`.
UTF-8 input SHA256:
`ef6b92dfd0f98017a1846cec786ab6cd2cebec44747c5c70c57fb614a837e9a7`.
Baseline/candidate runtime DLL SHA256:
`047a5ea94cd0ec0be716d94b3a4e4c6ecda6b50b4f1974f71f7a15d36fd75bb4` /
`99f5947fa55453036ede77d589277646cce7a8f67145c151d433531d00ed1188`.
Baseline/candidate probe DLL SHA256:
`c054c2e93e8e04d01a8afbe22a8c23293fe94ed35f1c0ea82ef5e134cb53c0c7` /
`1d1ee43ca8d9d40c62034b440b0736bcf0e952302ea68c83807b7b0b0898d064`.
Inspected baseline/candidate raw JSON SHA256:
`47a670c9793b0ff3b51c7e323b75699f5f8ae515c7853ab98fee0d36d32d24ab` /
`d60fa1cc4aa6036630b4a50d23428074bb99f5a5d948a5bc5d6f1d611de9f0e0`.

Only summaries, analysis, owned source/checkers and frozen contract/plan are
committed. Raw results/check outputs are transient under ignored artifacts, left
for coordinator cleanup after inspection; prepared caches and older retained
artifacts remain. No outside-owned paths were edited. Integration verification
and the later schema budget caller belong to the coordinator and owning tasks.

## Coordinator integration

Final integration `76911ffc84ff961a81e11c1428303fac68117887` contains the accepted source and handoff. Combined runtime checks passed at `0f5d9845e2def74e104aea798e293b978752f616`: the exact executed driver is retained in `integration-commands.py`. Cache-only restores and all three pinned Release builds passed with zero warnings/errors. All 368 XPath tests passed on actual ARM .NET 10.0.11, with zero errors/failures/skips/not-run. The compiled public union matches 1,254 declarations including the independently verified Schema enum member, and all five frozen internal accounting declarations match. Planning validates 171 tasks; whitespace passes.

The final merge adds only worker report/task evidence to that tested combination; source, tests, inputs and configuration are unchanged. The coordinator verified this exact two-path difference and retained the successful checks. All 14 worker paths are owned relative to the coordinated claim; the earlier base-to-claim coordinator metadata is not worker work. Worker terminal status and clean merged ancestry were checked before cleanup. Generated raw/check/preparation outputs are transient and removed with the completed checkouts after inspection; original retained milestone checkpoints, manifests, caches and toolchains remain untouched.
