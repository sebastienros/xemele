# XML comparison with the BCL

Standalone .NET 10 BenchmarkDotNet comparison of Xemele,
`System.Xml.XmlDocument`, `System.Xml.Linq.XDocument`, and
`System.Xml.XmlReader`. No dependency is added to the shipped library.
Use the SDK selected by `global.json`, with the .NET 10.0.11 runtime installed.

```sh
dotnet run --project benchmarks/XmlComparison -c Release -- --qualify
dotnet run --project benchmarks/XmlComparison -c Release -- --filter '*' --iterationCount 10 --artifacts artifacts/readme-xml-20261015-rerun --exporters json
```

Eleven cases use `MemoryDiagnoser` and `ShortRunJob`; the command above overrides
the measurement iteration count to 10. The deterministic ASCII fixture has 128
items, two attributes and a title each: 8,271 characters/UTF-8 bytes, SHA-256
`646593ecd3d16705a6efca7d609e6b04d886d4eec8a03dcf687377b1a918d728`.
Input generation is outside timing. Qualification checks ordered item
IDs/prices/text, exact complete serialized output, the compiled XPath sum, and
reader start-element counts. Setup rejects a mismatch before measurements.

- Parsing starts from the same string and returns a complete owned DOM.
  `XmlDocument` uses a fresh BCL reader/settings with DTDs prohibited and a null
  resolver; `XDocument` preserves whitespace. Xemele uses its finite default
  options. All trees contain the same data.
- Serialization starts from prebuilt documents and returns the same complete
  owned XML string, with no declaration, indentation or stream encoding. Each
  library's public string serialization API is measured, including setup and
  allocation performed by that API.
- XPath evaluates precompiled `sum(/catalog/item/@price)` on prebuilt documents.
  Navigators and expressions are created outside timing. Xemele's public
  evaluation snapshots the tree under resource budgets; BCL navigators evaluate
  over their existing trees. This measures end-to-end API costs, not an isolated
  expression interpreter with identical ownership semantics.
- Reader cases wrap the same pre-encoded UTF-8 bytes in a fresh `MemoryStream`,
  consume all tokens, and return the start-element count (257). Readers, options,
  stream wrappers and their disposal are inside timing. There is no DOM, disk or
  network I/O. This does not measure attribute copying or user callback work.

Results are returned to BenchmarkDotNet. Defaults and ownership costs are not
disabled to produce a favorable ratio. This fixture is not a conformance corpus
or a substitute for mixed namespaces, DTDs, Unicode, large inputs, async I/O,
retained-memory or native/Rust comparisons.

## Local results: October 15, 2026 rerun

These historical measurements predate the repository history squash and used
the adjacent `Benchmarks.cs`. Only the root README had uncommitted edits before
the run; runtime code, benchmark code and fixture bytes were unchanged.
The existing net10.0 target and pinned .NET 10.0.11 runtime were used.

Apple M4 Pro, 14 physical/logical cores; macOS Sequoia 15.8.1 (24H32),
Darwin 24.6.0; 48 GiB RAM; SDK 10.0.400; .NET 10.0.11 Arm64 RyuJIT armv8.0-a;
BenchmarkDotNet 0.15.8, concurrent workstation GC. ShortRun was overridden to use
one launch, three warmups and 10 measurement iterations. Default outlier removal
left 8-10 samples per case. Qualification passed and all 11 cases completed in
127.27 seconds. The host denied high-priority scheduling; this is shared local
desktop hardware on AC power in the normal power mode, with no CPU affinity or
tiering/PGO/GC environment overrides.

Times are mean microseconds/op. Error is BDN's **99.9% confidence-interval
half-width**, not standard deviation. KiB = 1024 managed bytes/op. These are
single-run fixture-specific measurements, not dedicated-hardware regression gates.

| Operation | Library | Mean (us) | Error (us) | Allocated (KiB/op) |
| --- | --- | ---: | ---: | ---: |
| Parse string | BCL XmlDocument | 35.700 | 1.502 | 81.44 |
| Parse string | BCL XDocument | 28.347 | 4.312 | 54.90 |
| Parse string | Xemele | 33.386 | 0.231 | 69.23 |
| Serialize string | BCL XmlDocument | 15.369 | 0.169 | 49.73 |
| Serialize string | BCL XDocument | 15.624 | 0.176 | 53.92 |
| Serialize string | Xemele | 45.972 | 0.653 | 50.08 |
| Compiled XPath sum | BCL XmlDocument | 8.883 | 0.077 | 6.64 |
| Compiled XPath sum | BCL XDocument | 10.346 | 0.158 | 13.70 |
| Compiled XPath sum | Xemele | 6.517 | 0.028 | 3.63 |
| Consume UTF-8 reader | BCL XmlReader | 13.533 | 0.136 | 14.85 |
| Consume UTF-8 reader | Xemele | 20.957 | 0.132 | 1.27 |

On this fixture Xemele's parse time and allocation fall between the two BCL DOMs.
XML string output takes about three times the BCL time and does not reproduce the
HTML serializer's measured advantage against AngleSharp. Compiled XPath is faster
than both BCL navigators and allocates less; it reuses an owned document snapshot
while the document is unchanged. Xemele's reader takes about 1.5x the BCL time while allocating over 11x less managed memory here. No broad speed claim or runtime regression conclusion follows.

Raw BDN logs, CSV/HTML and full JSON are generated under
`artifacts/readme-xml-20261015-rerun/` (ignored); this table preserves all case means,
error bars and allocations. The [HTML comparison](../HtmlComparison/README.md) separately
measures AngleSharp on HTML5 workloads and must not be mixed with these XML rows.
