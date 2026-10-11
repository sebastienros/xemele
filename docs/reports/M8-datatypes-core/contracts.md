# Shared datatype family contract (source freeze)

These internal joins extend verified M8-model without changing any of its declarations
or adding a public library. Numeric, temporal and text/binary workers implement the
interfaces below in their own directories; M8-datatypes composes the complete library.
There is no parsing implementation or partial advertised library in this task.

## Metadata and dispatch

`DatatypeTypes.All` is an immutable ordinal table of all 45 XSD 1.0 simple-type
names, including anySimpleType (absent variety/primitive), 19 primitives, 22 atomic
derivatives and three built-in lists. Each `DatatypeMetadata` records exact immediate
base, primitive, family, variety, effective whitespace, fixed flag, and list item.
`TryGet` recognizes only the XSD namespace, with case-sensitive names. The composer
maps the RNG XSD-datatypes namespace explicitly to this canonical namespace, and
maps the empty RNG library to string/token; no arbitrary namespace alias or unknown
type fallback. It charges one work unit before each metadata lookup/dispatch.

Numeric owns decimal and integer derivatives, float/double. Temporal owns
duration and eight calendar primitives. TextBinary owns boolean, string and derivatives,
anyURI, QName, NOTATION, hex/base64Binary, and textual anySimpleType construction.
Built-in list parsing belongs to the composer: split XML whitespace with borrowed
spans after collapse, parse atomic item types through the family, enforce the
built-in nonempty lists, then call CreateList. No list is passed to the atomic
provider. Schema-defined list/union selection and facets belong to M8-facets.

`IDatatypeFamilyProvider.TryParse(metadata, normalized, context, operation, out value,
out error)` accepts a known atomic metadata entry assigned to that provider (or
anySimpleType for TextBinary), already normalized and scalar-validated by core.
Each published provider is immutable and shared; the composer has exactly one
provider instance per family, and values retain that provider. Parsed values derive
from `DatatypeAtomicValue(type, primitive, provider)` and own immutable payloads.
No span, scratch buffer, live namespace map, operation or mutable caller collection
may survive parsing. QName/NOTATION retain expanded names; any required context
is snapshotted. Error strings are bounded static messages, never full attacker input.

Success means true/non-null value/null error, after cancellation/completion polling.
Invalid lexical means false/null value/DatatypeErrors.InvalidLexical, while unknown
type dispatch uses UnknownType. Invalid scalars use InvalidUnicode. Refused work
means false/null value/Stopped and `operation.IsComplete == false`. Cancellation,
argument errors and caller exceptions propagate. Providers charge every lexical
conversion step before doing it, and a component before each retained value,
limb/block or other growth allocation. Already charged normalization work does not
pay for numeric conversion or namespace lookup; charge those separately. Providers
must not report a successful result after operation refusal.

## Normalization

`IsWhitespace` recognizes only U+0009/U+000A/U+000D/U+0020. Preserve copies exact
UTF-16, Replace replaces each XML whitespace unit with a space, Collapse also trims
and coalesces runs. Other whitespace remains literal. All valid Unicode scalars
are supported; unpaired surrogates fail. XML character legality belongs to the
input parser and value-family lexical rules.

`TryMeasure` is a borrowed allocation-free validation/length pass with exactly one
work unit per input scalar (including removed whitespace), and entry/exit zero
polls. It returns output UTF-16 units separately from input scalar count. No new
resource quota is invented: work shares the enclosing operation, finite span
representation bounds output, and original input resources were charged by M8-model.
`TryNormalize` measures, then writes with another unit per input scalar, zero polls,
zero component charges and no owned result. Caller supplies a nonoverlapping
sufficient destination. Short/overlapping destinations throw ArgumentException;
on structured failure written=0/error nonnull (destination may contain partial
output after a second-pass refusal). No span survives this synchronous call.

