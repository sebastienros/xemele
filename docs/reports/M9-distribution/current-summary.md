# Current distribution checkpoint

The required Windows/Linux/macOS and x64/arm64 distribution matrix is verified. Linuxx64 and macOSarm64 passed run38457602484 at a19b7ea8; Windowsx64 passed run38460364837 at b2e2457c. Each OS passed all10,754 units with zero failures/errors/skips/not-run. Linux/macOS completed21 commands each; Windows completed23. Actual isolated managed, trimmed, NativeAOT and installed-tool executions passed. See [the source-bound matrix and receipts](hosted-windows-final/README.md).

Each OS produced byte-identical clean canonical runtime/tool package pairs with license/notices/docs and exact-revision embedded Source Link. Runtime/unit/fixture/consumer/package/build source is unchanged across these passing revisions; the later fix affects Windows NuGet source configuration and selects only its remaining job. Source Link identifies each actual tested commit; remote source fetching was not tested. Archive hashes and exact commands are retained per OS.

The initial missing-corpus, later Windows test path and Windows local-first NuGet source failures remain preserved. Their narrow repairs passed actual relevant checks, including the final Windows consumer modes. The final Windows run used one explicitly approved20minute job and finished in8.05 elapsed minutes. No additional runner budget is inferred.

The prior local unpublished canonical package/symbol checkpoint at34740332f remains retained with its18-command package/consumer evidence. No package publication or remote main/tag changes occurred. The distribution lane is verified; the remaining performance and robustness gates still require their mandatory evidence. Fuzzing remains deferred.
