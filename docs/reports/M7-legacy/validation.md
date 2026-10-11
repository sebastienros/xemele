# Legacy branch validation

Implemented branch only; integration verification belongs to the coordinator.
Runtime source: 918f52d3ac68e3c4632f776d0b0572f55586ffd8.
Measured checkout: 9661cf26b7be08a17a12d2de94a1745e112d80ad (plan-only change).
Verified shared PI model was merged from main 3608900 (integrated 84dfa585).
The task now depends on verified HTML model, PI/profile contract and PI model.

Release test, sample and probe builds used the pinned absolute dotnet host,
--no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
-p:RuntimeFrameworkVersion=10.0.11. All completed with zero warnings/errors.
Executions used the same host with --fx-version 10.0.11; sample and benchmark
probe reported actual .NET10.0.11/arm64. SDK is 10.0.400.

Focused command: dotnet --fx-version 10.0.11
 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll
 -namespace Xemele.Tests.LegacyHtml -noLogo -noColor
 -result-xml artifacts/M7-legacy/final-source-tests.xml
(with actual argument spacing as in the task evidence). Result: 543 tests,
0 errors, 0 failed, 0 skipped, 0 not-run. This is 539 full pinned-native vectors plus
4 facts. Every tree compares bytes and coalesced streams capped 1/3/17/1024;
Unicode character vectors also compare the string entry. Native original 443
objects remain untouched. Approved root-envelope expectations are separately
named complete trees, including two original duplicate-root fuzz cases and
later wrapper/attribute/comment/doctype/EOF controls. Actual raw native IO
short-read defects remain full separate oracle outputs and are covered against
the verified coalesced profile, not discarded as failing cases.

The mandatory native buffering matrix retains 24 exact memory/coalesced trees
at first non-ASCII positions 3935/3936/3990/3999/4000/4001 and declaration offsets 64/250/3999/4001,
plus 6 late-window controls. All built-in single-byte high-byte maps, malformed
UTF8 bytewise consumption/UTF16 converter-stop,100 UTF8-byte lexical names,
all 2231 named references, and bogus/late declarations have full original trees.
The two final multibyte-name witnesses confirm skipped oversized scalars permit
later ASCII fill. Incomplete late wrapper witnesses preserve the first root and
rollback only the discarded wrapper's new attributes.

Quota facts prove validation before transport, depth/attribute/expanded limits,
exact document bound, caller stream ownership and cancellation. The 10k-byte
source fails at scalar/byte offset 1100 while readPosition is below EOF and <= 5120;
it therefore demonstrates bounded incremental parsing progress. CRLF and a
supplementary scalar produce normalized line 2/column 1 diagnostics for string
input, with no fabricated byte offset. External doctype resources remain inert;
the earlier XML tree rejects XML-invalid element names.

Public sample executes legacy auto-close recovery, encoding, incremental caller
stream and earlier XML behavior. Compiled Roslyn API extraction matches 4 actual
HtmlParser members in docs/api/M7-legacy.txt; these were also checked against the
frozen M7 Engine declarations. No reserved future members are shipped.

The exact measurement wrapper's positive/final-positive controls accept the
frozen closure, unknown effective environment rejects, and final positive
accepts again. The sole exclusive run at 9661cf2 completed all 30 NoEmit BDN cases
and48 × 4096 zero-byte borrowed batches, with initial/final identity and cleanup
all passing. See measurements.md for allocation boundaries, raw hashes,
noisy/priority-denied timing and arm64-only limitations. No reruns were needed.

Historical draft checks were not final passing evidence: initial missing
restore assets and sandbox cache restore failed/hung (the scoped process was
stopped before successful cached restore); initial analyzer/compiler issues
were corrected. Two native multiple-root cases and decoder/sniff/name cases
failed on earlier drafts and were corrected using exact oracle outputs plus
the separately verified profile contract. The final focused results above
include every original case with zero exclusions.

Planning validation: 167 tasks valid. git diff --check passes. No cumulative
suite, x64 benchmark, or integration result is claimed by this worker; the
coordinator performs combined-tree verification.
