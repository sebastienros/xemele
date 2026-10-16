# Feature inventory

Reviewed October 15, 2026 against upstream source, not feature-name equivalence
or upstream performance claims. This is an inventory of public capabilities,
including deliberate omissions, not a certification of standards conformance.
The README summarizes the Xemele column.

Upstream snapshots:

- [libxml2 `c43dc98`](https://github.com/GNOME/libxml2/tree/c43dc98d27ac315a48d93dbd399c6c22cf7125b1):
  [module/build inventory](https://github.com/GNOME/libxml2/blob/c43dc98d27ac315a48d93dbd399c6c22cf7125b1/README.md),
  [public headers](https://github.com/GNOME/libxml2/tree/c43dc98d27ac315a48d93dbd399c6c22cf7125b1/include/libxml).
  Optional/deprecated interfaces are distinguished from core facilities; builds
  can disable modules.
- **.NET 10 BCL**, checked against the 10.0.11 reference assemblies:
  [`System.Xml.Linq.XDocument`](https://learn.microsoft.com/en-us/dotnet/api/system.xml.linq.xdocument?view=net-10.0).
  Its column distinguishes document APIs from companion BCL APIs such as
  `XmlReader`, `XmlWriter`, `XPathNavigator`, `XmlSchemaSet`, and
  `XslCompiledTransform`. A companion API is not an intrinsic document feature.
  Third-party packages are excluded unless explicitly named.

“Not exposed” means there is no corresponding documented public facility in
the reviewed surface; it does not mean the engine cannot use a similar internal
mechanism. Source presence does not imply complete behavioral parity.

## Parsing, trees, and I/O

| Capability | libxml2 | BCL XDocument | Xemele |
| --- | --- | --- | --- |
| XML 1.0 / namespaces | Yes | Yes | Yes; strict factories and owned mutable tree |
| DOM creation, traversal, attributes, namespaces, mutation, import/copy | Yes | LINQ to XML tree; detached nodes, copy constructors, functional construction | Yes; document-owned nodes and explicit import |
| Malformed XML recovery and diagnostics | Recovery options, structured errors | No recovery; `XmlException`, optional line info | Opt-in diagnostic-returning factories; strict entry points reject recovery |
| String, encoded bytes, stream input | Yes | `Parse`/`Load`; bytes via streams/readers, no span parser | Yes; byte/character spans and caller-owned streams |
| Encoding detection/conversion | Built-in encodings plus optional iconv/ICU | Via BCL reader/writer and available .NET encodings | XML UTF-8, UTF-16 LE/BE, ASCII, Latin-1, windows-1252; not arbitrary iconv encodings |
| DTD parsing, entities, defaults, external subsets | Yes | Via configured `XmlReader`; entity references expanded, not retained as entity-reference nodes | Yes; DTD opt-in, external resources require a resolver |
| SAX/event API | SAX1/SAX2 | No SAX parser; tree-change events are not parse events | XML callbacks with borrowed spans; not source-compatible SAX2 |
| Pull reader | `xmlTextReader` | Companion `XmlReader`; `CreateReader` traverses an existing tree | `Xemele.Streaming.XmlReader` |
| Push/incremental XML | Yes | No push parser; reader-driven loading | Yes |
| Async XML I/O | Caller-driven I/O/push integration; no .NET async API | `LoadAsync`/`SaveAsync`, async reader/writer integration | DOM/SAX/reader input, writer/output, resolver and inclusion async APIs |
| XML serialization, formatting, encoding | Yes | `ToString`/`Save`/`SaveAsync`; writer controls encoding/formatting | Owned strings and stream output, synchronous/asynchronous |
| Forward XML writer | `xmlTextWriter` | Companion `XmlWriter`; `CreateWriter` can populate the tree | Yes, synchronous/asynchronous |
| Caller-controlled external loading | Resource-loader APIs | Configure a supplied `XmlReader` and its resolver | `XmlResourceResolver`; no ambient file/network access |
| Transparent compressed input | Optional build support | No; caller supplies a decompression stream | Not built in; caller can supply a decompression stream |
| Built-in HTTP/FTP transport | Historical APIs/stubs; HTTP removed in 2.15 | File/URI loading via BCL XML infrastructure; external resources follow resolver policy | No; caller-provided resource streams |

Local evidence: [DOM factories](../src/Xemele/Parsing/Dom/Factories.cs),
[recovery](../src/Xemele/Parsing/Dom/Recovery.cs),
[decoder](../src/Xemele/Parsing/Input/XmlInputDecoder.cs),
[streaming source](../src/Xemele/Streaming),
[serialization source](../src/Xemele/Serialization),
[resource policy](specs/integration.md).

## Queries, validation, and document processing

| Capability | libxml2 | BCL XDocument | Xemele |
| --- | --- | --- | --- |
| XPath 1.0 compilation/evaluation, variables/functions | Yes | XPath extensions / `CreateNavigator`; compiled expressions and `XsltContext` via navigator | Yes; all 13 axes and 27 core functions, namespace items, reusable expressions |
| Additional XPath 2.0 functions | Not a built-in XPath 2.0 library | Not built in; custom functions via `XsltContext` | Not built in; explicit extension functions are possible |
| XPath 2.0/3.x language | No full implementation | No | No |
| CSS selectors | No standalone CSS engine | No; LINQ queries are not CSS selectors | XML and HTML; combinators/attributes/structural pseudos/restricted negation |
| Advanced CSS / Selectors Level 4 | Not applicable | No | No `:has`, `:is`, `:where`, attribute i/s flags, UI pseudos, pseudo-elements |
| DTD validation | Yes | Load through a DTD-validating `XmlReader`; no tree DTD validator | Yes; separate compile/validate API |
| RELAX NG | XML syntax | No built-in validator | XML syntax; compact syntax excluded |
| XSD | XML Schema 1.0 | XML Schema 1.0 via `System.Xml.Schema.Extensions.Validate` / `XmlSchemaSet` | XML Schema 1.0 including datatypes/facets/content/identity constraints; no 1.1 |
| Schematron | Optional module | No built-in validator | ISO 2016, XPath 1.0 `xslt` query binding, phases/abstract constructs/SVRL; no alternate bindings |
| Canonical XML | 1.0, 1.1, Exclusive 1.0 | Not intrinsic; convert for separate cryptography transforms | 1.0, 1.1, Exclusive 1.0; document or exact node selection |
| XInclude XML/text, fallback | Yes | No built-in processor | Yes; failure-atomic result and explicit resource grants |
| XPointer | Dedicated XPointer module | No | Shorthand ID and `element()` within XInclude only; no full XPointer engine |
| OASIS XML Catalogs | Yes | No built-in catalog resolver | Catalogs 1.0; public/system/URI mappings, rewrite, delegation, next catalogs |
| Standalone datatype/regex/automata utilities | Public datatype/regexp/automata interfaces | Companion `XmlSchemaDatatype`; no public schema-regex/automata engine | Internal managed datatype/facet/schema-regex engines; no equivalent standalone public API |
| Standalone streaming pattern engine | `xmlPattern` | No equivalent public engine | No equivalent public engine |
| XSLT | Separate libxslt, not libxml2 itself | Companion `XslCompiledTransform` (XSLT 1.0), via reader/navigator | Not implemented; BCL consumers can use adapters where compatible |
| Typed object serialization | External bindings/libraries | Companion `XmlSerializer`, via XML reader/writer; not automatic LINQ-to-CLR mapping | No automatic CLR object mapper |

BCL references: [XPath over LINQ to XML](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xpath.extensions?view=net-10.0),
[LINQ to XML XSD validation](https://learn.microsoft.com/en-us/dotnet/standard/linq/validate-xsd),
[`XslCompiledTransform`](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xsl.xslcompiledtransform?view=net-10.0),
and [XML cryptography transforms](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.xml?view=windowsdesktop-10.0).
`System.Security.Cryptography.Xml` is a separate assembly/package, not a
canonicalization method on `XDocument`. `XDocument` does not provide
XSD 1.1, XSLT 2.0/3.0, or a Schematron schema compiler; applying a separately
compiled Schematron XSLT stylesheet is not built-in Schematron validation.

Local evidence: [XPath](../src/Xemele/XPath), [CSS](../src/Xemele/Css),
[schema implementations](../src/Xemele/Validation),
[canonicalization](specs/dtd-c14n.md), [catalogs](../src/Xemele/Resolution/Catalogs),
[XInclude](../src/Xemele/Resolution/XInclude).
Profile boundaries are specified in [XPath](specs/xpath.md),
[schemas](specs/schemas.md), and [integration](specs/integration.md).

## HTML

| Capability | libxml2 | BCL XDocument | Xemele |
| --- | --- | --- | --- |
| Legacy error-tolerant HTML | Yes | No; well-formed XML/XHTML only | Separate `HtmlParser`, libxml2-style recovery profile |
| WHATWG HTML5 tree builder | No; HTML5 tokenization with custom nonstandard tree construction | No | Separate `Html5Parser`, including formatting reconstruction, foster parenting, SVG/MathML and templates |
| Contextual HTML5 fragments | Not a full WHATWG tree builder | No | Context element/ancestry/quirks-aware fragment API |
| HTML5 streaming tokens | HTML SAX uses its own tree/parser profile | No | Tokenizer/callback API; lexical events are not corrected tree traversal |
| HTML serialization | Yes | XML output only; companion XSLT supports HTML output | Explicit HTML5 and legacy modes; UTF-8 stream output |
| Browser execution / layout | No browser runtime | No browser runtime | No scripts, DOM events, CSSOM, navigation, layout or automatic downloads |

libxml2 explicitly describes its tokenizer/tree-builder split in
[`HTMLparser.c`](https://github.com/GNOME/libxml2/blob/c43dc98d27ac315a48d93dbd399c6c22cf7125b1/HTMLparser.c).
Do not compare its legacy tree construction as if it were a faster implementation
of the same HTML5 workload. Local evidence:
[HTML5 parser](../src/Xemele/Html5/Html5Parser.cs),
[legacy parser](../src/Xemele/Html/Legacy/HtmlParser.cs),
[exact HTML/CSS profiles](specs/html.md), and the
[AngleSharp comparison](../benchmarks/HtmlComparison/README.md).

## Integration and runtime-specific facilities

| Capability | libxml2 | BCL XDocument | Xemele |
| --- | --- | --- | --- |
| CLI | `xmllint` | No document CLI | Source-built `Xemele.Tool`; XML/HTML/query/schema commands, not a drop-in flag clone |
| BCL adapters | Through external bindings | `CreateReader`/`CreateWriter`, XPath navigator extensions | Read-only `XPathNavigator` and `System.Xml.XmlReader` adapters |
| C/C++ ABI | Native public API | No exported native XML ABI | No exported native ABI or binary compatibility |
| Language bindings | Optional Python and ecosystem bindings | Managed .NET API | Managed C#/.NET API |
| Native modules / allocator hooks / global-state compatibility | Build-dependent APIs | Managed runtime; no libxml2 compatibility hooks | Deliberately not reproduced; managed runtime and per-operation state |
| Hash/list/dictionary, URI, Unicode and debug utility APIs | Native public support APIs, some legacy/deprecated | Uses companion .NET facilities; not a libxml2 facade | Uses .NET facilities and private helpers; not a compatibility facade |
| Legacy XLink utilities | Legacy header/interfaces | No XLink processor | No public XLink processing API |
| Resource limits / cancellation | Engine-specific policies | Supplied `XmlReaderSettings` offers document/entity character caps; async DOM methods accept cancellation | Finite snapshotted quotas, cancellation tokens, bounded diagnostics |

Local evidence: [tool registry](../src/Xemele.Tool),
[interop](../src/Xemele/Interop),
[default quotas](../src/Xemele/Options/Common.cs).
This inventory does not equate the compared libraries' quota defaults or security
guarantees, nor infer thread-safety equivalence from a managed implementation.
The BCL character caps do not constitute an equivalent depth/token/operation
budget; configure reader settings explicitly rather than assuming equal defaults.

## Gaps and scope

libxml2 facilities without
equivalent Xemele public surfaces include full XPointer, standalone streaming
patterns, build-dependent compression, and native utility/plugin/ABI APIs.
These are gaps in feature parity, not implied commitments to implement them.

XSD 1.1, XPath 2.0/3.x languages, RELAX NG compact syntax and browser execution
are explicit Xemele exclusions, not features assumed to exist in the other libraries.
Xemele provides BCL adapters and async .NET XML APIs; `XDocument` also has async
load/save support.
Compare semantic profiles and corpus evidence before treating any “Yes” as a
drop-in replacement guarantee.
