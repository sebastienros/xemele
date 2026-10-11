# Text/binary family joins and accounting

`TextBinaryDatatypeProvider : IDatatypeFamilyProvider` implements only canonical
TextBinary atomic metadata plus textual `anySimpleType`. The composer supplies
one immutable shared instance; built-in lists, dispatch and complete library
composition remain with M8-datatypes. NOTATION declarations, ENTITY existence
and ID/IDREF document constraints remain with XSD validation. No public API added.

String-derived values and anySimpleType retain private owned UTF-16 arrays and
scalar lengths. AnyURI retains its normalized lexical sequence: no resolution,
case folding, percent decoding, scheme-specific restrictions or canonicalization.
QName/NOTATION retain only resolved immutable namespace strings and private local
arrays; prefix spelling and mutable caller maps are not retained. `xml` resolves
to the reserved namespace. Missing/empty prefixed bindings and `xmlns` fail.
Boolean and binary have distinct primitive identities; private binary arrays
retain exact octets. All family primitives are unordered: compare returns Equal
for equal values, otherwise Indeterminate (including boolean). Hashes ignore
declared type/prefix and are salted by primitive in core. QName/NOTATION/boolean
length and all digit measures are null, not zero.

Grammar sources: [XSD 1.0 Second Edition](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/),
[XML 1.0 Second Edition Appendix B](https://www.w3.org/TR/2000/REC-xml-20001006),
[XLink 1.0 §5.4](https://www.w3.org/TR/2001/REC-xlink-20010627/#link-locators),
[RFC2396 Appendix A](https://www.rfc-editor.org/rfc/rfc2396),
[RFC2732](https://www.rfc-editor.org/rfc/rfc2732.txt).
The pinned fixture independently lists original XML production ranges; exhaustive
BMP checks compare their union against the provider tables. XML Second Edition
names reject supplementary scalars and later-edition additions; strings accept
XML Char scalars, including supplementary values. Language uses the XSD lexical
pattern; strings keep ordinal identity. Base64 enforces collapsed-space placement,
quartets, final padding and zero unused bits. URI uses charged escape-projection
atoms and bounded grammar scans including IPv6/embedded IPv4; no arbitrary input
is passed to opaque runtime URI, conversion or regex routines. Query-only relative
references follow RFC2396 §5.2/C.1's explicit examples.

Every parse charges 1 canonical metadata lookup. Strings charge one per validating
scalar and another per copied scalar. XML name membership additionally charges
each binary-search range probe. Boolean adds one shape unit and one per matched
literal character (maximum five). Hex adds one even-length shape unit and one
per input character in validation and conversion. Base64 adds one quartet shape
unit, one per input UTF-16 unit on each pass, and one per emitted byte. URI adds
shape and actual token/escape/IPv6/port inspection units on each grammar pass.
QName adds one namespace dispatch, one per map-entry visit, one per equality
length check, two per compared scalar pair, one per retained namespace scalar,
and one per copied local scalar, in addition to lexical/name-table work.

Growth reserves one retained value plus ceil(payload UTF-16 units or octets/256)
components before array allocation. Empty arrays are canonical. QName reserves
two retained components (node and immutable namespace reference) plus local-name
blocks; prefix enumeration can allocate a transient boxed enumerator, accounted
separately as a parse boundary. No scratch or pool storage is retained.
All unbounded scans/copies/comparisons/hashes poll per scalar/octet/probe even with
null caps. Entry and final publication poll zero; cancellation/caller exceptions
propagate, refusal returns Stopped/null. Equality/hash/measures throw the core's
structured failure on refusal.

Core equality/hash/length each charge one dispatch. Provider equality adds one
shape unit, then text length check plus one per compared scalar pair, or one per
compared byte (length mismatch is a constant shape decision). QName compares both
namespace and local texts. Provider hash adds one shape and one per scalar/octet.
Provider measures add one unit and return stored length/null without rescanning.
Success/mismatch paths construct no diagnostics. Warm borrowed compare/hash/length
and invalid lexical rejection target zero allocations; parsing deliberately owns
node/array allocations. Measurements are pending explicit coordinator GRANT.
