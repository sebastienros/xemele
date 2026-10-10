# Three-engine integration checkpoint

Source: `f2088e4b76c835daff21c0226f5dd3884a275b65`, isolated branch
`codex/m0-references`, based on verified protocol, libxml2, xmloxide and wiring
prerequisites. No runtime parser implementation or public runtime API was added.

`tools/ReferenceRunners/Integration/ReferenceIntegration.csproj` builds an explicit
integration executable outside the ordinary solution. It consumes the unchanged
`ReferenceProcess.ExecuteAsync` implementation from TestUtilities. It exercises
51 real engine responses through strict built-in `xml.events` validation, not a
duplicate direct-process JSON checker. The known namespaced fixture matches a
literal independently specified five-event sequence on all three engines. Every
runner reports its actual source revision and actual engine metadata. libxml2's
embedded manifest is compared with its actual private build manifest; Rust's
revision, commit and lock hash are checked against actual private provenance.

Each engine separately passes invalid XML rejection, seven invalid protocol
request cases, HTTPS/file URI mapped external general entity checks and denied
resource checks. External DTD success is checked on BCL/libxml2 and explicit
unsupported capability on xmloxide. Document-level whitespace is independently
asserted on BCL and explicitly refused by both native adapters. Literal attribute
and CDATA CRLF behavior and namespace IRI acceptance/rejection preserve observed
engine differences; no native output is silently corrected and no product feature
is excluded.

Ten actual adversarial child modes cover nonzero exit, malformed JSON/UTF8,
identity/framing errors, stdout/stderr overflow and invalid event/failure payloads.
Timeout and cancellation checks independently verify the child PID was reaped
before the managed call completed. These 12 checks are process infrastructure
checks, separate from actual engine capability responses.

Commands and actual outputs:

- Private libxml2 `build.py --fetch` uses immutable commit
  `96498992efa48d52b0e8b83058bd88dbdaf153c1`, CMake3.31.6,
  AppleClang17.0.0 build1700.6.3.2, Make3.81, SDK26.2, arm64/target15.0.
  Native source/bridge are unchanged at the tested revision. Actual manifest,
  SHA256 and dependency linkage: `libxml2-build.txt`, `libxml2-provenance.json`.
- `python3 -B tools/ReferenceRunners/Xmloxide/build.py --offline` passes at the
  source revision using Rust/Cargo1.95.0 and immutable commit
  `555055f1ea6bf9944488fd96415b6f03b5b43696`. Actual lock/source/toolchain and
  dependency inventories: `xmloxide-build.txt`, `xmloxide-provenance.json`.
- `dotnet build tools/ReferenceRunners/Integration/ReferenceIntegration.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false`
  passes, zero warnings/errors (`managed-build.txt`), SDK10.0.400.
- `python3 -B docs/reports/M0-references/verify.py` passes:51 actual responses,
  12 adversarial process checks. `managed-responses.jsonl`, `managed-checks.txt`
  and `verification.txt` preserve output/actual .NET10.0.11 metadata.
- `python3 -B docs/reports/M0-references-protocol/verify.py` passes:51 focused
  tests, 54 solution infrastructure tests, three deliberate smoke failures with
  expected exit2, 11 direct BCL wire cases plus raw invalid UTF8 request rejection,
  build/help/pack/API checks,16 discovery/composition tests and two independent
  corpus fixtures. Fresh results were copied to `preserved-protocol-foundation.txt`
  and `preserved-bcl-wire.jsonl`; historical tracked reports remain untouched.

- `python3 -B docs/reports/M0-contracts/verify.py` passes:139M1/173M2 compiled
  contract members with139 preserved additively, compiled mutation probes,
  foundation checks,54solution infrastructure tests and tree/input correctness
  self-checks without new timings (`preserved-contracts.txt`).
- `python3 tools/planning/tasks.py validate` and `git diff --check` pass
  (`validation.txt`);101tasks validated and only owned paths changed.

Only macOS15.8.1/arm64 was exercised. Native capabilities/encodings and unusual URI
canonicalization remain bounded by the verified child contracts. Use BCL or other
capable references plus independent standards expectations for unsupported/native
divergent cases. No CPU/parser timing or allocation benchmark was run or claimed:
this task joins out-of-process reference infrastructure and changes no runtime hot
path. No remote publication occurred. Coordinator integration verification remains
required before setting `verified`.