`Normalize` performs the same two passes, reserving one retained component before
scratch/result allocation, and returns an owned immutable string. Scratch <=256
UTF-16 units is stack storage; larger scratch uses ArrayPool with finally-return.
Empty string is canonical and may allocate nothing. A malformed scalar raises
FormatException; an incomplete operation raises SchemaCompileException carrying
its terminal diagnostics. TryParse composers use the structured borrowed route
(or catch only this core FormatException at that explicit owned boundary), never
catch arbitrary caller exceptions. Exact cost for successful N input scalars is
2N work/1 component owned, 2N work/0 components borrowed. Allocation evidence
separates owned strings, cold pool/setup and warmed borrowed operations.

## Composites, retyping and accounting

Only core-produced composites/retypes and DatatypeAtomicValue descendants are
accepted. `CreateList` snapshots indexed inputs into a private array/read-only view,
retaining each immutable member's identity. It reserves 1+count components before
allocation, charges 1 dispatch and 1 per item visit plus unwrap traversal. Items
must be atomic or unions selecting atomic values; nested lists are rejected.
`CreateUnion` retains the exact immutable selected member (including its declared
type), charges 1 dispatch plus selected-member unwrap traversal, and 1 component.
Unions may select lists. Empty user lists are permitted; built-in minLength/facets
remain the composer's responsibility. Input collections must remain stable during
snapshot; changed count is detected, no stronger concurrent mutation guarantee.

`Retype` charges 1 work/1 component and creates an immutable declared-name wrapper;
it shares the already owned list snapshot or exact union member, and retains an
atomic payload without conversion. Successive atomic retypes do not create a chain.
A type name requires a nonnull namespace (empty allowed) and nonempty local name;
schema derivation legality/facets are the caller's responsibility. Private readonly
composites cannot form cycles. Nested union traversal is iterative and bounded by
shared work/cancellation. Unwrap polls zero at entry and before returning, even for a direct atomic/list.
Each union/retype unwrap edge costs 1 work. All helpers
poll zero before publishing success; refusal throws SchemaCompileException, so
facades must distinguish incomplete state from an ordinary mismatch.

## Equality, order, hashing and measures

`DatatypeValues` dispatches unwrapped atomic values to their retained provider,
only for the same primitive and same provider instance. Two provider sets are
not interchangeable; equal-primitive mixed sets are argument errors. Cross-family
or distinct primitive equality is false and ordering Indeterminate. This follows
[XSD 1.0 equal, section 4.2.1](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#equal):
primitive value spaces are disjoint. Integer and decimal derivatives share Decimal;
float/double/decimal are distinct primitives, without XPath numeric promotion.
NaN equals itself in XSD's value space; signed zeros hash equally. Temporal absent
zones/duration partial orders remain the temporal provider's responsibility.
String derived types share String. anyURI/string, QName/NOTATION and hex/base64
are distinct primitives. QName prefix spelling never affects equality or hash.

Each compare/equality/hash/measure invocation charges 1 dispatch, each unwrap edge
1, and provider work is additional actual scalar/limb/octet/temporal comparison or
hash work. Providers charge before each such unit; do not allocate for warm borrowed
primitive comparisons/hashes with preallocated state. Equal atomic values of one
primitive must hash equally irrespective of declared type or lexical spelling.
Core salts atomic hashes by primitive and excludes declared type/provider identity.
Union equality/hash/measure uses selected values; list equality compares equal-count
items in order, charging each recursive invocation (nested lists are impossible).
Lists have no order except Equal on equal sequences, otherwise Indeterminate.
List hashes fold ordered member hashes with seed17/multiplier31, no declared name.

Length is scalar count for strings/URI, octets for binary, items for list, otherwise
null; QName/NOTATION length is inapplicable. XSD1.0 length/minLength/maxLength
facets on these types are satisfied for any facet value; facet code must handle
that explicit rule, rather than interpreting null as a rejection. Decimal TotalDigits/FractionDigits use
value-space digits after insignificant-zero removal; other primitives and lists
return null. Union measures delegate. Nonnegative results and valid order enums
are enforced by core; null is never converted to zero. No diagnostic is constructed
on successful/mismatch paths. M8-datatypes and M8-facets must preserve these joins
and extend only through their owned source, with no sibling edits to core.
