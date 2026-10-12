# Schema validation contracts — M8

Decision owner: `M8-contracts`. Exact declarations are the nonpackaged
[fixtures](../../tests/Contracts/Fixtures/M8/); semantics and lane joins are frozen
in [schema contracts](../decisions/M8/contracts.md), with the machine-readable
[feature inventory](../decisions/M8/inventory.json). The per-consumer `.golden.txt`
files freeze emitted public **and internal** member IDs, signatures, nullable,
constant and init-only metadata. They are declaration baselines, not shipped APIs.

Prerequisites are verified M2 XML/resolver, M4 XPath 1.0 and M5 validation.
No HTML, CSS, XSLT engine, CLR serialization or later milestone is required.
The harness compiles an XML-only prerequisite assembly from those existing source
paths, and never executes an M8 declaration method.

## Public surface

`Xemele.Validation.RelaxNgSchema`, `XmlSchema` and `SchematronSchema` have private
constructors and three static `Compile` overloads: `XmlDocument`, `string`, and
readable `Stream`. RELAX NG and XSD accept `SchemaCompileOptions? = null` and
return `ValidationResult` from three `Validate` overloads with the same input
forms and `ValidationOptions? = null`. Schematron accepts
`SchematronCompileOptions? = null` and returns `SchematronResult` with
`SchematronValidationOptions? = null`. No `Document` alias or runtime stub ships.
Malformed or semantically invalid schemas throw `SchemaCompileException`, whose
owned nonempty diagnostics distinguish language restrictions, syntax, resource
refusal and quotas. Invalid instances return complete invalid results; a stopped
validation returns an incomplete invalid result with a fatal diagnostic.

`SchemaCompileOptions` freezes Resolver=null, BaseUri=null, finite default
`XmlResourceLimits`, MaxOperations=10,000,000, MaxComponents=100,000 and operation
CancellationToken. Schematron wraps these options, fixes ISO/IEC 19757-3:2016 and
XPath 1.0, and exposes independent finite M4 compile/evaluation limits. Phase
selection and phase-level let scope are per validation; optional authored pattern
IDs remain nullable, with explicit stable pattern/fired-event keys for detached
SVRL ordering and subject locations separate from assertion focus. All resource/work limits aggregate across imports,
includes, nested schemas and regex/datatype evaluation; exactly-at-limit succeeds.

The ordered shared repair adds **`XmlResourceKind.Schema = 5`** without changing
values 0–4. Fixtures use a named internal constant `(XmlResourceKind)5` against
current M2, and explicitly do not claim the runtime has this named enum member.
Schema access must never be disguised as entity or XInclude access. The coordinator
must assign the shared resolver source before model implementation starts. A second ordered internal M4 accounting
repair exposes actual operations even on failure; capped per-expression evaluation
and final actual charging enforce one shared schema work budget. Exact adapter
shapes are frozen in XPathAccounting.cs.fixture, not claimed as current runtime APIs.

## Complete release profiles and independent lanes

RELAX NG uses the OASIS 3 December 2001 XML syntax, simplification and restrictions,
including arbitrary name classes, recursion, interleave, lists, externalRef,
include, grammar scope and built-in/XSD datatype libraries. Compact syntax is the
only syntax exclusion. Compiler output is an immutable pattern graph; the
validation facade completes datatype/regex-dependent schema checks before
publishing a schema. Instance matching implements the whole declared XML profile.

XSD is W3C XML Schema 1.0 Second Edition Structures and Datatypes. Composition,
redefine, substitution, derivation, all compositors, wildcards, xsi:type/nil,
facets, defaults/fixed, ID/IDREF and key/unique/keyref are required. Content and
identity engines implement separate immutable programs and per-run hooks.
`M8-xsd-facade` composes them after verification; earlier compiler tasks expose
internal output only. XSD 1.1, persistent augmentation/PSVI and CLR mapping are
explicitly outside this profile. Validation applies effective defaults internally
without mutating documents. Applicable 1.0 cases cannot be excluded to hide gaps.

Schematron uses ISO 2016, schema namespace
`http://purl.oclc.org/dsdl/schematron`, the `xslt` query binding's XPath 1.0
expressions/context patterns, and direct M4 evaluation without generated XSLT.
Absent queryBinding means `xslt`; all other bindings are compile errors. Include,
abstract patterns/parameters/rules/extends, phases, lets, assertions/reports,
diagnostics, 2016 properties and dynamic message content are required.
The structured output is frozen in `SchematronResult`; `SchematronReport` produces
the documented SVRL XML profile as an owned string or UTF-8 stream output.

Datatypes, regex and facets are separate model-first lanes. Datatype parsing has
namespace context, exact value equality/order/hash and whitespace normalization;
facet composition handles restrictions/lists/unions and uses schema regex rather
than .NET regular expressions. `contracts.md` assigns every interface/output and
clarifies deferred checks at the facade joins. Conformance owners pin real source
versions, hashes and applicability with honest missing-case counts. Contract
compilation establishes compatibility, not runtime conformance or performance.

## Additive explicit resource outcomes

The ordered `M8-schema-resource-outcomes` repair freezes
[resource outcomes](../decisions/M8/resource-outcomes.md). It adds readonly explicit
Denied/Unavailable/Resolved outcomes and virtual synchronous ResolveResult;
legacy null remains Denied and existing Resolve/ResolveAsync stay unchanged.
Only the concrete optional schema loader seam may return absence for explicit
Unavailable. Mandatory loads remain strict; attempts share the work ledger,
absence creates no document/cache entry/resource allowance, and ownership,
parsing, redirected bases, quotas and cancellation remain common. Frozen lane
interfaces and non-schema consumers are unchanged. XSD directive semantics are
owned by the later ordered compiler repair.
