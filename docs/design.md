# Architecture and API proposal

Status: proposed architecture overview. For implementation, read your task and its linked [module contracts](specs/README.md). Those contracts define the detailed shapes and override earlier sketches here; task evidence alone records implementation status.

## Public API principles

Use `Xemele` as the package and root namespace. Preserve upstream concepts such as `Document`, reader, parser context, SAX handler, XPath expression, and schema. Translate operations into .NET methods/properties and use managed ownership. Follow the [.NET naming guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines): PascalCase public members, camelCase parameters, `I`-prefixed interfaces, `Async` suffixes, and names such as `Xml`, `Html`, `Dtd`, and `Uri`.

Proposed mapping:

| libxml2 concept | xmloxide concept | Proposed C# API |
| --- | --- | --- |
| `xmlReadMemory` | `Document::parse_str/parse_bytes` | `Document.Parse(string/ReadOnlySpan<byte>)` |
| `xmlReadFile` | `Document::parse_file` | `Document.Load(path/Stream)` and `LoadAsync` |
| `xmlDocGetRootElement` | `root_element` | `Document.RootElement` |
| `xmlNodeGetContent` | `text_content` | `Node.GetTextContent()` |
| `xmlGetProp` / `xmlSetProp` | `attribute` / `set_attribute` | `Node.GetAttribute` / `SetAttribute` |
| `xmlAddChild` / `xmlUnlinkNode` | `append_child` / `remove_node` | `Node.AppendChild` / `Remove` |
| `xmlCopyNode` | `clone_node` | `Node.Clone` and `Document.ImportNode` |
| `xmlDocDumpMemory` | `serialize` | `Document.ToXmlString()` / `Save` |
| `xmlTextReaderRead` | `reader::XmlReader` | `XmlReader.Read()` |
| `xmlCreatePushParserCtxt` / `xmlParseChunk` | `PushParser` | `XmlPushParser.Append` / `Complete` |
| SAX2 callbacks | `SaxHandler` | `XmlSaxParser` with `XmlSaxCallbacks` or `IXmlSaxHandler` / `XmlSaxHandler` |
| XPath compilation/evaluation | XPath evaluator | `XPathExpression.Compile` / `Evaluate` |
| HTML memory parsing | HTML / HTML5 parsing | `HtmlParser.Parse` / `Html5Parser.Parse` |
| Schema validation | validation modules | `XmlSchema.Compile` / `Validate`, analogous validator types |

Names that overlap BCL types can be namespace-qualified or aliased. Avoid adding a second public method for every upstream spelling. An explicit migration table is more useful than duplicated API surfaces.

```csharp
using Xemele;
using Xemele.XPath;

var document = Document.Parse("<library><book id='1'>XML</book></library>");
var root = document.RootElement!.Value; // proposed nullable value handle
var book = root.FirstChild!.Value;
book.SetAttribute("edition", "2");

var expression = XPathExpression.Compile("count(book)");
double count = expression.Evaluate(root).AsNumber();
string xml = document.ToXmlString();
```

The `.Value` ergonomics are one reason M0 must compare a value handle with a reference facade. The public representation is settled by measured cost and API usability, not by mechanically copying Rust.

## Ownership and tree representation

Preferred experiment: a document-owned arena of node/attribute records, accessed through a small `readonly struct Node` containing document identity, slot, and generation. Evaluate against ordinary reference nodes before accepting this design. Keep raw arena indexes internal initially.

- `Document` owns its managed storage and is not disposable. Documents do not retain pooled buffers requiring an explicit return.
- Names are interned per document, never in an unbounded process-wide table. Text storage avoids accidental retention of a very large original input by a small surviving value.
- Node handles retain their document and remain stable across arena growth. Equality includes document identity and generation. Default and stale handles fail deterministically.
- Detaching preserves a node; deletion, if exposed, invalidates it. Cross-document moves require explicit import. Parent/child cycles and invalid document roots are rejected.
- Namespace declarations and attributes have distinct identity. Query results never substitute the owning element for an attribute or namespace node.
- A detached subtree can be serialized or reattached. Import/clone rules define prefix reconciliation, base URI, and ID-index behavior.
- Mutation updates ID indexes and invalidates traversal enumerators. Returned query node sets are snapshots of identities, with documented behavior if nodes are later deleted.
- Concurrent reads are supported only when no mutation occurs. Parser contexts/readers are single-consumer. Immutable compiled queries/schemas may be shared with separate evaluation state.

