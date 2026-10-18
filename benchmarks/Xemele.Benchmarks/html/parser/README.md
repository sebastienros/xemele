# HTML5 parser scaling and reference benchmarks

`DeepNestingBenchmarks` parses unlimited-depth documents (`Limits.Depth = null`)
whose open-element stack grows with N. `ReferenceAttributeBenchmarks` parses a
128-item list whose attribute values carry terminated references (`dense`) or
occasional ones (`sparse`). Both classes use `[MemoryDiagnoser, ShortRunJob]`.

```sh
dotnet benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll --filter '*DeepNestingBenchmarks*' '*ReferenceAttributeBenchmarks*' --warmupCount 10
```

## Changes measured (2026-10-18)

Apple M4 Pro, macOS 15.8.1, .NET 10.0.11 Arm64, SDK 10.0.400, BDN 0.15.8.
This was a shared desktop, so some runs overlapped other builds. Baseline is
`2d2007bf1`. Raw BDN logs are not committed.

1. **Open-element marks and per-name open counts.** `HtmlNode.Open` is a byte
   field, and it fits existing padding: an `HtmlNode` is still 152 B. It replaces
   `stack.Contains`/`IndexOf`/`Remove` in reconstruction, adoption and removals.
   The builder also keeps counts of open `p`, `select`, `button`, `nobr`, `ruby`
   and `template` elements, so a scope query for an absent name fails in O(1).
   Foster-parent location stops at the nearer of template and table. The builder
   gains 24 B per parse.
2. **Terminated references in attribute values.** The whole-tag fast path now
   resolves `&name;` when the longest match is the whole name. It also resolves
   numeric references of up to seven digits that need no replacement. Neither
   form can raise an error or trigger the attribute no-semicolon rule. Any other
   `&` still falls back to the per-scalar path.
3. **Common-entity shortcut.** `amp; quot; lt; gt; nbsp;` are checked before the
   named-reference trie.
4. **Reused name scratch.** `NameString` used a per-call `stackalloc char[64]`,
   which is zeroed on every call because the assembly does not skip locals
   init. It now uses a thread-static 64-char buffer.
5. **`in Token`.** Tree-builder methods take the 6-field token struct by
   readonly reference instead of copying it through each frame.

Deep nesting, BDN mean (allocation unchanged within 1 KB):

| Shape | N | Baseline | After | After, 10k ÷ 1k |
| --- | ---: | ---: | ---: | ---: |
| adoption | 1,000 | 1.384 ms | 0.215 ms | |
| adoption | 10,000 | 138.5 ms | 2.689 ms | 12.5× |
| adoption | 50,000 | 3,478 ms | 42.05 ms | |
| reconstruct | 1,000 | 1.670 ms | 0.237 ms | |
| reconstruct | 10,000 | 225.0 ms | 6.567 ms | 27.8× |
| reconstruct | 50,000 | 5,602 ms | 54.10 ms | |
| blocks | 1,000 | 0.759 ms | 0.121 ms | |
| blocks | 10,000 | 80.87 ms | 1.872 ms | 15.5× |
| blocks | 50,000 | 1,962 ms | 24.23 ms | |

The baseline grows about 100× per 10× N, which is quadratic. After the change,
growth is roughly linear: between 10k and 50k it is 15.6×, 8.2× and 12.9× for 5×
N. The remaining superlinearity is GC: profiles at 50k show only GC and parser
work, with no stack scans.

`HtmlComparison` Xemele cases, BDN with 10 warmups and 10 iterations. The runs
alternated baseline, branch, baseline, branch, and the table shows the mean of
the two runs:

| Operation | Fixture | Baseline | After | Ratio | Allocation before → after |
| --- | --- | ---: | ---: | ---: | ---: |
| String parse | Article | 133.75 μs | 123.29 μs | 0.92 | 338.99 → 339.01 KiB |
| String parse | RecoveryForeign | 5.393 μs | 5.270 μs | 0.98 | 15.33 → 15.35 KiB |
| String parse | Small | 2.066 μs | 1.963 μs | 0.95 | 6.04 → 6.06 KiB |
| UTF-8 stream parse | Article | 141.24 μs | 125.90 μs | 0.89 | 339.28 → 339.30 KiB |
| UTF-8 stream parse | RecoveryForeign | 5.594 μs | 5.398 μs | 0.96 | 15.52 → 15.55 KiB |
| UTF-8 stream parse | Small | 2.287 μs | 2.173 μs | 0.95 | 6.23 → 6.26 KiB |
| Fragment | Table | 769.2 ns | 711.0 ns | 0.92 | 3.75 → 3.77 KiB |
| Fragment | Template | 1,085.5 ns | 1,041.7 ns | 0.96 | 4.70 → 4.73 KiB |
| Fragment | Textarea | 474.7 ns | 454.9 ns | 0.96 | 2.32 → 2.34 KiB |

Reference attributes, BDN ShortRun: `dense` went from 258.3 μs (251.9 KB) to
127.8 μs (240.6 KB). `sparse` went from 92.0 ± 18.3 μs to 94.7 ± 6.9 μs, which is
within the baseline's error. The interleaved check below measures the same page
as 9% faster.

Per-change attribution comes from an interleaved A/B harness. It loads two
`Xemele.dll` builds into separate load contexts, warms each for 1 s, and then
alternates 150 ms slices for 21 rounds. The values below are median paired
ratios; below 1 is faster:

| Change | Small | Article | RecoveryForeign | Dense references |
| --- | ---: | ---: | ---: | ---: |
| 1 open marks/counts† | 1.010 | 1.080 | 1.014 | 1.001 |
| 2 attribute references† | 0.993 | 1.002 | 1.030 | 0.494 |
| 3 common entities | 0.995 | 0.989 | 1.007 | 0.873 |
| 4 reused name scratch | 0.981 / 0.974* | 0.976 / 0.972* | 0.979 / 0.978* | 0.981 / 0.975* |
| 5 `in Token` | 0.971 / 0.966* | 0.972 / 0.953* | 0.991 / 0.979* | 0.970 / 0.971* |
| all, against baseline | 0.948 | 0.922 | 0.998 | 0.416 |

\* The second figure is the inverse of a repeat run with the order reversed.
† Measured while other builds loaded the host (load average up to 25), with
interquartile paired ratios spanning about ±10%. Treat these as no measurable
fixture change; the deep-nesting table is the evidence for change 1.

Measured no-gos:

- Probing `KnownNames` directly when a name has no upper-case ASCII
  (`ContainsAnyInRange`), which avoids the lowering copy: paired ratio
  0.991–1.011, so it was not kept.
- Per-token rare-state resets do not apply here. Tokens are fresh immutable
  `record struct`s, and the builder clears no per-token state. The only
  per-token cost found was the struct copy, which change 5 removes.

Correctness oracles: html5lib tokenizer and tree-construction corpora run at
every chunk boundary. `AttributeReferencesMatchThePerScalarPath` compares
whole-input tokens, token locations, diagnostics and quota exceptions against
one-character feeds for 57 reference forms in 8 tag shapes under 3 limit sets.
`UnlimitedDeepNestingKeepsTreeConstructionResults` checks that adoption,
reconstruction, scope and form cases build the same subtree under 5,000 divs
as under one. A deliberately wrong `p` count fails the tree corpus, and a
deliberately accepted error reference fails the attribute oracle.
