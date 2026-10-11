# Temporal measurement evidence

Root executed the two already built temporal children under combined grant
`cbf9167bd7d5213f0b7bced6ad6e272c791f4433` and durably recorded RELEASE
`49b063ec7390ad03fc711287c022a98de4620530` before any raw inspection.
Both worker and root independently inspected the results after release. No rerun.

Accepted runtime/test source is `8ed24fb19fc88feb45fa79c856e7ea3c9d216c09`.
The raw `sourceRevision` is `a11a484a5254a17f9d7c4885eed9ce083ff71dc5`, a later
review-metadata commit with unchanged runtime/test source. Prepared revision is
`be88b3351f9128127be0369907f18dd610864f01`, manifest SHA256
`3a43c3fb7d433f2d33d60c0f558e480bb230e5e73039ea07390c718dab2b153d`.
All445 actual source/config/payload/dependency/runtime/input identities matched
before and after. The supervisor completed in58.780023957951926s, well inside its
600-second bound (590 work/10 cleanup). Children24490/24493 exited0, were reaped,
owned groups were absent and logs closed. Raw outputs remain under ignored
`artifacts/M8-datatypes-temporal/measurement/`; only summaries are committed.

Environment: Apple M4 Pro,14 logical/physical cores, Arm64, macOS15.8.1,
SDK10.0.400, actual .NET10.0.11/RyuJIT, workstation GC (`serverGc=false`).
Counter-disabled12route controls, four oracle/provider controls, exact eight
ShortRun NoEmit discovery and elevated process-inspection/cleanup preflight passed
before grant. Fixture generation/setup occurred outside warm sample boundaries.

Each route had32 warmups and64 per-thread allocation samples. These synchronous
routes consume results through assertions/KeepAlive. Setup, scratch growth and
owned persistence are explicitly outside the warm primitive claim.

| Route | Exact bytes → sample count |
|---|---|
| Calendar equality |0 →64|
| Long fractional time comparison |0 →64|
| Duration partial order |0 →64|
| Duration equality |0 →64|
| Calendar hash |0 →64|
| Duration hash |0 →64|
| Owned zoned calendar |536 →64|
| Owned absent-zone bounds |976 →64|
| Owned arbitrary year/fraction |24008 →64|
| Owned arbitrary duration endpoints |67296 →63;67608 →1|
| Invalid long fractional time |624 →64|
| Operation/provider/context setup |920 →64|

The duration route's additional312 bytes in one sample are unexplained; the sample
is retained and is not attributed to JIT/runtime or discarded. The invalid input
allocates its parsed fraction before calendar rejection; the error itself is a
bounded shared static object. Cold fixture/setup cost148144 bytes, including JSON
loading, metadata/delegate initialization and initial owned parses. The positive
1024-byte array control measured1048 bytes. The128 retained long calendars had
inclusive full-GC memory273824→569784, delta295960 bytes. That includes the retained
array/runtime effects and is not an exact per-value retained size.

Eight `[MemoryDiagnoser,ShortRunJob]` cases used prebuilt InProcessNoEmit:
one launch, three warmups, three actual iterations each (24+24 total); no generated
builds/job CLI flags. The independent scalar oracle covers identical canonical
time whole seconds and zone absence, comparing only fractions. It is deliberately
smaller than the provider's dispatch/accounting/calendar-key path. The provider
is slower; no speedup or parser-wide optimization is claimed.

| Case | Scalar mean ns | Provider mean ns | Provider/scalar |
|---|---:|---:|---:|
| longEqual |238.173|518.600|2.18|
| longMiss |238.414|522.936|2.19|
| shortEqual |2.666|31.442|11.79|
| shortMiss |2.274|31.396|13.81|

All CSV rows report rounded0B/op (Markdown dashes), but the exact GC diagnostic
passes are as follows. Collections were0/0/0 for every row. Rounded rows do not
prove exact zero; the six separately sampled warmed primitive routes above do
establish zero for those fixtures. Nonzero BDN residuals are left unattributed.

| Case/method | Exact total bytes | Diagnostic operations |
|---|---:|---:|
| longEqual/scalar |0|2097152|
| longEqual/provider |2888|1048576|
| longMiss/scalar |2304|4194304|
| longMiss/provider |1208|1048576|
| shortEqual/scalar |0|134217728|
| shortEqual/provider |2888|16777216|
| shortMiss/scalar |0|268435456|
| shortMiss/provider |2600|16777216|

Full summary tables, exact counters and transient raw SHA256 references are in
`measurement-summary.json`. Timing is provisional single-host microbenchmark
evidence; composition/end-to-end and x64 release confirmation belong downstream.
No SearchValues/token matcher optimization is introduced: digit conversion and
metered arithmetic must visit each unit, and the implementation retains the
straightforward scalar path.
