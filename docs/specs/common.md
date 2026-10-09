# Shared runtime contract

Initial decision owner: `M0-contracts`; implementation owner: `M1-common`. Later changes require an ordered contract/repair task.

Runtime hot paths must follow the [string/byte performance rules](../performance-coding.md). API contracts identify borrowed versus owned values and intentional allocation boundaries; feature evidence must include applicable allocation and benchmark results.

## Shapes

```csharp
namespace Xemele;

public enum XmlDiagnosticSeverity { Warning, Error, Fatal }
public readonly record struct XmlLocation(
    Uri? SourceUri, int LineNumber, int LinePosition, long? ByteOffset);
public sealed record XmlDiagnostic(
    string Code, XmlDiagnosticSeverity Severity, string Message, XmlLocation Location);
public sealed class XmlParseException : Exception
{
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
}
public sealed record XmlParseOptions
{
    public bool Recover { get; init; }
    public XmlDtdProcessing DtdProcessing { get; init; }
    public XmlResourceResolver? Resolver { get; init; }
    public XmlResourceLimits Limits { get; init; }
}
public enum XmlDtdProcessing { Prohibit, Parse }
public sealed record XmlParseResult<T>
{
    public T? Value { get; }
    public bool IsComplete { get; }
    public bool IsWellFormed { get; }
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
}
```

This is the target M2 option surface. M1 exposes only implemented options: unsupported DTDs and recovery are rejected, and M2 adds their working properties/values. M1 does not reference an unimplemented resolver type. M0 fixtures distinguish stage-specific surfaces. Prefer additive overloads/properties with preserved defaults; never add abstract interface members to an already shipped public contract without a compatibility mechanism.

`XmlResourceLimits` has finite limits for document characters, depth, attributes per element, token characters, interned names, entity nesting/expanded characters, external bytes/count, inclusion depth, diagnostics, and evaluation work. M0 must select concrete defaults, integer widths, units, and explicit unlimited opt-in representation. Limits must be enforced cumulatively, including nested resource operations; tests cover exactly-at-limit and one-over-limit cases.

## Semantics

- Position: one-based lines and Unicode-scalar columns in normalized XML input; unknown line/column is zero. Byte offset, when present, is zero-based in the original byte source. String input has no byte offset. Locations retain their resource URI across entity/include boundaries.
- Diagnostic codes are stable module-prefixed strings (for example `XML1001`); each module owns its code catalog, avoiding a shared enum edit for every feature.
- `Parse`/`Load` return complete strict results or throw `XmlParseException`. Recovery has a distinct `ParseWithDiagnostics` result API. `TryParse` reports syntax rejection without catching argument, I/O, cancellation, or user-callback exceptions.
- Snapshot immutable options per operation. No global state, resolver, namespace cache, or error callback. Diagnostics collection is bounded.
- Caller-owned streams stay open. File entry points close streams they open. Cancellation is checked at bounded work intervals and before external reads.
- Resource resolution is denied by default; enabling DTD syntax does not grant access. Resource limits and cancellation apply equally to sync, async, DOM, and event paths.

Every implementing task's public baseline fragment lives in `docs/api/<task-id>.txt`, as listed in its ownership record. M0 specifies how CI composes these into one checked assembly surface without rewriting unrelated fragments. Partial types can have members in multiple fragments; duplicate/conflicting member declarations fail validation.
