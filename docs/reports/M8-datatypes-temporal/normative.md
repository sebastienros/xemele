# Pinned temporal cases

Primary source: [XSD 1.0 Datatypes Second Edition, 28 October 2004](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/).
Retrieved HTML: 644011 bytes, SHA256 `980de872aa2c50013d5202176eb85eef3f183a7df635fdcb6b4aa34c1c12eb72`.
The raw document is transient under `artifacts/M8-datatypes-temporal/`.

Sections 3.2.6–3.2.14 and Appendix E are the applicable primitive and calendar rules.
Owned tests cover extended signed years, forbidden zero/leading zeroes, Gregorian
leap validation, midnight rollover, arbitrary fractions, timezone extrema and
aliases, absent-zone uncertainty including exact fourteen-hour endpoints, date
interval identity, recurring primitives and Second Edition `--MM` syntax.
Duration cases cover grammar, equivalent fields, negative values and the four
reference starts (1696-09-01,1697-02-01,1903-03-01,1903-07-01), including the
published month/year versus day comparison examples. Appendix E uses signed-year
divisibility; no year-zero lexical value is admitted.

These are focused authored tests, not an imported conformance suite. No claim of
external corpus completion or x64 verification is made. Full composition and
release conformance remain downstream.
