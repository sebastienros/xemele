# Facet evaluator composition

The implementation owns only the facet subtree, tests, consumer, benchmark and
report paths. It composes verified datatype/registry and regex implementations
through the frozen IXmlFacetEvaluator interface, with no new public facade.

ValidateDefinition walks the complete restriction/item/member graph, rejects
cycles, unsupported shapes and final derivations, and compiles immutable per-level
facet plans. The depth cap is 256; refusal is a fatal incomplete operation, not a
schema mismatch. Every successful preparation is charged against its caller's
compile operation even when another plan was previously prepared. Weak-key tables
separate both registry identity and definition identity; complete plans are
published only after successful checks. Concurrent runs retain no mutable run
state in those plans. No definitions-only or placeholder library is executable.

TryValidate obtains an accepted plan or performs real preparation with the same
operation. It normalizes borrowed lexical spans in bounded stack storage (up to
256 UTF-16 units) or finally-returned pool storage, parses through verified value
providers, then checks inherited levels and produces an owned result. Union
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
values are arbitrary precision. Bounds preserve partial temporal/duration order;
incomparable values cannot satisfy an ordered facet. Enumeration uses typed
provider equality, including QName facet namespace snapshots, decimal equivalence
and XSD NaN equality. Fixed constraints and tightened bounds/lengths/digits are
checked through every inherited level. Whitespace cannot weaken or change a fixed
policy. A repeated exclusive endpoint follows the XSD exception.

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

Before the source commit: 270 owned facet tests plus 501 verified datatype,
model and regex regressions passed with zero failures/errors/skips/not-run on
.NET 10.0.11. All 316 frozen facet contract members and 1,393 preserved public
members matched. The regular and source-linked XML-only consumers passed,
planning validated 178 tasks, and the whitespace check passed. A subsequently
added fixed-flag alias guard and harness output isolation correction await their
focused rerun after the current park releases.

Earlier test builds failed on theory accessibility and a lambda discard; the
first focused run had six test-setup failures (provider-set identity, reserved
RNG registry name, pre-cancelled operation construction), corrected without
changing provider semantics. The first broad check stopped on a noncanonical
owned API fragment, corrected to the required empty xemele-api-v1 public fragment.
The first counters-disabled preparation build failed when project output flags
reached the runtime reference and selected duplicate generated attributes; the
owned probe project now removes those global flags at its project reference.
Controls/discovery have not run, and no allocation/timing measurement has run.

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
