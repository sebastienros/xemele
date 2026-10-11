# Parameter entity nesting validity

Product revision: `3221a52d045976dd4fc8e318288440580b4ccda1`.

The parser retains the first proper declaration, content-model group, or conditional-section parameter-entity nesting error in the existing immutable model diagnostic. Validation consumes that existing diagnostic. Nonvalidating parsing, first declaration bindings, original replacement-text source IDs/locations, and existing well-formedness restrictions remain unchanged. These are validity constraints in [XML 1.0 Fifth Edition sections 2.8, 3.2 and 3.4](https://www.w3.org/TR/2008/REC-xml-20081126/).

Declaration opening/closing delimiters use existing lexer source IDs. Groups add one integer source ID to each existing owned Group object. Conditional sections reuse the existing integer stack and lexical source fields. The first diagnostic allocates its owning diagnostic object; repeated mismatches preserve the prior object. No raw subset, public API, new source-context object or lexical sidecar is added. The added Group field is four logical bytes; alignment may increase the existing object allocation. Current owned compilation measurements will include this field; historical compiler totals do not describe this layout.

## Functional checks

- Focused class: 28 passed, zero failed/skipped, actual .NET 10.0.11; Release build zero warnings/errors.
- Committed product preservation: 2,261 passed, zero failed/skipped using direct pinned host with `--fx-version 10.0.11` and class filters `Xemele.Tests.Dtd*`, `Xemele.Tests.M5DtdModel.*`, `Xemele.Tests.M5ValidationModel.*`, `Xemele.Tests.ParameterValidity.*`. The first filter includes the actual focused class namespace `Xemele.Tests.DtdParameterValidity`. Selected source contains no allocation-counter tests.
- Scoped compiled API union: 948 members matched using the existing approved M0 extractor via `python3 docs/reports/M5-validation-model/check_api.py`, pinned direct .NET10.0.11. Official API inventory/corpus discovery did not run: the isolated checkout lacks ignored E14.dtd corpus payload; cumulative fixture discovery belongs to the corpus gate.
- Planning: 158 tasks validated; `git diff --check` passed.
- The eight fixtures are byte-for-byte copies from the immutable pinned corpus cache, with upstream paths/SHA256 in the owned manifest. Tests check every character split, Compile refusal, successful nonvalidating sync/one-byte async document reading, original external replacement URI/byte offsets, valid boundary controls, original well-formedness failures, prior diagnostic identity, cancellation staging and quotas.
- During test development, a replacement containing only `]]>` at DeclSep was initially expected to complete; the existing parser correctly rejects that shape as a well-formedness violation. The final control preserves that rejection. That provisional failed expectation is not counted as passing evidence.

## Prepared performance scope

The exact once measurement sequence completed successfully after durable exclusive grant `m5-dtd-parameter-validity-2f6cffb` and release. The standalone probe controls and discovers exactly three MemoryDiagnoser ShortRun NoEmit cases (three warmups, three iterations, one launch), actual .NET 10.0.11 Arm64. No CLI job flags or build/generation run in the measurement sequence.

The primitive binds its reflection delegate and initializes the prior diagnostic outside the measured window. Its 131,072 calls check repeated first-diagnostic retention, with 32 inclusive 4,096-call batch counters and a separate inclusive outer counter. This is deliberately a narrow boundary-retention primitive, not a claim that parsing/compilation allocates zero bytes. Cold fixture initialization, first diagnostic, a positive allocation control, current owned valid/invalid Compile totals, and retained schema domains are separate counters, without subtraction. The owned valid fixture exercises a parameter-entity group and declarations; invalid Compile includes its owning exception/diagnostics.

The executed exclusive sequence was bounded to 300 seconds: allocation at most 30 seconds, three NoEmit benchmark cases at most 180 seconds, initial/final identities at most 35 seconds each. The frozen identity covers all source, source configuration, generated files, probe/runner binaries, owned fixtures, entire pinned SDK/runtime/host closure and stable effective environment. Scoped process-group cleanup and full final identity run in finally, including failure. Raw output remains ignored for coordinator inspection; only summary tables/analysis are committed after release.


## Actual measurement evidence

Measured prepared revision: `2f6cffb3d1018a7dd83189c2a6a273de60befc7a`; product `3221a52d045976dd4fc8e318288440580b4ccda1`. Frozen manifest SHA256 `f2f3d80bda6337726fc565277a5cbd0792d81e7caeb9831506c00bee7d137d0b`: source175, binary43, runtime3852. Initial/final complete identities matched; phase exits allocation0/benchmark0, runner0, scoped cleanup no_child=true. Actual runtime .NET10.0.11 Arm64 RyuJIT; three cases completed in 27.38 seconds. Root separately approved source3221a52 and checked prepared identity; these are review evidence, not execution claims.

| Domain | Inclusive bytes | Operations/domain |
| --- | ---: | ---: |
| Cold fixture initialization | 5,016 | One initialization |
| Positive allocation control | 1,048 | One byte[1024] |
| First retained diagnostic | 72 | One prior-empty parser |
| Warm retention outer window | 0 | 131,072 |
| Warm retention distribution | 0 in each of 32 batches | 4,096 per batch |
| Owned valid Compile | 1,937,216 | 100 |
| Owned invalid Compile including exception | 944,424 | 100 |
| Retained total-memory domains | 176,536 before; 744,960 after | 100 retained valid schemas |

Owned sample totals are inclusive observed counts, without overhead subtraction. Retained domains measure total memory, not per-call allocation. Neither is a promise of fixed future object sizes.

| Benchmark | Mean ns | Error ns | StdDev ns | Inclusive GC bytes | GC operations | Inclusive bytes/op |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| WarmRetainedBoundaryDiagnostic | 1.642 | 0.0503 | 0.0028 | 0 | 268,435,456 | 0 |
| OwnedValidCompile | 6,110.926 | 1,699.8144 | 93.1726 | 2,535,605,936 | 131,072 | 19,345.138061523438 |
| OwnedInvalidCompile | 4,101.766 | 185.0229 | 10.1417 | 1,237,257,872 | 131,072 | 9,439.528442382812 |

All three raw `// GC:` records were inspected, rather than interpreting summary dashes as zero. Warm primitive GC is actually zero. The primitive is only first-diagnostic retention with an already initialized parser/delegate; full parsing and compilation own objects. End-to-end valid compilation includes the added Group source field and current model layout. Cold/owned totals from earlier compiler milestones are historical and are not reused. This change introduces no borrowed evaluator-loop work, and no evaluator benchmark was repeated.

Raw files were kept ignored under `artifacts/M5-dtd-parameter-validity/` and their exact paths/counts sent to the coordinator before any cleanup. Only this summary and analysis are committed. The coordinator independently inspected the full allocation JSON, all three inclusive GC rows and actual runtime/summary, confirmed the counts, and acknowledged cleanup only after this durable summary commit.
