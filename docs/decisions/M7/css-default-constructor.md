# Existing CSS selector constructor

M7-css-default-contract resolves a behavior omission in the existing freeze.
The original sealed CssSelector declaration implicitly published its public
parameterless constructor, already present in Selector.golden.txt. The runtime
must preserve that member; making the compiled-program constructor private does
not authorize removing the frozen default constructor.

`new CssSelector()` creates an immutable universal selector equivalent to
`CssSelector.Compile("*")` with default namespace options. Unqualified universal
matches every namespace; only elements match. XML/HTML graph overloads, default
and null argument handling, scope, document order, owner-retaining read-only
snapshots and live mutation observations are identical to the compiled universal
selector. No mutable uninitialized selector state or extra public member is added.

The declaration now spells out the same public constructor explicitly and a
representative signature consumer constructs it. Existing canonical baseline
remains byte-for-byte unchanged. The nonpackaged declaration bodies remain
compilation-only; actual universal behavior is proved by M7-css after this repair
is integrated and verified. No declaration stub is shipped to the product.
