# Release performance accounting

The frozen targets remain unchanged in benchmarks/baselines/release/targets.json.
`accounting.json` binds existing result/protocol sources and identifies every
required candidate/reference outcome. No controlled release comparison has run;
M9-performance-A1/A2 remain pending. The human reports no dedicated timing machine
or prior release report. Standard shared GitHub runners can provide informational
timing and actual OS correctness, not the stable dedicated-machine release gate.

All runtime source remains identical to verified M8, so its actual cumulative
allocation checks retain their original scopes. Earlier 1GiB streaming memory
reports retain observed bounds and missing-RSS limitations. Their older exact
closure is compared to current source; changed files require requalification,
not a silent whole-release memory pass. Earlier DOM and XPath microbenchmarks
are provisional single-host evidence, not equivalent native/Rust/BCL primary-suite
geometric comparisons. The documented unexplained7B/op64KiB push observation
remains7B, not zero. No new throughput, retained DOM ratio or regression credit.

When controlled hardware is available: finish the exact representative
fixture/reference operation freeze; prepare all consumers before timing; alternate
each pinned native/Rust/BCL reference and candidate on that hardware; validate
semantic outputs; record confidence, lifecycle, allocations, retained bytes and
RSS separately; evaluate every primary floor and aggregate target; repeat only
required confidence investigations. Preserve concise actual tables/provenance,
inspect/remove raw outputs and use finite cleanup. Allocation-only checks require
no PARK/exclusive grant, and no foreign-project coordination is permitted.

No runtime optimization request is justified by a fabricated comparative deficit.
Any demonstrated current runtime failure goes to its smallest explicit owner.
The baseline/report deliverable is locally reviewable; release timing remains an
honest external blocker, and preparation/requalification work is explicit.
