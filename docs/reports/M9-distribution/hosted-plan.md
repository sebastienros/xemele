# Bounded hosted platform checks

Human asked whether time-limited GitHub runners could provide OS evidence.
Read-only GitHub access succeeds; repository sebastienros/xemele is PRIVATE and
its remote currently has no registered workflows. Standard Linux/Windows x64 and
macOSarm64 runners are available. Each owned release job is capped20minutes with
at most3parallel jobs; upload only concise outcome summary, retention7days.
The push trigger is restricted to codex/m9-os-validation; no package publication.

Per OS: SDK10.0.400/.NET10.0.11; full10741unit test expectations with zero failures,
skips/errors/not-run; deterministic runtime/tool/symbol packages with notices/docs
and embedded Source Link; fresh managed/trim/NativeAOT consumers; local tool install.
Windows has a concrete exact-runtime managed ILLink host, because servicing-suffix
ILLink runtimeconfig cannot be assumed to start under Disable roll-forward.
Hosted-specific behavior is still UNRUN and gets no credit until actual outputs.

The owning M9-build-compatibility repair is verified: parent Contracts and standalone
usage build0warnings/errors; the fixture uses the established .cs.fixture convention.
Generic build.yml is capped20minutes and excludes only the reserved validation
branch push to prevent duplicate jobs; normal build/PR checks are preserved.

A new private validation branch must contain the current local implementation and
workflow. Earlier explicit instruction excludes remote pushes, so pushing that
specific branch and consuming private-repository Actions minutes require a concrete
human approval. No remote main update, tag or package publication is proposed.
Standard private jobs use included minutes and can be billed beyond the allowance.

This supplies OS correctness/package evidence when executed. Shared hosted timing
remains informational under the frozen performance protocol; the6-hour GitHub job
limit does not establish a24-hour fuzz campaign without validated continuation.

GitHub sources: https://docs.github.com/en/actions/reference/runners/github-hosted-runners
https://docs.github.com/en/actions/reference/limits
https://docs.github.com/en/billing/concepts/product-billing/github-actions
