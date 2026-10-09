# Input and parser contracts — M1/M2

Decision owners: `M1-contracts` (strict subset), `M2-contracts` (full XML). A resumable engine is required internally in M1 even though public push/reader APIs ship in M3.

## Internal separation

```csharp
internal enum ParseStep { NeedInput, ProducedEvent, Completed }
internal interface IXmlInputDecoder
{
    DecodeResult Decode(ReadOnlySpan<byte> input, Span<char> output, bool isFinalBlock);
}
internal interface IXmlEventSink
{
    void StartElement(XmlStartElementEvent element);
    void EndElement(XmlEndElementEvent element);
    void Text(ReadOnlySpan<char> text);
    // Contract fixture includes document, lexical, and diagnostic callbacks.
}
internal sealed class XmlParserCore : IDisposable
{
    public XmlParserCore(IXmlEventSink sink, XmlParseOptions options);
    public ParseStep Append(ReadOnlySpan<char> input, bool isFinalBlock);
}
```

The event types here are internal M1 types, distinct from the public M3 callback views. The contract must define consumed/written counts, decoder states, source mapping, and sink backpressure before the decoder/scanner lanes begin. `ParseStep` is illustrative: suspend before emitting another event when the sink requests it, so a pull reader can consume one token without buffering the rest of a chunk. Span data never escapes the current call. Decoder failure is distinct from needing more bytes. Input routing handles string input without inventing a source encoding.

`M1-input` owns decoding/transcoding only. `M1-syntax` owns scanner/grammar/state only. `M1-dom` composes them through their contracts. Scanning retains only unfinished tokens and open-element state, not the complete input. Long text is chunked; partial names/attributes obey token limits. No DOM allocation occurs in the core.

## M1 supported slice

UTF-8 and UTF-16 with BOM/declaration checks; XML declarations; one root; namespace-aware names/attributes; text; predefined/numeric references; comments, CDATA, processing instructions, line normalization. Validate scalars and document structure. Unsupported DTD syntax is rejected with a dedicated diagnostic. All results must be correct within this slice; M1 need not claim full conformance.

The M1 sink contract defines namespace scope, attribute ordering/identity and normalized values, empty-element start/end, and lexical boundaries. Construct a tree independently to test serializer behavior; do not make tests circular by using the unverified parser as the only oracle.

## M2 additions

- `M2-dtd`: internal/external subset grammar, declarations and parameter entities; emits an immutable `DtdModel` and default/type declarations. This parses declarations; document validity is M5.
- `M2-resolver`: implements the resolver boundary and bounded resolution session in [integration](integration.md); DTD/parser code has no direct filesystem/network calls.
- `M2-entities`: general entity expansion, recursion/output budgets, defaults/normalization, entity references, ID tracking, and the nonvalidating XML rules. Depends on DTD and resolver lanes.
- `M2-encoding`: agreed additional encoding table and incremental detection/conversion tests, preserving M1 UTF behavior.
- `M2-conformance`: remaining grammar/namespace edge cases and full nonvalidating applicable suite; owns ordered updates to the M1 syntax lane.
- `M2-recovery`: opt-in recovery policy and diagnostic results, with deterministic examples for every repair rule. Strict behavior remains unchanged.

DTD and recovery integration use a contract-approved internal hook provided by the core; no dependency on future validators. A second independent parser is not an acceptable recovery implementation.

## Required evidence

Character/name boundary tests; all short-input chunk splits; malformed byte sequences; split surrogate/scalar cases; decoder locations; namespace collisions; normalization; duplicate attributes; entity budgets; prohibited external access; strict/recovery differential fixtures. Full corpus cases remain visible failures until their assigned milestone completes them.
