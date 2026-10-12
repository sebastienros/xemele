# M8 exact schema contract

This decision freezes declarations, behavior and ownership. It is not implementation
or conformance evidence. The authoritative signatures are `tests/Contracts/Fixtures/M8`
and their emitted `xemele-api-v1` baselines. Internal declarations are included in
comparison because independent workers share one final runtime assembly. The
fixture projects are separate nonpackaged executables; their declaration bodies
are never called. There are no shared runtime/build changes in this task.

## Standards and scope

The required inventory is `inventory.json`. Normative anchors are
[RELAX NG 2001](https://relaxng.org/spec-20011203.html),
[XSD Structures Second Edition](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/),
[XSD Datatypes Second Edition](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/),
and [ISO/IEC 19757-3:2016](https://www.iso.org/standard/55982.html).
These are deliberately pinned editions, not a claim to the newest Schematron.
The Schematron project's [2016 properties explanation](https://schematron.com/document/2760.html)
clarifies the required diagnostic/property result distinction.

RNG XML syntax is complete. Both the empty URI built-in string/token library and
`http://www.w3.org/2001/XMLSchema-datatypes` library are mandatory. XSD type names
use `http://www.w3.org/2001/XMLSchema`; the RNG library maps its own namespace to
that shared value implementation. Unknown datatype libraries/types are schema
errors. Caller extensions and RNG compact syntax are excluded, not guessed.
XSD Structures and Datatypes 1.0 are complete; 1.1 cases alone are excluded by
version metadata. Observable document augmentation, PSVI export, CLR mapping and
XSLT evaluator generation are outside the release profile.

Schematron namespace is `http://purl.oclc.org/dsdl/schematron`. The accepted
queryBinding value is `xslt`; omission selects it. The public enum calls this
`XPath1` because expressions are evaluated directly by M4. No alternate aliases,
XPath2/3, EXSLT extensions, external document() function, user callbacks or XSLT
instructions are accepted. This is the declared XPath 1.0 binding, not an XSLT
processor. Reject unsupported binding/foreign executable syntax explicitly.
Includes, abstract patterns with parameters, abstract rules/extends, phases, all
let scopes, tests, diagnostics/properties and message expression evaluation are
required. Non-executable foreign annotations may be ignored with no effect.

## Entry points, snapshots and exceptions

All facade signatures use actual M1 `XmlDocument` (no new Document type).
Compile accepts document, owned string, or readable caller stream. Validate has
the same three input forms. Null arguments fail with ArgumentNullException;
wrong stream capability, relative BaseUri, undefined option enums, null nested
options and nonpositive finite quotas fail before any resource access. Stream
input starts at the current position, never rewinds and remains open on every
exit. No implicit filename interpretation, HTTP fetch, global cache or file I/O.

Compile returns an immutable schema safe for concurrent independent runs.
Snapshot every input collection and nested option/limit object; no caller schema
nodes, mutable graph lists or namespace dictionaries may remain in the published
schema. Detect input ContentRevision changes during snapshot/traversal with
InvalidOperationException. There is no lock held across resolver calls.
Validation preserves the instance and its ContentRevision: no insertion of
schema-default attributes/text, no xsi changes, no retained typing annotations.
The run owns identity tables, derivative/cursor state, variable contexts and
its diagnostics. Mutation during a run fails with InvalidOperationException.
No per-run state resides in a schema, datatype library or regex program.

Compile malformed XML or schema restrictions as an owned SchemaCompileException,
with nonempty, nonnull diagnostics and at least one Error/Fatal. Constructor
snapshots input; diagnostics use shared source URI/scalar location conventions.
Document input with incomplete provenance is a compilation failure. XML syntax
errors produced by the owned input parser are schema diagnostics; resolver/caller
exceptions (even caller-thrown XmlParseException) must propagate unchanged.
Use a structured parser result to distinguish provenance instead of catching
arbitrary parse exceptions. Compile parsing is strict, DTD processing prohibited;
Resolver enables schema composition only, never entity access or recovery.

A parsed invalid instance returns ValidationResult(IsComplete=true, IsValid=false).
An incomplete input or exhausted operation returns false/false and a Fatal entry.
String/stream instance syntax rejection similarly returns incomplete results,
while argument, I/O, callback and cancellation exceptions propagate. Those
conveniences parse with strict M2 defaults, DTD prohibited and no resolver; a
caller needing retained DTD ENTITY/ENTITIES context supplies a parsed document.
Cancelled operations throw OperationCanceledException at entry, before external
calls, during traversal/scanning and before returning success. No catch-all
translation. Input streams are never disposed by the facade.

Schema diagnostics use stable string prefixes and bounded messages. Required
classes are SCHEMA.Syntax, SCHEMA.IncompleteInput, SCHEMA.ResourceDenied,
SCHEMA.ResourceCycle, SCHEMA.Limit, RNG.SchemaRestriction, RNG.Datatype,
RNG.Instance, XSD.SchemaRestriction, XSD.Datatype, XSD.Content, XSD.Instance,
XSD.Identity, REGEX.Syntax, SCH.SchemaRestriction, SCH.QueryBinding,
SCH.Phase, SCH.XPath, SCH.Assert and SCH.Report. Fine-grained additive codes may
be catalogued by owners; they cannot replace these distinctions with one generic
invalid-instance message. M5 VALIDATION.Limit is preserved for its existing
operation adapter; SCHEMA.Limit is the compile terminal. Error means semantic
rejection; Fatal means an incomplete computation/resource failure. Format no
messages on success. Exactly-at-cap diagnostic output succeeds; one-over replaces
the last slot with the single fatal limit diagnostic, never exceeds the cap.

## Options and budgets

SchemaCompileOptions defaults: Resolver/BaseUri null; Limits=new XmlResourceLimits;
MaxOperations=10,000,000 (Int64?); MaxComponents=100,000 (Int32?); token default.
Null quota explicitly means unlimited, with checked finite representation limits.
All shared quotas retain M2/M5 units; neither UTF-16 length nor decoded byte count
may masquerade as scalar character counts. Effective compile work cap is the
minimum of nonnull MaxOperations and Limits.EvaluationWork, same rule as M5.

A compile component charge counts each retained graph node/name-class node,
schema declaration, datatype/facet definition, compiled regex AST/state and
Schematron pattern/rule/check/text-expression node, before allocation. Charge
work for scanned scalar, graph edge/transition, declaration lookup comparison,
regex state transition, lexical conversion step, value comparison/hash and
identity tuple comparison, before work; zero performs cancellation polling.
Facades share one compile operation across all finalization passes, one validation
operation across content/datatype/identity evaluations. New per-expression M4
limits do not reset the enclosing budget. ISchemaOperation.RemainingWork is
null for an unlimited enclosing budget, otherwise the nonnegative effective cap
minus already consumed work; it never returns a negative value. Use the exact
ordered internal adapter below, cap to min(remaining/per-expression), and charge
actual consumed work in finally. Do not precharge the entire allowance or silently
reset work between tests. Count actual work, never allocate a fresh quota for
every test. Poll loops in bounded intervals even with null caps.

Shared ISchemaOperation is implemented by M8-model, adapting M5
ValidationOperation for run accounting and diagnostics without changing that
class. The adapter owns compile component/resource accounting and exposes
Charge/ChargeComponents false on refusal, IsComplete=false and a terminal
Fatal diagnostic. Engines must distinguish false matching from incomplete state.
No engine can report success after a refused charge. Compile callers throw the
collected diagnostics; validation callers return the M5 result. Program/cache
construction must also charge before persistence, not only after matching.

SchematronCompileOptions.Schema owns resolver/token/resource/work policy;
XPathLimits copies M4 defaults (65,536 expression characters, 16,384 tokens,
16,384 AST nodes, depth 256). ValidationOptions defaults are unchanged M5.
SchematronValidationOptions.Validation owns the token and shared budget;
XPathLimits copies M4 defaults (1M operations, 100K input items, 16M input
characters, 100K node-set items, 1M string characters). Both caps apply. Phase
is null by default, selecting schema defaultPhase or #ALL; an unknown runtime
phase is ArgumentException before instance traversal. Compile rejects duplicate
or undefined phase/pattern references as SCH.Phase. #ALL activates concrete
patterns in schema order; #DEFAULT resolves schema default, falling back #ALL.
Phases map source IDs to SchematronCompiledPhase(Id,Lets,ActivePatternKeys).
Only the selected phase's lets are evaluated, after schema lets and before
pattern lets; #ALL has no named-phase let scope. Phase selection cannot change
one published schema's bindings or leak into another run. A let name duplicate
within a scope is a compile error; permitted inner-scope shadowing is isolated.
Two phases can assign different values to the same variable for one shared
pattern. Phase active references use declared authored pattern IDs at compile
time, then immutable keys; unnamed patterns participate in #ALL without fake
source IDs. The project's supporting grammar pin in sources.json has phase
p*,let*,active*, optional concrete pattern id and rule/check subject. The ISO2016
publication remains the normative selected edition, and the pin is not a claim
to implement a later edition.

## Resource boundary and required ordered repair

M8-model owns ISchemaResourceLoader/SchemaResourceReference and snapshots final
resources. Resolve lexical href against inherited xml:base/BaseUri; document
input source URI is inherited from retained source metadata, with options BaseUri
as fallback, never an implicit current directory. An absolute href needs no base;
a relative href with no base is SCHEMA.ResourceDenied. Requests carry absolute
Uri, original lexical SystemId, PublicId=null and final referring resource URI.

The public kind must be **XmlResourceKind.Schema=5** for every RNG externalRef/
include, XSD import/include/redefine and Schematron include. Internal purpose
distinguishes language semantics without misleading caller authorization. Null
resolver or null result refuses; no fallback networking. Resolver success
transfers ownership, and disposal occurs exactly once even on parse refusal,
quota, cancellation or downstream exception. Returned final absolute SourceUri
sets nested base/location and cache/cycle identity. A compiler cannot trust
request URI as final identity after redirects. External bytes/resources and
InclusionDepth are aggregate across the entire composition, including cache
misses and redirects, and checked before growth/read. Reject illegal cycles;
XSD legal mutually importing component sets use a bounded in-progress graph,
not an unconditional cycle error. RNG recursive refs are pattern edges, not
resource recursion. Cache identity includes effective target namespace for XSD
chameleon includes; no persistent/global cache or cross-operation allowances.
Schema parsing does not enable DTDs/entities. XSD instance schemaLocation hints
are never fetched: the explicitly compiled set alone defines available types.

Required coordinator ownership request: add Schema=5 to
src/Xemele/Resolution/Core/Resources.cs, preserving all existing values/members.
Suggested new owning repair paths: docs/api/M8-resource-kind.txt for the
additive enum field, tests/Xemele.Tests/M8-resource-kind/ for resolver
authorization and existing 0–4 kind behavior, and that repair task's JSON/report.
Do not rewrite old baseline fragments to conceal an additive member. The present fixtures use only the named internal
constant `(XmlResourceKind)5`; actual M2 probes demonstrate values 0–4, lexical
request fields and disposal. They do not claim a named Schema field already ships.
No dependent model implementation may start before that ordered ownership is
established and the additive contract can be implemented under it.

## Ordered XPath accounting join — M8-xpath-accounting

M4's current XPathExpression privately constructs XPathBudget, so public
Evaluate cannot supply spent work. A model-only shim over that public method
is insufficient. The ordered repair owns src/Xemele/XPath/XPathExpression.cs
and new src/Xemele/XPath/Evaluation/Accounting.cs, with additive internal
baseline/tests under docs/api/M8-xpath-accounting.txt and
tests/Xemele.Tests/M8-xpath-accounting/. Existing public members and behavior
remain unchanged. Required internal instance overloads on XPathExpression are:

```csharp
internal XPathValue EvaluateWithAccounting(XmlNode context, XPathContext? options, out long operations);
internal XPathValue EvaluateWithAccounting(XPathItem context, XPathContext? options, out long operations);
```

Thin internal static Xemele.XPath.Internal.XPathEvaluationAccounting forwards
to those overloads with the existing expression as its first argument; exact
signatures are XPathAccounting.cs.fixture. This static declaration allows the
current fixture to consume the real sealed/nonpartial M4 expression type, without
shadowing it or claiming the adapter already exists in the runtime.

Initialize operations=0 before guards. Instantiate exactly the same budget/path
as existing Evaluate, including context validation, variable/function snapshot,
input/navigation, binding and expression evaluation. In finally copy the actual
XPathBudget.Operations, even on evaluation limit, cancellation, callback failure
or argument refusal after budget creation; before creation it stays zero.
Rejected attempted work is not added to the successful-charge count. Do not catch
or translate engine/callback exceptions in the adapter. Delegate existing public
Evaluate to an equivalent core, preserving public results/exception provenance.

The compiled AccountingJoin.cs.fixture caps both XmlNode/XPathItem overloads to
min(enclosing RemainingWork, per-expression MaxOperations), preserving other
M4 limits and the shared cancellation token; zero remaining refuses before
creating invalid zero-cap M4 options. It charges out actual count in finally,
then the enclosing caller checks IsComplete before publishing any success.
A caller translating an XPath failure must do so after accounting; no accounting
exception may conceal an existing callback failure. If cancellation prevents
the M5 charge, preserve the primary exception; an aborted operation must not
publish success. The report contains no successful result in this case.

Actual success/failure count, repeated-expression shared cap, exact-limit/one-over
and preservation tests belong to M8-xpath-accounting. Present fixtures establish
only that the exact proposed join and current M4 types compile. That ordered
repair and M8-resource-kind must verify before M8-model starts; neither shared
source is edited by this contract worker.

## Datatype, regex and facet joins

M8-model owns all declarations in Model.cs.fixture, including SchemaName,
namespace snapshots, datatype definition/facet/value base and the operation,
resource, datatype/registry/facet/regex interfaces. M8-datatypes implements
IXmlDatatypeLibrary and immutable XmlDatatypeValue subclasses; the name denotes
atomic/list/union values, not CLR boxing or XPath numbers. M8-model publishes
no fake successful parsing implementations. The value base exposes Variety, owned Items only for lists and SelectedMember only
for unions. TypeName is the declared value type; a union preserves the actual
member type on SelectedMember. Facet code uses library ValueLength (scalar
characters/binary octets/list items), TotalDigits and FractionDigits; null means
the measure is inapplicable, not zero. Retype preserves primitive representation/selected member while assigning a
derived declared name. CreateList/CreateUnion snapshot inputs and
construct immutable typed composites without exposing concrete value classes to
facet workers. Decimal digit measures ignore insignificant lexical zeros.
Library parsing retains namespace
context where QName/NOTATION need it and returns an owned value/error, never a
view of a caller span. Failure has null value and nonnull error; success the
reverse. Normalize returns an owned string; borrowed spans are consumed synchronously and never retained.

XSD decimal/integer precision and temporal years are arbitrary, charged against
resource/work limits before growth; do not route them through double/decimal CLR
limits. Float/double include XML Schema INF/-INF/NaN and signed zero semantics.
Timezone absence induces partial order where prescribed; Compare returns
Indeterminate rather than inventing an ordering. Duration, QName, binaries,
URI and list/union values follow their actual value spaces. Equality/hash are
consistent across comparable derived types and numeric primitives where XSD
permits equality, including identity tuple keys; prefixes do not affect QName
identity. NaN equality follows XSD, not CLR operators. XSD language whiteSpace
uses XML #x9/#xA/#xD/#x20 only. NOTATION checks use the schema notation declarations; ENTITY/ENTITIES use
retained DTD unparsed entities. Both belong to XSD validation, while datatype
lexical/value behavior remains shared.

M8-regex implements IXmlSchemaRegexCompiler/Matcher; program subclasses are
immutable and opaque. Its grammar is XSD1.0 Appendix F, with Unicode scalar
categories/blocks, \i/\c, character-class subtraction, bounded/unbounded repeat
and XSD's whole-language matching semantics (implicitly anchored). Literal ^/$
are not .NET anchors. Pattern facets apply the XSD prescribed pattern rule,
including alternative sibling patterns versus conjunctive inherited restrictions;
never substitute a .NET Regex or silently reinterpret escapes. Track AST/state
construction separately from transitions with the shared budget.

M8-facets implements IXmlFacetEvaluator after regex and values. ValidateDefinition
checks applicability, inherited/fixed constraints, restriction consistency and
facet lexical forms; TryValidate enforces inherited/final facets and atomic/list/
union selection. Keep enumeration namespace contexts. List whitespace is fixed
collapse; union selection tries declared members in order and retains actual
selected type. Multiple enumeration values/pattern alternatives combine within
the restriction, while restrictions at successive derivations conjoin. Bounded
regex/parse failure yields incomplete state, not a clean datatype mismatch.
Registry views are sealed snapshots over built-ins and schema-defined simple
types; unknown names are not auto-registered. Extension libraries are excluded.

## RELAX NG compiler/evaluator contract

M8-rng-compile owns RngNameClass variants, RngPattern/RngCompiledSchema and
IRelaxNgCompiler in its Compilation subtree. M8-rng-validate implements
IRelaxNgEvaluator and owns the public facade under its assigned paths.
The graph is indexed by dense immutable IDs, Start valid, children/reference IDs
in range. Ref is an edge, not an inlined recursive tree. Choice/group/interleave
have two children after normalization; oneOrMore/list/element/attribute have one;
data has zero or one except child; leaf empty/notAllowed/text/value have none.
Datatype/value/context fields exist only for their kinds; name class only for
element/attribute; params only for data; refs one target. Constructors enforce
these shape invariants at publication. Name-class except placement, overlap,
attribute repetition/content restrictions and reference legality are compile
errors, not runtime instance rejection. Context is the original value/parameter
namespace/base snapshot; never substitute the instance's namespace map.

Compiler handles full syntax, grammar/parentRef scope, combined definitions,
external resources, name classes, simplification and datatype-independent
restrictions. It may retain datatype parameters/value lexical strings without
parsing: M8-rng-validate's public Compile joins the verified facets/regex library
and completes datatype-dependent restrictions before returning RelaxNgSchema.
It is forbidden to ship the public Compile facade earlier or permit validation
to discover an invalid schema for the first time. The compiler's definition-only registry may have no executable libraries yet;
the facade seals a replacement registry with verified real libraries when
finalizing its new immutable graph. No placeholder library throws or returns
fake success. The compiler's internal graph is not a public compiled schema and carries no false conformance claim.
Evaluator handles unordered attributes, ordered/coalesced text, interleave,
recursive graph matching and schema constraints; whitespace handling follows
RNG element/list/data semantics, not indiscriminate string.Trim. A mismatch
emits RNG.Instance at the instance source, restrictions emit schema locations.

## XSD compiler, content, identity and facade contract

M8-xsd-compile owns declarations/terms/particles/component tables/IXsdCompiler
in Xsd.cs.fixture. Expand model/attribute groups during component compilation,
retaining particle locations; attribute-use default/fixed overrides also retain
their own SchemaNamespaceContext and XmlLocation in XsdAttributeBinding. Resolve
QName values from the actual constraint's namespace map: a referenced declaration's
map is used only for the declaration constraint; a local override uses the local
binding map, which can assign the same prefix a different URI. Do not replace
either map with the instance's namespace context. Carry local locations into
compile diagnostics and effective typed values; preserve local element IDs independently of global
name dictionaries. Names map expanded namespace/local names, not lexical QName.
Simple types remain explicit XmlDatatypeDefinitions; built-in anyType and
anySimpleType are not unspecified missing types. Anonymous types receive stable
internal names and Anonymous=true; xsi:type may resolve only declarations with
Anonymous=false. Element Block uses distinct XsdElementBlock values Substitution/Restriction/
Extension; complex-type Block uses XsdTypeBlock Restriction/Extension and means
prohibited substitutions. Final controls derivation separately: Substitution
is never a Final derivation. Expand #all into the relevant block/final set.
Simple datatype definitions also retain Anonymous and Final
derivation sets, so restrictions/list/union compilation cannot lose final controls. Recursive type refs are bounded graph
edges. Chameleon include and redefine resolve component identity correctly.
Global namespace/type lookup is derived from sealed tables, not caller maps.

XsdParticle occurrence counts use BigInteger; null MaxOccurs means unbounded,
not unsupported large finite integers. Enforce nonnegative/min<=max and grammar
limits (including all-group constraints) before content construction. Block/final
lists interpret #all after expansion; restriction/extension validity and
substitution-group closure belong to compilation/content checks. The compiler registry likewise carries definitions only until facade
finalization injects the real datatype/facet implementations. The compiler
reports schema composition/redefinition/structural errors without depending on
facets, identity evaluator or an unmerged content branch.

M8-xsd-content owns IXsdContentCompiler/Matcher/Cursor and opaque program
subclasses. Compile validates content restrictions/UPA/element-declaration
consistency and particle derivation rules; each CreateCursor returns isolated
state. Accept outputs the matched declaration ID or wildcard policy; null ID
is only wildcard/open anyType content. Strict resolves and validates a declared
child or errors; lax validates when known, accepts otherwise; skip does not
type-validate that subtree. Complete checks occurrence acceptance. No expansions
proportional to maxOccurs or shared cursor state. Content/identity interfaces
are supplied as dependencies, not hidden static singleton engines.

M8-xsd-validate implements IXsdEvaluator. It dispatches to supplied content and
facet engines, validates attributes/simple/mixed content, xsi:type/nil, root/local
declarations, effective defaults/fixed, block/final and substitution, schema NOTATION/
DTD ENTITY and ID/IDREF document-wide consistency. XsdCompiledSchema.Notations
is a sealed expanded-name table of XsdNotationDeclaration (public/system ID,
namespace/base context and location); NOTATION values must resolve declared
schema notations and satisfy their required enumeration restrictions. NOTATION
is not a DTD notation-table check. ENTITY/ENTITIES, separately, require retained
DTD unparsed-entity context. Content programs are compiled once by the facade and supplied as a sealed
type-name dictionary; the evaluator never recompiles content on each run. A
nonnull factory requires a nonnull identity program, and conversely. Null
identityFactory/program is solely
for independent pre-facade core tests; it cannot advertise full XSD1.0 validation.
Full facade always supplies a real factory and prepared identity program, even schemas with zero constraints.
Effective default values are typed/run-local and delivered to identity hooks.
XsdValidatedValue stores validity and actual selected type/value; invalid values
cannot be treated as absent key fields to hide an instance error.

M8-xsd-identity owns IXsdIdentityCompiler/Factory/Session and opaque immutable
XsdIdentityProgram implementation. Its Compile prepares every restricted XPath
and validates identity references at schema compilation; factory Create consumes
that immutable program and returns only new per-run state. Each run/scope
starts fresh; EnterElement opens constraints associated with a declaration;
Attribute receives each effective typed attribute exactly once; LeaveElement
receives final typed element content before scope closes; Complete checks final
references. Hooks see all element scopes in document order, including untyped
wildcard nodes where selectors can traverse them. Restricted XPath is compiled
from selector/field syntax using the definition namespace map; this is XSD1.0
restricted XPath, not an arbitrary M4 expression. Selector/field cardinality,
key missing/nil restrictions, unique absent-field rules, keyref target/cardinality
and cross-type equality use XSD value semantics and lexical/source provenance.
Identity tables charge tuple construction and comparison before growth.

M8-xsd-facade alone owns the public XmlSchema.cs file. Public Compile composes
compiler, content compiler, facet-definition checks and identity restricted-XPath
preparation, rejecting any invalid schema before publication. Public Validate
always composes both content and identity engines. The facade begins only after
those tasks are verified. Shared internal declarations can be added by the
compiler owner before dependent implementations; interfaces in its output remain
exact as frozen. No later task edits an already-owned evaluation engine.

## Schematron evaluator and SVRL

M8-sch-compile owns compiled patterns/rules/check/text parts and compiler
interface. M8-sch-validate owns evaluator and public SchematronSchema.
Compiler expands includes first, abstract patterns/parameters next, rule extends
next, validates IDs/references/cycles, then compiles XPath. Abstract parameter
substitution must respect token boundaries and lexical strings, never an unsafe
substring replacement. Rule context is the XPath1 binding's match-pattern
language: translate its union/location predicates into a root-context selector
with equivalent matching, not blindly Evaluate(contextString, documentRoot).
Reject constructs outside that binding, not valid constructs the worker dislikes.
No implicit default namespace for XPath names; sch:ns supplies prefix bindings.
Concrete patterns retain optional authored Id=null separately from their stable
nonnegative Key assigned in declaration order; never synthesize an authored ID.
Compiled rules retain stable schema-wide Key, and each fired event gets a unique
per-result Key, even when two rules have identical context/location strings.
A document node and attributes are valid contexts. First matching rule per node
per active pattern wins, rules retain declaration order, patterns run in schema
order and matched nodes in document order. position()/last() focus at an assert
is the matched node singleton, per XPath1 rule binding, not the selection array.
Lets evaluate in schema/selected-phase/pattern/rule scopes with dependency order, no unresolved
or circular variables; inner bindings shadow outer scope under the binding rules.
Dynamic diagnostics/properties/name/value-of evaluate in the assertion focus.
All typed expressions/variables are M4 XPathValue/XPathExpression, not CLR dynamic
or generated stylesheet output. Render mixed message markup as textual content;
formatting span/emph/dir annotations never suppress their contained text.

A failed assertion is SCH.Assert Error and invalidates ValidationResult. A
successful report is SCH.Report Warning and remains a structured finding;
role/flag strings are metadata, not hidden severity policies. Subject is retained
as source text plus compiled XPathExpression on rules/checks. A check subject
overrides the rule subject; otherwise use the rule subject, then context node.
Evaluate subjects from the original assertion singleton focus with the same lets
and accounting; they never change test/message focus. A subject must evaluate to
a node-set; choose the first node in document order for the reported location.
An empty subject set produces an empty reported Location and unknown
SourceLocation, preserving the nonempty context location; a non-node-set or
failed evaluation is SCH.XPath and incomplete, not an assertion failure. False reports
and true assertions produce no finding. Findings snapshot ID/role/flag,
PatternKey/FiredRuleKey plus nullable authored PatternId, context/test,
deterministic namespace-independent reported subject location/source location,
separate ContextLocation/ContextSourceLocation for the assertion focus, expanded text and diagnostic/property references in declared
order. Unknown source locations use shared zero/null semantics; never store
XmlNode handles in results. Public constructors snapshot collections, reject
null records/required strings, undefined kinds and inconsistent validation
status. Result construction also rejects duplicate/negative active-pattern or
fired-event keys, findings referencing absent events, event/pattern mismatches,
and authored IDs inconsistent with the active-pattern record. Optional authored
IDs may be null. Context and reported subject locations remain independent. Findings have owned strings and remain usable after documents change.

SchematronResult owns the M5 ValidationResult, selected concrete phase, title,
namespace map, active-pattern/fired-rule traces and findings. Fatal XPath/resource/
quota errors terminate the run with SCH.XPath or VALIDATION.Limit and incomplete
result; never treat them as a failed assertion. ActivePatterns/FiredRules/Findings
preserve evaluation order, with explicit PatternKey and FiredRuleKey identifying which fired event produced
a finding; no string-based join is allowed. Records and traces charge the same operation and finding
count obeys Limits.Diagnostics, including warning reports and terminal refusal.

M8-sch-report owns SchematronReport in Reporting. SerializeSvrl returns an owned
XML string; SaveSvrl writes UTF-8 without BOM/declaration, leaves output open and
propagates write/cancellation exceptions. Namespace is
`http://purl.oclc.org/dsdl/svrl`; output root schematron-output has phase/title,
ns-prefix-in-attribute-values entries sorted by prefix, then for each active pattern in collection order, active-pattern with its
optional authored id/name, its fired events in collection order, and each event's
findings in collection order. Join solely by explicit keys; patterns/events with
zero findings still appear. A subject differing from the rule context cannot
alter the join. Null authored IDs are omitted, not synthesized.
Findings include test/location and nonnull id/role/flag, text, diagnostic-reference
(@diagnostic) and property-reference (@property). Empty optional attributes are
preserved if present; absent values omitted. Escaping uses XML serialization,
never string interpolation of raw messages. Location paths use local-name() and
namespace-uri() predicates plus one-based same-expanded-name sibling positions;
attributes use expanded-name predicates, the document root is '/', and literals
with both quote kinds use XPath concat. This is a deterministic interoperable
SVRL profile with flattened presentation markup; richer styled report markup,
JSON reports, XSLT output and host-specific paths are not advertised. Findings
remain serializable without access to a live document or compiled schema.

## Conformance and integration obligations

Corpus tasks pin suite upstream revision/download digest and each resource digest,
classify every case by edition/binding, and retain expected outcomes and reasons.
RNG restrictions and schema invalidity are separate from instance invalidity;
libxml2 differentials pin version/build and report disagreements rather than
rewriting expectations. XSD W3C applicability uses original metadata, preserves
all applicable missing/failing cases and rejects a full claim until they pass.
Schematron suites exercise all declared XPath1/ISO2016 constructs and SVRL
serialization with canonical XML comparisons; alternate bindings are explicit
negative compile tests. Missing suite provenance is pending implementation work,
not an excluded case or a contract-time pass. Corpus/CLI/gate owners run actual
engines only after their lane joins are verified, preserve earlier checks and
keep unsupported features fail-closed. No release/cumulative conformance or
allocation/throughput result is inferred from these declaration tests.

## Additive explicit resource outcomes

The ordered `M8-schema-resource-outcomes` repair freezes
[resource outcomes](resource-outcomes.md). It adds readonly explicit
Denied/Unavailable/Resolved outcomes and virtual synchronous ResolveResult;
legacy null remains Denied and existing Resolve/ResolveAsync stay unchanged.
Only the concrete optional schema loader seam may return absence for explicit
Unavailable. Mandatory loads remain strict; attempts share the work ledger,
absence creates no document/cache entry/resource allowance, and ownership,
parsing, redirected bases, quotas and cancellation remain common. Frozen lane
interfaces and non-schema consumers are unchanged. XSD directive semantics are
owned by the later ordered compiler repair.
