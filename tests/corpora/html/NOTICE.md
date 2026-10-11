# Integrated HTML corpus

`inventory.json` preserves the paths, original lengths and SHA-256 values of all
240 retained prerequisite fixture/provenance/report files. The originals remain
in `tests/fixtures/M7-*`; no resource is rewritten, refetched or filtered here.
The tokenizer and serializer sources pin html5lib-tests
`c777c408b61078ea2eb4acefc2535f54dbc8b28a` with their original MIT license.
The complete WPT parsing inventory and template support pin
`ddb25fdfff7766af4cd75eac3fb3b6ac206140ce` with `WPT-LICENSE.md` retained.
Legacy parser/serializer native outputs pin libxml2
`96498992efa48d52b0e8b83058bd88dbdaf153c1`; original copyright files and
source/compiler/native output provenance are retained in their reports.

The counts describe different domains, not interchangeable runtime passes:
6,823 tokenizer originals / 7,049 state vectors; 1,968 original tree cases /
14,316 applicable public lanes; 166 individually classified WPT resources;
539 native legacy parser fixtures; 640 native serializer graphs; 230 original
html5lib serializer dispositions. The integrated prerequisite tests execute
these domains; the new inventory tests enforce complete dispositions, original
case identity, license/hash preservation and named composition witnesses.
`regressions.json` contains ten independently authored expected text witnesses.
See `docs/reports/M7-corpus/README.md` for exact exceptions and runnable checks.
