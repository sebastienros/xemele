# M8-rng-lexical handoff

Branch implementation is complete; coordinator main integration/verified metadata
remain required before corpus consumes the repair.

Only Compiler.cs, own task record and dedicated repair tests/fixtures/probe/reports
changed. datatypeLibrary now follows normative XLink projection and RFC2396/
RFC2732 absolute/nonfragment grammar, with empty support and source provenance.
Historical XML name rules cover every existing schema NCName/QName call site.
Projected output length is checked, its owner/components reserved before one
string allocation, and every emitted character is charged. The newly owned
location adapter is reserved per compilation. Frozen public/internal APIs remain
unchanged.

All nine original Jing schema predicates are retained byte-for-byte from
b18c2a5bd5cf8accfb62a3d4cba260ad6cbb78b9 with original expectations and hashes;
no corpus branch/harness/runtime or oracle edits were consumed. The pre-fix run
failed all nine on base83c47475; the final runtime/test revision
dc3c6e2a2bd8a5268b708015ed825471fe2e2307 passed405 affected tests:
80repair,88compiler,89validator,148textbinary; zero errors/failures/skips/notrun.
Release builds had zero warnings/errors. PublicAPI1410, frozenRNG444 and both
normal/true XML-only consumers (10 routes each) passed.

The coordinator independently reviewed source/accounting and actually reran the
same checks on combined source51550524f0c08647073da7657d4659e5affc78e7, including
182-task validation. Durable review9301cacc99ba37fb01b5e38d737f7b6de59f9bb7.
The later owned reusable checks.py was not claimed executed; manual equivalents
were executed. Prior failed owned test builds were fixed, and malformed UTF-16
witnesses construct surrogate units at runtime to avoid metadata replacement.

The root-controlled allocation-only lease completed with both children0/reaped/
groups absent/logs closed,497 unchanged identities and durable release before raw
inspection. Root independently accepted the exact aggregate totals/64 means.
See allocations.md and measurement-summary.json for ownership, denominators,
cold setup and approximate retained/RSS observations. No timing, speed,
individual distribution, primitive-zero or cross-architecture claim.

Required follow-up: coordinator integrates final reports/task metadata and records
verified main evidence before resuming corpus. No additional runtime fixes or
shared-contract requests. This worker relinquishes all task writes after its final
handoff; zero owned jobs/PIDs/sessions/cells remain.
