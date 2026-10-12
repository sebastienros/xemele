# Exact component mapping allocation proposal

Proposal only. Coordinator01a12c6c is sole measurement launcher after fresh actual
host-wide PARK ACKs and a durable exact grant/latest-start/expiry. No worker
measurement, retries, extensions, new actors or frozen writes. Maximum30seconds:
20work+10cleanup; latest start grant+60, absolute expiry grant+90. Expiry does
not resume actors; durable cleanup RELEASE must precede raw inspection/resume.

Frozen source/probe revision: `fe72a34773e28a776c223d628fcccbd91f4b9bec`.
Accepted runtime/correctness revision: `bc8d191609fc48d84ae66d0a42ea8657ecb4a0fe`.
Compiler SHA256: `3f54c130d91a37feb4330980d0889a4bb638b5c32bdf036bf62aafb41f3f9ac3`.
Manifest `preparation.json`: `25b4d9c5ed5b31ca6f4235bc10f5ef13f82d6adf99ea94a6255f350d9f47dd60`,
464identities. Supervisor `measure.py`:
`aeea1c017c5a879c65f364989e50006796783db807b799c838f63c26b67faf7f`.
TERM/KILL/reap/group-absence/log-close core is byte-identical to accepted syntax
supervisor. Checkout `/Users/sebastienros/.codex/worktrees/c384/xemele`. SDK10.0.400/.NET10.0.11, macOS Arm64.

Exact prebuilt children, control then allocation only:
```json
[
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/c384/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--control",
    "/Users/sebastienros/.codex/worktrees/c384/xemele/artifacts/M8-xsd-component-mapping/measurement-1/control.json"
  ],
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/c384/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-component-mapping/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--measure",
    "/Users/sebastienros/.codex/worktrees/c384/xemele/artifacts/M8-xsd-component-mapping/measurement-1/allocations.json"
  ]
]
```

Environment: DOTNET_TieredCompilation=0, DOTNET_gcServer=0,
DOTNET_GCConcurrent=0, NuGetAudit=false, RuntimeFrameworkVersion=10.0.11,
UseSharedCompilation=false, XEMELE_MAPPING_REVISION=fe72a34773e28a776c223d628fcccbd91f4b9bec;
pinned dotnet directory prepended to PATH. No source/binary writes under grant.

19 routes:13 owned outputs (mixed zero; legal/invalid short/long ASCII,
multilingual and supplementary NCNames; actual long-whitespace declaration,
QName and schema-ID normalization; any empty namespace and whitespace-only
anyAttribute; public mixed-zero composition) and6 borrowed scanner routes
(short/long ASCII, long multilingual, short/long/supplementary invalid).
The borrowed operation/state is prebuilt, unlimited but still cancellation-aware;
results are consumed without boxing. Borrowed scans target0B steady allocations;
owned compiler/facade/diagnostic outputs require positive allocation controls.
No throughput, optimization timing or cross-architecture claim.

Each route has one separate first call,16warmups and64aggregate operations;
fixture construction/output JSON are outside per-route counters. Cold fixture
setup, first calls, positive1024byte array and net heap change with16retained
compiled graphs are separate. Retained heap delta is a coarse live-heap reading,
not a per-result size. Invalid routes require XSD.SchemaRestriction Invalid NCName,
preventing quota failures from counting as name rejection.

Actual final Release build/control at fe72a347:0warnings/errors. All19routes,
1555calls and36643consumed units pass; ALL allocation/GC readings disabled.
Actual elevated verify-only matches464identities and confirms ps permission.
Actual disabled supervised preflight PID96986 exit0/reaped/groupabsent/logclosed,
464before/after identities matched. No worker --measure launched. Root must
perform its own fresh elevated verify/preflight before grant.

Accepted correctness1263=117owned+33syntax+1113affected, resolver42;
public1417/frozen316+139/257/24/9/2/7 and normal+trueXML-only consumers;
5builds0warnings/errors;planning187/diff pass at bc8d1916. Task A3 remains
pending actual root measurement and durable RELEASE/raw summary.
