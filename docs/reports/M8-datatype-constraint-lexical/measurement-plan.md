# Planned allocation-only evidence (not executed)

Run preparation only after coordinator source acceptance and host RELEASE.
`prepare.py` builds the standalone probe offline, consumes all routes with
counters disabled, and freezes source, runtime, hostfxr, build inputs, probe,
assembly and control identities. No BDN or timing claim is requested.

Request one fresh durable 120-second lease, including 110 seconds for work and
10 seconds reserved for cleanup. `measure.py` requires approved manifest SHA256,
latest-start UTC and absolute-expiry UTC. Late start never extends the grant.
Exactly two serial prebuilt children: control, then measurement; no build,
discovery, retry, result inspection or second launch after failure.

One cold setup/first-owned-result numerator has denominator 1. Nine warm routes
have 16 warmups then one allocation-counter aggregate over exactly 64 consumed
calls each. The borrowed writer is the sole zero target; all eight result routes
own allocations. Registry/evaluator preparation and fixtures stay outside warm
counters; owned parsing, formatting, result and actual assessment costs remain
inside. The positive byte-array control proves instrumentation. Sixteen retained
large decimal representations have whole-heap before/after observations, not a
portable marginal retained-memory claim.

Before and after each lease, verify frozen identities; supervise process group,
kill/reap on failure or timeout, close/drain output, and prove no owned children
remain. Report lifecycle and request immediate durable RELEASE before reading
raw allocation results. Durable evidence contains concise numerators,
denominators and limitations only; raw logs/results stay ignored and are removed
only after coordinator inspection. Source-independent review and integrated
correctness/API/normal/XML-only checks remain coordinator responsibilities.
