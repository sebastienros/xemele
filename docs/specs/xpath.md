# XPath contract — M4

Frozen by M4-contracts. Production implementation starts with M4-model; this
contract adds no runtime XPath code. Exact proposed declarations are
`tests/Contracts/Fixtures/M4/Public.cs.fixture` and `Model.cs.fixture`.
`Public.golden.txt` is the canonical xemele-api-v1 compiled 177-member proposed
public baseline; it is not a shipped runtime API fragment.
The [decision](../decisions/M4/xpath-profile.md) fixes semantics, lifetime,
quotas, diagnostics and lane ownership. Those files supersede the earlier `Node`
proposal: the verified tree exposes `XmlNode`, `XmlAttribute` and `XmlDocument`.

The profile is XPath 1.0 expressions, with all 13 axes, 14 binary operators,
unary minus and all 27 core functions. No XPath 2.0 sequences, types, `if`,
`for`, comments, scientific numeric literals, or implicit extension library.
The complete grammar and lexical decisions are in the decision. The pinned
normative reference is the [16 November 1999 Recommendation](https://www.w3.org/TR/1999/REC-xpath-19991116/).
`inventory.json` contains an explicit acceptance ID and concrete expected
behavior for every axis/operator/function and grammar/edge case. These are
future runtime acceptance vectors, not claimed runtime test passes.

Prefix bindings are copied at Compile, including expanded variable and function
names. Unprefixed names use the empty namespace. XPathContext contains expanded
variable/function bindings, limits and cancellation; it has no dynamic namespace
property. Compile copies extension signatures; Evaluate copies bindings and
snapshots the input tree under quotas before dispatch. Compiled expressions are
immutable, reentrant and reusable. Result membership, order, names and values are
owned snapshots; exposed tree handles retain their existing live tree semantics.

M4-model owns immutable AST, static context, item/value representation, budgets,
core function signature metadata and internal interfaces. Compiler, navigator
and function lanes compile independently against that integrated model. The
compiler produces AST, navigation implements the tree adapter, functions consume
navigation/focus/budget; evaluator joins them. M4 composition alone supplies the
public XPathExpression facade. No lane requires schema, canonicalization, or
later milestones. No public declaration fixture is packaged or linked to runtime.

Contract checks: run `python3 tests/Contracts/Fixtures/M4/check.py --dotnet <pinned-dotnet>`.
It compiles public usage and runs all three independent seam probes, checks exact
inventory and expected compile failures, then checks declaration markers and
ownership. It does not evaluate XPath. Production conformance must execute the
vectors and pinned differential cases; production performance must measure
compilation and evaluation separately with the repository benchmark policy.
