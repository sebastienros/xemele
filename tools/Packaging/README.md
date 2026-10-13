# Xemele release candidate

Managed net10.0 XML/HTML toolkit: owned XML trees, strict XML1.0/namespaces and
explicit recovery, SAX callbacks, bounded pull/push/async transports and forward
writer, XPath1.0, DTD validation, canonicalization, catalogs/XInclude, BCL adapters,
legacy and WHATWG HTML, CSS selectors, RELAX NG XML syntax, XSD1.0 Second Edition,
and ISO2016Schematron with direct XPath1 and SVRL. The `Xemele.Tool` package
provides the `xemele` command. Safe defaults prohibit ambient external access.

Use XmlDocument.Parse/Load/LoadAsync for an owned tree, XmlSaxParser for callbacks,
XPathExpression.Compile/Evaluate for reusable queries, and the named schema
Compile/Validate APIs. Caller streams stay open. Borrowed callback views last
only during that callback. TryParse's false/null/diagnostics describe syntax
rejection; invalid arguments, cancellation, I/O and caller exceptions propagate.
Validation IsComplete=false is incomplete work, not an invalidity verdict.

See the repository's docs/migration/README.md, runnable examples and module
specifications at the package's exact repository commit for detailed contracts.
The first preview's222public signatures remain unchanged. Nullable/default-handle,
recovery, resolver, finite-budget and ownership behavior are documented there.

Excluded: XML1.1, XPath2+, XSD1.1, RNG compact syntax/custom datatype registration,
XSLT engines, browser rendering/scripts/DOM, CLR-object serialization, native C ABI
and broader XPointer profiles. Schema profiles and exact conformance exceptions
are documented in the release inputs/reports; no blanket native-library parity.
The155approved original XSD nonpasses remain144false/11null and receive zero pass
credit. Separate application-profile evidence is not original standards credit.

This0.9.0-rc.1package is local and unpublished. A successful local smoke test does
not establish the remaining platform, dedicated timing or sustained-fuzz gates.
Source Link records the exact local repository commit; remote source retrieval
is unavailable until that commit is deliberately pushed. Public XML documentation
is supplemented by this guide; full per-member XML-comment coverage is not claimed.
