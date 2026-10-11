# Additive CSS selectors for XML trees

Decision owner: M7-css-xml-contract. This ordered repair extends the M7 CSS
freeze to satisfy XML and HTML tree coverage without changing either tree model.
`tests/Contracts/Fixtures/M7/Selector.cs.fixture` is a nonpackaged declaration
model, not a shipped selector implementation.

The existing `Matches(Xemele.Html.HtmlNode element)` and
`IReadOnlyList<Xemele.Html.HtmlNode> Select(Xemele.Html.HtmlNode root)` remain exact.
Add `bool Matches(Xemele.XmlNode element)` and
`IReadOnlyList<Xemele.XmlNode> Select(Xemele.XmlNode root)`. Both use the same
compiled selector grammar and immutable namespace-option snapshot. Consumers
use actual verified XmlDocument/XmlNode handles; no conversion to HtmlNode,
new XML wrapper, parser dependency, or later XPath/schema dependency is required.
The graph type passed to a call determines its matching rules.

## Names, namespaces and attributes

XML local names, attribute names and namespace URIs compare ordinally and
case-sensitively. This includes XML elements/attributes whose namespace URI is
`http://www.w3.org/1999/xhtml`; the URI alone never enables HTML case folding.
Of-type siblings compare the ordinal pair (namespace URI, local name), not prefix.
HTML retains ASCII-insensitive HTML-namespace names and ordinal foreign names.
ID, class and attribute values remain ordinal sensitive in both graphs.

Prefix mappings are captured at Compile; unbound selector prefixes reject there.
`prefix|name`, `*|name`, and `|name` retain their existing meanings. The empty
mapping key supplies the default namespace for unqualified type selectors only;
without that key they match any namespace. Unqualified attribute selectors mean
no namespace regardless of the default mapping. Prefix spelling on a node does
not affect expanded-name identity. Match only actual attributes; XML namespace
declarations are separate tree metadata and no DTD default attributes are
fabricated. Existing unsupported grammar and syntax-offset rules are unchanged.

## Scope, roots and results

`:root` matches the document element, not an arbitrary detached element.
`:scope` in Matches means the supplied element. In Select it means the supplied
root if it is an element, otherwise its owner document's document element;
a document without an element supplies no element scope. Selection visits only
the supplied root and its actual descendants, including the root when an element;
it never adds that owner document's element when outside the query subtree.
HTML template content remains outside Children traversal.

Matches returns false for every non-element and for default(XmlNode), checking
IsDefault before reading Kind. Select(default(XmlNode)) throws ArgumentException
for root; null HtmlNode in either overload throws ArgumentNullException.
Valid documents, fragments, detached elements and other non-element roots may
be selected; roots without element descendants produce empty results.

All structural pseudo-classes count element siblings only. XML :empty ignores
comments and processing instructions but any Text or CData child, including
whitespace, makes it false; HTML keeps its existing comment/text rule. Negation,
selector lists and every other frozen grammar feature keep the exact CSS matrix.

Results contain unique elements in preorder tree order, with selector-list order
irrelevant. The returned IReadOnlyList is an immutable membership/order snapshot
of XmlNode handles or HtmlNode references, retaining their permanent owners.
It is not a deep DOM copy: later mutation is visible through retained nodes.
Detached trees remain supported. Subsequent Matches/Select calls observe current
attributes, values and links without stale global caches. Options, results and
owners are not shared mutable global state.

## Contract verification boundary

The independent Selector declaration fixture compiles XML and HTML signature consumers and
executes only verified XML tree/stream probes. Selector declaration bodies are
never executed. The compiled baseline adds exactly two methods; Engine and
Tokenizer baselines remain unchanged. Missing-member controls must reject.
M7-css implements and tests this complete two-graph semantic matrix; runtime
selector correctness or allocation performance is not asserted by this repair.

The declaration lanes intentionally keep the frozen Model.cs.fixture alongside
the shipped model reference for canonical fixture metadata. Builds use only
`NoWarn=CS0436` to permit that duplicate declaration-model warning. They do not
prove actual HtmlNode consumption. The separately owned nonpackaged
`docs/reports/M7-css-xml-contract/ActualModels` project excludes Model.cs.fixture,
compiles the same selector declaration/consumer against actual shipped models
with normal warnings-as-errors, and verifies model assembly identity and actual
HTML/XML tree probes. No selector declaration body executes in either lane.
