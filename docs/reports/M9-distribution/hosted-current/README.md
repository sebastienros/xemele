# Current repaired hosted validation

[Run 38454912150](https://github.com/sebastienros/xemele/actions/runs/38454912150), attempt 1, used `929585ddf1a5413d9560fb7cef4b4180d640da67`. The directly authorized validation branch push triggered exactly one three-OS matrix. Each job was capped at 20 minutes; observed job elapsed time totaled 14.27 minutes (not an assertion about GitHub billed-minute rounding). No dispatch, duplicate run, stale rerun, fuzzing or publication occurred.

| RID | Unit tests | Package/managed/trim/AOT/tool | Result |
| --- | --- | --- | --- |
| linux-x64 | 10,754 passed; zero failures/errors/skips/not-run | All passed | Success |
| osx-arm64 | 10,754 passed; zero failures/errors/skips/not-run | All passed | Success |
| win-x64 | 10,712 passed; 42 failed; zero errors/skips/not-run | Not reached | Failure |

Linux and macOS each completed 21 commands, all with clean direct-child/process-group/log receipts. They built two clean byte-identical canonical runtime/tool package pairs, verified license/notices/docs/embedded exact-revision Source Link, restored isolated PackageReference-only consumers, and executed managed, trimmed and NativeAOT usage plus tool install/help. Remote source fetch was not tested. All three OS jobs provisioned all 3,386 hash-pinned W3C files with exact original bytes. The earlier missing corpus setup is resolved on this commit.

Windows diagnostics identify 38 `M8XsdTypeRelations.OriginalTests` failures with `Denied outside fixtures`, plus four allocation-child guards rejecting `Child request outside owned artifacts` in PI, streaming, tree and DTD framing tests. The child logs confirm exited children, not timeouts. These are test-harness path portability failures; no unsupported runtime correctness or Windows package pass is inferred. Fixes require their own owned repair task and actual Windows verification. The consumed run budget does not authorize another push/run.

`outcome.json` preserves run/job identities and times, package hashes and links to concise OS summaries. Summaries retain exact command identities/receipts, downloaded-artifact hashes, every failure identity/message and bounded child diagnostics. Full matrix acceptance remains pending; M9 distribution stays blocked. Fuzzing remains deferred and dedicated timing requirements remain outstanding.