A DOM is semantic, not a source editor: quote style and entity spelling need not survive. Whitespace, CDATA boundaries, entity references, and source-location retention each require explicit policies and tests.

## Parsing architecture

```text
string / bytes / Stream / incremental chunks
                    |
      input + encoding + source location
                    |
    resumable XML scanner and grammar state
                    |
        semantic events + diagnostics
         /          |          \
   tree builder   SAX sink    pull reader
       |
  tree/query/validation/serialization
```

The diagram describes semantic reuse, not a requirement to allocate an event object for each token. State-machine dispatch and sinks should permit allocation-free transient events.

UTF-8 is the preferred byte-path experiment. Measure how string input is handled before adopting mandatory UTF-16→UTF-8 conversion. Encoding detection, scalar validation, line normalization, and position accounting must be shared rules even if optimized input paths differ.

The parser must suspend inside names, attributes, entity references, multibyte sequences, and declarations. `Append` processes available data; `Complete` verifies end-of-input. Async entry points await I/O and propagate cancellation; they do not wrap synchronous parsing in `Task.Run`.

SAX and pull modes must not build a hidden DOM or retain the full input. Memory may depend on open-element depth, the largest buffered token, and bounded parser tables. Very large text can be delivered in chunks. Callers are told that text callback boundaries are not semantic boundaries.

Transient spans expire at the next read/append or callback return. Persistent values require explicit copying. A synchronous callback can use spans; async APIs must not let borrowed spans outlive buffer ownership. Input streams remain open by default; parser-owned file streams are disposed. Contexts owning pooled buffers implement `IDisposable` and, where necessary, `IAsyncDisposable`.

HTML5 needs its own tokenizer and tree-construction algorithm. Sharing tree storage and utilities is useful, but XML grammar must not constrain HTML semantics. Tokenizer callback order is not a SAX traversal of the corrected HTML5 DOM.

## SAX-style callback API

