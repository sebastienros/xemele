# Independent selector overlap

`bcl-overlap.json` contains 35 independently authored CSS/XPath pairs. The BCL
XPath oracle runs on 24 deterministic generated trees, repeated under invariant,
Turkish and French cultures, comparing both actual Xemele graphs before and after
mutation while retaining old query snapshots. Its domain is namespace-free,
lower-case names and XML/CSS common whitespace. It is not a full CSS engine.
The verified 89-test prerequisite matrix still covers every advertised grammar,
namespace/case/scope/default-constructor rule and unsupported family.

`native-input.json` supplies the exact original authored graph and 35 expected
results. `native-output.json` is unmodified actual output of
`xmloxide::css::select`, pin `555055f1ea6bf9944488fd96415b6f03b5b43696`
(package 0.5.0, MIT; `XMLOXIDE-LICENSE.txt` copied verbatim).
`native-dispositions.json` accounts for every result: 23 exact overlaps, four
scope-root differences, eight unsupported native grammar cases. All 35 full
Xemele results remain mandatory and are also checked against BCL XPath.

The four scope differences are `*`, `:first-child`, `:last-child`, `:only-child`:
xmloxide visits descendants only, whereas Xemele includes the supplied element.
Native rejects `:scope`, all five of-type variants exercised here, `:not` lists,
and the hexadecimal escaped identifier. These are differences in the pinned
reference, not product exclusions. Raw native errors are retained verbatim.

The independent probe, Cargo.lock, exact source/tree/compiler/features, reused shared
lock package identities and input/output/license hashes are recorded under
`tests/corpora/css/NativeSelector/` and `native-selector-provenance.json`.
No native dependency is added to the runtime or shared protocol runner.
