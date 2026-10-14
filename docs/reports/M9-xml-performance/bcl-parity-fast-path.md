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
3. Async load and stream-based SAX still use the streaming parser.
4. SAX over in-memory input is now about 1.3-2.9x the BCL reader (see the SAX follow-up).
5. soap-large allocation and retention at 1.14x BCL.
6. BenchmarkDotNet ShortRun via `default_context.py` has not been run for
   this revision; numbers above come from the in-process runner.

## Follow-up: UTF-16 (char) DOM fast path

`XmlDocument.TryParse(ReadOnlySpan<char>)` now tries the optimistic parser first. The input is validated and
transcoded to UTF-8 (`Utf8.FromUtf16` with `replaceInvalidSequences: false`, so lone surrogates bail to the
streaming parser), then parsed by the same fast path with byte offsets recorded as absent. Inputs starting with
U+FEFF and `Recover` mode bail.

Paired result (char DOM, ratio vs BCL, geomean): 1.73 -> 0.69. Entity-medium is unchanged (2.61); text-large is
0.74-0.82 (was 0.63) because of the transcode cost. Differential harness: 0 divergences over 388k inputs.

## Follow-up: SAX fast path (`XmlSaxParser.Parse(string | ReadOnlySpan<byte>)`)

`OptimisticSax` (`src/Xemele/Streaming/Sax/OptimisticSaxParser.cs`) handles in-memory input for the same well-formed
subset as the DOM fast path, in two passes over one buffer:

1. A validating pass delivers nothing. It checks the grammar, namespace bindings, attribute uniqueness, characters
   and the token/name/depth/reference limits. A document it declines is handed, untouched, to the streaming parser,
   so errors, diagnostics, recovery and every unusual construct keep their authoritative behavior.
2. A delivery pass re-walks the buffer and calls the handler. It cannot fail (a failure is an
   `InvalidOperationException`, never duplicated events). Attribute values are decoded into a pooled `char[]` and
   exposed through a second backing of `XmlAttributeView`; text is delivered in bounded chunks.

Public API is unchanged. `Parse(Stream)` and the async entry points stay on the streaming parser, because the
design requires that SAX over a stream not retain the whole input. `Parse(string)` transcodes to a UTF-8 buffer
(as the char DOM path does) and `Parse(ReadOnlySpan<byte>)` reads the caller's span directly; both are borrowed,
already-in-memory inputs, so no memory contract changes.

Event equivalence, including locations, empty-element and CDATA handling, whitespace outside the root, and
declaration data, is checked against the streaming parser with text callbacks coalesced (text callback boundaries
are not a contract). Differential harness: 0 divergences over 518k inputs (149k accepted by both) for UTF-8 and
UTF-16 under default, tight and quota-edge limits. `OptimisticSaxTests` adds 90 permanent tests.

| Fixture | before us | UTF-8 after us | UTF-16 after us | BCL reader us | after/BCL | alloc after | alloc BCL |
|---|---:|---:|---:|---:|---:|---:|---:|
| feed-small | 159 | 11 | 12 | 8 | 1.38 | 3,904 | 20,376 |
| svg-small | 435 | 62 | 61 | 25 | 2.46 | 4,384 | 15,728 |
| deep-medium | 2,651 | 139 | 141 | 105 | 1.33 | 12,832 | 134,472 |
| entity-medium | 13,997 | 13,925 | 13,764 | 1,203 | 11.57 | 534,522 | 1,263,241 |
| xhtml-medium | 9,527 | 923 | 990 | 544 | 1.70 | 4,384 | 315,848 |
| build-medium | 9,597 | 1,452 | 1,486 | 515 | 2.82 | 3,921 | 34,424 |
| soap-large | 33,416 | 3,066 | 3,182 | 2,204 | 1.39 | 4,673 | 1,043,970 |
| configuration-large | 42,948 | 6,212 | 6,480 | 2,405 | 2.58 | 4,194 | 57,994 |
| business-large | 184,484 | 16,268 | 16,638 | 8,886 | 1.83 | 4,038 | 4,925,991 |
| text-large | 392,530 | 12,959 | 16,627 | 15,881 | 0.82 | 3,189 | 14,714,028 |

Geomean vs the BCL `XmlReader` drops from about 17x to 2.05x (1.7x without entity-medium, which still takes the
streaming path). Allocation is roughly 4 KB per parse regardless of document size, below the BCL reader on every
fixture. The speedups (7-30x) are far outside the same-DLL control noise, so no reversed-order repeat was needed.
Splitting the cost: the validation pass alone runs at about BCL-reader speed, so the remaining gap is the delivery
pass. Taping pass-1 tokens would remove most of the repeated scanning and hashing; it was left out for complexity.

## Follow-up: review fixes, line breaks, and repeatability

### Review fixes
- **Token limits.** PI targets, closing-tag whitespace and whitespace runs outside the root are charged against
  `TokenCharacters` exactly as the streaming parser does (tight limits of 5/8/11/14/20 are part of the harnesses).
- **Bounded stack.** PI target length no longer controls an unbounded `stackalloc`.
- **Name-table probing** is bounded (`MaxProbes = 256`); pathological collisions bail to the streaming parser.
- **Cancellation.** A seekable stream's final read honors the token. In SAX, the token is checked after every
  delivery callback (`StartDocument`, declaration, text, comment, PI, CDATA, start/end element), so cancelling from a
  handler stops delivery like the streaming parser. It is deliberately not checked after `EndDocument`: the streaming
  parser completes successfully in that case, and a test documents it.

### CR and CRLF
Input containing CR is rewritten to LF in a pooled buffer (`FastLineBreaks`), recording the output index of every LF
whose CR was folded. Line/column are identical after normalization (CR, LF and CRLF are each one break); byte offsets
are mapped back as `index + (folded CRLFs before it)`. Quota prechecks and the reference budget use the raw length.
`&#13;` references still bail to the streaming parser. Pretty-printed (CRLF) copies of the fixtures keep the same
ratios as the LF versions: DOM geomean 0.67x BCL, SAX 2.04x BCL reader.

### Repeatability
Same-DLL control noise aside, BenchmarkDotNet runs in normal and `--reverse` (after-first) order agree:

| | normal | reversed |
|---|---:|---:|
| geomean after/BCL | 0.662 | 0.669 |
| geomean before/BCL | 1.84 | 1.80 |
| geomean after/before | 0.35 | 0.35 |

Per-fixture after/BCL agrees within about 0.05 (build 0.47, business 0.56, configuration 0.40, deep 0.58,
entity-medium 2.55, feed-small 1.02, soap 0.54, svg 0.58, text-large 0.66, xhtml 0.51).

### Validation
DOM harness: 1.04M inputs, SAX harness: 1.17M inputs (including CR/CRLF/mixed variants), 0 divergences. Full
`Xemele.Tests` run: only the two pre-existing failures (`PreparedAffineAsciiDispatchAllocatesZero`,
`PreparedBorrowedTextDispatchAllocatesZero`), which also fail on the base commit.

### Not done
- DOCTYPE/entity input (entity-medium: 2.6x BCL in DOM, ~11x in SAX) still uses the streaming parser.
- Non-ASCII element/attribute names bail to the streaming parser.
- SAX token tape (est. ~30% off the delivery pass) is not implemented.
- Retained-memory (`memory.py`) was not re-run for this revision.
