# M7-tokenizer work evidence

Owned incremental scalar state machine and public owned-token string adapter.
The implementation keeps the current token and fixed-size reference/declaration
candidates, never the whole input. Callback tokens own immutable snapshots.
Input CR/CRLF preprocessing, split surrogates, scalar source locations and
nullable source-byte maps are retained across Feed calls. The tree builder can
change text state and CDATA allowance synchronously during token delivery.

Current WHATWG https://html.spec.whatwg.org/multipage/parsing.html retrieved
2026-10-11 SHA-256 `34e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf`
requires processing-instruction states (§13.2.5.72–76), entered unconditionally
from '?' in Tag open. The originally frozen six-kind model cannot represent
those tokens. The coordinator is arranging the ordered additive PI contract/model
repair. No earlier standard is substituted. This branch cannot claim implemented
until the verified repair is integrated and every current corpus case passes.

Pinned full fixture corpus and classification: tests/fixtures/M7-tokenizer/README.md.
All 7,049 tokenizer state vectors run with every chunk split. Parse errors remain
separate from token comparison. Encoding sniffing/byte decoding is parser
composition, not a string tokenizer API; its original resources remain pinned
for that owner and the final gate.

Performance status: no allocation or timing measurements have executed. Borrowed
lexical classification/references and owned callback/result persistence will be
measured separately only after independent review and the coordinator's exclusive
prepared durable lease, using the existing model freeze/wrapper lifecycle.

Actual pinned ordinary builds: SDK10.0.400, RuntimeFrameworkVersion10.0.11.
Actual test execution uses `dotnet --fx-version 10.0.11 ...Xemele.Tests.dll`.
Initial full corpus run fails at pending PI states as expected; no exclusion.
