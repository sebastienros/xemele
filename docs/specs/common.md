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

## Exact M1 freeze

M1 reuses Common.cs.fixture unchanged and compiles it without M2 symbols in each
independent declaration project. Constructor semantics, nullable/width/default
limits, diagnostics snapshot and strict success invariant remain M0's.
[Strict-subset decisions](../decisions/M1/strict-subset.md) locate each exact
M1 declaration and usage fixture. M1 has no public callback, forward writer,
reader, resolver or async transport types. Internal source mappings are scalar
first-byte offsets parallel to decoded UTF-16 units, with an explicit original
byte length for EOF; raw decoder counts, scanner counts and normalized scalar
locations must not be conflated. Common declares none of the decoder/core types.

M1 result diagnostics are owned on rejection; internal decoding/scanning results
carry a single owned terminal diagnostic. Internal callback exceptions never
become syntax diagnostics. Public output errors use argument/operation exceptions,
not parse rejection. Existing module-prefixed codes are strings, not a global
enum. Tree construction has no parser or serialization dependency. Its default
resource-limits enforcement and persistence measurements belong to M1-tree;
parse limits apply at decoder/core/composition boundaries, with no shared mutable
quota state or later feature automatically enabled by a common quota property.

## Exact M2 freeze

[M2 XML/resource decisions](../decisions/M2/xml-resources.md) supersede earlier
M2 proposals while preserving foundation/M1 declarations and finite defaults.
Separate nonpackaged resolver/DTD/encoding/entities/recovery projects compile
only their required declarations. The foundation-compatible resolver types remain
in Xemele, with nullable refusal and operation cancellation on sync/async methods.
Request lexical SystemId/PublicId are additive init-only properties. Resource
ownership uses the final absolute SourceUri; core never implicitly fetches.

DtdProcessing and Resolver are M2-entities options; Recover is M2-recovery's
separate partial option. Strict entry points reject Recover=true at entry.
Diagnostics factories accept it explicitly. XML 1.0 nonvalidating processing,
bounded repair rules, all quota units/aggregate boundaries, supported encoding
labels, model hooks, metadata and staged implementation ownership are fixed by
the decision and compiled fixture families. No runtime stub is delivered here.
The coordinator-assigned ordered partial-options extension is recorded in the task handoff.
