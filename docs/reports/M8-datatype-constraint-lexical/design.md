# Internal constraint representation

`DatatypeConstraintRepresentation.TryCreate` accepts declared definition,
borrowed authored lexical, declaration context, registry, facet evaluator and
one shared operation. It validates through the supplied evaluator before any
formatting. Its private constructor owns only the final assessment string and
retains the exact declared typed value and immutable declaration context.
No paired caller hint or parsed-value lexical field exists.

The formatter measures then writes directly into the owned string. Both passes
charge visits/copies, bounded runtime formatting, and calendar arithmetic.
The adapter, output storage blocks and result are reserved before allocation;
arbitrary year arithmetic uses the existing charged temporal Integer buffers.
Fixed stack scratch cannot grow with input. Returned budget refusals have a
private sentinel; caller exceptions and cancellation propagate unchanged.
UTF-16 output byte capacity stays positive Int32 even with null work caps.
Published typed values are never modified. Result construction intentionally
allocates; no zero-allocation owned-result claim is made.

Pinned [XSD 1.0 Second Edition Datatypes](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/)
§§3.2.2.2–3.2.5.2, 3.2.7.2–3.2.9.2, 3.2.15–3.2.16 and integer-derived subsections
supply the mappings. Integer ancestry produces integer lexical forms; decimal
restrictions with fractionDigits=0 still include the decimal point. IEEE values
use their selected precision and invariant shortest round-trip digits rendered
scientifically; both signed zeroes produce `0.0E0`.

Date retains its recoverable offset (+12:00 through -11:59); dateTime/time use
UTC only when a timezone was supplied. Supported leap phases, arbitrary signed
nonzero years and fractional precision survive. Duration/g* have no prescribed
1.0 canonical subsection; validated normalized authored forms are retained.
QName/NOTATION keep authored prefix and captured context; normalized stored text
and relative anyURI forms are retained. Binary forms encode stored bytes.
Selected unions format their existing member without retrying selection; list
members use matching borrowed tokens and one separating space.

[XSD Structures §3.3.4, 5.1.1–5.1.2](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#cvc-elt)
requires actual-type assessment of the prepared lexical form. The consumer must
call existing `facets.TryValidate(actualType, representation.Lexical,
representation.Context, registry, op, ...)` and retain that call's selected
actual value. Fixed comparisons use `representation.DeclaredValue`. The helper
itself has no core compiler dependency and changes no frozen contracts.
