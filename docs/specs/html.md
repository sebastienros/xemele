# HTML and CSS contracts — exact M7 freeze

Decision owners: M7-contracts and the additive M7-css-xml-contract / M7-html-pi-contract repairs. [Resolved decisions](../decisions/M7/html-css.md)
and `tests/Contracts/Fixtures/M7/{Model,Engine,Tokenizer,Selector}.cs.fixture`
are the exact declarations. They are independent nonpackaged compilation models,
never shipped stubs. Actual earlier types are `Xemele.XmlDocument`, `XmlNode` and
`Xemele.Streaming.XmlReader`; provisional `Document`/`Node` names are superseded. HTML uses the
separate `Xemele.Html.HtmlDocument`/`HtmlNode` representation owned by
M7-html-model under HtmlCommon; XML factories and validation are unchanged.

## Parsing profiles

`HtmlParser` is legacy libxml2-compatible HTML recovery, not the HTML5 algorithm:
case folding, implied html/head/body, paragraph/list/table autoclosing, legacy
entity decoding and malformed-input recovery are differential-tested against the
pinned libxml2 oracle in M7-legacy. Differences require named durable vectors
and an explicit compatibility decision; they must not be relabeled HTML5.
`Html5Parser` implements the WHATWG HTML parsing algorithm, including all tokenizer
states, insertion modes, active formatting reconstruction/adoption agency, foster
parenting, scope checks, implied elements/end tags, foreign SVG/MathML integration,
foreign name/attribute adjustment, templates and full named-reference table.
The implementation corpus owners pin upstream revisions/resources and account for
all cases. This freeze authorizes no subset of the parsing algorithm.

Both accept string, borrowed encoded byte span, and caller-owned stream.
Strings are decoded characters with no encoding sniff. HTML5 byte input uses WHATWG
encoding label mapping: BOM wins, then the first applicable in-band declaration in
the first 1024 bytes, then windows-1252 default; UTF-16 declarations without BOM
map to UTF-8 per HTML sniffing. Legacy byte input uses BOM, then recognized in-band encoding declaration, then windows-1252 fallback as pinned libxml2 does; byte-span and Stream input use the same bounded public-memory/coalesced-IO native buffering and grow profile, with caller short reads coalesced until the requested read or true EOF; there is no unbounded full-span sniff; legacy label support, parser-only 100-UTF-8-byte lexical-name retention, bytewise malformed UTF-8 replacement and native converter-stop recovery are fixed by its differential corpus and the [legacy profile decision](../decisions/M7/legacy-root-recovery.md); they must not inherit XML declaration or HTML5 decoder rules. Programmatic shared-model names remain uncapped except by existing quotas. No HTTP transport or transport encoding hint API.
Unsupported labels follow HTML fallback, never XML encoding rules. Streams remain
open on success/fault/cancellation; no parser fetches external entities, DTDs,
images or scripts. No asynchronous entry point is promised by M7.

`ScriptingEnabled=false` by default, retained in HtmlDocument.ScriptingEnabled; true controls noscript parsing only. No
JavaScript execution, document.write, browsing context, navigation, layout,
networking, custom-element reactions or executable script callbacks. Tree-vector
exclusions are limited to these browser-execution dependencies; scripting-flag
and noscript vectors remain required. Parse errors perform prescribed HTML
recovery; quota/invalid-option/I/O/cancellation failures are exceptions. Limits
are snapshotted and validated before work; shared limits retain exact units and
finite defaults; HTML expansion includes decoded named references, no entity
resolver. TokenCharacters bounds each token name/data and aggregate attribute name/value characters; Depth bounds open-element stack and final tree depth; AttributesPerElement bounds accepted token attributes; InternedNames bounds distinct interned name strings. DocumentCharacters counts decoded input Unicode scalars; ExpandedCharacters counts emitted decoded-reference scalars as well as input-derived text. There is no token-count quota. These include
intermediate tree-builder work, with bounded diagnostics where emitted.

## Document, fragment and ownership

Full parse returns a fresh document containing doctype, comments and the prescribed
implied structure: normally html/head/body, but frameset insertion modes can produce
html/head/frameset without body (for example <!doctype html><frameset><frame></frameset>). QuirksMode is determined by the complete WHATWG doctype
rules (including public/system identifiers and force-quirks); all three modes are
retained. Legacy defaults to Quirks unless its recognized HTML doctype selects
NoQuirks. Token doctype absent identifiers differ from empty strings.

