# Numeric correctness

Tested source: `9dee538394f076df86ea12ea282126fbe39da8f2`.
Command: `python3 docs/reports/M8-datatypes-numeric/checks.py`.
SDK 10.0.400; Release runtime pinned to 10.0.11; local package restore;
no allocation/timing tests selected. Exact positive counts asserted in XML.

- 76 numeric, 18 verified core and 12 verified model cases: 106 passed;
  zero errors, failures, skipped or not-run cases.
- All 1393 compiled public API members match the existing baseline.
- Genuine provider/core consumers passed against the regular assembly and an
  XML-only source assembly containing only verified prerequisites and Numeric.
- Task metadata/graph validator and whitespace diff check passed.

The numeric cases cover all decimal/integer derivations and exact range edges;
8192-digit integers and fractional scales; caller-buffer detachment; culture;
lexical rejection and bounded static errors; compatible derived equality/hash,
unions/retypes and primitive-domain separation; NaN/infinity/zero signs; exact
normal and subnormal midpoint ties, carry and overflow; 4097-place nonzero tails
on both sides of ties and both signs; 160 deterministic 1800-digit floating
oracle cases; BigInteger rational decimal-order oracle; 8192-place exponent/
mantissa displacement; independently specified parse/component and 500-digit
comparison/hash costs; exact and one-short quota refusal; cancellation during
long lexical, exponent and bounded serialization phases; caller exceptions.

## Bounded IEEE conversion proof

Only binary32/64 conversion uses a significant prefix; Decimal retains every
significant digit. A binary64 rounding midpoint is an integer multiple of
2^-1075 with coefficient bounded by IEEE's 54-bit rounding precision after
normalization. Its terminating decimal has at most the digits of 2^54×5^1075
(< 770); integer midpoints have at most 309 digits. Binary32 requires less.
The retained 1100-digit prefix exceeds either bound. Every omitted tail lies in
a single rounding interval unless that prefix equals a midpoint exactly; in
that case a nonzero sticky digit places the approximation on the same side as
the original. Tests cover exact ties, zero tails and nonzero tails much farther
than the retained prefix, including negative sign reversal.

The full lexical pass polls and separately charges every digit conversion.
The opaque runtime input is at most 1116 characters.
Scientific exponent combines integral digit displacement, first significant
position and the authored exponent. Authored exponents saturate at 10^10,
which exceeds every possible Int32 span displacement plus binary64 range; this
cannot turn overflow/underflow into a finite nonzero result. Zero is handled
with its original sign after validating the complete lexical input. Scratch
construction and bounded runtime conversion are separately charged. No opaque
runtime method receives an arbitrary-length input span.

Upstream .NET conversion code uses bounded stack NumberBuffer and internal
fixed BigInteger storage; that upstream observation is not a claim of directly
retrieved source for the installed servicing build. Actual pinned host/runtime,
complete payload dependencies and inputs are hashed in the measurement manifest.
Installed runtime .version reports commit
`e2f47b0110ed922f21a1522da67279133ce28f32` and `10.0.11`.

## Independent source review

The coordinator's independent read-only reviewer accepted corrected source at
`9dee538394f076df86ea12ea282126fbe39da8f2`; the original arbitrary-span
conversion polling issue is resolved by the bounded scratch and proving cases.
No remaining source blockers. The reviewer did not execute tests.

Accepted SHA256: NumericProvider
`88283553aa4f1c6c0a782e5d7117cc9f581527cad8a50d9858ed59f40e656071`;
NumericTests `aa06a4e1b9d7670289761f15c379ca0baf2aac1a0f3f9dc025b4c997e98a84b5`;
joins `925bfbe7a0eb16a63f94f95f857f4eb865048b69aa8667abc7eb5d0daae1ebe8`.
