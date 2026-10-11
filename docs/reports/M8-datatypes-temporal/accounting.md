# Representation and accounting

The provider accepts known core metadata and already normalized/scalar-validated
borrowed input. It never retains a span, operation, context or mutable caller
state. Owned values contain up to four immutable keys: signed decimal whole
seconds plus a trimmed positive fraction. Calendar values carry zone presence;
absent-zone bounds are constructed once. Duration keys contain all four normative
endpoints. Owned base10^9 Integer arithmetic exists only in synchronous parse scratch; single-limb values are inline.

Lexical readers charge before every inspected UTF-16 unit, every two-digit
conversion, and every arbitrary digit multiply/add. Calendar/rank/bound checks
charge before evaluation. All size-dependent integer arithmetic uses owned loops:
each add/subtract/multiply/divide/remainder limb and each magnitude/leading-zero
inspection polls and charges before processing. Helpers charge one entry unit.
Array growth reserves one node plus its actual limb capacity before allocation;
inline single-limb arithmetic and sign-sharing allocate no arrays. Small constants
keep each limb step bounded, with no BigInteger, general multiplication or scale
exponentiation. Decimal output divides by ten through these loops and polls before
each output digit. The 128-limb proving fixture independently expects N+4 work and
N+2 components for multiplicationby2, N+1/0 for remainderby3, and verifies
cancellation inside multiplication and the first output division. Formatting123
costs13 work/4 components independently of parse accounting.

String/character persistence reserves one node plus one component per sixteen
UTF-16 units before allocating. Fraction and final string copying/complementing uses .NET10 ref-struct-capable
string.Create callbacks, polling and charging before every written unit. The final value reserves one component before construction. The
char format buffer is parse scratch with explicit ownership to be measured; it is not
retained. Representational overflow refuses the shared operation.

Comparison charges before sign/length handling and each actual examined whole or
fraction digit; virtual fractional zero padding is charged too. Hashing charges
each whole/fraction digit plus separator, leap phase and zone fold. Neither operation converts
numbers, allocates scratch or constructs diagnostics on success/mismatch. All
methods poll before publishing results. Refusal during parse is caught using a
private provider-only exception, so caller and cancellation exceptions propagate.
Core's additional dispatch/unwrap charges remain intact.

Tests measure consumed work/components through a counter-only ledger, replay the
exact capacities through real SchemaCompileOperation, and reject one unit less.
These counters account logical work, not allocation or elapsed time.