`HtmlFragmentContext` holds a real context element, preserving its namespace,
local name, attributes and ancestor chain, plus explicit document quirks mode.
Fragment parsing reads that context without mutating it, snapshots its relevant
ancestry/form-pointer context at entry, uses scripting options, initializes the
correct tokenizer state/insertion mode, and handles template-context stack and
foreign integration points. It returns a detached Fragment in a fresh owner;
children are the WHATWG fragment result, never a synthetic html wrapper. Existing
form ancestors and template ancestors are honored; invalid non-element context
throws ArgumentException. Import into context.OwnerDocument is required before
attachment. Passing detached elements is valid and denotes no ancestors.

HTML nodes have stable reference identity, a permanent owner and nullable absent
links. Mutation checks same owner, acyclicity and valid container kinds before
changing links. Fragment insertion transfers its children in order and empties
the fragment. Import makes a detached target-owned copy; Clone remains same-owner.
Document has at most one element root and one doctype; legacy duplicate-html-root native vectors use the named [root-envelope recovery](../decisions/M7/legacy-root-recovery.md), preserving content order without relaxing that invariant; text belongs to elements
or fragments. Attributes have ordered expanded-name identity; SetAttribute
replaces in place or appends; snapshot records cannot mutate the tree. Duplicate
HTML token attributes retain the first after ASCII folding; foreign adjustment
occurs in the tree builder. HTML nodes preserve arbitrary HTML names, control
characters allowed by HTML, comment '--'/trailing '-', namespace attributes,
doctype identifiers and template contents without XML-name coercion. HTML input preprocessing normalizes CRLF/CR per the algorithm. NUL handling is
state-specific, not a preprocessing replacement: Data emits a NUL character token;
in-body tree insertion ignores it, so <p>a\0b has text ab. RCDATA and comment
states replace NUL with U+FFFD, so textarea a\0b yields a�b. Other tokenizer and
insertion states follow their prescribed NUL rules; no XML sanitization occurs. TemplateContent is a distinct owned fragment on
HTML template elements; it is not in Children and has no Parent; deep copy includes
it. Traversal and selectors use actual Children, not template content implicitly.
Name casing follows HTML ASCII folding and WHATWG foreign adjustments. TextContent
concatenates descendant text only, excluding comments/processing instructions/doctype/template content.

## Tokens and callbacks

Seven token kinds are Doctype=0, StartTag=1, EndTag=2, Comment=3, Character=4, EndOfFile=5, ProcessingInstruction=6. The ordered [processing-instruction decision](../decisions/M7/html-processing-instructions.md) freezes mandatory current WHATWG states, target/data ownership, the appended node kind and CreateProcessingInstruction factory; no opt-in profile or algorithm subset is permitted.
The HtmlToken constructor defensively copies Attributes, rejects null name/data/list or null entries and undefined token kinds, and exposes get-only properties. Every emitted token owns immutable snapshots of Name/Data/ordered Attributes, SelfClosing,
nullable doctype and XmlLocation. Location identifies the first source scalar;
line/column are normalized one-based, byte offset is null for string input. EndOfFile
is emitted exactly once. Character batching may differ but concatenated data and
ordering must match; fixtures compare normalized contiguous character runs.
Self-closing acknowledgement is tree-builder state, distinct from the lexical
flag. RcData/RawText/ScriptData initial states require LastStartTagName to recognize
appropriate ends; PlainText has no end-tag recognition. Tokenizer/tree builder
must cooperate on CDATA allowance and state changes in foreign contexts; public
standalone tokenizer defaults to HTML Data, not a fake complete tree builder.

Tokenize callback fires synchronously in lexical token order. Tokens can be
retained after callbacks and operation completion. Callback exceptions propagate
unchanged, including XmlParseException, stop delivery immediately and trigger
owned-resource cleanup. Cancellation is checked at entry and bounded intervals.
The separate callback adapter delivers these same tokenizer tokens; it must not
pretend to emit SAX-equivalent events for the corrected HTML DOM.

## Serialization

