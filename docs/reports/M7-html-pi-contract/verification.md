# PI contract repair verification

Tested source revision: `86e6bdf1ecb1024e90e2a5f94a73c1d546ec9a61`. Follow-up `f999c3163c0afb7699d1bdcb16a35fe86b78226b` changes only downstream task names in the decision; no binaries, declarations, vectors or checker changed.

Executed:

```
python3 docs/reports/M7-html-pi-contract/prepare.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 docs/reports/M7-html-pi-contract/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --ownership-base ebc68ea
```

Prepare passed before the source commit: cached/offline restore, pinned SDK10.0.400 and forced runtime10.0.11, compiler-server reuse disabled. It emitted exact nonpackaged Engine176 / Tokenizer119 / Selector136-member fragments.
Check passed at the source revision above: each independent declaration consumer compiled and executed only existing XML tree/stream probes on actual .NET10.0.11; all three exact baselines matched. Every previous baseline row, including old enum numeric values, is identical to the claim. Each adds exactly the two enum fields(value6) and factory method. Three generic missing-member API controls and nine PI-specific removed-member controls (all three added members in all three families) rejected with API mismatch.

Only the deliberate duplicate-model declaration lanes use environmental NoWarn=CS0436. The canonical checker receives the current source HEAD as ownership-base because its predicate belongs to M7-contracts; the owned repair checker separately audits the entire claim-to-current diff and untracked files against this task's actual owned paths. The strict audit and git diff --check passed. No shared checker, build file or runtime source changed.

ActualModels excludes Model.cs.fixture and builds with normal warnings-as-errors: zero warnings/errors. It verifies actual shipped HTML/XML model assembly identity, HTML ownership/attribute/link behavior, existing XML tree/streaming behavior and actual selector-signature consumption; selector declaration methods are not invoked. This lane does not claim shipped PI APIs or behavior. The independent tokenizer PI signature consumer is declarations-only, never executes any stub.

Independent authoritative parsing source retrieval returned788775bytes and SHA25634e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf, matching the pinned9October2026 source. Exact original html5lib PI/EOF cases and file hashes are retained in pi-corpus.json; inspection found no demonstrated discrepancies or exclusions. Independent normative witnesses cover termination/questionmark/EOF/reservedtarget/NULdata. These are frozen expected data, not tokenizer test runs.

Independent read-only native evidence inspection checked original fuzz-047/fuzz-118 sibling-root trees, latin1bytes3c703ee980→é€, vector-015PI→comment and pinned HTMLparser.c3289–3296/4204–4206. legacy-vectors.json preserves the original native data beside the explicitly named adapted expected trees. Check.py validates that the exact adaptation keeps the first wrapper and appends later-root children in order. This validates frozen records, not an implemented legacy parser. Full runtime differential tests, repeated-envelope/attribute-conflict/document-comment/doctype tests and allocation evidence belong to M7-legacy.

Planning validation passed167tasks. Coordinator source-only review approved86e6bdf; the requested downstream task-name correction is committed separately. No allocation tests, timing measurements or cumulative suite ran. No native/XML runtime implementation, public stub, later dependency, exclusion or scope reduction was delivered. Runtime PI semantics/copy/quota tests belong to ordered M7-html-pi-model; tokenizer/builder/serializer implementation remains required.
