# Package third-party notices

Original managed implementation: Xemele contributors, MIT (included LICENSE).
No native/Rust reference parser, upstream conformance corpus, benchmark/test
framework or Roslyn assembly is included as a runtime package dependency.

Generated HTML named character-reference data and foreign-name normalization
maps derive from the WHATWG HTML Standard. Attribution: WHATWG contributors.
Pinned source: https://html.spec.whatwg.org/entities.json, SHA256
 d741d877ac77c4194c4ad526b5b4a19aef8dfe411ab840a466891cdbb9f362e6,
retrieved2026-10-11. Foreign-name parsing source:
https://html.spec.whatwg.org/multipage/parsing.html, SHA256
34e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf;
extracted dataSHA2566c7f80b3ea63b1b8ecfbce525dee21cdcbe7fad5f865fd96fc16dbd358eb993c.
The verified model provenance records these standard data under CC-BY-4.0:
https://creativecommons.org/licenses/by/4.0/ . Generated C# formatting and
length-dispatched matching adapt the original tables; this does not imply WHATWG
endorsement. These data retain their attribution/license; the original code's MIT
license does not relicense the standard data.

.NET runtime/toolchain components used to execute/build/trim/AOT retain their
own Microsoft license and notices. They are not packed into the managed library
or dotnet-tool archive. Self-contained consumer distributions must retain the
runtime's accompanying notices. See the pinned SDK/runtime package metadata.
