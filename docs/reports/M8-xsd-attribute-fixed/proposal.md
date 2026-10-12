# Exact fixed attribute allocation proposal

Proposal only; coordinator01a12c6c sole launcher after fresh host-wide explicit actual actor PARK ACKs and durable exact grant/latest-start/expiry. No worker allocations/retries/extensions/new actors/frozen writes. Maximum30 seconds total:20 work +10 cleanup reserve. Latest start Grant+60, absolute expiry Grant+90. Expiry alone does not release actors; durable cleanup RELEASE must be relayed before raw inspection/resume.

Frozen revision: `139b9cf98a66f4a6a42f859a7a1b5360482ed6eb`. Accepted focused runtime/test revision: `5dfc443663069e7b25fc1817363da50a07bd1d21`; runtime unchanged from reviewed78c44c372 repair.
Manifest `docs/reports/M8-xsd-attribute-fixed/preparation.json`, SHA256 `b164cb10b3ce774f1e9472eb7a89d97372ef4a438589bfd4c08597a898429238`,454 identities.
Supervisor `docs/reports/M8-xsd-attribute-fixed/measure.py`, SHA256 `99bd3834d0a466b058a46c35369068b6181c1ceaf2fd140919063bcd05cf5526`. TERM/KILL/reap/group-absence/log-close core byte-identical to accepted facade supervisor.
Checkout `/Users/sebastienros/.codex/worktrees/b04b/xemele`.
Pinned SDK10.0.400/.NET10.0.11; macOS15.8.1 Arm64.

Exact prebuilt child argv (control then allocation only; no BDN or throughput):
```json
[
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/b04b/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--control",
    "/Users/sebastienros/.codex/worktrees/b04b/xemele/artifacts/M8-xsd-attribute-fixed/measurement-1/control.json"
  ],
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/b04b/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-attribute-fixed/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--measure",
    "/Users/sebastienros/.codex/worktrees/b04b/xemele/artifacts/M8-xsd-attribute-fixed/measurement-1/allocations.json"
  ]
]
```

Environment: DOTNET_TieredCompilation=0, DOTNET_gcServer=0, DOTNET_GCConcurrent=0, NuGetAudit=false, RuntimeFrameworkVersion=10.0.11, UseSharedCompilation=false, XEMELE_ATTRIBUTE_REVISION=139b9cf98a66f4a6a42f859a7a1b5360482ed6eb; prepend pinned dotnet directory to PATH. No source or binary writes under grant.

12 owned routes,16 warmups/64operations each: typed list short/long equivalent and unequal; token multilingual equivalent and unequal; QName equal expanded names/different namespace contexts; public complete composition short decimal and long multilingual valid/invalid. Consume every result/diagnostic. Fixture construction and output JSON are outside per-route counters; cold fixture setup/positive byte-array control/16retained programs-schemas are separate readings. Invalid routes check actual XSD.SchemaRestriction, preventing quota rejection false positives. No zero-allocation or throughput claim; all routes own outputs/temporary typed values.

Preparation control executed all12 routes,1456consumed,16retained with ALL counters/GC/timing disabled. Actual elevated verify-only passed454identities+ps. Actual elevated disabled supervised preflight PID88642 exit0/reaped/groupabsent/logclosed;454before/after matched. Raw preflight/control files remain ignored artifacts. No worker --measure was run.

Checks: owned45/datatype77 at5dfc443663069e7b25fc1817363da50a07bd1d21; affected1110/resolver42/public1417/frozen APIs/normal+trueXML-only at a024304fc82de5bf9cabd4da4252f797c8a0da05 with empty runtime diff; original baseline and both witness-only failed attempts preserved in checks.json/task evidence. A3 remains pending actual root allocation evidence.
