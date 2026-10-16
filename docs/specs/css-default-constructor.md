# Default CSS selector constructor

`new CssSelector()` creates an immutable universal selector equivalent to
`CssSelector.Compile("*")` with default namespace options. Unqualified universal
matches every namespace; only elements match. XML/HTML graph overloads, default
and null argument handling, scope, document order, owner-retaining read-only
snapshots and live mutation observations are identical to the compiled universal
selector. No mutable uninitialized selector state or extra public member is added.
