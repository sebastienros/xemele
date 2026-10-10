# Reference protocol v1

UTF-8 JSON Lines, one request and one response per LF-terminated line. CRLF is
accepted by JSON whitespace rules. Logs belong only on stderr. The BCL process
supports several requests until stdin EOF; the managed client uses one owned
process per case and requires successful exit and exactly one response.
Limits: request/response 1,048,576 UTF-16 characters; client caps stdout and stderr
independently at that size. BCL XML document limit 1,048,576 decoded characters,
entity expansion limit 262,144. Infrastructure limits are not conformance verdicts.
Quota XmlExceptions are recognized by exact localized framework diagnostic
messages calibrated from tiny controlled quota probes at startup, never property
name substrings that could also appear in invalid input diagnostics.
Boundary tests exercise accepted 262,000 and rejected 263,000
expanded characters. Malformed UTF-8 is parse rejection with either auto encoding
or the explicit strict UTF-8 hint.

Request required fields: `protocolVersion:1`, nonempty `caseId`,
`operation:"xml.events"`, `inputBase64` (original bytes), `profile:"xml-1.0"`.
Optional `encodingHint` is absent/null (XML BOM/declaration autodetection) or
`"utf-8"` (strict UTF-8; no BOM autodetection override). `options` is absent/null
or empty; unknown options, profiles, operations, encoding hints and versions fail
explicitly. `offlineResources` is absent/null or a dictionary of absolute URI
to base64 bytes. URI keys use System.Uri canonical absolute URI spelling;
relative references resolve against `urn:xemele:input` or the external entity URI.
No filesystem or network fallback exists. XML declarations and namespace prefixes
are syntax, not semantic events. Unknown additive JSON fields are ignored in v1;
future modules may add optional fields but cannot change these meanings.

Responses always include `protocolVersion`, `engine`, `outcome`, `diagnostics`.
`caseId`/`operation` echo deserialized request values; absent/null identity is
omitted on requests that could not be decoded. Engine has `name` (System.Xml),
`version` (actual assembly version), `runtime` (actual framework description),
`build` (actual runner informational version including source revision when built
by the SDK). It does not invent an independent BCL package or parser version.
If echoing a huge request identity would itself exceed the response line limit,
the limit failure omits identity; the client categorizes this as infrastructure
failure. The limit applies to failure responses as well as successful payloads.
Outcomes: `success`, `parse-rejected`, `runner-failure`. Diagnostics have category
and human message. Categories: `xml`, `protocol`, `resource-denied`,
`infrastructure`, `limit`. Unsupported requests, resource policy violations,
process exit/malformed response/identity mismatch/pipe overflow/timeout are
infrastructure failures, never a successful parse or invalid XML verdict.
Cancellation propagates OperationCanceledException after killing and awaiting
the owned child and its pipe operations. Client timeout throws ReferenceFailureException.

`ReferenceProcess.ExecuteAsync` accepts an optional final
`Func<JsonElement, bool> validateSuccessPayload` callback. Built-in `xml.events`
successes always undergo strict v1 field validation; a callback may add checks
but cannot bypass it. Other operations require a supplied callback returning true,
after shared framing/identity/metadata/diagnostic validation. Callbacks inspect the
complete response synchronously while its JsonDocument is alive (clone any element
retained beyond the callback). Module-owned adapters supply their own validator
without editing this earlier client. A false result is an infrastructure failure.
Actual child tests use a synthetic `fixture.custom` payload to prove this boundary;
no future parser/query/output operation is implemented or reserved by that fixture.

Success includes `events` in document order. `start`/`end` use `namespaceUri`
and `localName`, including explicit empty namespace string. Start has an
`attributes` array (including empty), excluding xmlns declarations, sorted by
namespace URI then local name using ordinal comparison. Attributes use expanded
names and XML-normalized values. Empty elements emit start and end. Text, CDATA,
whitespace and significant whitespace emit `text`/`value`, adjacent text events
coalesced; XML newline and entity normalization are performed by the reference.
Comments, processing instructions, declarations and DTD syntax produce no events.
Absent fields mean not applicable, never an empty semantic value; empty strings
and empty arrays remain explicit when meaningful. No locations are emitted:
engine exception messages may contain engine-specific locations and are diagnostic
only, never portable location assertions. Rejection/failure omits `events`,
including partial events. v1 has no query/output-byte or tree operation; those
require later task-owned adapters. XML 1.1 is unsupported by this BCL profile.

The BCL is test/reference infrastructure, never part of Xemele runtime packages.
Subprocess duration is not parser performance. No CPU or allocation benchmark is
claimed for this infrastructure task. macOS arm64 checks do not establish other
platform/toolchain support.

Projects are siblings (Protocol and Bcl); neither recursively includes the other's
sources or obj. Client and tests use existing parent SDK globs and create no nested
project. Ordered build wiring should add to Differential csproj:

```xml
<ProjectReference Include="../../tools/ReferenceRunners/Bcl/BclReference.csproj" ReferenceOutputAssembly="false" />
```

Until that owned follow-up lands, build BclReference explicitly before solution
tests. `python3 -B docs/reports/M0-references-protocol/verify.py` does this and
preserves focused, wire and bootstrap transcripts. Use pinned SDK 10.0.400 on PATH
and DOTNET_ROOT, with necessary compiler/test IPC and restore network permissions.
