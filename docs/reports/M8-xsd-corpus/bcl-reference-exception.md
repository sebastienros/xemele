# BCL reference exception source handoff

The source payload is identified by `campaign-source-identities.json`. This is a
SOURCE-only correction. Corrected throwing-stream controls, auxiliary final-row
controls and reporting tests remain UNRUN. Root's partial preparation4 on the
previous source is recorded below; no build, focused repro, campaign, runtime
repair, or official original outcome was executed by this worker. The task remains
`in_progress`, A1/A3 passed and A2 failed.

Root reviewed63557 and found one P2 blocker: augmented schema compilation and
hint probes ignored sticky resolver failures, while prepared/probe/instance
principals bypassed the read tracker. BCL could wrap those failures or report
warnings, allowing semantic valid, schema-unavailable or preparation-limitation
output. The exact21 binding/frame/report structure and unchanged supervisor/
nesting/ten-command preparation were otherwise accepted. That review did not
authorize execution; it authorized only this narrow SOURCE correction.

Root subsequently gave SOURCE PASS to7990809/fdda4e0. Preparation3 failed its
clean-source check before build because of root-created bytecode cache; root
removed only that cache. Preparation4 on unchanged fdda/799 passed build, runtime
controls and hint controls, then aborted optional command3 at ReferenceControls
line169, `actual throwing stream origin rejected False`. All five owned groups
were terminal/absent, direct children reaped and logs closed; root committed
durable cleanup main1cfde7ee3 before inspection. Commands4..9 and the later
auxiliary final-observation controls were UNRUN. Raw failure log remains ignored
at `artifacts/M8-xsd-corpus/fdda4e0b5311155fba4abc198a1507e57386c399-controls-a8b7e7e903f14cbaa1d070e1a7b84d45/command-3.log`,
SHA256 `5de38105e42b07276266ad51fbc800815806f99241b16d282ab4b54269f96978`.

Read-only inspection confirms21 exact original witness/attribution records and21
actual-stack/original-false PASS markers before the negative failure: all Add,
runtime10.0.11, pinned module SHA and module MVID
`66917835-a7e6-4a12-912e-df7f9f705820`. These are root-executed controls at799,
not a successful preparation, current-source pass or complete corpus campaign.
The historical source80 harness observations and root stopped campaign survive.

Both fault helpers inherited empty seekable MemoryStreams. Current primary
[XmlReader source](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlReader.cs)
sizes a seekable input buffer from its Length;
[XmlTextReaderImpl source](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlTextReaderImpl.cs)
uses that size during stream initialization. A zero-size buffer bypassing the
intended read is the source-based diagnosis; the failed assertion did not log
each predicate, and no worker repro was run. The minimal correction makes only
the two fault helpers non-seekable and respects zero-length reads. ThrowingInput
now requires an actual nonempty read count, exact recorded fault identity,
unknown-origin rejection, a final production sticky harness observation/original
false, and (for a directly caught ArgumentException) rejection by ApiFailure even
with a previously validated21 binding. Assertions are stronger, with no allowance,
production classifier, wrapper, Runner, helper/supervisor or policy change.
Corrected controls are UNRUN pending renewed source review and root activation.

The follow-up wraps all three principals and records acquisition/read exceptions
in a sticky per-operation resolver tracker. Augmented compilation checks the
tracker after Add and Compile before any schema-unavailable output. Instance
reading checks it before semantic or late-hint-limitation output. Hint probes
check it before semantic output and preserve harness failures through their
preparation-result mapping. Catch branches give infrastructure precedence over
wrapped XML/schema errors and over the hint-probe path. A standalone harness
failure also stays a harness failure in dependent observations. The exact21
reference allowance is still available only at standalone Add/Compile boundaries;
the probe's ordinary empty-ID exception remains a harness failure.

New native controls call production Run/ProbeHintSchema and assert final harness
observations, original passed:false and the injected fault's unique detail. Their
internal injection delegates default to the unchanged original acquisition paths
and do not alter Runner policy. Twenty acquisition/read cases cover ArgumentException
and IOException from resolver acquisition/returned streams and prepared/probe/
instance principals. Eight additional final observations cover caller-wrapped
XML/schema exceptions, including the complete Run→hint-preparation→probe path.
Healthy controls establish the production late-hint/probe-limitation branches.
The augmented/probe controls require an actual BCL warning or a newly wrapped
exception; tracker population alone cannot pass. Instance native wrapping is
reported separately; caller-wrapped semantic errors must still yield final harness
failures. All controls remain UNRUN. Synthetic fixtures/journals stay in memory
or one uniquely owned ignored artifacts folder, removed by the control's finally.
No Runner, original metadata, optional mapping, approval guard, runtime/product,
helper or supervisor code changes are part of this correction.

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
support this path; root preparation4 supplies actual pinned-runtime frame evidence
on799. Corrected current-source controls and complete preparation remain pending;
that partial evidence is never an original pass or full campaign result.

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
only; they cannot substitute for actual native stack evidence. Root preparation4
ran21 witnesses and preceding negatives, then failed the throwing-stream control;
the corrected stream controls, later negatives and reporting tests remain UNRUN.

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
