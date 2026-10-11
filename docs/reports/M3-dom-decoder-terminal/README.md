# DOM terminal decoder repair

Corpus fuzz qualification found five UTF-8 mutations for which the decoder
returns a valid detection prefix and terminal InvalidInput in the same call.
DOM previously forwarded that prefix before recording the decoder rejection;
declaration validation then threw an internal InvalidOperationException.

The bounded repair checks terminal InvalidInput before forwarding an unvalidated
declaration prefix in both Append and Complete, synchronous and asynchronous.
It returns the decoder's existing owned diagnostic. Once declaration validation
has succeeded, existing prefix forwarding and diagnostic precedence are unchanged.
No public API, buffers, decoder grammar, quotas or valid-byte decoding change.
Encoding rejection is unrecoverable; an undecided prolog cannot contain an
already completed root, so the guard discards no completed DOM root.

The five exact mutations are frozen by DecoderTerminalTests. Tests cover byte
TryParse/strict Parse, synchronous and genuinely suspended asynchronous short
reads at sizes 1, 2, 3 and 4096, and diagnostic APIs with recovery on/off. Existing
input, DOM, recovery and async input checks provide valid-input preservation.

The prepared probe measures actual process-wide cold/warm DOM ownership,
diagnostic ownership, true async suspension and an escaping array positive
control. These paths intentionally allocate setup, tree state and diagnostics.
The guards introduce no strings, arrays, delegates or retained state. Existing
borrowed decoder/core allocation regressions remain required; no optimization,
speedup, zero-DOM-allocation or comparative latency claim is made.

Source review approved commit 4172287c100f14d80c20622aea6f66fc61c9d47c.
Release builds passed with zero warnings/errors. At source a164900ffd0315d4824fad442700a37183232386, all 166 focused tests passed (no failures/errors/skips), including the five new regressions, existing input zero-allocation control, DOM, recovery and async-input preservation. Official API comparison matched 720 members. The actual process-wide probe exited 0: cold owned DOM 53,208 B; 128 warm valid DOM operations 6,804,480 B; 128 invalid owned diagnostic operations 6,757,376 B; 128 genuinely suspended async invalid operations 7,372,072 B; escaping-array positive control 527,360 B. These are inclusive owned operation costs, not primitive allocation or comparative timing claims.

The initial test helper recursively used MemoryStream's virtual fallback and the test process exited 134 before a test summary. The corrected helper reads through ReadByte; runtime repair source remained unchanged. Failed output is preserved in helper-recursion-failed.log.gz. No unrun or failed check is reported as passing.
