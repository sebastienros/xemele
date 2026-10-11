# Integrated verification

Integrated implementation d2cb0f3d1101028c3669591e99f1c2a70f4d1e2f combines final worker eb892b9758f421c431d0283e5a53999ee7571de8 with verified main, including the construction repair. Release tests, source sample and API helper built with SDK10.0.400, zero warnings/errors. Explicit .NET10.0.11 execution passed 83 focused cases: 30 functions,41 model,12 construction; no failures/skips. API checker emitted893 members and rejected two corruption controls. Source and fresh isolated package consumer passed the27-signature/scalar/substring/number demonstration. Planning validated149 tasks; all16 introduced paths are owned; worker diff whitespace passed.

The first API invocation could not find the unbuilt helper DLL. Building that existing helper resolved preparation; the subsequent API check passed. Existing package readme/CSSM notices remain informational.

Function runtime remains source-reviewed b7d3a2e. The later construction repair affects AST/name construction rather than the measured primitive dispatch/conversion paths. No benchmark rerun was needed or performed; actual10.0.11 allocation and ShortRun evidence remains in performance.md, including inclusive nonzero diagnostic totals and hardware limitations. All three benchmark classes declare [MemoryDiagnoser, ShortRunJob]. No raw results are committed.
