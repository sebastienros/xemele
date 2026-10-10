# Syntax-prevalidated persistence contract

This internal boundary is for verified syntax sinks and independently validated
owned model replay only. It has no parser dependency or public API addition.
The caller proves the EXACT immutable strings supplied: XML 1.0 scalar legality;
NCNames and expanded names; reserved xml/xmlns bindings; PI XML Name target and
case-insensitive non-xml restriction; comment/CDATA/PI delimiter legality; unique
expanded attribute keys and namespace prefixes on each element. Passing invalid
syntax to this trust boundary is misuse, not a validation experiment. Existing
public factories/mutation and checked builder members continue to validate.

Signatures on XmlDocument.DomBuilder (all internal):

- CreateSyntaxValidatedNode(XmlNodeKind kind, string localName, string namespaceUri,
  string prefix, string value, CancellationToken cancellationToken)
- AppendSyntaxValidatedChild(XmlNode parent, XmlNode child, CancellationToken cancellationToken)
- AppendSyntaxValidatedAttribute(XmlNode element, string localName, string value,
  string namespaceUri, string prefix, CancellationToken cancellationToken)
- AppendSyntaxValidatedNamespaceDeclaration(XmlNode element, string prefix,
  string namespaceUri, CancellationToken cancellationToken)

Start BeginDomConstruction on a fresh private document. Create then immediately
attach the newest sole detached leaf to a previously constructed container.
Element has nonempty localName and empty value. PI has nonempty target localName,
empty URI/prefix. Text/CDATA/comment have empty localName/URI/prefix. Other kinds
reject. Strings are required nonnull; no nullable normalization is offered.
Container/owner/freshness/root checks reuse the verified builder. Creation and
members reuse original private primitives for intern quota/capacity, version
preflight and postcommit notifications. No references survive arena growth.
Caller must not structurally mutate outside the builder or append duplicate keys.

Each entry checks cancellation before work. There is no custom string/name/scalar
or delimiter loop in this boundary. Platform string hashing/comparison, interning
copy on miss, and arena allocations can scale with input size; these are not
claimed bounded or cancellable. Cancellation abandons private construction; no
allocation rollback. Full accumulated comment/CDATA/PI strings must be validated,
including delimiters spanning callback fragments. Persist fragments by explicit
ownership, never by retaining borrowed views. Probe borrowed names before miss
ToString; sample model uses ordinal alternate lookup and owns every stored value.

The independent sample parses a complete owned event model with verified syntax,
disposes parser, then replays only that model. It is not the later DOM adapter.
Allocation probe separates first-use setup, fixture validation/materialization,
warm attachment, arena growth, complete owned construction and retained estimate.
Prepared capacity warm persistence is also tested at zero allocated bytes.
Measurements compare identical prevalidated owned replay: checked factory/member
validation versus trusted persistence, with the same builder attachment in both.
Fixtures/validation are outside measurement; values and names cover short/long
sizes and all supported kinds. No timing starts until exclusive coordinator grant.

## Completed branch evidence

Runtime/tests/sample correctness revision:
`46e6d1bfe439c4b949ca534d917d371a1e2b3cb9`. Coordinator independent
read-only review passed with no blockers. Benchmark oracle refinement only:
`ecd4a2961d3126257f9c4cdfdf28276ed9a35bad`. The complete oracle checks all
322 nodes, every kind/name/URI/prefix/value, child position and member fields/order.
Its harness passed all four parameter pairs, without timing.

Correctness: Release zero warnings/errors; persistence8, builder11, Tree14,
Input10, Syntax70, solution223 and Foundation11 passed without skips. Explicitly
rebuilt foundation139/173/ApiProbe8 and M1 models/protocol38. Actual compiled
public215 matches approved Serializer golden and mandatory canonical fragments.
Independent source model and fresh local-feed NuGet-only model consumer passed
using isolated RestorePackagesPath. Planning104 and owned-path/diff audits pass.
Warm prepared-capacity persistence regression is0B. Separate first-use
setup1560B, fixture name materialization/validation35568B, warm4096 attachment0B,
node arena growth1387712B, owned512 attribute/declaration pairs271280B;
retained estimate229445B/document over32 documents, excluding shared fixture
strings. Counts and lifetime evidence remain distinct from whole-parser claims.

One exclusive coordinator-authorized run at the oracle revision completed eight
workloads, wrapper and all children exit0, three actual measurements each.
Apple M4 Pro Arm64, macOS15.8.1, SDK10.0.400/runtime10.0.11; no active task
build/timing processes after completion. No repeat was initiated. Elevating
priority was denied, so ordinary scheduling applies; wide confidence ranges are
retained in raw/result files. Measurements are provisional synthetic persistence
microbenchmarks on one architecture, not complete input parsing. Fixture
validation/materialization is outside timing, intentionally identical for both
prevalidated replay paths. Retained inputs are shared owned strings.

| Case | Checked mean | Trusted mean | Managed allocation (both) |
| --- | ---: | ---: | ---: |
| short names |27.24us|18.14us|150.52KB|
| short values |22.49us|16.93us|150.46KB|
| long names |8781.56us|2868.41us|214.45KB|
| long values |3108.51us|16.59us|150.46KB|

Long-name trusted work still includes platform hashes/comparisons/intern copying;
long-value persistence retains the already owned string. No platform operation
is claimed cancellable. End-to-end DOM and x64 release checks remain future-owner
work. Earlier failures are preserved: initial uncommitted build xUnit1051 was
fixed with test context tokens; sandbox MTP IPC failed before any test outcome,
then same-revision authorized rerun passed. New oracle sandbox restore was
cancelled130, then authorized rerun passed. Coordinator reporting was rejected
twice by automatic approval review; direct human authorization in this chat then
allowed status reporting. These failures are not represented as passing checks.

## Integration harness corrections

The initial committed-delta check at a2d62048 failed on four trailing-whitespace
lines generated by BDN in Markdown/HTML. Earlier working-tree-only checks did not
inspect those committed lines. Presentation files were normalized; the raw gzip
is byte-for-byte unchanged. Verification now checks the explicit committed delta
plus working changes. Use --audit-base with the pre-merge coordination revision
for integration; its default remains the original task base. Allowed ownership
is unchanged, and unrelated coordinator changes are excluded only by selecting
the correct integration comparison base, never by widening allowed paths.
