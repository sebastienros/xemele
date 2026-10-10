# Persistent resolution session cancellation boundary

The frozen hook is `internal void Xemele.Resolution.Core.XmlResolutionSession.BindCancellationToken(System.Threading.CancellationToken token)`.
It changes no public member, constructor, handle, or existing resolution/read method.
A sequential owner calls this once while idle before work for a new call. New
sessions still receive that call's token through the existing constructor.

Binding first enters the existing session guard, rejecting disposed, faulted,
and busy/reentrant sessions before mutating state. It stores the exact token,
then checks it before the budget's independently captured constructor token.
Cancellation faults and synchronously drains every accepted handle through the
existing drain mechanism, preserving the original cancellation exception and
token even if cleanup throws. A busy rejection neither changes the token nor
faults the active operation. Successful binding preserves quota counters, active
URI keys, open parent handles, terminal diagnostics and the shared budget.

The limits constructor still creates a budget capturing its original token.
Rebinding the session cannot revoke that budget's independent cancellation.
Owners requiring only per-call tokens use the existing shared-budget overload
with an appropriately scoped budget. No token linking, registration, stable CTS
bridge, global state or budget replacement is introduced. Callers retain the
sequential-use obligation; this is not a concurrent session API.

The sample independently compiles the exact internal call against source and a
fresh local package using the existing friend assembly. Reflection inventories
its assembly-only visibility, void return, and single CancellationToken argument.
Tests use actual resolver overrides and suspended async stream work. The sample's
explicit allocation mode and the short-run memory-diagnosed benchmark are held
until the coordinator grants exclusive measurements. The baseline performs the
same token and budget checks without session guard/store work. Warm binding
measurements exclude construction and rejection; they are provisional Arm64
microbenchmarks, with consumer composition and other platforms left to their
own tasks. This task makes no parser/core-consumer implementation claim.
