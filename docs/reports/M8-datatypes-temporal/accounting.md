# Representation and accounting

The provider accepts known core metadata and already normalized/scalar-validated
borrowed input. It never retains a span, operation, context or mutable caller
state. Owned values contain up to four immutable keys: signed decimal whole
seconds plus a trimmed positive fraction. Calendar values carry zone presence;
absent-zone bounds are constructed once. Duration keys contain all four normative
endpoints. BigInteger exists only in synchronous parse scratch.

Lexical readers charge before every inspected UTF-16 unit, every two-digit
conversion, and every arbitrary digit multiply/add. Calendar/rank/bound checks
charge before evaluation. Arithmetic uses stable limb units: each operand-size
inspection costs one, each operation reserves and charges the maximum operand
limb count, and reserves a node plus that count and one possible result limb.
This is conservative for inline CLR integers and reserves scratch/result growth
even when the CLR shares storage. Division is always by a small positive constant;
there is no unmetered general multiplication or scale exponentiation. Formatting
repeatedly divides by ten through the same accounting path, then charges the
written digits. No opaque culture-dependent BigInteger parse/format is used.

String/character persistence reserves one node plus one component per sixteen
UTF-16 units before allocating. Fraction copying/complementing charges every
written unit. The final value reserves one component before construction. The
char format buffer is parse scratch with explicit measured ownership; it is not
retained. Representational overflow refuses the shared operation.

Comparison charges before sign/length handling and each actual examined whole or
fraction digit; virtual fractional zero padding is charged too. Hashing charges
each whole/fraction digit plus separator and zone fold. Neither operation converts
numbers, allocates scratch or constructs diagnostics on success/mismatch. All
methods poll before publishing results. Refusal during parse is caught using a
private provider-only exception, so caller and cancellation exceptions propagate.
Core's additional dispatch/unwrap charges remain intact.

Tests measure consumed work/components through a counter-only ledger, replay the
exact capacities through real SchemaCompileOperation, and reject one unit less.
These counters account logical work, not allocation or elapsed time.
