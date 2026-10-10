# Shared runtime contract

Initial decision owner: M0-contracts; implementation owner: M1-common.
Later changes require an ordered contract/repair task. Exact representative
compiled declarations are `tests/Contracts/Fixtures/foundation/Common.cs.fixture`;
[foundation decisions](../decisions/M0/foundation.md) specify their semantics,
constructor contracts, limits, units and owned/borrowed boundaries. These models
are compiled per stage and never shipped as runtime stubs.

M1 XmlParseOptions exposes only Limits. M2 additively adds Recover=false,
DtdProcessing=Prohibit and Resolver=null, with working enum/resolver/resource
APIs. M1 has no DTD/recovery/resolver references; unsupported DTD input is safely
rejected. Enabling syntax never grants resource access. No later resolver type is
needed to compile or use M1. XmlParseResult<T> constrains T to reference types;
well-formed implies complete and nonnull Value. Results/exceptions own diagnostic
snapshots; a parse exception requires at least one diagnostic.

The nullable positive limits record has explicit null unlimited opt-in, finite
defaults, Int32/Int64 widths and exact units in the approved decision table.
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
M2 recovery has the distinct ParseWithDiagnostics API. Snapshot immutable options
and limits per operation, with no global resolver/cache/callback state.
Caller streams remain open; entry points close opened files. Resolver success
transfers returned resource ownership to the parser for exactly-once disposal.
Cancellation is an operation parameter checked at entry, before external work
and at bounded intervals. Detailed pause/fault/EOF and decoding contracts are
in the foundation decision; asynchronous state retains owners and offsets, never
spans across awaits or views into returned buffers.

Runtime hot paths follow [performance coding rules](../performance-coding.md).
Allocation-free warm primitive targets are distinct from setup, persistence,
pool growth, owned results and diagnostics; each implementing task supplies
applicable evidence. M0 choices are provisional single-Arm64 synthetic evidence,
with actual XML composition and cross-architecture release checks still required.

Public baseline fragments stay task-owned at docs/api/<task-id>.txt. The exact
xemele-api-v1 canonical schema is tools/ApiBaseline/README.md. [Compiled comparison
scheme](../decisions/M0/api-compatibility.md) compares member IDs and normalized
metadata; inventory validation alone does not establish assembly compatibility.
