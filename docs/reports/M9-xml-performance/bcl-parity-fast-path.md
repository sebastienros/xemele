# BCL parity: optimistic whole-buffer UTF-8 DOM fast path

Branch `codex/xml-performance-bcl-parity`, based on `codex/xml-performance`.

## Change

`XmlDocument.Load(Stream)` and `Parse(ReadOnlySpan<byte>)` first try
`OptimisticDomParser`, a single-pass parser over the whole UTF-8 buffer that
writes the node, attribute, namespace and location arenas directly. It is
deliberately narrow: anything unusual or erroneous makes it return `false`
and the unchanged streaming parser runs from the start, so diagnostics,
locations and limits stay authoritative.

Accepted by the fast path:

- ASCII names with at most one colon; LF line breaks; optional UTF-8 BOM; a
  strict UTF-8 XML declaration.
- Predefined and numeric character references (control characters, surrogates,
  noncharacters and values above 10FFFF bail).
- Comments, processing instructions, CDATA, namespaces, attributes.

Bails out to the streaming parser:

- DOCTYPE and general entities, CR/CRLF, non-ASCII names, `Recover`, any
  error. UTF-16 input, async load and SAX are not routed through it.

Techniques: vectorized `IndexOfAny` scanning for text, quote and name ends;
single-pass UTF-8 to UTF-16 text decoding with line tracking; presized arenas;
an ASCII short-value cache and name table; no per-scalar state machine.

`Load(Stream)` uses it only for seekable streams without a source URI.
`MemoryStream` buffers are used zero-copy; others are read into a pooled
buffer. Cancellation is checked per read, and the stream is rewound on
fallback.

## Validation

- Differential harness comparing the fast-path tree with the streaming
  parser's tree (nodes, attributes, namespaces, locations, validation
  metadata, revisions, quota counters) over about 194k generated and mutated
  inputs: 0 divergences.
- Permanent tests: `tests/Xemele.Tests/M9-xml-performance/OptimisticParserTests.cs`
  (accepted-input differential, rejected-input fallback, mutation fuzz, limits,
  stream behavior).
- `Xemele.Tests`: all pass except `PreparedAffineAsciiDispatchAllocatesZero`
  and `PreparedBorrowedTextDispatchAllocatesZero`, which fail on the baseline
  too. Conformance 920/920, differential 618/618.

## Results

UTF-8 DOM, µs per parse, median of 7 alternating-order rounds
(`Runner paired`, before = saved baseline DLL, default-context isolation).
Ratios are candidate / BCL.

| Fixture | Baseline | Now | BCL | Base/BCL | Now/BCL | Alloc now/BCL |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| feed-small | 54 | 17 | 17 | 3.2 | 1.01 | 0.81 |
| svg-small | 203 | 61 | 107 | 1.9 | 0.57 | 0.59 |
| deep-medium | 566 | 149 | 255 | 2.2 | 0.59 | 1.00 |
| entity-medium | 4675 | 4663 | 1726 | 2.7 | 2.70 | 0.71 |
| xhtml-medium | 3064 | 745 | 1371 | 2.2 | 0.54 | 0.92 |
| build-medium | 3573 | 1158 | 2529 | 1.4 | 0.46 | 0.58 |
| soap-large | 13144 | 2961 | 5565 | 2.4 | 0.53 | 1.14 |
| configuration-large | 17882 | 5779 | 14441 | 1.2 | 0.40 | 0.62 |
| business-large | 65039 | 25265 | 44626 | 1.5 | 0.57 | 0.80 |
| text-large | 19958 | 7970 | 17678 | 1.1 | 0.45 | 1.00 |
| **Geomean** | | | | **1.81** | **0.64** | |

Now/baseline geomean is 0.34 (about 2.9x faster). The ratio versus BCL
reproduced in an independent `quick` run (0.64) with the order reversed.

Retained DOM memory (compacted held minus released heap, `MemoryWorker`) is
unchanged versus the baseline on every fixture. Xemele/BCL geomean is 0.87;
soap-large is 1.14, matching its allocation ratio, so that gap is the DOM's
node and member representation size, not parser transients.

## Other input paths (unchanged by this work)

| Path | Geomean Xemele/BCL |
| --- | ---: |
| UTF-8 DOM (fast path) | 0.64 |
| UTF-16 / char DOM | 1.73 (text-large 0.63) |
| UTF-8 SAX vs BCL `XmlReader` | 16.8 |

## Known gaps

1. DOCTYPE and entity documents (`entity-medium`, 2.7x BCL). A fast path needs
   the real `DtdParser` model plus identical `ExpandedCharacters`,
   `EntityDepth` and `InternedNames` accounting. It was judged too risky for
   this change.
2. CR/CRLF input (needs byte-offset remapping) and non-ASCII names.
3. `Parse(string)` / char input and async load still use the streaming parser.
4. SAX consumption is 10-20x slower than the BCL reader.
5. soap-large allocation and retention at 1.14x BCL.
6. BenchmarkDotNet ShortRun via `default_context.py` has not been run for
   this revision; numbers above come from the in-process runner.