Default Html5 mode uses WHATWG HTML fragment serialization for a node and document
child serialization for documents; Fragment emits children, Element emits its
outer markup, template emits TemplateContent. HTML void elements omit end tags;
foreign elements have explicit ends. Escape '&', NBSP, '<' and '>' as specified for text; attributes use
double quotes and escape '&', NBSP and '"'. Raw text for HTML style/script/xmp/
iframe/noembed/noframes/plaintext is unescaped; noscript follows parser scripting
metadata. Namespace/prefix output follows the HTML serialization algorithm.
Doctype writes its retained name; HTML5 serialization does not promise preservation
of public/system identifiers in output. Processing instructions serialize in Html5 mode as <?target data?> with one separator space, including empty data; target and data are not escaped. Comments preserve data; serializing a
programmatically malformed comment does not silently sanitize it. Legacy mode
follows pinned libxml2 HTML output rules and its own vectors, including legacy
void/boolean/doctype differences. No XML serialization mode, pretty-print or
semantic reparsing-equivalence promise. Save writes UTF-8 without BOM, leaves the
caller stream open, and propagates I/O/cancellation. Output is HTML, even when it
cannot be reparsed as XML.

## Exact CSS matrix

Supported grammar: selector lists, universal/type, ID, class, attributes with
presence, =, ~=, |=, ^=, $=, *=; whitespace descendant, > child, + adjacent and ~
general sibling combinators. Identifiers/string escapes follow CSS syntax; grouping
is strict (one invalid branch rejects the entire compilation).
Supported pseudo-classes only: :root, :scope, :empty, :first-child, :last-child,
:only-child, :nth-child(an+b), :nth-last-child(an+b), :first-of-type,
:last-of-type, :only-of-type, :nth-of-type(an+b), :nth-last-of-type(an+b),
:not(simple-selector-list). Negation arguments contain compounds without
combinators or nested :not; complex/nested arguments reject. nth supports odd,
even, signed integer and an+b, not the Level 4 'of selector' extension.
Unsupported: pseudo-elements, :is/:where/:has, :lang/:dir, state/UI/link/form
pseudo-classes, column combinator, attribute i/s flags and every unlisted feature.
These throw CssSelectorException with zero-based UTF-16 syntax offset at Compile;
never partial matching or ignored syntax. Null arguments use ArgumentNullException.

Namespaces support prefix|name, *|name and |name; options prefix mappings are
snapshotted, unbound prefixes reject. Mapping key empty denotes default type
namespace; without it unqualified types match any namespace. Unqualified
attributes mean no namespace (default namespace never applies). The additive
[XML selector decision](../decisions/M7/css-xml.md) freezes overloads accepting
the actual `Xemele.XmlNode`, alongside the unchanged `HtmlNode` overloads.
For XML trees, every type/attribute name and of-type expanded name is ordinal
case-sensitive, including nodes in the XHTML namespace; namespace URIs are always
ordinal sensitive. For HTML trees, HTML namespace type and attribute names use
ASCII-insensitive matching; foreign names and all ID/class/attribute values are
ordinal sensitive, even in quirks documents.
Attributes compare actual DOM values; no fabricated default DTD attributes.
:empty ignores comments and processing instructions but includes any text, including whitespace. Position
pseudos count element siblings only; of-type uses expanded names with HTML casing.
:root matches the document element; :scope means supplied Select root if it is an
element, otherwise its document element; Matches scope is the element itself.
Matches(non-element) is false, including default XmlNode handles. Select of a
default XmlNode throws ArgumentException; null HtmlNode arguments throw
ArgumentNullException. Select includes root when it is an element,
returns unique elements in preorder document order, snapshots the result and
retains owner via handles. Detached trees work. Mutating the DOM after compile is
observed by subsequent calls; no stale global cache. No later XPath/schema module
is needed. Selector corpus covers each declared feature and rejected family;
xmloxide differential overlap is pinned and separately classified by its owner.

Stream engines decode/tokenize incrementally with bounded transport buffers, never
buffer the entire stream merely to call Parse(string). DOM/results, active-formatting
and open-element state consume memory proportional to retained output/algorithm
state within quotas. String and byte-span entry points borrow only until return;
results retain no input buffers. Callback-only tokenization permits bounded token
buffering and does not accumulate a document. Standalone Tokenize(list) intentionally
owns all tokens. New HtmlDocument validates/snapshots limits. Children/Attributes
expose read-only live views, with fail-fast mutation during enumeration; callers
cannot mutate backing collections. Snapshot Select outputs are read-only arrays of the input graph’s node type.
