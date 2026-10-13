# Ordinary core branch coverage

The complete frozen XML core parser partial implementation and byte decoder achieve **2,086 / 2,268 branches (91.9753%)**, above the unchanged90%requirement. The scope contains all11Core/Recovery/Input source files listed in ordinary-coverage-scope.json. No branch/file/attribute exclusions were applied within this scope; production source is unchanged. Other parsers/validators and sustained campaigns are separate requirements.

| Source | Covered | Total | Uncovered |
| --- | ---: | ---: | ---: |
| src/Xemele/Parsing/Core/XmlParserCore.AsyncResources.cs | 104 | 122 | 18 |
| src/Xemele/Parsing/Core/XmlParserCore.AttributeExpansion.cs | 110 | 126 | 16 |
| src/Xemele/Parsing/Core/XmlParserCore.Attributes.cs | 221 | 272 | 51 |
| src/Xemele/Parsing/Core/XmlParserCore.Doctype.cs | 164 | 168 | 4 |
| src/Xemele/Parsing/Core/XmlParserCore.Entities.cs | 138 | 154 | 16 |
| src/Xemele/Parsing/Core/XmlParserCore.ResourceMode.cs | 37 | 40 | 3 |
| src/Xemele/Parsing/Core/XmlParserCore.Resources.cs | 39 | 42 | 3 |
| src/Xemele/Parsing/Core/XmlParserCore.ValidationProvenance.cs | 37 | 38 | 1 |
| src/Xemele/Parsing/Core/XmlParserCore.cs | 925 | 984 | 59 |
| src/Xemele/Parsing/Input/XmlInputDecoder.cs | 204 | 210 | 6 |
| src/Xemele/Parsing/Recovery/XmlParserCore.Recovery.cs | 107 | 112 | 5 |

Pinned cached Coverlet.console6.0.4 executes on the explicit.NET10.0.11host. Its positive control records exactly one hit and one unhit branch, proving nonempty/partial coverage rather than accepting a zero or inflated report. All10,754ordinary tests run and pass under core-only instrumentation. Only disposable copied DLL/PDB files were instrumented; original production hashes remain unchanged. The first all-runtime instrumentation attempt failed four exact-IL scalar getter/setter checks, is retained as failed, and receives no passing credit. Those checks remain enabled and their unrelated modules remain uninstrumented in the accepted core run.

Initial accepted core coverage was2,038/2,268(89.8589%),belowtarget.32fixed character/byte DOCTYPE rejection/public-ID normalization cases were added after examining uncovered paths; they are deterministic named grammar checks, not generated mutations or a fuzz entry point. Their normal uninstrumented run passes. Only those new cases were measured afterward against the exact previously measured DLL/PDB hash, and their hits were merged into the valid unit report. No unit suite was repeated solely to harvest coverage. The branch identity tuples are unique and all frozen source files are present in the result.

Remaining uncovered error paths were reviewed in source without excluding them:

- Decoder: concurrency rejection, byte/location capacity failure and encoding-selection alternatives remain uncovered edges; checks occur before state reuse or returned decoded values.
- Core tokenization: capacity/overflow guards, malformed declaration/name/reference alternatives and some recovery stop paths remain uncovered. They terminate through explicit diagnostics or argument exceptions; no success is inferred from missing hits.
- Attribute/default/entity expansion: custom non-dictionary lookup alternatives, allocation capacity guards, malformed replacement/default references, unresolved/default provenance and entity-cycle alternatives remain. Normal parser-owned dictionaries cover the common lookup path; fallback scans poll cancellation.
- Resource/async cleanup: rare multiple-disposal failures and diagnostic-bearing request alternatives retain exception/cancellation propagation and bounded cleanup. Uncovered cleanup alternatives are not classified as tested.
- DOCTYPE: the whitespace-separator invariant, unfinished literal/split alternatives and effectively enormous metadata capacity guards remain. New tests cover explicit malformed keywords/quotes/public-ID characters and normalization; remaining branches stay in the denominator.

Source line numbers for every remaining uncovered branch are preserved per file in core-coverage-summary.json. This is focused source review, not additional execution coverage or a guarantee that every path is reachable. No production optimization, public API, fuzz target or sustained campaign changed. Raw instrumentation/report files are transient; durable counts, identities, commands, controls and limitations remain in the owned reports.
