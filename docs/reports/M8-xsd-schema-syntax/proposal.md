# Exact schema syntax allocation proposal

This is a proposal, not a grant. Coordinator01a12c6c alone launches after fresh explicit actual actor ACKs and durable exact grant/latest-start/expiry. No worker measurements/retries/extensions/new actors/builds. Max30seconds total,20work,10cleanup reserve. Disabled control then allocation-only measurement; no BDN or throughput.

Frozen input revision: `28e26b68231ca3981702b2e17d7fd445afe42c4c`; runtime/test acceptance revision: `444365439998500d6fac163af1a1c2259ec31168` (unchanged runtime thereafter).
Manifest: `docs/reports/M8-xsd-schema-syntax/preparation.json`, SHA256 `f613780fa3d57c7e0b1f82054c5ccb32f14c24c579f17c6839dfb220b56a1ce8`,456identities.
Supervisor: `docs/reports/M8-xsd-schema-syntax/measure.py`, SHA256 `d95f10918f135bfe80a739dc0db85193a8ec73850da0ed9b8f312d883ea8e6eb`.
Working directory: `/Users/sebastienros/.codex/worktrees/c928/xemele`.
Pinned SDK10.0.400/.NET10.0.11, macOS15.8.1 Arm64.

Exact child commands (argv):
```json
[
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/c928/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-schema-syntax/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--control",
    "/Users/sebastienros/.codex/worktrees/c928/xemele/artifacts/M8-xsd-schema-syntax/measurement-1/control.json"
  ],
  [
    "/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet",
    "--fx-version",
    "10.0.11",
    "/Users/sebastienros/.codex/worktrees/c928/xemele/benchmarks/Xemele.Benchmarks/M8-xsd-schema-syntax/bin/probe/net10.0/Xemele.Benchmarks.dll",
    "--measure",
    "/Users/sebastienros/.codex/worktrees/c928/xemele/artifacts/M8-xsd-schema-syntax/measurement-1/allocations.json"
  ]
]
```

Environment sets DOTNET_TieredCompilation=0, DOTNET_gcServer=0, DOTNET_GCConcurrent=0, NuGetAudit=false, RuntimeFrameworkVersion=10.0.11, UseSharedCompilation=false, XEMELE_SYNTAX_REVISION=28e26b68231ca3981702b2e17d7fd445afe42c4c; prepends pinned dotnet directory to PATH. Manifest hashes every source/runtime/binary/control/check input. No source or binary writes under grant.

Actual elevated verify-only passed all456identities+ps permission. Actual elevated supervised disabled control PID78856 exited0/reaped/groupabsent/logclosed;12routes x64 measured-loop operations executed with every counter/GC/timing disabled. No allocation result inferred. TERM/KILL/reap/group-absence/log-close core is byte-identical to the existing accepted facade supervisor. On completion/failure/expiry supervisor fsyncs cleanup RELEASE before rawinspection. Root must relay durable release before actors resume; expiry alone insufficient.
