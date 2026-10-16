![Xemele](assets/banner.png)

# Xemele

A .NET **XML and HTML toolkit for .NET 10**, inspired by
[libxml2](https://gitlab.gnome.org/GNOME/libxml2). Parse, build, query, validate,
and serialize all kinds of XML documents without any dependency, as fast as possible.

## Versions and package feeds

| Source | Purpose | Package feed |
| --- | --- | --- |
| `v*.*.*` tags | Versioned releases | [![NuGet.org stable version](https://img.shields.io/nuget/v/Xemele.svg?label=nuget.org)](https://www.nuget.org/packages/Xemele) |
| `main` branch | Active development | [![Feedz preview version](https://img.shields.io/endpoint?url=https%3A%2F%2Ff.feedz.io%2Fsebastienros%2Fxemele%2Fshield%2FXemele%2Flatest)](https://f.feedz.io/sebastienros/xemele/nuget/index.json) |

For preview packages add Feedz alongside NuGet.org:

```sh
dotnet nuget add source https://f.feedz.io/sebastienros/xemele/nuget/index.json --name xemele-preview
dotnet add package Xemele --prerelease
```

## Features shared with the .NET BCL

Like `XDocument` and the companion BCL XML APIs (`XmlReader`, `XmlWriter`,
`XPathNavigator`, and `XmlSchemaSet`), Xemele provides:

- **XML 1.0 and namespaces:** owned, mutable DOM; document construction,
  traversal, and mutation; string and stream input.
- **Streaming XML:** forward reader, streaming writer, and asynchronous XML
  input/output.
- **XPath 1.0:** reusable expressions, variables, and extension functions.
- **Validation:** DTD parsing and validation, and XML Schema 1.0 validation.
- **XML serialization:** formatting, encoding, and synchronous/asynchronous output.
- **Resource resolution:** caller-controlled external resource loading.

These are shared capabilities, not claims of identical APIs or ownership behavior.
Some BCL capabilities require a companion API rather than `XDocument` alone.

## What Xemele adds beyond the .NET BCL

- **Parsing extensions:** direct character-span and byte-span input, and opt-in
  malformed XML recovery with structured diagnostics.
- **SAX and push parsing:** XML callbacks with borrowed spans and incremental
  push parsing, in addition to the pull reader.
- **HTML:** separate legacy libxml2-style recovery and WHATWG HTML5 parsers,
  contextual fragments, SVG/MathML integration, templates, tokenizer callbacks,
  and HTML serialization.
- **CSS selectors:** queries over both XML and HTML trees.
- **Additional validation languages:** RELAX NG XML syntax and ISO Schematron
  2016 with XPath 1.0 expressions and SVRL reports.
- **XML resource processing:** OASIS XML Catalogs and XInclude XML/text/fallback.
- **Integrated canonicalization:** Canonical XML 1.0, 1.1, and Exclusive C14N 1.0,
  including selected-node canonicalization. .NET also offers C14N 1.0 and
  Exclusive C14N through separate XML cryptography transforms.
- **Resource controls:** finite default limits for document size, depth,
  attributes, tokens, entity expansion, external resources, and evaluation work;
  cancellation and structured diagnostics. These extend beyond the BCL reader's
  document/entity character caps.
- **Command line:** XML/HTML linting, parsing, XPath, catalog, inclusion, and
  schema-validation commands, with Native AOT release builds on selected platforms.
  See the [CLI guide](docs/cli.md) for `xemele lint` inputs and diagnostic output.

BCL `XmlReader` and `XPathNavigator` adapters let existing .NET XML consumers
work with Xemele trees.

### What it does not do

- No XSLT engine, XPath 2.0/3.x language or built-in XPath 2.0 extension library,
  XSD 1.1, RELAX NG compact syntax, or automatic CLR object mapping.
- No full XPointer engine: XInclude supports shorthand IDs and `element()`,
  not arbitrary `xpointer()` expressions.
- No browser runtime: JavaScript, layout, navigation, CSS stylesheets, and
  resource downloading are outside scope (use [Jint](https://github.com/sebastienros/jint) for this). HTML has synchronous parsing APIs;
  its callbacks are lexical tokens, not corrected-DOM SAX events.
- No complete Selectors Level 4: `:has()`, `:is()`, `:where()`, pseudo-elements,
  UI/state pseudos, and attribute case flags are unsupported and rejected.
- No implicit filesystem/network access, transparent compressed-file loading,
  native C ABI, libxml2 binary compatibility, or native plugin/allocator hooks.

The [feature inventory](docs/features.md) compares libxml2 and the BCL's
`XDocument` with Xemele by capability, records profile limits and remaining
gaps, and links to source.
Feature presence is not a claim of identical APIs or universal conformance.

## Quick start

### Parse and query XML

```csharp
using Xemele;
using Xemele.XPath;

var document = XmlDocument.Parse(
    "<catalog><book price='12'>XML</book><book price='8'>HTML</book></catalog>");

Console.WriteLine(document.RootElement.FirstChild.GetTextContent()); // XML

var total = XPathExpression.Compile("sum(/catalog/book/@price)");
Console.WriteLine(total.Evaluate(document.RootElement).AsNumber()); // 20

foreach (var book in XPathExpression.Compile("/catalog/book").Evaluate(document.RootElement).AsNodeSet())
    Console.WriteLine(book.StringValue);
```

Compile expressions once when reusing them. Unprefixed XPath names match the
empty namespace; bind prefixes at compilation for namespaced documents.
XPath result membership and values are snapshots, not a live query.

### Build and serialize XML

```csharp
using Xemele;

var document = new XmlDocument();
var root = document.CreateElement("catalog");
document.DocumentNode.AppendChild(root);

var book = document.CreateElement("book");
book.SetAttribute("id", "42");
book.AppendChild(document.CreateText("XML & HTML"));
root.AppendChild(book);

Console.WriteLine(document.ToXmlString());
// <?xml version="1.0"?><catalog><book id="42">XML &amp; HTML</book></catalog>
```

`Save(Stream, options)` writes encoded XML without closing the caller's stream.
Nodes belong to one document; use `ImportNode` before attaching a node from a
different owner.

### Read XML without building a DOM

```csharp
using Xemele.Streaming;

using var input = new MemoryStream("<catalog><book>XML</book></catalog>"u8.ToArray());
using var reader = XmlReader.Create(input);
while (reader.Read())
{
    if (reader.TokenKind == XmlTokenKind.StartElement)
        Console.WriteLine(reader.LocalName.ToString());
    if (reader.TokenKind == XmlTokenKind.Text)
        Console.Out.Write(reader.ValueSpan);
}
Console.WriteLine();
```

Reader spans and attribute views expire on the next read or disposal. Copy only
values that must outlive the current event. Text can arrive in multiple chunks.
For event-driven or incremental
input, see the [SAX](samples/streaming/sax/Program.cs),
[push parser](samples/streaming/push/Program.cs), and
[writer](samples/streaming/writer/Program.cs) samples.

### Asynchronous XML

```csharp
using Xemele;

using var input = new MemoryStream("<root>async XML</root>"u8.ToArray());
var document = await XmlDocument.LoadAsync(input);
Console.WriteLine(document.RootElement.GetTextContent()); // async XML
```

The reader also supports `ReadAsync`; XML serialization, the writer, and SAX
have asynchronous APIs. Caller-owned primary streams remain open.

### HTML5 and CSS

```csharp
using Xemele.Html;
using Xemele.Css;

var document = Html5Parser.Parse("<main><p class='intro'>Hello <b>HTML</b></main>");
var selector = CssSelector.Compile("main > p.intro");
foreach (var paragraph in selector.Select(document.DocumentNode))
    Console.WriteLine(paragraph.GetTextContent()); // Hello HTML

Console.WriteLine(HtmlSerializer.Serialize(document));
```

Use `HtmlParser` instead of `Html5Parser` only when legacy recovery behavior is
required. HTML byte input uses HTML encoding detection, not XML declarations;
undeclared input falls back to windows-1252. HTML trees are separate from XML
trees. CSS supports combinators, attribute operators, structural pseudo-classes,
and restricted `:not()`; see the [exact grammar](docs/specs/html.md#exact-css-matrix).

### Validate against a schema

```csharp
using Xemele.Validation;

var schema = XmlSchema.Compile("""
    <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
      <xs:element name="quantity" type="xs:positiveInteger"/>
    </xs:schema>
    """);

var result = schema.Validate("<quantity>3</quantity>");
Console.WriteLine(result.IsValid); // True
foreach (var diagnostic in result.Diagnostics)
    Console.WriteLine(diagnostic);
```

Reuse compiled schemas. `RelaxNgSchema` and `SchematronSchema` provide the same
compile/validate entry-point pattern; `DtdSchema` handles DTD validation.
Malformed schemas throw `SchemaCompileException`; invalid instances return
results. Check completeness as well as validity when an operation hits a quota.
Validation does not mutate the instance or publish a PSVI.
See [DTD](samples/validation/dtd/Program.cs),
[XSD](samples/validation/xsd/Program.cs),
[RELAX NG](samples/validation/relax-ng/Program.cs), and
[Schematron/SVRL](samples/validation/schematron-svrl/Program.cs) samples.

### Canonical XML

```csharp
using System.Text;
using Xemele;
using Xemele.Serialization;

var document = XmlDocument.Parse("<root z='2' a='1'/>");
using var output = new MemoryStream();
XmlCanonicalizer.Canonicalize(document, output, new()
{
    Mode = XmlCanonicalizationMode.Exclusive10
});
Console.WriteLine(Encoding.UTF8.GetString(output.ToArray()));
// <root a="1" z="2"></root>
```

Canonicalization produces UTF-8 bytes, not a signature; cryptographic signing
and verification are separate concerns.

### BCL interoperability

```csharp
using Xemele;
using Xemele.Interop;

var document = XmlDocument.Parse("<root><item>value</item></root>");
var navigator = new XemeleXPathNavigatorAdapter(document);
Console.WriteLine(navigator.Evaluate("string(/root/item)")); // value
```

`XemeleXmlReaderAdapter` wraps a Xemele streaming reader for consumers expecting
`System.Xml.XmlReader`. The navigator is read-only; it does not turn Xemele into
the BCL DOM. See [adapter examples](samples/integration/adapters/Program.cs).

## Resource policy and diagnostics

DTD processing is **prohibited by default**. Opt in with
`DtdProcessing = XmlDtdProcessing.Parse`; external resources additionally require
an explicit `XmlResourceResolver`. Catalogs rewrite resolution requests, not
resource-access permissions. No ambient network/file resolver is installed.

```csharp
using Xemele;

var options = new XmlParseOptions
{
    Limits = new XmlResourceLimits { Depth = 64, DocumentCharacters = 1_000_000 }
};
var document = XmlDocument.Parse("<root/>", options);
Console.WriteLine(document.RootElement.LocalName); // root
```

Strict factories throw `XmlParseException` on rejected XML. `TryParse` and
`ParseWithDiagnostics` expose diagnostics without conflating recovered content
with well-formed input. Recovery requires `ParseWithDiagnostics` (or its loading
counterpart) and `Recover = true`; strict `Parse`, `Load`, and reader APIs reject
that setting. Inspect `IsWellFormed`, `IsComplete`, and diagnostics before using
recovered data. I/O errors and cancellation propagate.

## Benchmarks

See the [XML comparison](benchmarks/XmlComparison/README.md) and
[HTML comparison](benchmarks/HtmlComparison/README.md) for reproducible commands,
workload equivalence checks, and measurement boundaries.

### XML versus the BCL

Rerun October 15, 2026 on Apple M4 Pro, .NET 10.0.11 Arm64, with 10
measurement iterations per case. Input: an 8,271-byte catalog with 128 items.
Cells show **mean time / allocated KiB per operation**.

| Operation | BCL XmlDocument | BCL XDocument | Xemele |
| --- | ---: | ---: | ---: |
| Parse string | 35.700 us / 81.44 KiB | 28.347 us / 54.90 KiB | 33.386 us / 69.23 KiB |
| Serialize string | 15.369 us / 49.73 KiB | 15.624 us / 53.92 KiB | 45.972 us / 50.08 KiB |
| Compiled XPath sum | 8.883 us / 6.64 KiB | 10.346 us / 13.70 KiB | 6.517 us / 3.63 KiB |

| Streaming operation | BCL XmlReader | Xemele reader |
| --- | ---: | ---: |
| Consume UTF-8 input | 13.533 us / 14.85 KiB | 20.957 us / 1.27 KiB |

Xemele's parsing time and allocation are between the two BCL DOMs on this fixture.
String serialization takes about three times the BCL time; Xemele's pull reader
takes about 1.5x the BCL time while allocating over 11x less managed memory. Compiled XPath is faster than both BCL navigators and
allocates less; it reuses an owned document snapshot while the document is
unchanged. These local desktop measurements do not establish general speed ratios.
[Full results and methodology](benchmarks/XmlComparison/README.md#local-results-october-15-2026-rerun).

### HTML versus AngleSharp

Rerun October 15, 2026: Apple M4 Pro, macOS 15.8.1, .NET 10.0.11 Arm64,
BenchmarkDotNet 0.15.8 ShortRun with 10 measurement iterations, AngleSharp 1.8.2.
Both sides use equivalent HTML5 workloads; the legacy HTML parser is not used as
a faster substitute.

| Operation / input | AngleSharp time | Xemele time | AngleSharp allocated | Xemele allocated |
| --- | ---: | ---: | ---: | ---: |
| Parse / 229-byte page | 6.055 us | 2.350 us | 11.52 KiB | 6.04 KiB |
| Parse / 22,507-byte article page | 316.959 us | 145.027 us | 468.05 KiB | 338.99 KiB |
| CSS string query / article page | 17.736 us | 6.882 us | 1,256 B | 1,168 B |
| Serialize / article page | 102.901 us | 29.841 us | 181.53 KiB | 90.14 KiB |
| Contextual fragment / textarea | 1.684 us | 0.619 us | 6.75 KiB | 2.32 KiB |

Xemele is faster and allocates less across all measured parsing, stream loading,
contextual fragment, CSS string-selector query, and serialization workloads on
these fixtures.
[Full results, confidence intervals, and measurement context](benchmarks/HtmlComparison/results.md).

## Command line

The CLI is packaged as the `Xemele.Tool` .NET global tool and as standalone
archives. Publication is triggered by version tags; see the [release guide](tools/Packaging/README.md)
for validation and publishing steps. Once a version has been published, install
from NuGet.org with the .NET 10 SDK:

```sh
dotnet tool install --global Xemele.Tool
xemele --help
```

For preview versions, add the Feedz source shown above and use
`dotnet tool install --global Xemele.Tool --prerelease`.

For tagged releases, the installer automatically selects a Native AOT executable
on `linux-x64`, `osx-arm64` (macOS ARM64), and `win-x64`. Other .NET 10-supported targets use a
portable build and require the .NET 10 runtime. Installing the NuGet tool requires
the SDK even when its executable is native.
Continuous Feedz previews use the portable .NET 10 tool on every platform.

Standalone archives are attached to [GitHub releases](https://github.com/sebastienros/xemele/releases):
extract the matching native archive and run `./xemele` (`xemele.exe` on Windows)
without installing .NET. For other targets, extract the `any.zip` archive and run
`dotnet Xemele.Tool.dll`.

```sh
xemele format --input document.xml --output formatted.xml
xemele xpath --input document.xml --expression "count(//item)"
xemele validate-xsd --input document.xml --schema schema.xsd
xemele html5 --input page.html --output normalized.html
```

External resources require explicit grants such as `--allow-root`; DTD parsing
requires `--allow-dtd`.
See the [schema CLI guide](docs/cli.md) for validation flags and exit codes.

## Building and contributing

Runnable examples are grouped by feature in [samples](samples/README.md).
The [feature benchmark guide](benchmarks/Xemele.Benchmarks/README.md) distinguishes
the aggregate BenchmarkDotNet entry point from standalone diagnostic probes.

Only the .NET SDK selected by `global.json` is required to build and test:

```sh
dotnet build Xemele.slnx -c Release
dotnet test --solution Xemele.slnx -c Release --no-build
```

The first test-project build downloads the pinned XML corpus using .NET and
verifies the archive and every fixture hash. Subsequent builds reuse verified
local bytes; corrupt data fails explicitly. NuGet restore and the initial corpus
download need network access. No Python, Java, CMake, native compiler, or separate
runtime installation is needed for ordinary builds and tests.

For the additional CI API/provenance and test-runner checks:

```sh
dotnet run --project tools/Build -c Release --no-build -- verify
dotnet run --project tools/Build -c Release --no-build -- test
```

Live native/Java reference runs are isolated in the weekly/manual
[test-data workflow](.github/workflows/test-data.yml), which proposes reviewed
reference-result updates. See the [testing guide](docs/testing.md) for caching,
provenance, and maintenance details. Native AOT release packaging has separate
toolchain requirements.

See the [specifications](docs/specs/README.md) for behavior contracts,
the [samples](samples) for focused executable examples, and
[CI](.github/workflows/build.yml) for the full corpus/API/test pipeline.
Specifications describe profiles; runtime source and tests establish
what is implemented. Text is normalized to LF in Git; preserve the original
bytes of corpus, encoding, canonicalization, and frozen benchmark fixtures.
