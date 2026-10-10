# Declaration version correction

`verify.py` compiles only explicit frozen model inputs and the owned version
probes. It compares five public model baselines, executes inherited model checks,
checks four internal declaration/sink/result shapes against the freshly built
runtime (including nullable and readonly metadata, each exercised by a compiled
negative mutation), compares the actual product's approved public API against an exact
temporary copy of every approved API fragment (hashes recorded; shared discovery
validates this API-only snapshot, without requiring corpus materialization), requires two specific
compiler rejections and detects an actual public metadata mutation. It then runs
the exact inherited 4594-test Xemele.Tests suite and planning tests/validation plus ownership and
whitespace audits. Raw compiler outputs/XML/builds are held in an auto-cleaned
temporary directory under this owned report path (so inherited allocation
children can locate repository markers); all newly created allocation child files
are inspected and removed while pre-existing outputs remain intact. Stdout supplies concise outcomes/identities only. Existing
historical reports are preserved. No corpus runner, benchmark or active peer
branch is required. This is model/preservation evidence, not a corrected runtime
parser or full XML conformance claim.

Run with SDK 10.0.400 selected in PATH and DOTNET_ROOT, and
`DOTNET_CLI_USE_MSBUILD_SERVER=0`:

```sh
python3 -B docs/reports/M2-version-contract/verify.py
```

The bounded regex model covers only declarations, with exact ASCII digits,
quotes, whitespace and field ordering. Separate scalar/namespace/newline/quota
predicates illustrate effective-profile constraints; they do not execute a
streaming parser, verify callback pause/cancellation/quota enforcement or prove
arbitrary body grammar. Runtime enforcement and all-split tests remain with
M2-version-declarations and its implementing owners.

Required runtime transitions routed to M2-version-declarations (no edits here):

- `src/Xemele/Parsing/Core/XmlParserCore.cs`, `ParseDeclaration`: replace the
  `value.SequenceEqual("1.0")` guard and canonical `version = "1.0"` assignment
  with bounded ASCII VersionNum recognition and exact lexical callback retention.
- `src/Xemele/Parsing/Dtd/Framing/XmlTextDeclarationFramer.cs`, `Process`:
  replace fixed `valueIndex != 3`, the `"1.0"[valueIndex]` guard and canonical
  result string with incremental ASCII VersionNum recognition and owned exact
  lexical digits. Preserve charging before retention, mapping, body boundary,
  cancellation and null version when absent.
- `tests/Xemele.Tests/M1-syntax/SyntaxTests.cs`: move
  `<?xml version='1.1'?><r/>` out of XML.Syntax negative InlineData to positive
  callback/split/pause coverage under XML 1.0 content rules.
- `tests/Xemele.Tests/M2-dtd-framing/FramingTests.cs`: move
  `<?xml version='1.1' encoding='X'?>` out of the invalid array to valid lexical
  framing coverage; decoder-supported encoding remains a separate check.

W3C primary sources consulted: [XML 1.0 Fifth Edition, section 2.8](https://www.w3.org/TR/REC-xml/#sec-prolog-dtd)
(productions 23–26 and compatible-1.x processor note), [section 4.3.1](https://www.w3.org/TR/REC-xml/#sec-TextDecl)
(production 77), [section 2.2](https://www.w3.org/TR/REC-xml/#charsets)
(production 2), [section 2.11](https://www.w3.org/TR/REC-xml/#sec-line-ends),
and [Namespaces 1.0 Third Edition sections 5 and 6](https://www.w3.org/TR/REC-xml-names/#ns-qualnames).
Source-purpose/VERSION metadata is retained as historical intent; effective
expectations require declaration and content evaluation under these editions.

For integration, pass `--audit-base <full integration-base hash>` and
`--expected-branch <integration branch>`. The claim and verified prerequisite
ancestry guards still apply. Runtime/test/build inputs are hashed against committed
Git and checked unchanged from the audit base. Product/test output closures are
hashed before/after execution; exact test names have a durable aggregate identity.
SDK host hash/runtime versions are included. These checks preserve inherited
behavior and do not assert the runtime declaration correction is implemented.
