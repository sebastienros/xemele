# Shared runtime contract

Representative compile-only declarations are in
`tests/Contracts/Fixtures/foundation/Common.cs.fixture`. The
[runtime foundation](foundation-runtime.md) specifies constructor contracts,
numeric limits, units, and owned/borrowed boundaries. Models are never shipped
as runtime stubs.

`XmlParseOptions` defaults to `Recover=false`, `DtdProcessing=Prohibit`, and
`Resolver=null`, with finite resource limits. Enabling DTD syntax never grants
resource access. `XmlParseResult<T>` constrains T to reference types; well-formed
implies complete and nonnull Value. Results and exceptions own diagnostic snapshots;
a parse exception requires at least one diagnostic.

The nullable positive limits record has explicit null unlimited opt-in, finite
defaults, Int32/Int64 widths and exact units in the approved contract table.
Validate before work. Exactly-at-limit succeeds; one-over rejects with checked
arithmetic. Aggregate counters span nested resources and all sync/async/DOM/event
paths. Representational capacities remain finite even with null input quotas.

Locations use one-based normalized Unicode-scalar line/columns, zero unknown;
original-source zero-based byte offsets are nullable and absent for character
input. Resource URIs survive entity/include boundaries. Module-prefixed string
codes are stable; each module owns its catalog, not a shared enum. Diagnostics
are bounded, including terminal limit entries; no-error paths format no messages.

Strict Parse/Load return complete well-formed values or XmlParseException.
TryParse handles structured syntax rejection without catching argument, I/O,
cancellation or callback exceptions, including callback-thrown parse exceptions.
Recovery has the distinct ParseWithDiagnostics API. Snapshot immutable options
and limits per operation, with no global resolver/cache/callback state.
Caller streams remain open; entry points close opened files. Resolver success
transfers returned resource ownership to the parser for exactly-once disposal.
Cancellation is an operation parameter checked at entry, before external work
and at bounded intervals. Detailed pause/fault/EOF and decoding contracts are
in the runtime foundation; asynchronous state retains owners and offsets, never
spans across awaits or views into returned buffers.

Runtime hot paths follow [performance coding rules](../performance-coding.md).
Allocation-free warm primitive targets are distinct from setup, persistence,
pool growth, owned results and diagnostics. Measure actual XML composition
and validate hardware-sensitive claims across architectures.

Public API baseline fragments live under `docs/api/`. The exact
xemele-api-v1 canonical schema is tools/ApiBaseline/README.md. [Compiled comparison
scheme](api-compatibility.md) compares member IDs and normalized
metadata; inventory validation alone does not establish assembly compatibility.