Callback-based parsing is a required public API. It follows the same model as [libxml2's SAX2 callback handler](https://gnome.pages.gitlab.gnome.org/libxml2/html/struct__xmlSAXHandler.html) and [xmloxide's `SaxHandler` and `parse_sax`](https://github.com/jonwiggins/xmloxide#sax2-streaming), adapted to C# delegates and handler methods.

Offer two ways to consume the same callbacks:

- `XmlSaxCallbacks`: optional `On*` delegate properties, convenient for lambdas and small tasks.
- `IXmlSaxHandler` with an `XmlSaxHandler` base class providing no-op virtual methods: useful for reusable, stateful processors that override only the callbacks they need.

Both use the same event data and parser engine. The delegate container derives from the handler base and overrides its methods to dispatch `On*` properties, avoiding method/property name collisions. Prefer this explicit per-parse callback registration over a multicast .NET event surface. Snapshot delegate registrations at the start of parsing.

Illustrative API, subject to M0 signature review:

```csharp
using Xemele.Streaming;

XmlSaxParser.Parse(xml, new XmlSaxCallbacks
{
    OnStartElement = element => Console.WriteLine(element.LocalName.ToString()),
    OnText = text => Console.WriteLine(text.ToString()),
    OnEndElement = element => Console.WriteLine(element.LocalName.ToString())
});

await XmlSaxParser.ParseAsync(stream, callbacks, cancellationToken: cancellationToken);
```

Use typed delegates for span-bearing arguments. Text is exposed as `ReadOnlySpan<char>`; start/end-element data is a borrowed view containing local name, prefix, namespace URI, and location. A start element additionally exposes attributes and namespace declarations introduced by that tag. Attribute data includes expanded names, normalized values, and whether an attribute was defaulted. These views are valid only during the callback; retaining a value requires a copy. The final view types and dispatch costs are validated in M0.

The callback inventory includes document start/end, namespace-aware element start/end, text, comments, processing instructions, CDATA boundaries, document type/declarations, and diagnostics. Ordinary text callbacks include CDATA content; lexical callbacks mark its boundaries without duplicating text. Namespace declarations are exposed separately from ordinary attributes. Resource resolution remains an explicit resolver service, rather than an ambiguous notification callback.

Callbacks run sequentially in parse order. An empty element generates a start/end pair. Text may arrive in several chunks; consumers must not depend on chunk sizes or equate whitespace with ignorable content. No DOM is required. The same handler can consume synchronous input, asynchronous stream input, or incremental push input.

`ParseAsync` performs asynchronous I/O while callbacks remain synchronous, providing natural backpressure and safe borrowed-buffer lifetimes. It does not guarantee a fixed callback thread. Awaitable user callbacks would require a separate owned-data contract and are deferred. Consumers must not use `async void` callbacks.

Callback exceptions propagate unchanged and stop parsing; they are not converted into XML syntax errors. Cancellation throws `OperationCanceledException`. No further content callbacks or successful `EndDocument` notification follow failure/cancellation. Re-entering the same parser context from a callback is rejected. Recovery notifications and partial-input behavior must be specified and tested separately from strict-mode success.

## Errors, options, and resource policy

- `Parse` throws a dedicated parse exception containing structured diagnostics; invalid API arguments use standard argument exceptions.
- `TryParse` returns failure plus diagnostics for malformed input. I/O failures and cancellation retain their normal exception behavior.
- Recovery produces an explicit result with diagnostics and a completeness/well-formedness indication. Never silently mark repaired input as well formed.
- Diagnostics have stable Xemele codes, severity, source URI, line/column, and optional byte offset. Define position units and encoding behavior before publishing them; don't promise byte offsets for string input.
- Snapshot options when parsing starts. Avoid mutable global parser flags and process-wide error handlers.
- Proposed defaults: strict parsing; prohibit DTD processing unless enabled; no external resource access without an explicit resolver. Conformance/compatibility profiles opt into required DTD behavior with controlled fixtures.
- Resolver policy covers entities, schemas, catalogs, and XInclude consistently. Enabling DTD syntax alone does not grant network or filesystem access.
- Finite configurable limits cover depth, attributes, token size, entity expansion output and nesting, external bytes/resource count, inclusion recursion, diagnostics, and query/validation work. M0 selects numeric defaults from adversarial and real-world fixtures; unset/unlimited values must be explicit.
- No automatic compressed-input handling or implicit remote URL loading. Add such capabilities only through explicit bounded adapters if later needed.

## Query and validation architecture

XPath compilation creates an immutable expression. Evaluation context supplies namespaces, variables, context position/size, and optional extension functions. Results distinguish node sets, strings, numbers, and booleans with XPath 1.0 conversion rules. Attribute/namespace handles require a richer query-item type than a child-node-only tree handle.

Validation separates schema compilation from document/stream validation. Compiled schemas are immutable; diagnostics and resolver state belong to each operation. Do not use .NET regular expressions as an assumed implementation of XML Schema regex semantics. Datatypes, pattern languages, namespace rules, and identity constraints need their own conformance work.

Canonicalization writes bytes according to the selected specification. It must handle node-set selection and inherited namespace/XML attributes correctly, independently of normal serialization.

## Proposed repository organization

```text
Xemele.slnx
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.github/workflows/
src/
  Xemele/
    Tree/ Parsing/ Streaming/ Serialization/ Diagnostics/
    XPath/ Validation/ Html/ Html5/ Css/ Resolution/ Internal/
  Xemele.Tool/
tests/
  Xemele.Tests/
  Xemele.Conformance.Tests/
  Xemele.Differential.Tests/
  Xemele.TestUtilities/
  Xemele.Fuzz/
  fixtures/                  # small owned regression cases
  corpora/                   # manifests, licenses, expectations
benchmarks/
  Xemele.Benchmarks/
  fixtures/
  baselines/
tools/
  ReferenceRunners/          # pinned C/Rust test and perf runners
  CorpusManager/
samples/
docs/
  decisions/
artifacts/                  # ignored downloaded corpora and reports
```

Start with one runtime library/package and one optional .NET tool. Namespaces provide module boundaries; avoid one assembly per subsystem before dependency or packaging needs justify it. Add test projects as their suites become executable, rather than scaffolding many empty projects.

Pin an installed .NET 10 SDK via `global.json` (10.0.400 is available in the current workspace). Use C# 14, nullable annotations, deterministic builds, Source Link, XML API docs, analyzers, central package versions, and warnings as errors. Avoid preview language/runtime features. Keep runtime dependencies minimal; optional legacy encoding support must declare its package/runtime requirements explicitly.

Use xUnit for behavioral suites and BenchmarkDotNet for managed benchmarks. Select and pin exact compatible versions during M0. Record public API baselines from the first preview. Add trimming/AOT checks early; expose any unsupported dynamic behavior before release.

## Session boundaries

Implementation status lives in [task records](tasks/README.md), with one writer per task. The [milestone map](milestones/README.md) describes dependency joins and additive releases. Module specs own exact shapes; workers edit only declared source/test/API-fragment paths in isolated worktrees. Shared build settings and contracts are integrated before their dependent work starts.
