# Pinned XPath reference and deterministic regressions

The reference is System.Xml.XPath XPathDocument/XPathNavigator executing in the same explicitly selected .NET10.0.11 process with invariant culture. XML reader DTD handling is enabled for fully resolved input and external resolution is disabled. Engine extensions, quotas and chosen attribute/namespace order are contract-specific rather than BCL equivalence claims.

The156 passing tests comprise55 core expression comparisons,96 node-set/Boolean comparisons,3 namespace/attribute order-and-membership rows,1 supplementary-character classification and1 entry-context test containing27 comparisons. The96 comparison cases cover all six operators, empty/zero/one/nonnumeric sets, both Boolean values and both operand orientations. This independently protects whole-set Boolean coercion for relational as well as equality operators.

Every ordinary node-set comparison checks address identity, kind, lexical and local name, namespace URI, prefix, string value and document order. Element identity addresses include expanded name and sibling index; attributes and namespaces include owner identity. Adjacent text/CDATA is a single logical text item. Entry contexts include an attached attribute, inherited namespace and physical CDATA member rebound to its maximal logical text run. Reverse-axis step predicates and parenthesized filters are compared separately.

BCL namespace/attribute order is implementation-dependent. Only the three explicitly classified namespace/attribute-only result lists compare membership after ordinal row sorting; a separate assertion enforces the product's frozen namespace-prefix or expanded-attribute-name order. Ordinary node-set results retain exact order assertions. The namespace rows cover inherited/default namespaces, implicit xml, valid empty default undeclaration, and absence of xmlns. Attribute declarations are excluded from ordinary attributes.

Actual reference deviations are explicit assertions, not exclusions:

| Expression | Frozen/product result | Actual System.Xml10.0.11 result | Interpretation |
| --- | --- | --- | --- |
| `string-length('A😀B')` | 3 | 4 | Product counts Unicode scalars; reference counts UTF-16 code units |
| `substring('A😀B',2,1)` | 😀 | Lone high surrogate U+D83D | Same scalar-versus-code-unit distinction |
| `translate('A😀😀B','😀','X')` | AXXB | AXXB | Equal outcome despite the differing character model; explicitly checked |

There are no unresolved semantic mismatches or changed frozen expectations.

The owned fuzz class uses a fixed LCG seed0x4D345850 (decimal1295276112),32 generated documents and24 expressions each. It covers arithmetic/comparison precedence, all-axis choice, predicates/proximity, filter/path/union, functions, name resolution and coercion. Each of768 expressions is independently evaluated by the pinned BCL and then evaluated twice through the same compiled product expression (768 reuse comparisons). Namespace-only generated results use the same explicitly permitted order classification.

Additional deterministic checks execute128 rejected syntax strings,32 mutations with preserved old owned result metadata/membership and changed subsequent evaluation,32 binary-searched exact operation limits plus one-less rejection, and64 cancellation checks retaining the supplied token identity. This bounded deterministic corpus does not claim exhaustive grammar generation, arbitrary callback preemption, concurrent mutation support or later-milestone functionality.

Run the owned driver by building `docs/reports/M4-corpus/Fuzz.Driver.csproj`, then invoking its Release DLL with the pinned executable and `--fx-version 10.0.11`. The existing shared bootstrap fuzz Program is unchanged. The driver prints a concise JSON count summary and throws/returns nonzero on a mismatch.
