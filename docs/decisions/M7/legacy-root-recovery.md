# Legacy single-root envelope compatibility

Owner: M7-html-pi-contract; implementation and full differential tests: M7-legacy.
Pinned libxml2 revision `96498992efa48d52b0e8b83058bd88dbdaf153c1`.
The oracle profile is HTML_PARSE_NODEFDTD | HTML_PARSE_NOERROR |
HTML_PARSE_NOWARNING | HTML_PARSE_NONET. NODEFDTD omits the fabricated HTML 4
Transitional doctype when none was supplied; explicit input doctypes remain
owned document children with retained metadata. NONET is defense in depth;
the managed parser never fetches external resources.

## Named native-to-managed adaptation: legacy-root-envelope-v1

The verified managed model permits at most one document element. Pinned native
legacy recovery can emit multiple sibling html elements after an explicit closing
html tag. Keep that managed invariant. Reuse the first document html whenever
legacy insertion requests a subsequent implied html wrapper after the stack was
closed. Append that later wrapper's children directly after the first root's
existing children, in their exact native order. Do not move them into its body,
create another body, or change preceding structure. This is envelope adaptation,
not exact native document topology or HTML5 recovery.

Retain the first root's ordered attributes. Attributes arriving for a subsequent
wrapper are accepted only if their expanded-name identity is absent, appended in
arrival order; existing names keep their first values and order. Legacy attribute
names use the already frozen ASCII folding. Distinct new attributes survive;
conflicting later values do not replace the first. Document comments and explicit
doctype remain at document level and are not folded into html. The parser does
not invent a default doctype. Its HTML namespace mapping remains the existing
managed HTML model contract; native null namespaces are normalized in comparisons,
without altering names, attribute values or content.

The exact original oracle vectors fuzz-047 and fuzz-118 remain byte-for-byte
unchanged. The owned report retains each native tree and the separately named
managed expected tree. In fuzz-047 the first html has body(Text('&'), b()); the
second html's span(unknown[x='1/'](Text('&'), Comment('c'), Text('xxx'))) becomes
a sibling after body within the first html. In fuzz-118 the first body's p() and
form[x='1/'](tr(Text('xx'))) remain; second-root Text('&x') and
p[x='1/'](unknown()) follow body under the first html. Both vectors must execute
against the complete exact adapted expectations; none may be excluded or reduced
to text-only comparison. Additional repeated-envelope, attribute-conflict,
document-comment, supplied-doctype and absent-doctype vectors must test the rule.
The adapter's quotas/owner/cycle checks stay active. Public model construction
still rejects duplicate document roots; it is not globally relaxed.

## Exact legacy encoding correction

The pinned HTMLparser.c lines 3289–3296 use XML_CHAR_ENCODING_WINDOWS_1252 when
htmlFindEncoding returns no declaration. The original oracle vector named latin1
has encoded bytes 3c 70 3e e9 80 (`<p>` then E9/80) and yields Text('é€'). The
previous specification's ISO-8859-1 fallback was incorrect: that encoding would
produce U+0080 instead of the euro sign. Freeze windows-1252 fallback, preserving
recognized declaration/BOM precedence and the exact pinned label corpus. Retain
the original vector name, bytes and expectation; do not silently relabel it.
This correction does not change XML decoding or import HTML5 label rules.

The same pinned native parser HTMLparser.c 4204–4206 treats '<?' as bogus comment:
original vector-015 contains '<?pi x?>' and expects Comment('?pi x?'). Legacy does
not emit PI nodes, so its profile does not share HTML5's lexical PI recognition.
