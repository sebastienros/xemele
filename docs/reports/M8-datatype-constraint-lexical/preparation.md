# Prepared allocation-only probe

Runtime source accepted at `a58aed481ea2c6831111b73d037f92d1da21a2c1` remains
unchanged. Probe/supervisor source preparation revision:
`e009c3643258342804d42ddb3c432a0d57c6b209`.

After explicit host RELEASE, offline Release preparation passed with zero
warnings/errors and actual .NET 10.0.11. All nine warm owned-result routes and
cold first-result/control/retained routes ran successfully with allocation
counters disabled. Existing SCH TERM/KILL/reap/group/log helpers were reused
verbatim. Counter-disabled supervised preflight PID 30026 exited 0; child reaped,
process group absent, no group members, logs closed. Its initial 439 identities
matched before/after. Complete runtime/hostfxr/probe files and the preflight
control/log/lifecycle were then added; final 447 frozen identities and process
inspection passed verification. No allocation measurement has run.

Manifest SHA256:
`85b8b3d23e21d0d40e1f146275d333e5de94d63b95f1cd4ee7ac98820c44aa1e`.
Supervisor SHA256:
`bdfa23f93beb50414268459c70a49d97ee8b8fca628da7bd1f7b16168ee411d5`.

Proposed single fresh grant: maximum 120 seconds total, 110 work plus 10 cleanup;
exact two serial prebuilt children (control, then measurement), stop first failure.
The root must provide approved manifest hash, latest-start UTC and absolute
expiry UTC. No builds, tests, discovery, retries or raw inspection under grant.
All jobs/sessions finished before this request. Local fsynced lifecycle/release
and coordinator durable host RELEASE must precede raw result inspection.
