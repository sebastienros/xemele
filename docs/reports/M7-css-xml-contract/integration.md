# Integration verification

Root verified integrated `383c7eef263280dfcd3433f165260070e9094b61` independently.
The canonical checker with explicit `NoWarn=CS0436` passed Engine173,
Tokenizer116, Selector133 exact members, three missing-member negative controls,
and XML tree/stream preservation on actual runtime10.0.11. Its ownership base
is integrated HEAD; separate complete worker claim67c9894→handoff6b1e1f8
owned-path audit passed all10 changed paths.

The actual-model consumer restored from cached packages, built with pinned
SDK10.0.400, RuntimeFrameworkVersion10.0.11, shared compilation disabled and
normal warnings-as-errors: zero warnings/errors. Explicit --fx-version10.0.11
execution passed actual HTML/XML model assembly identity and tree/stream probes.
No declaration bodies execute. Planning167 and whitespace validation passed.

This verifies additive declarations/semantics only; M7-css owns runtime matching.
