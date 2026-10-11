# HTML tokenizer callback composition

The exact `docs/specs/html.md` ownership freeze and
`tests/Contracts/Fixtures/M7/Tokenizer.cs.fixture` supersede the task's older
"borrowed lifetimes" wording. The approved callback is
`Html5Tokenizer.Tokenize(string, Action<HtmlToken>, HtmlTokenizerOptions?, CancellationToken)`.
M7-tokenizer already shipped and verified that overload. This task adds consumer
composition and regression evidence, without a second handler, public stream or
push entry point, borrowed view, or runtime adapter. The API inventory forbids
empty fragments, so this task supplies no fragment; the callback declaration
remains owned by `docs/api/M7-tokenizer.txt`. No new public members are introduced.

Each callback runs synchronously in lexical order and receives an immutable owned
snapshot. All seven kinds, including processing instructions and exactly one
successful EOF, use the same lifetime: consumers may keep tokens after callback
return and operation completion. Attribute collections cannot mutate backing
storage. Callback exceptions, including `XmlParseException`, escape as the same
object, immediately ending delivery. Cancellation/quota failure may follow already
delivered prefix tokens; it does not promise rollback or a successful EOF.

These are lexical tokens, not corrected DOM or XML SAX events. For example,
`<table>outside<tr><td>inside</table>` delivers the character data `outside` after
the table start token. An HTML5 tree builder can foster-parent that text, insert
implied elements and correct end tags. Standalone tokenization performs none of
that tree construction; consumers must not interpret callbacks as the final tree.
RCDATA/RAWTEXT/script initial states require the last start-tag name; public Data
is the default. Only the existing internal tokenizer is used to test chunk feeds
and foreign CDATA cooperation, without promising any additional public API.

`CallbackIntegrationTests` covers all seven retained kinds, ordered attributes,
PI retention, callback thread/synchrony, all five public initial states at every
split and single-unit chunks, surrogate/CRLF/reference/tag boundaries, internal
CDATA, early delivery before completion/quota failure, exception identity at
every delivery including EOF, and cancellation before/during processing.
The collection regression drops 512 emitted token snapshots while keeping the
tokenizer and callback alive, then requires all weak references to be collectible
before completing the operation. This detects a document/token-list accumulator;
it is not a measurement of exclusive heap bytes or a throughput benchmark.

Source review confirms bounded current-token builders/attributes, a 1024-character
text batch, fixed maximum named-reference scratch, scalar quota counters and no
DOM or accumulating output list in callback-only tokenization. Owned token
construction allocates by contract. No runtime source changed, so the verified
[tokenizer measurements](../M7-tokenizer/measurements.md) remain the relevant
allocation evidence: 98,304 borrowed primitive operations at 0 B, separately
reported owned callback/list/chunk allocations, and inclusive retained heap delta
468,000 B for 128 retained lists. Those figures are single-host Arm64 evidence,
not a callback-wide zero-allocation or precise per-token retention claim. No
duplicate measurements, performance optimization or x64 claim is made here.

The public sample uses only verified dependencies: it counts lexical starts/EOF
and keeps one PI snapshot without building a document or accumulating all tokens.
M7-builder/M7-corpus continue to own corrected DOM and byte-decoding composition.

## Branch checks

Pinned SDK 10.0.400 and .NET runtime 10.0.11 on macOS Arm64. Release test,
sample and approved M0 API checker builds passed with zero warnings/errors,
`RuntimeFrameworkVersion=10.0.11`, `UseSharedCompilation=false` and disabled build
servers. Offline restores used `tests/Contracts/Fixtures/M7` with NuGet audit
disabled. Test restore/build initially stalled under sandbox restrictions and
were cancelled; escalated executions completed. An initial test analyzer error
(xUnit2030) was fixed. The first focused execution passed 25/26 checks: the
authored fixture incorrectly expected an unquoted trailing slash to set the
self-closing flag. Adding the required whitespace corrected that expectation;
no runtime change was needed.

Final explicit `dotnet --fx-version 10.0.11 ...Xemele.Tests.dll -namespace
Xemele.Tests.HtmlCallbacks -namespace Xemele.Tests.HtmlTokenization -parallelMode
none -noColor -result-xml artifacts/M7-callbacks/tests.xml` passed all 26 tests
(11 callback checks and 15 unchanged tokenizer checks), zero failures/skips.
The dependency checks include all pinned tokenizer state vectors and authored PI
witnesses at every boundary. The public sample printed
`Lexical starts=3; EOF=1; retained PI=build:v1`.

Official `tools/ApiBaseline/compare.py --report
artifacts/M7-callbacks/runtime-api.json` with the pinned host on PATH matched all
1,210 compiled members, bootstrap false and 3,388 corpus fixtures. Discovery
initially rejected absent ignored W3C resources; copying and SHA256-verifying all
3,386 original resources from the existing cache restored the physical inventory.
The source cache remains intact; nothing was fetched or deleted. No public API
fragment was added. Planning validation passed 167 tasks and diff whitespace
checks passed. No cumulative suite, new allocation or timing measurement ran.
