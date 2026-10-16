# Feature benchmarks

The aggregate BenchmarkDotNet entry point discovers the feature benchmark classes:

```sh
dotnet run --project benchmarks/Xemele.Benchmarks -c Release -- --list flat
dotnet run --project benchmarks/Xemele.Benchmarks -c Release -- --filter '*Writer*'
```

Code is grouped into `xml`, `streaming`, `xpath`, `validation`, `integration`,
`html`, and `schemas`. Standalone `*.Probe.csproj` projects provide explicit
diagnostic entry points where a process needs its own counters or input mode;
they are not automatically executed by the aggregate or CI. Run them only when
investigating the corresponding feature, on an otherwise idle host.

For end-to-end public API comparisons, use the
[XML comparison](../XmlComparison/README.md) and
[HTML comparison](../HtmlComparison/README.md). Measurement boundaries and
required allocation evidence are in the [performance guide](../../docs/performance.md).
