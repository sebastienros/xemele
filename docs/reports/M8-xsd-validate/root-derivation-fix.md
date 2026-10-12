# Root direct derivation fix

The worker explicitly handed back only `Evaluation/Values.cs` and
`AssessmentRouteTests.cs` at clean `039fb27b687ab4cce63bc7d7206b5176ec9ba9a2`.
All worker source, metadata and runtime jobs remained held during that interval.
Root added six list/union block controls at
`26732aefc3d03c51c0d15d596ba9756518fe5e8c`, then committed the minimal
simple-to-anyType restriction guard for a complex walk reaching a simple base at
`33bead34c657c7c92ec01732062a80b56a31773c`. Both paths and all ordinary
worker writes were explicitly returned; the checkout was clean.

Root actually ran pinned Release builds with zero warnings/errors at both source
revisions. The class-selected `AssessmentRouteTests` run passed 20/22 before the
fix, with list/union restriction-block cases incorrectly valid; after the fix it
passed all 22. Both runs had zero errors/skips/not-run on .NET 10.0.11 Arm64.
An earlier bare-method selection returned zero tests and was discarded; it is not
passing evidence. Exact test command:

```text
/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.M8XsdValidate.AssessmentRouteTests -parallelMode none -noLogo -noColor -result-xml /private/tmp/M8-core-direct-{before,after}.xml
```

The worker read both transient XML outputs and confirmed the 20/2 and 22/0
counts and pinned environment; raw files remain transient and are not committed.
Independent contract review accepted the same-blocker closure by source inspection
at `33bead34`; the reviewer executed no tests. Confirmed source identities:

| File | SHA-256 |
|---|---|
| Values.cs | 4260f592334420faa28dc9232d04b9f709e3726161adc873b3c1a357dbee5397 |
| AssessmentRouteTests.cs | f441e4e09e381146e9c17d5cf3e46b506dc441ab4fbff7df0c15e19f01703d74 |

The worker's actual full owned/affected rerun at this immutable revision selected
851 cases (156 owned, 695 predecessor): 849 passed and two failed, with zero
errors/skips/not-run. The only failures are the unskipped
`DynamicDefaultRequiresCanonicalDeclaredValue` and
`CanonicalDecimalDefaultDoesNotBecomeIntegerLexical` cases awaiting the ordered
verified datatype-constraint lexical helper. The all-in-one script stopped at
those failures.

Independent safe-resolver/API/consumer checks at the same source passed:
42 resolver cases with zero failures/errors/skips/not-run; 1,403 public and two
frozen evaluator members matched; normal and true XML-only consumers completed;
Release builds had zero warnings/errors; planning 180 and diff checks passed.
The resolver command is the exact safe argv from
`docs/reports/M8-resource-kind/test-commands.json` with the result path changed to
the task's ignored artifacts. No WarmBorrowed allocation test was selected.

The documented canonical-helper seam is planning information only; no unverified
implementation or dependency has been consumed. Allocation preparation remains
held until canonical closure, corrected-source acceptance and a fresh exact host
grant. All writes remain worker-owned after the explicit temporary handback ended.
