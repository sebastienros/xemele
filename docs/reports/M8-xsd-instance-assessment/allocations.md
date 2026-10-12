# Actual instance assessment allocation evidence

Coordinator-only granted scope `M8-three-prepared-repairs-allocation-1` completed
successfully. Root session62384 terminated exit0; root durable cleanup RELEASE
at primary revision `f42e1a5e8` preceded root and worker raw inspection. This
worker launched no allocation measurement. Six serial control/measurement
children across three Xemele repairs exited0/reaped/groups absent/logs closed;
all1358 identities were unchanged before/after. Coordination covered Xemele only;
no claim about other projects or global host idle state.

Own frozen source/probe revision `092a3ba4b4fc513e3439a496fef690a1a0563a99`;
accepted correctness/runtime revision `16687e8eae341b184f891a1f720e8c29be9e13fc`.
SDK10.0.400/.NET10.0.11, Arm64, macOS15.8.1, workstation GC;
DOTNET_TieredCompilation=0, DOTNET_gcServer=0, DOTNET_GCConcurrent=0.
ManifestSHA256 `42cb79cc5bd5626c123e2256b93185ffbc19f5b72650afee3465f23087138312`;
supervisorSHA256 `2a74dfa9dd82c398f4a356e1a890ecd7f48af9bc53ce57c067a287361667c7d4`.
ProductassemblySHA256 `949790B99546568FB785E77F0A4E2700172D837201B0805720FE89B3810FC67A`;
probeSHA256 `DCD1939872BBA44A67998FAA0734CA81D8B95B3726F1C81B174A7C2628273D11`.

Separate counter-disabled control process and measurement process both passed,
with identical16routes/1312calls/1968consumed units. All control allocation/GC
fields and per-route firstBytes/bytes were0; that reflects disabled counters.
All measured first/warm owned routes were positive. Each route has1first call,
16warmups and64aggregate operations; fixture/output serialization and result-row
construction are outside per-route counters. Results and diagnostics are consumed.

| Prebuilt DOM validation route | First-call B | Warm aggregate B /64 | Warm B/op |
| --- | ---: | ---: | ---: |
| wildcard-lax-local | 9,944 | 622,080 | 9,720.000 |
| wildcard-absent | 10,224 | 650,240 | 10,160.000 |
| wildcard-strict-missing-global | 10,160 | 650,240 | 10,160.000 |
| wildcard-strict-global-fixed-equal | 11,976 | 714,752 | 11,168.000 |
| wildcard-strict-global-fixed-unequal | 11,608 | 743,720 | 11,620.625 |
| wildcard-lax-global-invalid-datatype | 10,536 | 674,304 | 10,536.000 |
| wildcard-skip-known-global | 9,720 | 622,080 | 9,720.000 |
| permitted-local-before-global | 10,352 | 662,528 | 10,352.000 |
| prohibited-missing-default | 10,136 | 645,632 | 10,088.000 |
| nil-string-unique-exemption | 15,624 | 999,936 | 15,624.000 |
| nil-anyType-identity-rejection | 13,920 | 890,880 | 13,920.000 |
| nil-complex-simple-content | 13,728 | 878,592 | 13,728.000 |
| nil-element-only-identity-rejection | 13,920 | 890,880 | 13,920.000 |
| nil-effective-xsi-string | 14,536 | 930,304 | 14,536.000 |
| nil-long-multilingual-simple-lookup | 42,728 | 2,734,592 | 42,728.000 |
| nil-long-graph-element-only-rejection | 21,064 | 1,348,096 | 21,064.000 |

Cold setup allocated29,438,480B, separately compiling16schemas, parsing16DOMs,
building the fixture's durable arrays/options and initializing relevant runtime
state. The positive1024byte array control allocated1,048B. Sixteen retained
validation results produced a coarse net live-heap change of240,472B
(4,145,664B before;4,386,136B after). This process-wide retained estimate includes
runtime/diagnostic/result state and is not a per-result object size.

Per-operation costs belong to public validation run state, schema lookup/traversal,
frames, typed values/constraints, identity rows and owned result/diagnostic
snapshots. The fix adds no explicit string/collection allocation in the prohibited
candidate branch or charged effective-type lookup. These composed owned costs do
not isolate that lookup or establish an allocation-free public validation API.
The unequal-fixed route's fractional11,620.625B/op is the observed aggregate;
no exact stable per-call claim or rerun is implied. Invalid routes check intended
validity/diagnostic outcomes and exclude SCHEMA quota/resource failures.

Own supervisor matched462identities before/after, with no mismatches. Control
PID35316 and measurementPID35318 exited0, were reaped, had absent process groups
and closed logs. Own lifecycle0.9484545s was within30s(20work+10cleanup); global
sequence was capped90s and stopped on any failure, with grant+60 latest start and
grant+90 absolute expiry. Lifecycle elapsed time proves cleanup bounds only.
No retry/extension/build/restore/discovery or frozen input write occurred under
grant; raw results stayed unopened until durable root RELEASE.

Evidence is synchronous allocation ownership on this one Arm64 runtime/setup.
No throughput, latency optimization, x64, AOT, zero-allocation public validation,
retained-size precision or global host-idle claim is made. Optional BDN class has
MemoryDiagnoser+ShortRunJob but was not run. No raw results/logs are committed or
archived; preserved ignored artifacts remain for coordinator cleanup.

Raw evidence SHA256 (content retained only in ignored artifacts):
- `control.json`: `603cc82ee671802cc33137fafd11f5fbe2a2298cdae5e4d6b1151fcb9efc8e3d`
- `allocations.json`: `91f809b07cc569952726cdbb2fe1ca5c65f0ab9b2479055f611a8926e15ab86f`
- `lifecycle.json`: `2a7ca992e2ba11de7106d0c66e59255ddf2ca86912cd759bda974ca380c0b873`
- `release.json`: `6414a73cb7273aaafa52cc2b73b72fff4a0a3d4b8a098d7cbcca7c97653bf18d`
