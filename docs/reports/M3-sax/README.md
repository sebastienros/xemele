# M3-sax verification

The implementation adds exactly the six frozen synchronous overloads and the
empty sealed XmlSaxCompletion record. The copied Sync fixture is audit evidence;
its fake bodies are never compiled into the runtime. String transport supplies
borrowed character memory directly. Bytes/Stream transport owns one 4096-byte
scratch array until the pump reports NeedInput. Caller streams remain open.
No SAX transport uses a DOM, a whole-input byte clone, or an encoding roundtrip
for string input. No new scanning/matching optimization is introduced.

The internal HandlerSink directly forwards all sixteen borrowed notifications.
Parser/decoder rejection is represented by the pump result, never inferred by
catching XmlParseException. Consequently callback-thrown parse exceptions retain
identity. Diagnostic results and exceptions use the pump's ordered diagnostic
values and existing owned snapshot constructors. EndDocument is suppressed by
the pump after repair or failure. Cleanup preserves a primary exception.

The probe distinguishes cold operation setup, warm whole operations, owned
completion/diagnostic results, and zero-allocation borrowed dispatch/copying.
The generated stream has no corpus-sized buffer; callbacks sample live managed
memory at 1 MiB intervals and check post-disposal retention. Literal scanning
and pump allocations remain their existing owners' responsibilities. Throughput
from this shared Arm64 host and Dry BenchmarkDotNet is informational, with x64
release confirmation outstanding. No cross-architecture claim is made.

## Reproduction

Pinned executable: `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`.
Use Release and `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false
--disable-build-servers`, restore with `-p:NuGetAudit=false --ignore-failed-sources`.

- Build `tests/Xemele.Tests/Xemele.Tests.csproj`, `samples/M3-sax/M3-sax.csproj`,
  `benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj`, and
  `docs/reports/M3-sax/Probe/Probe.csproj`.
- Build `docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj` and
  `docs/reports/M3-contracts/Sync/Sync.csproj`; the latter requires
  `-p:NoWarn=CS0436` because the now-shipped types duplicate its nonpackaged fakes.
  This does not modify the frozen fixture or shared projects.
- Run the test DLL with `-namespace Xemele.Tests.Sax -noLogo -noColor`.
- Run the standalone sample DLL.
- Run `python3 -B docs/reports/M3-sax/check-api.py`: exact compiled SAX freeze
  plus all-fragment comparison through the approved unchanged extractor.
- Run `python3 tools/planning/tasks.py validate`.

The repository `tools/ApiBaseline/compare.py` discovery wrapper currently rejects
an unprovisioned XML corpus fixture `xmlconf/eduni/errata-2e/E14.dtd`. The direct
compiled comparison above checks all public declarations independently of corpus
availability. Corpus provisioning is outside this task's ownership.

## Parked measurement sequence

Builds, fixture generation and warm-up source preparation occur before the host
measurement lease. Run only after explicit coordinator authorization, sequentially,
with no other measurement/build/test children. The bounded lease script below
runs only prebuilt DLLs and imposes a 90-second overall wall-time limit.

```sh
python3 -B docs/reports/M3-sax/measure.py
```

The first child parses 8388608 generated text bytes (8 MiB) for feature retention
evidence. This does not satisfy or remove the full 1 GiB acceptance requirement,
which remains mandatory in M3-corpus under coordinator ownership.
The second child runs eight end-to-end BenchmarkDotNet Dry/in-process cases,
with MemoryDiagnoser, short/long ASCII and multilingual text, and SAX/DOM results
consumed. Dry timing is a smoke measurement and cannot establish a speed benefit.
Raw outputs live under this owned report directory. Allocation controls and the
retention oracle fail the command if their assertions fail. Measurement results are recorded below and in the task evidence at the frozen
source revision.

Prepared control, before any counter attribution, runs the same warm-up and
100000 borrowed dispatch/copy operations, all six operation scopes, and 8 MiB
generated stream with measurement counters/timers/retention collections disabled:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' docs/reports/M3-sax/Probe/bin/Release/net10.0/Xemele.Benchmarks.dll --control 8388608
```

Control timeout: 20 seconds. Measurement timeout: 90 seconds overall, with a
30-second allocation-child maximum and the remaining budget for BDN. Gates:
callback count exactly 330000 and copied bytes equal input; allocation scope
100000 borrowed dispatch/copy operations exactly 0 B with a >=4096 B positive
control; generated stream callback count exactly 8388608, sampled live retention
delta <=2 MiB and post-disposal delta <=1 MiB. Whole-operation setup and owned
results are reported separately without a zero-allocation claim. BDN must finish
all eight cases with allocation columns; inspect its output, since process exit
alone cannot prove benchmark success. Control has now run successfully: see control.json and control.txt. Its printed
zero metrics are disabled placeholders. Allocation and BDN measurements subsequently completed under the explicit root
lease; see measurement.json, allocation.txt and benchmark.txt.

## Measured feature evidence

Frozen probe source: `0f214e5f21ac144bd4e29bb2aefd96eb43ad3b1a`; unchanged runtime
implementation: `0f51da9a2715314c32ffbd898ccce1f3b7cee15c`. Exact prebuilt hashes
match the earlier counters-disabled control (control.json / measurement.json).
Lease `m3-sax-0f214e-measure` ran the prepared command unchanged; script and both
children exited 0, and all eight BDN Dry/in-process cases completed. No builds,
source changes, option changes or retries occurred inside the lease.

100000 warmed borrowed HandlerSink.Text dispatches plus bounded 4096-byte span
copies allocated **0 B**. Positive allocation control allocated **4120 B**.
Warm-up/setup owners precede that measured scope; its exact callback/copy oracle
passed. The synchronous per-thread counter applies to this synchronous scope;
it makes no async or global-allocation claim.

Parser operation allocations are explicitly nonzero and include pump/core
setup, fixed scratch, names and owned outputs: cold string 51448 B; warm string
51400 B/op, byte span 56192 B/op, Stream 56256 B/op. Diagnostic factory results
include setup plus owned completion/snapshot: valid empty-element result
51464.48 B/op, malformed result 51600 B/op (100 operations each). These are
whole operations, not zero-allocation promises.

The generated 8MiB stream delivered exactly 8388608 text bytes with no
corpus-sized input owner. GC live-memory samples every 1MiB found a maximum
**136312 B** increase from the forced-GC baseline; after parser disposal the
delta was **-560 B** (sampling/baseline variation). Gates <=2MiB live and <=1MiB
post-disposal passed. This feature evidence does not establish 1GiB acceptance;
that remains mandatory in M3-corpus.

BDN MemoryDiagnoser completed four methods at 32 and 16384 characters: string,
byte-span and Stream SAX plus the prior DOM path, including multilingual tails.
Its cold whole-operation allocation columns are in the raw CSV/Markdown reports.
Do not interpret generated timing ratios as a comparison: this was a Dry
one-iteration run on a contended shared host (coordinator reported an unowned
CPU-heavy process). Timing is informational only, with no repeatability,
regression or comparative claim. All raw outputs and context are retained.
