# Pinned html5lib tokenizer and encoding corpus

Upstream: https://github.com/html5lib/html5lib-tests
Revision: `c777c408b61078ea2eb4acefc2535f54dbc8b28a`.
The original 21 resources, including upstream LICENSE, are retained verbatim.
`manifest.json` identifies every resource by original path, length and SHA-256.

All 13 tokenizer `tests` arrays are required: 6,823 source tests expand into
7,049 initial-state vectors. No state or malformed input is excluded. Every
vector runs whole-input, one UTF-16 unit at a time, and at every two-chunk split;
this includes splits in surrogate pairs, CRLF, references and markup prefixes.
Character tokens normalize only by contiguous coalescing. Upstream expectations
and inputs are never rewritten. Unknown state or token names fail the harness.
Parse errors have a separate complete-code comparison rather than becoming tokens.

The four `xmlViolationTests` are a different, explicitly XML-infoset coercion
profile described by upstream tokenizer/README.md. This HTML model intentionally
preserves arbitrary names, noncharacters and comments; it does not expose an XML
coercion mode. These four records remain retained and classified, not counted as
passing HTML lexical vectors.

Encoding directory includes 59 sniff vectors in tests1.dat, 22 in tests2.dat,
one Yahoo Japanese byte-resource vector, one scripted encoding-change vector,
and a Big5 chardet resource. The standalone frozen tokenizer accepts decoded
strings and does not sniff encodings or execute scripts. String scalar/CRLF
preprocessing and original-byte map cooperation are exercised here. Actual byte
sniffing/decoding and encoding-vector composition belong to the byte/stream HTML5
parser owner. Browser script execution is outside the declared M7 scope; the
single scripted record remains separately classified. No XML decoder substitutes
for HTML byte decoding.

The current-spec error discrepancy ledger `normative-discrepancies.json` records
only domjs.test indices 0–2: exact upstream inputs and original error arrays are
retained. Mandatory PI-open rejects normalized LF at column 3 with
invalid-first-character-of-processing-instruction-target. Their original comment
token outputs pass unchanged; the other 7,046 state-vector error arrays match.
