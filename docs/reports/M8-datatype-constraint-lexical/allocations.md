# Owned constraint result allocations

Accepted runtime `a58aed481ea2c6831111b73d037f92d1da21a2c1`; probe/source preparation
`e009c3643258342804d42ddb3c432a0d57c6b209`; frozen preparation handoff
`a848a03a30875b0906d3bc795336d4f70efb6c2c`.
Actual macOS 15.8.1 Arm64 .NET 10.0.11, SDK 10.0.400, workstation GC,
DOTNET_TieredCompilation=0, DOTNET_GCConcurrent=0.

Root granted `M8-canonical-a58aed48-alloc-only` at
`780bd70f49f97745febda2fc166819ec86ec3d98`. Latest start
2026-10-12T04:14:58.631096Z, absolute expiry 2026-10-12T04:15:58.631096Z;
120 seconds maximum including 110 work and 10 cleanup. Exactly two serial
prebuilt children ran: control PID 32885, measurement PID 32887; both exit 0,
reaped, process groups absent, logs closed. All 447 frozen identities match
before/after. Local fsynced cleanup release and root durable host RELEASE
`b22333d82fd4a5bc4616337d3e96799ff1410914` preceded raw inspection. The 0.664951-second
supervisor lifecycle is operational cleanup evidence, not a latency result.

Each warm row has 16 warmups, then one synchronous per-thread allocation-counter
aggregate over exactly 64 consumed calls. Preparation/fixtures stay outside these
warm numerators; parsing, canonical formatting and owned results stay inside.

| Owned route | Bytes / 64 calls | Exact mean B/call |
| --- | ---: | ---: |
| Text constraint (`abc`) | 13,824 / 64 | 216 |
| Int constraint (`01` → `1`) | 13,312 / 64 | 208 |
| Decimal (1,024 integer digits plus fraction) | 275,968 / 64 | 4,312 |
| Double (maximum finite) | 13,824 / 64 | 216 |
| Binary (1,024 base64 characters) | 192,512 / 64 | 3,008 |
| Temporal (70-digit year, 1,024 fractional digits) | 1,660,928 / 64 | 25,952 |
| QName with captured declaration context | 18,432 / 64 | 288 |
| List of selected union members | 40,448 / 64 | 632 |
| Actual-type canonical default assessment | 23,552 / 64 | 368 |

Separate cold fixture/registry/evaluator preparation plus first owned result:
100,056 bytes / 1 invocation. Positive 1,024-byte array control: 1,048 bytes.
All counter-disabled control numerators are zero because instrumentation is off;
that establishes no zero-allocation behavior. Every measured owned-result route
has a positive numerator, as expected. Actual-type assessment includes the
lexical facet check and typed equality, not a value-only shortcut.

Sixteen retained large decimal representations: whole-heap before 282,416 bytes,
after 429,680 bytes (delta 147,264). This informational observation includes
runtime/fixture/heap effects; it is not a portable marginal object-size claim.
These single-Arm64 synchronous allocation observations provide no sample
distribution, throughput, speedup, zero-allocation owned-result, x64 or async claim.
Final release cross-architecture performance confirmation remains a release gate.

Runtime assembly SHA256:
`32f95345d38a9b59e7c0c6623aae13ae0720465477e5d2aa41a81b9028ab5cde`.
Probe SHA256:
`b6aca09cbbebb8209159dc4e75a0c742fdd0c265a8ac3a4bf4307b153efdf6d2`.
Input SHA256:
`288ae296bf044b10fc99e660a2935f477970cb41b3d331e300630b6979a03cf5`.
Raw allocation SHA256:
`6ed3f2387d6b76ac8b4826d8937ea91130333896c6cd4bbbb23da750232f9f6f`.
Raw control SHA256:
`1095c89902eb15fae56236ae5cbf0af29a2b48ab3d10b2bda4bb7fae7643f150`.

Worker independently checked runtime/source/binary/input identities, all nine
64-call positive numerators, counter-disabled controls, cold/positive/retained
fields and both cleanup records after host RELEASE. Raw logs/results remain
ignored for coordinator inspection; only these concise aggregates are committed.

Root independently inspected and accepted these exact totals and control/positive/
retained identities at `aff6a0a3`, in its durable orchestration review record.
Final bench/report/preparation inputs were integrated into candidate `b91b9d99`;
source/tests/build/API/consumer/check inputs remained unchanged from the actual
combined-tree 731 checks at `e66a40943021b76c57923d3440446c4bfdcb43d0`.
