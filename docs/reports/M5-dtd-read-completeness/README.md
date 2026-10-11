# Retained unread DTD parameter completeness

Implementation revision: `51f0fe950984a1cf981040fa057224f3f56662ba`.
Claim: `d246b684038b1a0dbe1dc0c39b32e603f88d0a68`.
Base verified prerequisite tree: `5810975bb426f8de2972e6bed3a2ecbd1dd57640`.

The internal immutable `DtdModel.UnreadParameterLocation` is nullable and getter-only.
Its optional final constructor argument preserves existing five/six-argument calls.
A null value means no unread parameter evidence was retained. A value is the original
first skipped parameter reference location, including its resource URI and original
byte offset when available. No public API, source reparse, raw subset or lexical
sidecar is added.

`DtdParser.FinishSource` snapshots `priorUnreadParameterLocation ?? FirstUnreadParameter`.
The prior-model value is held in a new private scalar field that is **separate from
live declaration-reading controls**. `SeedPriorModel` stages that value and publishes
it only after all existing staged collections and the final cancellation check.
The earliest prior evidence wins over a later current-source skip. ConfigureReading,
FirstUnreadParameter, suppression, standalone and nonvalidating success behavior
are unchanged. `DeclarationValidityDiagnostic` remains a separate retained value;
its original diagnostic identity and first bindings survive.

## Actual checks

Offline restore used the local NuGet cache, disabled audit and ignored failed sources.
Pinned SDK 10.0.400 Release build used disabled MSBuild servers/shared compilation:

```text
DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' build tests/Xemele.Tests -c Release --no-restore -p:UseSharedCompilation=false --disable-build-servers -m:1
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class 'Xemele.Tests.DtdReadCompleteness.*' -noLogo
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class 'Xemele.Tests.DtdCompositionTests' -class 'Xemele.Tests.DtdModel.*' -class 'Xemele.Tests.DtdDeclarationProvenance.*' -class 'Xemele.Tests.DtdGrammarJoinedTests' -noLogo
python3 tools/planning/tasks.py validate
git diff --check
```

All ran at the implementation revision and passed. Build: zero warnings/errors.
Focused suite: **13 cases**, zero failed/skipped; preservation selection: **60 cases**,
zero failed/skipped. Planning: **156 records** validated. Runner and explicit in-test
FrameworkDescription check confirmed actual `.NET 10.0.11`; installed folder inventory
was not used as actual-host evidence.

| Focused behavior | Actual verification |
| --- | --- |
| First skipped PE | External root declaration before missing PE; every character/byte input split, repeated second missing PE, original line/column/offset |
| Complete models | Internal/external declared PE expansion and old constructor calls retain null |
| Nested external PE | Redirected child URI, normalized line 2 column 1 and original byte offset 51 |
| Current DOM metadata | Sync and one-byte async external subsets, nested external PE and internal-to-external composition carry original location; caller stream stays open |
| Control independence | Seeded unread prior value alone does not suppress a subsequent default declaration; a later live skip still sets only the existing live control and suppresses later defaults |
| First retention | Prior location wins over later live location; prior declaration diagnostic identity and EMPTY first binding stay intact |
| Transactional seeding | Pre-canceled token, failure after staging, and deterministic cancellation after a first staged element leave live metadata/collections unpublished; clean reseeding succeeds |
| Storage/code shape | Internal get-only nullable type; optional constructor default null; seven-byte getter IL; constructor scalar field-store sequence; 40-byte nullable field payload |

The mid-staging cancellation test uses reflection solely to inject a canceling test
collection after the first element. Production constructors still own read-only
snapshots; this test does not create a public mutable-model contract.

## Allocation applicability

There are no new scan/match loops, strings, diagnostic objects, collection allocations,
boxing operations or source buffers. The added `XmlLocation?` fields are inline scalar
payload: actual `Unsafe.SizeOf<XmlLocation?>()` is **40 bytes** on this verified Arm64
runtime, one field in the model and one field in the temporary parser. This is field
payload, not a measurement of whole-object size/alignment or retained whole heap.
The existing immutable location includes an existing URI reference; no URI copy or
new source text is stored. The constructor copy adds `ldarg.0; ldarg.s 7; stfld` and
the getter is `ldarg.0; ldfld; ret` (7 IL bytes). Both were checked from actual compiled
method bodies. Nullable coalescing and seed publication copy existing values at
one operation-completion/initialization boundary, with no new hot token work.

No timing or allocation counters ran. No zero-byte whole-model allocation claim is
made: existing parser/model snapshot allocations remain and each object has additional
inline field payload. Dedicated benchmark measurement would not substantiate a new
hot-loop claim because this repair introduces no hot-loop algorithm. Earlier verified
performance evidence is not relabeled or rerun. No raw benchmark outputs were created.

The later facade owns consuming this retained hook to reject incomplete declaration
provenance; it must start from coordinator-verified integration. This task delivers
only retention and preserves permitted nonvalidating parse success. Broader milestone
checks remain the gate owner's responsibility. No unmerged peer dependency, shared
build/API edits, main/state writes, remote push or publication occurred.
