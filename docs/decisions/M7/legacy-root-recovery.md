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

## Parser-only native names and malformed encoding profile

HTMLparser.c line 51 defines HTML_PARSER_BUFFER_SIZE=100; htmlParseHTMLName at
2312–2386 consumes a full lexical element/attribute name but retains at most 100
UTF-8 bytes. ASCII uppercase is folded. A multibyte scalar or NUL's three-byte
replacement that does not fit is skipped; later narrower characters can still fill
the remaining capacity. This is not simply a UTF-16 length cap or a prefix sliced
through a scalar. Exact native name-99/name-100 are unchanged; name-101/name-105
retain 100 ASCII bytes. The rule applies to parsed start/end tag and attribute
names only. Programmatic HtmlDocument factories preserve arbitrary names and gain
no cap. Input/token quotas must account for the full consumed source, not merely
the truncated stored name; native truncation must not bypass resource limits.

HTMLparser.c htmlValidateUtf8 (370 onward) and character-data recovery (3304
onward) replace an invalid UTF-8 byte with U+FFFD and consume one byte, then retry.
This includes each byte of an invalid/truncated sequence at EOF after UTF-8 is
selected. Original utf8-invalid-1 (E0 80 80) and -2 (ED A0 80) produce three
replacements; -3 (F4 90 80 80) four; -4 (truncated F0 9F) two; -6 (E2 82 78)
produces two replacements followed by x. A valid supplementary sequence remains
one scalar. This profile differs from decoders that group a malformed sequence
into a single replacement; managed compatibility must preserve the native cases.

Pinned encoding.c UTF16LEToUTF8 (2489 onward), UTF16BEToUTF8 and EightBitToUtf8
(2951 onward) report conversion errors without emitting a replacement for the
bad unit/undefined table slot. Native parsing retains the successfully converted
prefix, stops conversion there and returns its recovered tree. It does not decode
later source into replacement text. EOF-incomplete UTF-16 units are likewise not
turned into replacement text. Exact UTF-16LE BOM + '<p>' vectors utf16-invalid-0
(high surrogate followed by x), -1 (lone low surrogate), -2 (lone high surrogate)
and -3 (odd trailing byte) retain p with no text; -4's valid surrogate pair emits
U+10000. Chunk splitting must preserve pending complete units/pairs until enough
bytes or final EOF arrive, rather than treating a transport boundary as EOF.

For built-in ISO tables, zero means an undefined slot and conversion stops.
Original iso-8859-3-bytes reaches A5, ISO-8859-6/8 reaches A1, ISO-8859-7 reaches
A4, ISO-8859-11 reaches DB; each preserves only its preceding converted input.
These exact additional oracle vectors remain unchanged and required in the legacy
owner's fixture; no malformed encoding vector is excluded or relaxed. This is
the pinned legacy byte-input profile only; string input is already decoded and
XML and HTML5 retain their own contracts. Cancellation/I/O/quota exceptions still
propagate, and caller streams remain open even when legacy decoding stops early.

## Bounded public memory / coalesced stream encoding sniff

The reference entry points are public htmlReadMemory and htmlReadIO with a callback
that fills each requested read or reaches genuine EOF. HTMLparser.c 5736–5752
passes XML_INPUT_BUF_STATIC without ZERO_TERMINATED; xmlIO.c 1931 onward therefore
wraps even the borrowed memory span in xmlMemRead instead of exposing the entire
remaining span to the parser. Both entry points have bounded input buffering and
agree on the retained 24-case sniff-boundaries.json matrix. There is no separate
unbounded-memory sniff profile and no memory/stream exception to the frozen API.

Mirror the pinned native requested-read/growth profile: xmlIO.c MINLEN=4000 and
xmlParserInputBufferGrow rounds smaller read requests up to that minimum;
parserInternals.c xmlParserGrow and each HTML lexical state's own grow triggers
control when more input becomes visible. In character data HTMLparser.c 2999
onward requests growth when available bytes are at most 64, before examining that
next scalar. This describes the actual grow algorithm, not a universal
'4000+64' sniff-window formula or a fixed offset cutoff. Other states retain their
own native grow rules. Transport buffers stay bounded and do not retain the full
input merely for prescan or string parsing.

Before encoding is selected, actually parsed meta declarations use htmlCheckMeta
(3669 onward), including its quoted content-charset handling. At the first
non-ASCII input, htmlFindEncoding (324 onward) searches the then-available raw
buffer from current position to end for its native case-insensitive substring
chain HTTP-EQUIV, CONTENT and CHARSET=, then falls back to windows-1252. It neither
rescans already consumed input nor performs WHATWG's first-1024-byte prescan.
A future declaration may affect earlier bytes only when that exact native
buffer/growth schedule makes it available. A prior declaration-looking comment
is not an actual parsed meta and is not rediscovered behind the current position.
BOM precedence and the existing fixed native encoding-label profile remain exact.

The retained matrix crosses first non-ASCII offsets 3935/3936/3990/3999/4000/4001
with future meta offsets relative to that position 64/250/3999/4001. All 24 exact
memory/coalesced-IO trees agree. Additional witnesses with first non-ASCII at byte
3 show a future declaration near relative offset 1100 selecting UTF-8 while one
near 3999 does not, producing windows-1252 text. A managed implementation must
compare complete required expected trees, not only text or a guessed cutoff.

The oracle also retains native-io.json with raw callbacks capped at 1/3/17/1024/
4096 bytes. Raw 1/3-byte callbacks expose native BOM/comment corruption in some
cases. These are named differential controls, preserved with their actual native
outputs, not silently excluded or represented as product expectations. The
managed Stream entry point coalesces caller short reads to the requested native
read size (until real EOF) and must match the public-memory/coalesced-IO result
regardless of caller chunk size. This upholds the existing BOM and transport
chunk-independence contract. It does not treat a short read as EOF, fetch resources,
or buffer the whole stream. M7-legacy must retain and classify every raw control
and execute the corresponding managed short-read checks against the coalesced
profile, alongside the full original corpus and boundary matrix.
