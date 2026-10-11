# M7-builder branch handoff

Status: implemented branch work, awaiting coordinator integration verification.
Implementation: `9abeeb9c8e21c47ace9f949b76b14de372271f39`.
Branch: `codex/m7-builder-continuation`.
Worktree: `/Users/sebastienros/.codex/worktrees/464f/xemele`.
Claim `713f91d`, base `b5a449e353526d791b4739b0b733d1896cbe44d1`, retained checkpoint `8d2d3668d4ff360718c42e6ebc4ece53d6d71e53`.
Both verified prerequisite integrations are ancestors. No main, original checkout,
shared contract/build or peer-owned implementation edits. `integration_revision`
is intentionally null; only the coordinator may set verified after integration.

The four public entry points implement document parsing from character/byte input,
caller-owned stream loading, and contextual fragments. Owned tree-construction
files implement insertion modes, adoption/foster parenting, templates, foreign
content, selectedcontent and processing-instruction patch semantics. Byte input
uses a bounded prescan, generated ASCII label matching and original encoding
indices. Noscript fragment state preserves the tokenizer's appropriate-end-tag
semantics for scripting-enabled raw text.

The full 166-resource ledger is complete, with original bytes and hashes retained.
All applicable original parser assertions run. Explicit browser execution
requirements and exact frozen-model API projections remain individually recorded;
they are not blanket script/extension exclusions. One serialization assertion is
preserved for its owning task below.

Correctness at committed measured revision (unchanged frozen Release binaries):

- 113 focused tests pass: 69 builder and 44 inherited tokenizer/model/PI/parsed-text
  tests; errors/failures/skips/not-run all zero. Output
  `artifacts/M7-builder/final-9abeeb9.xml`.
- 14,316 public-lane WPT executions, zero mismatches; 336 original standalone/static
  configurations and 899 exact assertions, all 15 referenced template support
  resources and both original HTML5_ELEMENTS loops; 2,231 named references.
- Expanded existing repeated-text witness covers adoption subtree moves,
  selectedcontent copies and patch range removal. Initial unquoted `name=x` PI
  witness correctly did not patch; the required quoted `name='x'` witness passes.
  An affected-case discovery invocation initially omitted its required artifact
  path and exited 134 before any workload; rerunning discovery with the path
  passed. Historical template-harness, analyzer and API-invocation failures remain
  described in the earlier reports.
- Official API union matches 1,235 compiled declarations (1,230 predecessors
  unchanged, four parser methods plus public type). All 3,388 fixture SHA values
  verified; bootstrap false; missing/added/changed negative controls reject.
- Four-entry-point sample runs; contextual tbody fragment and caller stream
  remaining open are demonstrated. Release builds had zero warnings/errors.
- Encoding generator check reproduces 228 labels and 91,711 index cells.
  Correctness-only probe validates 228 byte/char labels and six scan/prescan
  oracles; discovery confirms 24 affected ShortRun NoEmit cases.

Reproducible final checks (pinned host executable
`/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`):

```text
dotnet --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.HtmlTreeConstruction -namespace Xemele.Tests.HtmlTokenization -namespace Xemele.Tests.HtmlModel -namespace Xemele.Tests.HtmlProcessingInstructions -namespace Xemele.Tests.HtmlParsedText -result-xml artifacts/M7-builder/final-9abeeb9.xml
python3 docs/reports/M7-builder/check_api.py
dotnet --fx-version 10.0.11 samples/M7-builder/bin/Release/net10.0/M7-builder.dll
python3 docs/reports/M7-builder/generate_encoding_tables.py --check
python3 tools/planning/tasks.py validate
git diff --check
```

Released measurement evidence is in `final-measurements.md`. The first exclusively
granted run exposed repeated-foster copying with 2.204/2.400/2.676 allocation
ratios; it was reported before correction. Per-node resumed append buffers now
survive location switches and materialize at exact observation/move/copy/removal
boundaries and EOF. Corrected foster ratios are 1.980/1.990/1.995; adoption ratios
1.988/1.994/1.997. At N=2048, eight foster parses allocate 31,643,456 B instead of
66,136,896 B. All 98,304 borrowed operations allocate 0 B; DOM/byte/stream/clone,
cold/cache/oracle initialization and retained heap costs are explicitly reported.
The additional field costs 8 B per parser instance on the measured fixtures.

Durable coordinator GRANT and RELEASE enclosed exactly one corrected frozen
sequence. Initial/allocation/24 affected BDN/final/overall exits are all zero;
no child remains; source/binary/runtime/host/environment identity is unchanged.
Raw inspection began only after release. The 48 unchanged primitive timing rows
retain first-run provenance with six exact SHA proofs; no autonomous reruns or
further tuning. One Arm64 host, noisy timing and denied high priority; no universal
speedup or x64 release claim. Representative large/small encoding table getter IL
uses static data via RuntimeHelpers.CreateSpan<int>, with no getter array creation.

Required follow-up:

1. Coordinator integrates this branch and reruns applicable checks on the combined
   tree before recording its full integration revision and setting verified.
2. M7-serializer retains the exact original
   `noscript-template-querySelector.html` assertion
   `noscriptNode.innerHTML == "&lt;div&gt;&lt;/div&gt;"`, input
   `<template id="template1"><noscript><div></div></noscript></template>`,
   `ScriptingEnabled=true`. Parser text/node/null-descendant assertions pass;
   this serialization assertion is not claimed passing. Root approved this exact
   deferral; original bytes remain available.
3. Later performance ownership validates hardware-sensitive release claims on
   both x64 and Arm64. No additional branch measurement or tuning is requested.
