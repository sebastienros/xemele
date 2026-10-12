# Combined integration verification

Actual tested revision: `8917aceb24b132e0124405279fe9abd6359bca86`. Accepted Schematron evaluator43c6ab4 and effective-content compilerade942a4 were merged with current main in an isolated integration checkout.

Both scripts executed successfully:454 Schematron checks and282 compiler checks (suite counts with overlapping predecessors), zero failures/errors/skips/not-run. Frozen9/154/119/257 and public1400 members matched. Regular and genuine XML-only Schematron consumers and both compiler XML-only consumers passed. Release builds reported zero warnings/errors;179-task planning and whitespace checks passed.

After those checks, only orchestration/task/report metadata was merged, including terminal worker evidence and allocation summaries. Source, tests, benchmark/consumer inputs, API baselines and shared build inputs are identical to the tested revision. No rerun is claimed for metadata-only changes. Both prepared allocation probes subsequently passed under fresh all-host serial-2 grant51357005, with durable release076a64c0 before raw inspection; see owned allocation reports.
