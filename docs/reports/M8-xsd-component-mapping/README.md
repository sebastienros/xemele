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

The coordinator amendment adds A4 on the same owned compiler boundary. At
92b327ff3 the unchanged wildK001/wildC030/wildZ010 original schema witnesses
passed independent BCL compilation then failed product compilation: all81
component regressions passed, the three wildcard witnesses failed (84 total,
zero errors/skips/not-run). The repair permits an explicit empty namespaceList
to map to a positive empty set, preserving omitted ##any and all existing tokens.
Borrowed source-located scalar work is charged before namespace normalization;
name normalization likewise polls/charges before scanning arbitrary whitespace.
The original invalid groupH007 restriction witness is also retained to check
that mixed zero-occurrence synthesis cannot bypass its element-only base.

Actual final correctness at bc8d191609fc48d84ae66d0a42ea8657ecb4a0fe:
1263 passed =117 owned +33 predecessor syntax +1113 affected;42 safe resolver.
Zero errors/failures/skips/not-run; public1417, frozen model316+139/compiler257/
identity24/content9/evaluation2/facade7 match. Normal and true source-linked
XML-only consumers pass; five Release builds0warnings/errors;planning187/diff
pass. groupH007 now rejects its mixed restriction without a separate runtime
change. Initial failed build/checker attempts and pre-normalization pass are
recorded honestly in task evidence. Distinct malformed-scalar labels prevent
xUnit's replacement-character rendering from collapsing test identities.

Source review required actual normalization-loop work/cancellation, now addressed
by ComponentCollapse only for NCName/QName/wildcard namespace inputs. It returns
the borrowed canonical string; otherwise each actual normalization scalar is
charged at the authored source. Four independent padding-work formulas and
null-cap cancellation 128scalars into normalization are asserted. No timing or
allocation measurement has been launched; A3 stays pending coordinator evidence.

Source review PASS at bc8d1916 accepted the normalization repair and prior mapping
semantics. Final preparation at fe72a347:19-route disabled control1555calls /
36643consumed; probe build0warnings/errors; frozen464identities, actual elevated
verify-only and disabled preflightPID96986 exit0/reaped/groupabsent/logclosed.
See proposal.md and allocation-preflight.json. Cleanup supervision is reused
unchanged. No worker measurement has run; A3 remains pending root-only grant.
