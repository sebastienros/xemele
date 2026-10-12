# Exact instance assessment allocation proposal

Proposal only. Coordinator01a12c6c is sole launcher after fresh actual host-wide
PARK ACKs and durable exact grant/latest-start/expiry. No worker measurements,
retries/extensions/new actors/frozen writes. Maximum30seconds:20work+10cleanup;
latest start grant+60, absolute expiry grant+90. Expiry alone never resumes
actors; root durable cleanup RELEASE must precede raw inspection/resume.

Frozen source/probe revision `092a3ba4b4fc513e3439a496fef690a1a0563a99`.
Accepted runtime/correctness revision `16687e8eae341b184f891a1f720e8c29be9e13fc`.
ValuesSHA256 `9996e56e717b4cf47e84b697b7e7a7eac2379d1a5ea497a99dd9d77f6c9c5104`.
SessionSHA256 `543440d2a89b801497bf2dbbe12e2774b4bf785193e75a8dea89faddd20845fb`.
ManifestSHA256 `42cb79cc5bd5626c123e2256b93185ffbc19f5b72650afee3465f23087138312`,462identities.
SupervisorSHA256 `2a74dfa9dd82c398f4a356e1a890ecd7f48af9bc53ce57c067a287361667c7d4`.
Checkout `/Users/sebastienros/.codex/worktrees/1156/xemele`; SDK10.0.400/.NET10.0.11 macOSArm64.

Exact prebuilt children, control then allocation only:
```json
[
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/1156/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-instance-assessment/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--control",
    "/Users/sebastienros/.codex/worktrees/1156/xemele/artifacts/M8-xsd-instance-assessment/measurement-1/control.json"
  ],
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/1156/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-instance-assessment/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--measure",
    "/Users/sebastienros/.codex/worktrees/1156/xemele/artifacts/M8-xsd-instance-assessment/measurement-1/allocations.json"
  ]
]
```

Environment DOTNET_TieredCompilation=0,DOTNET_gcServer=0,DOTNET_GCConcurrent=0,
NuGetAudit=false,RuntimeFrameworkVersion=10.0.11,UseSharedCompilation=false,
XEMELE_ASSESSMENT_REVISION=092a3ba4b4fc513e3439a496fef690a1a0563a99; pinned dotnet directory prepended to PATH.
No build/restore/discovery/source/binary writes under grant.

16owned positive-cost routes: matching lax local wildcard; no wildcard; strict
missing global; strict globally typed fixed equal/unequal; lax invalid global
integer; skip known global; permitted local before global; absent prohibited
default+key; nil string unique tuple exemption; nil anyType rejection; nil complex
simple content; nil element-only rejection; effective xsi:string; long multilingual
simple type lookup over16nils;64complex-type candidates before element-only nil
rejection. Setup precompiles schemas and parses DOMs. Each run validates a
prebuilt DOM and owns run/state/results/diagnostics; no public validation0B claim.
Invalid routes require the independent expected behavior and relevant diagnostic;
SCHEMA quota/resource diagnostics cannot count as intended rejection.

Per route1separate first call+16warmups+64aggregate calls, with outcomes consumed.
Cold fixture initialization, first-call costs,1024byte positive array and coarse
net live-heap change with16retained validation results are separate. Retained heap
delta is not per-result size. No timing/throughput/optimization/cross-architecture
claim. Optional BDN class has MemoryDiagnoser+ShortRunJob and is not launched.

Actual Release/control and elevated462identity verification/supervised disabled
preflight passed; see preparation-summary.md. Root must perform fresh elevated
verify/preflight and collect fresh actual host ACKs before any grant. Correctness
44owned+1146syntax/affected+45fixed+42safe;public1417/frozen316+139/257/24/9/2/7;
normal+true source-linked XML-only;planning188/diff. Runtime source independently
accepted by root/contract review at16687e8e; allocation payload awaits review.
A3 remains pending actual root allocation run and durable RELEASE/raw summary.
