# XSD component mapping repair

Pinned original witnesses: W3C xsdtests commit
7bc3365c652a322f3d762021b3879eb92dae7e30; unchanged bytes, source paths,
SHA-256 and original/profile expectations are in the owned fixture manifest.
00COPYRIGHT carries the upstream document-license notice.

The mixed=true zero-occurrence mapping follows accepted groupN007 corpus behavior
and the independent BCL instance oracle. W3C [R-204](https://www.w3.org/2001/05/xmlschema-rec-comments.html#pfiminMaxZero)
records the cross-section mapping gap; the [original comment](https://lists.w3.org/Archives/Public/www-xml-schema-comments/2003JanMar/0038.html)
identifies §3.4.2 versus §3.7.2/§3.8.2. This is a bounded resolution of that gap,
not a claim of an unambiguous published correction; no applicable Second Edition
erratum was found by the prerequisite review.

Original Saxon xv001/xv004 expectations remain VALID. The independently approved
Second Edition profile expects INVALID. These are different oracles. BCL's
rejection of their XML1.1 declarations is unsuitable name-profile evidence.
[Structures schema-for-schemas](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#normative-schemaSchema)
uses xs:NCName; [Datatypes NCName](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#NCName)
and [XML Second Edition Appendix B](https://www.w3.org/TR/2000/REC-xml-20001006#CharClasses)
pin the older productions. U+0133 is excluded between U+0131 and U+0134,
and the name productions have no supplementary range. XML parser Fifth Edition
behavior is preserved. The coordinator-approved adjudication is
`docs/orchestration/M8-xsd-xmlversions-adjudication.json`.

Actual baseline at 9fcb4a58c: offline restore and Release build passed with
zero warnings/errors; four public witnesses produced one pass and three expected
failures. groupN007 was complete-invalid with XSD.Content character-content
rejection despite BCL validation success. xv001 wrongly published. xv004 already
rejected at its NCName enumeration facet (line13/column25/byte541), not at its
component names. At f433b15ca, direct compiler witnesses confirmed both xv001
and xv004 wrongly compiled: six cases produced one pass/five expected failures.
Raw output is ignored under artifacts/M8-xsd-component-mapping.

The repair validates borrowed UTF-16 scalars against the verified Second Edition
TextBinaryNames tables. A single component-charged adapter per compilation maps
all lookup work/refusal to the authored source node. No temporary lexical value
or datatype payload is constructed. Mixed zero-occurrence synthesis reuses the
existing charged source-located mapping boundary. Repaired results and allocation
evidence remain pending.
