# Xemele HTML5 versus AngleSharp

Standalone .NET 10 comparison; no benchmark package enters the shipped library.
AngleSharp 1.8.2 and BenchmarkDotNet 0.15.8 are pinned in this project. Run from
the repository root using the SDK selected by `global.json` (10.0.400) and the
installed .NET 10.0.11 runtime. Ensure that this SDK's `dotnet` is first on PATH
so BenchmarkDotNet child builds use it too.

```sh
dotnet build benchmarks/HtmlComparison/HtmlComparison.csproj -c Release
dotnet benchmarks/HtmlComparison/bin/Release/net10.0/HtmlComparison.dll --qualify
dotnet benchmarks/HtmlComparison/bin/Release/net10.0/HtmlComparison.dll --list flat
dotnet benchmarks/HtmlComparison/bin/Release/net10.0/HtmlComparison.dll --filter '*' --iterationCount 10 --artifacts artifacts/readme-html-20261015-rerun --exporters json
```

Use `--filter '*ParseBenchmarks*'` for document and stream parsing, or a particular
class (`'*QueryBenchmarks*'`, `'*FragmentBenchmarks*'`, `'*SerializeBenchmarks*'`).
The complete run contains 30 cases: five classes, three parameter values, two libraries.
`MemoryDiagnoser` and `ShortRunJob` apply to every class; the command above
overrides the measurement iteration count to 10, retaining one launch and three
warmups. Do not add `--cli` without checking job discovery: BDN can add another
job alongside ShortRun.

The deterministic corpus is generated outside timing in `Fixtures.cs`. It includes
small HTML, a 128-article page, and malformed markup with foreign content/templates.
Fragments use table, textarea and template contexts. UTF-8 stream inputs have an
explicit meta charset; both sides wrap the same prepared byte array in a new
MemoryStream per operation. No file/network I/O is measured. The AngleSharp parser
is reused, while its document is newly created each time; Xemele exposes static
parse APIs. Scripting is disabled on both sides. Default limits/options otherwise
remain enabled, including Xemele's resource checks.

Query measures each public string-selector API on prebuilt documents, returning
ordered result snapshots. Xemele calls `CssSelector.Compile(selector).Select(...)`
and AngleSharp calls `QuerySelectorAll(string)`, including each library's default
internal selector caching. No caller-supplied
compiled selector is used. Serialization creates a full owned document string on
both sides. Fragment measures each public API's native returned container (AngleSharp
node list versus Xemele fragment), including ownership costs. Results are returned
to BDN. No normalization, fixture generation or oracle checks occur inside timing.

GlobalSetup rejects non-equivalent measured workloads: structural snapshots compare
node kinds, namespaces, local names, values, expanded attributes, child order,
doctype identifiers, separate template contents and document quirks mode. Output
must be byte-for-byte equal as .NET strings; queries compare ordered subtrees.
The standalone qualifier also asserts documented feature differences. Unexpected
failures exit nonzero. These focused probes do not establish full HTML5 conformance.

See [full results, feature and measurement analysis](results.md).
Raw BDN artifacts are ignored. Commit summary tables and analysis only.
