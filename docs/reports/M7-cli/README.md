# M7 HTML CLI handoff

Implementation revision: `a3dc9a68cfa46f1a88745b2b6f9afee0b461563b`. Branch: `codex/m7-cli`; isolated checkout: `/Users/sebastienros/.codex/worktrees/762a/xemele`. Claim: `9581d9778f439361e68a8eaf07ee34deeb683a8c`; verified base: `4af21789ba152d435564f68e3f4259b5527cb90c`. Claim/base ancestry checked before edits; the assigned existing branch was attached to the initially detached managed checkout.

Four statically registered commands compose verified modules: `html-legacy`, `html5`, `html-fragment`, `html-select`. Only the assigned HTML command subtree, owned tests/fixtures/sample/report and own task record changed. No XML command, shared registry/Program/build file or runtime implementation changed. Full command/option/output inventory and public usage are in `samples/M7-cli/README.md`; a linked executable sample exercises the delivered registry plus unchanged XML composition. Internal CLI additions expose zero library public members, so `docs/api/M7-cli.txt` is intentionally absent, as approved by the coordinator and consistent with M6-cli.

The commands distinguish parser profiles, scripting metadata, serialization modes, byte document sniffing and strict UTF-8 fragment input. Fragment context is a real detached element or one selected from a parsed HTML5 context file, preserving attributes, ancestors, namespace and document quirks. CSS supports only the frozen matrix and rejects unsupported syntax with UTF-16 offsets. Primary file and borrowed/standard stream ownership, finite library limits, invocation/rejection/cancellation/internal exit categories and staged result publication are exercised. Selected markup/text emits one LF after each match; embedded newlines remain literal and are not a record framing promise. HTML never passes through the XML parser or serializer.

## Actual checks

All passing checks below ran at the implementation revision on macOS 15.8 / osx-arm64, SDK 10.0.400. The exact host is `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`. Managed executions explicitly selected runtime 10.0.11. The linked sample asserts `.NET 10.0.11` and arm64 is recorded in its output. No other OS/architecture is claimed.

| Check | Outcome |
| --- | --- |
| Forced Release CLI build | Passed; zero warnings/errors |
| Forced Release approved API checker and owned sample builds | Passed; zero warnings/errors |
| Managed HTML CLI process controls | 111 passed |
| Existing XML CLI process controls | All 57 passed, including seven commands, resource grants/rejection and relative entities |
| Compiled owned usage/ownership sample | 28 controls passed; success, quota rejection, canceled operation, input/output I/O and stream lifetime; XML preservation |
| Full official API inventory/compiled union | 34 real fragments, 1,253 members matched; 3,388 fixture hashes validated; bootstrap exemption false |
| API negative/zero-addition controls | 7 checks passed: actual union match; missing/changed/added member rejection; tool emits zero public members; duplicate/conflicting inventory member rejection |
| NativeAOT publish | Passed; genuine Mach-O 64-bit arm64 executable; ILCompiler and platform ILCompiler 10.0.11 |
| Native HTML and existing XML controls | 111 HTML plus all 57 XML passed against the actual native executable |
| Planning, whitespace, ownership | 169 tasks validated; `git diff --check` passed; every changed path owned relative to claim; no shared/XML edits |
| Owned process inspection | No build/compiler/native/API/smoke children remained; every yielded exec session reaped |

Build commands use `-c Release -t:Rebuild -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false -p:NuGetAudit=false --source tests/Contracts/Fixtures/M7 --disable-build-servers -m:1` for `src/Xemele.Tool/Xemele.Tool.csproj`, `samples/M7-cli/M7.CliSample.csproj`, and `docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj`.

