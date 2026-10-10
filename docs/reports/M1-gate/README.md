# M1 strict XML checkpoint

The gate owns a standalone source demo and runs that exact source as a freshly
packed, isolated NuGet consumer. Its independent input model and System.Xml
reader oracle check parse/mutate/serialize/reparse semantics and constructed
exact output. Caller streams remain open on success, rejection and cancellation.

Supported: one XML 1.0 root, namespaces, predefined/numeric references, strict
Unicode UTF-8/UTF-16LE/BE, text, comments, CDATA and NCName PI targets. DTDs,
unknown entities, XML 1.1, colon PI targets and other byte encodings reject.
There is no resolver, recovery, public streaming reader, async transport or
forward writer dependency. The M2 foundation compilation model is a preserved
M0 compatibility check, never a runtime dependency.

Run `python3 -B docs/reports/M1-gate/verify.py --report-prefix <new-prefix>` with
SDK 10.0.400 on PATH and DOTNET_ROOT pointing to that SDK. Default ownership
audit starts at coordinated claim ed13ab8d34d42618944f6a7c6fc24634c787264e;
coordinator integration may pass an explicit pre-integration `--audit-base`.
Each invocation creates a new directory and preserves failures. The verifier
adapts the verified corpus join without writing any peer report: 801 cumulative
runtime tests, 501 corpus subset tests, 11 foundation checks, independent compiled
models/protocol, API222 canonical/approved union, 3388 discovery fixtures,
bootstrap, five provenance checks and 14 planning regressions. It verifies all
3386 original private resource hashes, joins all 2585 IDs, executes 242 supported
rejections, and keeps 2343 later-capability gaps visibly unrun. System.Xml's three
exact VersionNum divergences remain separate from 239 matched rejections.

No production changes or new timing measurements. Existing verified raw timings
in M1-dom/bdn, M1-tree-dom-persistence/bdn and M1-serialize/bdn retain their
provisional Arm64 scope; benchmark class MemoryDiagnoser/ShortRunJob configuration
is inspected here. Existing exact-zero warm syntax/DOM allocation tests run as
part of the cumulative suite. Historical 216B/6000B probe failures have no proven
cause; their records remain in the owning reports. Windows/x64/release AOT and
full XML conformance remain later unrun release checks.

Checkpoint results: corrected/summary.json records source 0e8f6929abc4f95e9608176c1db0dee8c0d7cf34 and passed commands/results in corrected/checks.txt.gz. All801 cumulative+501 subset+11 foundation tests passed with no skips. Source/package output is identical (source-demo.txt/package-demo.txt). Initial attempt at80ee540 failed only the gate demo local helper name collision; first/checks.txt.gz preserves that failure. The helper and benchmark lookup were corrected in0e8f692 before the complete successful run. Planning14/provenance5/build-discovery16 and validation110 passed. No production changes or timing repetition.
