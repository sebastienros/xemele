# M5 corpus final closure

The corrected full corpus passed once at tested merge revision
`0f992893665ec5aa347c2d4f78e5693d9e8d8c8f`, containing only the verified
DTD attribute, parameter-boundary and source-instance provenance repairs from
coordinator main `5afe328c5e8950370a0b6dd7f61a24489a69f93b`.
The actual executable reports .NET 10.0.11, Arm64 and that source revision.
Driver exit 0, process exited and its own group cleaned. No runtime source,
corpus expectations or original resource hashes changed in this closure.
No benchmarks, allocation counters or broad allocation suites ran.

All 2,585 original IDs and 3,386 original SHA256 resources remain. The 976
associated DTD cases produced 788 valid, 103 invalid, 85 rejected and zero
incomplete results, all satisfying their independently frozen expectations.
The 1,609 prior grammar cases retain 453 accepted and 1,156 rejected results,
all passing. Rejected invalid-source cases remain visibly distinct from completed
invalid validation; eight PE nesting witnesses reject before facade evaluation.
The earlier four hash-bound effective XML10 corrections remain disclosed.

Exactly the sixteen previously identified runtime witnesses changed product
outcome. All now satisfy the existing expectations: eight PE nesting errors
reject, two overridden ENTITY defaults validate, three E15 source lexical
witnesses fail ElementValid, two namespace Name-typed colon tokens fail, and
the fully read undeclared general entity yields invalidity instead of incomplete.
`partial.md` retains the initial 24 failures and their diagnoses; no historical
run has been relabeled as passing. The first raw bytes remain under a distinct
ignored filename with the original audited hash.

All 32 canonical exact byte vectors and all 26 declared BCL reference comparisons
passed (14 Inclusive 1.0 and 12 Exclusive 1.0). Six applicable whole-document
Inclusive 1.1 vectors passed actual libxml 2.9.13 through the corrected adapter.
Product mode 1 maps to libxml mode 2; subset references and an unavailable BCL
1.1 algorithm remain unclaimed. The fixed published 1.1 E11-01 bar/foo vector,
prior fixtures and algorithm-specific pins remain unchanged.

Ten ordinary conformance manifest/driver/refusal controls and twelve authored
DTD/canonical differential checks passed on .NET 10.0.11. Driver, conformance
and differential Release builds had zero warnings/errors. Classifier regeneration
`--check` passed with 976 associated / 1,609 prior cases. The approved compiled
API checker matched 948 members. Task validation passed 159 records. Earlier
performance evidence remains applicable; this verification task introduces no
runtime allocations or APIs.

Reference comparison is not a consensus claim. The original audit accounts all
831 unequal labels, including 441 associated DTD labels, and the three E15 cases
where both engines originally gave the same wrong validity result. The final
`final-reference-audit.json` accounts all 830 unequal labels (440 associated DTD,
390 prior grammar) and every sixteen product outcome transitions, with zero
reference infrastructure errors. Counts by source/profile disposition:

| Disposition | Final unequal labels |
|---|---:|
| Reference legacy name/token profile | 605 |
| Reference native-version profile | 118 |
| Same invalidity, different reporting stage | 68 |
| Preserved M2 grammar profile disagreement | 21 |
| Permitted optional error behavior | 6 |
| Reference misses unused-default syntax constraint | 4 |
| Explicit effective XML10 validity override | 4 |
| Reference misses lexical ElementValid constraint | 3 |
| Reference misses empty NMTOKENS constraint | 1 |

The final E15 differences are supported by XML VC ElementValid and official E15,
not by changing expectations to reference results. Other classifications retain
the independently reviewed source/profile bases from the first audit. Only
invalid/rejected stage dispositions change for repaired PE and undeclared entity
witnesses. No missing reference infrastructure errors are hidden as validity.

Retained ignored raw for coordinator independent inspection:

| Artifact | SHA256 |
|---|---|
| first-corpus.json | aed98a4f25920a9981d24132d0e97e4e1cd10f1251b6227b0899e6385424fa38 |
| corpus.json | 46f44f45fa894436c08d62dbb456f7e6f96d87d667898222e4da129aa144becb |
| libxml.json | bc9f53270a78d1b8c272e1ad55e074d9883b069a6442d41df18511e7e42b9258 |

The earlier corrected canonical-only raw and first libxml raw also remain intact.
Only summaries and analysis are committed. Cleanup awaits explicit root inspection
ACK; no rerun is needed. Task status is implemented on this branch; coordinator
integration and combined checks determine verified status.
