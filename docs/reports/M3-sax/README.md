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
memory at 16 MiB intervals and check post-disposal retention. Literal scanning
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
runs only prebuilt DLLs and imposes a 180-second overall wall-time limit.

```sh
python3 -B docs/reports/M3-sax/measure.py
```

The first child parses 1073741824 generated text bytes for retained-memory evidence.
The second child runs eight end-to-end BenchmarkDotNet Dry/in-process cases,
with MemoryDiagnoser, short/long ASCII and multilingual text, and SAX/DOM results
consumed. Dry timing is a smoke measurement and cannot establish a speed benefit.
Raw outputs live under this owned report directory. Allocation controls and the
retention oracle fail the command if their assertions fail. No measurement has
run until the task evidence explicitly records its tested revision and outcome.
