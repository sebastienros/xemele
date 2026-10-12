# Source basis

[Second Edition QName resolution](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#src-resolve)
clause 4 tests the document containing the QName for an import, independently of
whether a collected component exists.
[Import semantics](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#composition-schemaImport)
separates namespace permission from the optional location hint.

[W3C4057](https://www.w3.org/Bugs/Public/show_bug.cgi?id=4057) specifically identifies
the elemZ007 included-author import scenario, also names schZ005, and resolves the
listed original schemas as invalid. [W3C5779](https://www.w3.org/Bugs/Public/show_bug.cgi?id=5779)
clarifies that a foreign QName without an authoring-document import violates a
schema representation constraint rather than merely deferring resolution. The
2009 working-group disposition adopted the clarification; this source repair
preserves the corpus expectations rather than deriving a new oracle.

Original DataTypes metadata marks anyURI_a001_1336 INVALID for 1.0 and VALID for
1.1 and names W3C15865; its 1.1-only instance is not applicable. The source URI
forms start with digits before the first colon. The integrated TextBinaryUri
validator implements RFC2396 plus RFC2732 with the XLink escape projection; the
Second Edition source attributes use this existing charged validator after XML
whitespace collapse. This does not alter composition location retrieval.
