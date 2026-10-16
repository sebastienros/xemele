# CSS selectors over XML trees

Matches/Select accept XmlNode as well as HtmlNode, sharing the compiled grammar
and immutable namespace-option snapshot. The graph type determines name matching.

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
