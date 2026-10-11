# M4-contracts branch handoff

Implementation revision: `c732d51d43db610b2acc37338d3c6d4881386be8`.
Base: `011833b2e69a3fd105735bb931d63f99e83c7158`.
Coordinated claim: `8426569c66102e1897c4916783453ed7544f1233`.
Checkout: `/Users/sebastienros/.codex/worktrees/f9ae/xemele`.
Branch: `codex/m4-contracts`. Integration and verified status belong to coordinator.

Deliverable: precise XPath 1.0 grammar and 13-axis/15-operator/27-function inventory;
immutable AST/static context/navigation/function seams; public value/item/factory,
context, extension and diagnostics proposals; static namespace resolution and
expanded dynamic variables/functions; bounded snapshots, operations, compile,
input, intermediate/result strings/node-sets and cancellation. Source tree uses
verified XmlNode/XmlAttribute handles; no production XPath APIs or stubs added.
Exact public baseline has 177 compiled members, including record-generated APIs.

Fixtures: independently compiled Consumer, Compiler, Navigator and Functions
projects. Lane probes use fake implementations. Their success establishes seam
wiring and compilation, not production XPath parsing/evaluation/conformance.
InvalidNamespace proves no dynamic prefix-map rebinding; InvalidAttribute proves
attributes cannot masquerade as nodes. Seven mutated inventories and two mutated
compiled API controls must be rejected. The inventory has 105 unique acceptance
vector IDs. `expected` uses JSON scalar values or node-identity path arrays;
`NaN`, `Infinity` and `-0` strings encode special number results; `XPATH-*` strings
encode diagnostic codes. Other strings are string results. Vectors are future
runtime acceptance requirements, not reported production test passes.

Checks are captured verbatim in `checks.txt`; final task evidence records their
actual result at the implementation revision. Focused command:

```text
python3 tests/Contracts/Fixtures/M4/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 tools/planning/tasks.py validate
git diff --check
```

The helper verifies SDK 10.0.400/runtime 10.0.11, performs local-source package-free
restores, builds the verified runtime as a project dependency, runs the four
projects, expects two compiler failures, compares all four assemblies with the
same canonical API proposal, exercises corruption controls and checks changed/
untracked ownership against the claim. The owned ApiCheck copy additionally
resolves sibling Xemele.dll metadata so nullable tree structs remain accurately
represented; no earlier checker changed. Existing source/build/contract fragments
are byte-preserved. Earlier tests/measurements are not rerun or newly claimed.

Allocation/timing evidence: not applicable to contract-only declarations/fakes.
Future runtime owners must implement semantic vectors, differential tests and
separate compilation/evaluation measurements. Benchmark classes must carry the
exact `[MemoryDiagnoser, ShortRunJob]` attributes. No benchmark added here.

Outside-owned change requests: none. Remaining work is coordinator integration
verification followed by M4-model production implementation and dependent lanes.
