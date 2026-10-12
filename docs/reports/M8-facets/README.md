# Facet evaluator composition

The implementation owns only the facet subtree, tests, consumer, benchmark and
report paths. It composes verified datatype/registry and regex implementations
through the frozen IXmlFacetEvaluator interface, with no new public facade.

ValidateDefinition walks the complete restriction/item/member graph, rejects
cycles, unsupported shapes and final derivations, and compiles immutable per-level
facet plans. Restrictions accept retained item/member fields only when they
match the inherited graph, including member order. The depth cap is 256; refusal is a fatal incomplete operation, not a
schema mismatch. Every successful preparation is charged against its caller's
compile operation even when another plan was previously prepared. Weak-key tables
separate both registry identity and definition identity; complete plans are
published only after successful checks. Concurrent runs retain no mutable run
state in those plans. No definitions-only or placeholder library is executable.

TryValidate obtains an accepted plan or performs real preparation with the same
operation. It normalizes borrowed lexical spans in bounded stack storage (up to
256 UTF-16 units) or finally-returned pool storage, parses through verified value
providers, then checks inherited pattern/enumeration levels and the effective scalar set,
and produces an owned result. Union
members are tried in order using each member's own whitespace rule; selection
retains the member value and its declared type. Lists collapse XML whitespace,
validate each token against the item plan, and publish an immutable item snapshot.
A user list may be empty; inherited built-in NMTOKENS/IDREFS/ENTITIES cannot.

Each XSD level combines sibling patterns as alternatives, and successive levels
conjoin. RNG parameter definitions use the RNG XSD-datatypes namespace on their
explicit known alias name and matching canonical XSD base; patterns conjoin, scalar duplicates
reject, and whiteSpace/enumeration are excluded. The explicit RNG adapter rejects empty-library parameters;
generic no-target-namespace XSD definitions have no inferred origin. Registry ingestion still rejects reserved alias
names; a facade passes its per-occurrence parameter definition directly to the
evaluator rather than registering it as a schema-defined type.

Length uses scalar characters, binary octets or list items. QName/NOTATION length
facets are schema-valid but impose no value check, preserving the providers'
inapplicable null measure. Decimal totalDigits/fractionDigits use the verified
canonical measures: .001=(3,3), 1.00=(1,0), 0=(1,0), 1000.00=(4,0). Facet integer
values use a native word through nineteen canonical digits and an owned
canonical decimal string thereafter. Scanning, native conversion, string copying
and digit comparisons charge each visited digit; one string component is reserved
before canonical growth. Long leading zero sequences do not allocate count storage. Bounds preserve partial temporal/duration order;
incomparable values cannot satisfy an ordered facet. Enumeration uses typed
provider equality, including QName facet namespace snapshots, decimal equivalence
and XSD NaN equality. Fixed constraints and tightened bounds/lengths/digits are
checked through every inherited level. Whitespace cannot weaken or change a fixed
policy. A repeated exclusive endpoint follows the XSD exception. Equal exclusive minimum
and maximum endpoints prepare an empty value space; mixed equal endpoints reject.
Cross-facet checks replace obsolete same-kind scalar facets while historical fixed
and derivation checks remain. Effective length/minLength/maxLength combinations
require the matching min/max values in an ancestor whose effective set has no length.

The run adapter translates only a returned work/component/diagnostic refusal
into an internal sentinel. Cancellation and caller exceptions propagate unchanged,
including a caller-thrown SchemaCompileException after marking a run incomplete.
Schema regex syntax errors remain their owned SchemaCompileException and never
publish a prepared plan. Ordinary lexical or facet failure has a complete
operation, null value and nonnull datatype error; stopped work has incomplete
state and the dedicated stopped error.

The borrowed CheckPreparedValue primitive is isolated from normalization,
parsing, plan construction and owned results. Measurement preparation will
exercise this primitive separately from cold/owned/invalid/pooled/retained routes;
no allocation result is inferred from source or correctness tests.

Normative anchors are the pinned [XSD 1.0 Second Edition datatype facets](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#rf-facets)
and [RNG XSD datatype guidance](https://relaxng.org/xsd-20010907.html).
The frozen M8 contracts and accepted RNG compiler finalization handoff determine
this repository's joins and parameter representation.

## Checks so far

Historical 773 checks passed but the bounded review found five missing rules. All
five newly written review witnesses then failed against the pre-correction code;
see review-1.md. The corrected targeted suite passed 86 tests (6 review witnesses,
25 independently calculated number tests, 48 definition tests, 7 primitive tests),
zero failures/errors/skips/not-run on .NET10.0.11. The full corrected suite then passed804 tests (303 owned plus501 verified
predecessors), zero failures/errors/skips/not-run, frozen316/public1393, both
normal and true XML-only consumers, planning178 tasks and whitespace validation.

The first witness build omitted the xUnit cancellation argument, corrected before
execution. The first correction build exposed a local-name collision; corrected.
The first numeric test run had three setup failures because a zero MaxOperations
option is invalid; one-short tests now consume one unit from a valid positive
budget before entry. These do not count as passing runs.

Earlier test builds failed on theory accessibility and a lambda discard; the
first focused run had six setup failures (provider-set identity, reserved RNG
registry name, pre-cancelled operation construction). The first API check rejected
a noncanonical fragment, corrected to the empty xemele-api-v1 fragment. The first
counters-disabled preparation build forwarded output flags into the referenced
runtime, corrected in the owned project. Its later output path omitted net10.0,
corrected before any control execution. The coordinator accepted the corrected
source review. Subsequent disabled controls/discovery and462-identity process
preflight passed; see preparation.md. Allocation/timing measurements have not
run and require a fresh all-host PARK/root grant.

## Required XSD publication join

The coordinator accepted the frozen-contract decision that generic facet
preparation does not infer an XSD origin to impose NOTATION's required enumeration.
Bare shared/RNG NOTATION parsing, vacuous length checks and authored typed
enumerations remain supported. The XSD facade/evaluator must, before publication,
reject direct NOTATION use and derived uses without an effective enumeration
(inherited enumeration qualifies), resolve authored enumeration QName values
against XsdCompiledSchema.Notations using each facet's captured namespace context,
and follow relevant base/item/member references. Its independent tests must prove
inherited-enumeration acceptance plus direct, non-enumerated and undeclared-name
rejection. No shared API expansion or source-origin heuristic is introduced here.
