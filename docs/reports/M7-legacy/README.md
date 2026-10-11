# Managed legacy HTML parser

HtmlParser implements the pinned libxml2 2.15.4 legacy recovery profile at
96498992efa48d52b0e8b83058bd88dbdaf153c1 using only the verified managed HTML
model. No native parser or System.Xml implementation is linked to the product.
Public string, borrowed byte-span and synchronous stream entries return fresh
owned documents. Streams remain caller-owned on completion, recovery, limit,
I/O and cancellation failures; the parser never resolves external resources.

The test-only native oracle uses NODEFDTD | NOERROR | NOWARNING | NONET and the
exact offline pinned engine with HTML/ISO8859X enabled and iconv/ICU/HTTP disabled.
Its full trees retain names, ordered attributes, text, comments and supplied
public/system doctype identifiers. Native absent namespaces map to the managed
HTML namespace by the frozen model contract. Adjacent native text runs normalize
to one comparison run; no text, element, attribute or comment is dropped.
Original 443 vectors remain intact. Subsequent vectors independently cover
malformed bytes, all built-in single-byte high-byte mappings, all 2231 named
references, name truncation, the 24 native growth boundaries, late sniff controls,
root envelopes, and actual native raw-short-read defects. Only the explicitly
verified legacy-root-envelope-v1 adapter changes topology; separately named
complete managed trees are retained in adaptations.json. No vectors are excluded.

Both byte entry points mirror the public native memory/coalesced-IO buffering
profile: 4000-byte requested reads, lexical prefetch at character-data64/name32
remaining bytes, and at most4064 owned transport bytes for streams. The borrowed
span is never copied or wholly decoded merely to use the string entry point.
Actually parsed meta declarations precede encoding selection; first non-ASCII
fallback searches only remaining currently available bytes, following the native
HTTP-EQUIV/CONTENT/CHARSET chain. Windows-1252 is the actual fallback. BOM wins;
unsupported real meta labels are ignored while a failed guessed label leaves raw
UTF8 recovery, as the pinned source does. Quoted content labels are honored.
Native raw callbacks capped1/3 expose BOM/comment defects, retained separately;
managed short reads coalesce and compare to the public coalesced profile.

Parser-only names retain at most100 UTF8 bytes, consuming/counting all source
scalars and skipping nonfitting scalars before allowing later narrower ones.
Programmatic model factories remain uncapped. Invalid selected UTF8 consumes one
byte per replacement; invalid UTF16/undefined single-byte table entries stop
conversion at the valid prefix, preserving the recovered tree. UTF32-looking
BOMs follow actual legacy detection (LE begins UTF16LE; BE is fallback data),
not XML decoding. Numeric references use the native C1/invalid-codepoint rules.

Limits validate/snapshot before transport; document and expanded counters count
scalars; tokens and aggregate attribute characters, accepted attributes, lexical
name storage, open stack and final tree remain bounded. Fatal quota diagnostics
carry normalized scalar locations and original byte offsets, absent for strings.
Nonrecoverable exceptions propagate. The quota-before-EOF test demonstrates
parsing progress without reading the full stream.

Transient names use borrowed alternate dictionary probes against operation-owned
storage. New names/text/attributes are explicit persistence allocations. Reused
scratch grows only within token quotas; transport and lookahead have operation
lifetime and results retain neither caller input nor scratch. Performance probes
compare owned allocated-name lookup with the actual borrowed pool, cached
SearchValues with a direct delimiter loop, and character/byte/chunk-stream DOM
composition. Cold setup, warm primitives, owned DOM/new-name growth and retained
whole-heap domains are separate. Measurements remain pending until the coordinator
reviews the frozen source and grants an exclusive lease.
