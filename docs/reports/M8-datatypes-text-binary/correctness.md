# Branch correctness (source 8535124db51b86faf1a2c57159fc0738ede38e9c)

`python3 docs/reports/M8-datatypes-text-binary/checks.py` passed on Release
SDK10.0.400/runtime10.0.11 Arm64. Exactly139 family+18 core+12 model=169 tests,
zero errors/failures/skips/not-run. Family checks include exhaustive65536 BMP
name-start/continuation membership against independent XML2 production ranges,
supplementary text/name distinction, runtime-constructed malformed UTF-16,
XML-only whitespace, language syntax, boolean identity/unordered semantics,
all256 octets, all128 final-unused-bit/padding combinations, strict URI grammar,
expanded QNames/default namespace/context snapshot, derived/retyped ownership,
primitive disjointness, exact work/components, final refusal and mid-scan/copy
cancellation with null caps.

Both genuine normal and XML-only provider/core consumers passed; the XML-only
assembly includes only verified XML/M4/M5/model/core plus this family. Public API
extractor matched all1393 compiled members. Planning validation passed176 tasks;
Git whitespace check passed. No broad resolver allocation test selected.

Initial exploratory run:168/171 passed; three malformed-surrogate InlineData
strings had been altered by attribute metadata serialization. Those fixtures were
replaced by runtime UTF-16 constructors; the final169-case run above is passing.
No production source change resulted from that fixture correction.

Preparation (no measurements): Release probe/BDN builds0warnings/errors, probe18
routes consumed with32 warm/64 control samples and counters disabled, both scalar
baseline/provider controls consumed, ten ShortRunJob/MemoryDiagnoser cases resolved
with InProcessNoEmit (no generated builds). Process preflight passed ps permission,
counters-disabled probe, child reaping and absent owned group. Allocation/timing,
retained memory and BDN measurements remain pending a fresh explicit root GRANT.
