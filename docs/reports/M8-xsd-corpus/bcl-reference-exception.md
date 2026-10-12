# BCL reference exception source handoff

The source payload is identified by `campaign-source-identities.json`. This is a
SOURCE-only change. All new native and reporting controls are UNRUN; no build,
focused repro, campaign, runtime repair, or new original outcome was executed by
this worker. The task remains `in_progress`, A1/A3 passed and A2 failed.

Root preparation2 on b904bcaab completed all ten commands with 42,793 frozen
identities; its cleanup was recorded on main56554b6e4 before inspection, with
summary9023309f5. Root fullcampaign1 on that source stopped at command110,
`msMeta/AttributeGroup_w3c.xml`, on original INVALID schema attgA003:
`ArgumentException` with parameter `name`, no resource callbacks and original
`passed:false`. Root recorded cleanup on maina9ca7b983 before inspection:
111 launched children exited0, reaped, groups absent, logs closed; all112 root
registered groups absent. Product covered all93 sets/41,736 observations with
neutral counts39,311 pass/163 nonpass/11 indeterminate/2,251 excluded. These are
root-reported observations, not gate conformance or worker checks. BCL covered
only18 launched sets. Both frozen identities and historical raw artifacts remain
preserved. Preparation1's earlier assertion failure remains recorded separately.

`bcl-reference-exception-scope.json` pins exactly21 historical source80cd BCL
harness exceptions. Each is an applicable, accepted, original INVALID schema
with one authored document, an empty ID attribute, no callbacks and original
`passed:false`. The full row binds original expectations, status history,
scope/applicability, document metadata and order. Inventory/catalog, authored
metadata, principal bytes, source URI, purpose, runtime10.0.11 and
System.Private.Xml module SHA are guarded. Principal hashing uses the acquired
stream and resets it before the reader uses those same bytes.

Only literal XmlSchemaSet.Add(null, reader) and Compile calls catch the exact
ArgumentException type. Acquisition and reader construction occur outside those
boundaries. Attribution requires actual exception frames from the loaded pinned
BCL module: optional CoreLib ArgumentException guards; XmlConvert.VerifyNCName;
Preprocessor.ValidateIdAttribute; a contiguous BCL path containing the correct
schema API signature; then the corresponding non-inlined owned boundary. Inner
exceptions, caller/resolver/stream frames, callbacks, diagnostics or tracked
infrastructure failures reject attribution. Missing frames due to JIT inlining
also reject it. Exception text alone never allows a result. Current primary source
[Preprocessor](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.Xml/src/System/Xml/Schema/Preprocessor.cs)
and [XmlConvert](https://source.dot.net/System.Private.Xml/System/Xml/XmlConvert.cs.html)
support this candidate path; they do not prove the pinned runtime's actual stack.
The actual-stack native controls must establish that path before execution
evidence can be accepted.

The new outcome is `reference-engine-exception`, a nonassessment with structured
module/method/operation provenance in `detail`. Original `passed` stays false;
it is neither schema-invalid, an original pass, nor an approved gate disposition.
The report guard independently checks scope, complete original metadata, runtime,
hashes, API signature and frame sequence. Every `harness-*` row still stops the
campaign. Normal schema compilation tracks principal and resolver stream failures
as harness errors even if BCL wraps them or reports a warning. Authored absence keeps its existing reference policy;
no optional mapping or approval guard is expanded.

The existing optional-profile preparation command now also runs the real21
fixture/stack controls and rejects unknown origins/IDs, changed metadata/hash/
URI/purpose/runtime, caller XmlConvert exceptions, throwing streams, throwing
resolver callbacks and returned streams, declared missing inputs, changed catalog
and authored metadata. Six additional reporting test methods use synthetic data
only; they cannot substitute for actual native stack evidence. All are UNRUN.

The proposed high-level command remains
`PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/controls.py`:
ten commands,1800s total,300s work+10s cleanup per child, root outer1800/1810,
unchanged finite supervisor and durable cleanup-before-inspection rules. Root
SOURCE review and explicit activation are required before any execution. A future
campaign needs successful current-source preparation, newly frozen source/product/
BCL/runtime identities and a separate explicit campaign plan. Historical b904
product rows must not be combined with new BCL classification as a new full run.
Runtime/product, originals, caps, profiles, approvals, build files and supervisor
function bodies remain unchanged. Further product repairs or dispositions require
the coordinator's separate scoped brief and, where applicable, human approval.
