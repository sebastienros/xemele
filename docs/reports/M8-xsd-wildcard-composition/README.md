# XSD wildcard composition repair

Private declaration compiler now rejects each non-expressible intermediate
namespace constraint before a later operation can discard its operands.
[W3C Structures Second Edition §3.10.6](https://www.w3.org/TR/xmlschema-1/#cos-aw-intersect)
requires each intermediate union/intersection to be expressible. A complement
is representable only as any, not(absent), or not(one namespace and absent).
Union keeps local processContents; intersection with an authored local wildcard
now also keeps its processContents, as required by §3.4.2. Group-only composition
keeps the first group's setting. Original element/source/byte locations are passed
at all three callsites. No retained model or frozen/public API changes.

Composition uses an owned list, references to already-owned namespace strings,
and the existing immutable XsdWildcard snapshot. Each namespace traversal and
character comparison is charged/polled; one component reserves the result before
list allocation and each retained namespace reserves another before growth. The
final snapshot copy is charged by its bounded size. Straightforward pairwise
comparison has explicit quadratic work bounded by the shared operation budget;
no optimizer, custom hash, token matcher or speedup claim. New owned setup/result
allocation measurement is pending. Existing compiler evidence is historical.

Initial Release build:0warnings/errors. Actual focused11tests passed, including
162 valid/invalid pair combinations against an independent finite witness oracle,
character/byte direct-union source positions, redirected invalid intermediates,
local processContents, exact and one-short limits, cancellation inside composition
and unchanged immutable operands. Broader254checks also passed:11repair+11resource-base+88RNG+72XSD+30model/location+42safe resolver, frozen119+257/public1393 and both genuine XML-only consumers;0warnings/errors,178tasks valid. Independent review and measurement pending.
