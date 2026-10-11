# Numeric family join

`NumericDatatypeProvider` is immutable and internal. The composer creates one
instance and supplies canonical metadata, already scalar-validated/collapsed
borrowed input, namespace context and the enclosing operation. No library facade
or cross-primitive promotion is introduced. Retained decimals own a private
char array of significant decimal digits; zero borrows only a static literal.
Leading zeros and trailing fractional zeros disappear; integer trailing zeros
remain, keeping scale nonnegative. Float/double retain their IEEE value, with
float rounded directly by Single.TryParse before storage as an exact double.

Decimal and all integer derivations share Decimal identity; Float and Double are
separate. NaN equals itself and compares Equal to itself, Indeterminate to other
values; zero signs compare/hash equally while floating payload signs survive.
Canonical digit arrays let comparisons/hashes visit characters without temporary
strings, BigInteger conversions or scaling allocations. Range checks compare
canonical integer spans with exact built-in bounds.

## Digit measures for deferred facets

Use the formal XSD 1.0 Second Edition
[totalDigits definition and validation rule](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#rf-totalDigits):
smallest positive t with v=i×10^-n, |i|<10^t and 0≤n≤t. With minimal
nonnegative scale s and coefficient c, FractionDigits=s and
TotalDigits=max(decimalDigits(abs(c)),s), counting zero as one digit.
The coordinator confirmed this interpretation after a bounded contract review.

| Value spellings | TotalDigits | FractionDigits |
| --- | ---: | ---: |
| .001 / 0.00100 | 3 | 3 |
| 1.00 / 0001.000 | 1 | 0 |
| 0 / -0.000 | 1 | 0 |
| 1000 / 1000.00 | 4 | 0 |

M8-facets must reject .001 under totalDigits=2 and accept it under 3;
1.00 and zero satisfy totalDigits=1. A facet definition of totalDigits=0
is invalid. Float/double digit measures and all numeric length measures are null.

## Accounting and persistence

Parse polls entry, charges metadata dispatch, each ASCII lexical inspection,
range/sign work, each range-character comparison, and each retained digit copy.
Decimal reserves one value plus one component per nine-digit storage block before
array allocation. Floating literal probes charge each length/character test;
ordinary floating conversion reserves a separate unit per input character before
invariant span parsing. The CLR IEEE converter owns bounded transient scratch,
not retained arbitrary decimal precision; success reserves one value and polls
completion before publication. Structured refusal returns null value/Stopped;
argument, cancellation and caller exceptions propagate.

Comparison charges provider dispatch and each compared digit; hashes charge each
digit. Core separately charges dispatch/unwrap and primitive salt. Measures read
cached representation metadata. All paths poll before returning success. No new
quota, global cache or mutable provider state exists. Measurements separate cold
metadata/provider setup, owned values/storage, warm borrowed primitives and
retained values; there is no pool in this implementation.