Managed process controls: `python3 -B tests/Xemele.Tests/M7-cli/process_controls.py` and `python3 -B tests/Xemele.Tests/M6-cli/process_controls.py`. The owned driver launches the exact host with `--fx-version 10.0.11`. Sample: exact host `--fx-version 10.0.11 samples/M7-cli/bin/Release/net10.0/M7.CliSample.dll`. API controls: `python3 -B tests/Xemele.Tests/M7-cli/api_controls.py`, using unchanged official `compose(ROOT)` and approved extractor under explicit runtime 10.0.11; real fragments are not rewritten. Compiled API was rechecked after native preparation.

Native publication: `DOTNET_ROLL_FORWARD=LatestPatch` followed by the exact host `publish src/Xemele.Tool/Xemele.Tool.csproj -c Release -r osx-arm64 -p:PublishAot=true -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false -p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages --disable-build-servers -m:1 -o artifacts/M7-cli/native`. LatestPatch is local build-tool servicing only; actual consumer runtime and compiler packages are explicitly 10.0.11. Native controls use `XEMELE_CLI_NATIVE="$PWD/artifacts/M7-cli/native/Xemele.Tool"` with the same two process-control scripts. Static registration remains reachable after trimming/AOT without reflection or dynamic registration.

Native executable SHA-256: `556a3abc9cbe8f1cbde44cb0d0509cc122746a487922d3ed3dfa5f19343d8fc6` (4,103,128 bytes). Managed tool SHA-256: `77088c703b20683aab4771813c46c13cb7e3d665e663eb69eb08a3037f2de680`. Library SHA-256: `cf043e89f3be41932aee938aeae7f444de422d7ba1b7e802b157fe3848a67b8b`. Ignored raw logs, JSON cases, API inventory and identity manifest, native executable and debug symbols remain under `artifacts/M7-cli/` for coordinator inspection.

## Preparation corrections and limits

The initial HTML controls exposed the verified HTML5 engines' existing exact `InvalidOperationException` quota messages. The owned CLI now maps only those known messages to quota rejection/exit 1; unrelated algorithm failures retain internal/exit 3. A preliminary stack-trace assertion incorrectly matched the decoder's legitimate “at index” text; corrected to the actual stack-line pattern. No behavior case was dropped. The actual final HTML count is 111; a preliminary coordination message incorrectly said 119 and was corrected. A preliminary changed-API negative did not alter an enum row; corrected to assert an actual declaration mutation before invoking the extractor. The final missing/changed/added controls all reject as required.

The first full inventory attempt lacked ignored XML corpus fixtures in the isolated worktree. The coordinator prepared 3,386 existing private-cache fixtures after SHA validation, with no fetch, original cache modification or tracked source writes. A temporary API-only inventory was exploratory and is superseded by the passing full official inventory. An early API check overlapped a sample rebuild that briefly removed the shared library output; checks were rerun sequentially after the rebuild. Generated Python bytecode was removed before the implementation commit was finalized; no generated bytecode is shipped.

The first sandboxed NativeAOT preparation paused after managed build without a compiler child/native artifact. Its log is preserved as `artifacts/M7-cli/publish-aot-sandbox-preparation.log`; it is not passing native evidence. The owned process was stopped (SIGINT followed by SIGTERM), reaped, and scoped elevated publication produced the tested native executable. No permission rejection or unresolved process remains.

CLI composition deliberately allocates the final result memory stream, retained DOM/query results and the fragment string required by the frozen API. No throughput, latency, allocation-free, or measured allocation claim is made; no benchmark/timing/allocation campaign or inherited full suite was run. Fragment parsing, selection and per-node string serialization use existing synchronous APIs without mid-call cancellation, as documented in the usage inventory. The actual focused HTML/XML correctness checks and AOT satisfy this composition task; cumulative inherited runtime and allocation gates remain coordinator work.

All three criteria pass on this branch. State is `implemented`, with `integration_revision=null`; coordinator integration and integrated verification remain. No outside-owned source/contract repair or other unresolved task criterion is requested. Preserve earlier runtime/corpus evidence and the original unpublished M6 package/cache artifacts.
