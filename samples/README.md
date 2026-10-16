# Samples

Run examples with the SDK selected by `global.json`:

```sh
dotnet run --project samples/xml/dom/Sample.csproj -c Release
dotnet run --project samples/streaming/reader/ReaderSample.csproj -c Release
dotnet run --project samples/validation/xsd/Xsd.Sample.csproj -c Release
dotnet run --project samples/validation/relax-ng/RelaxNg.Sample.csproj -c Release
```

| Directory | Examples |
| --- | --- |
| [XML](xml/) | DOM, names, encoding, entities, resource policy and recovery |
| [Streaming](streaming/) | Reader, SAX callbacks, push parser, writer and asynchronous I/O |
| [XPath](xpath/) | Compilation, evaluation, navigation and functions |
| [HTML](html/) | HTML5 and legacy parsing, tree construction, tokens, CSS and output |
| [Validation](validation/) | DTD, XSD, RELAX NG and Schematron/SVRL |
| [Serialization](serialization/) | Canonical XML |
| [Integration](integration/) | Catalogs, XInclude, BCL adapters and source identity |
| [CLI](cli/) | Source-built XML and HTML command composition |
| [XmlToolkit](XmlToolkit/) | A complete scenario combining several public APIs |

Internal source-subset and declaration-model consumers live under
[`tests/Contracts/Consumers`](../tests/Contracts/Consumers/), not among the public
examples. The schema command-line reference is in [the CLI guide](../docs/cli.md).
