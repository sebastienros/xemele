# HTML command usage

Build `src/Xemele.Tool/Xemele.Tool.csproj` with the .NET 10 SDK, then run its `Xemele.Tool.dll` or the published native `xemele` executable. The tool retains all seven XML commands and adds four statically registered HTML commands. Each command accepts `--help` alone.

```sh
xemele html-legacy --input page.html --mode legacy --output recovered.html
xemele html5 --input page.html --scripting false --mode html5 --output recovered.html
xemele html-fragment --input rows.html --context table --mode html5
xemele html-fragment --input rows.html --context-file page.html --context-selector '#rows'
xemele html-select --input page.html --profile html5 --selector 'ul > li:nth-child(2n)' --result markup
xemele html-select --input page.html --profile legacy --selector '.item' --result text
xemele html-select --input page.html --selector 's|circle' --css-namespace 's=http://www.w3.org/2000/svg'
```

`--input PATH` and `--output PATH` name local primary files. Omit them for stdin/stdout; there is no special `-` path value. Unknown, duplicate, missing and inapplicable options reject. Values are separate arguments, without `--option=value` syntax. An explicit file establishes no network/entity permission. HTML parsers do not fetch external DTDs/entities, scripts, images or any URLs, and do not execute JavaScript.

| Command | Parsing and required options | Output |
| --- | --- | --- |
| `html-legacy` | Pinned legacy HTML recovery; byte input uses the library legacy encoding profile | Full document HTML; default `--mode legacy` |
| `html5` | HTML5 document parsing; byte input uses WHATWG encoding sniffing | Full document HTML; default `--mode html5` |
| `html-fragment` | HTML5 string fragment parser; requires one of the context forms below | Fragment children only, without a wrapper; default `--mode html5` |
| `html-select` | `--selector CSS` required; `--profile html5|legacy`, default html5 | `--result markup|text`, default markup; unique selected elements in preorder, each followed by LF; no matches produce empty output |

All commands accept `--mode html5|legacy`. The default follows the parser profile; specifying it chooses serialization independently of parsing. HTML5 mode preserves the HTML algorithm's escaping, raw text, void elements, foreign elements and template contents; legacy mode uses its distinct output rules. Output is HTML and is never coerced to XML. `--result text` emits descendant text without markup or escaping (excluding comments, processing instructions and template content); embedded line breaks are preserved, so LF is an output separator rather than an unambiguous record framing protocol. Markup can likewise contain embedded LF. Serialization mode affects markup only.

HTML5 document/fragment/select accepts `--scripting true|false`, default false. It controls noscript parsing and serialization metadata only. Legacy parsing rejects this option, even with false. Other legacy/HTML5 differences follow the library's separate verified parser profiles.

## Fragment contexts

`--context TAG` creates a detached real context element, with optional `--context-namespace URI` (default XHTML `http://www.w3.org/1999/xhtml`) and `--quirks no-quirks|limited-quirks|quirks` (default no-quirks). This form has no attributes or ancestors. HTML namespace names use ASCII folding; foreign names retain casing.

Alternatively, `--context-file PATH --context-selector CSS` parses the context file with HTML5 byte input, the same scripting setting and finite limits, then requires exactly one matching element. This uses that actual element, including namespace, attributes, ancestor chain, ancestor form pointer, template context and parsed document quirks; the context tree is read without mutation. It rejects detached-context options. Repeatable `--css-namespace PREFIX=URI` is available for the context selector. Zero/multiple context matches reject.

The primary fragment input is strict UTF-8 (optional UTF-8 BOM), decoded to a bounded owned string because the frozen `ParseFragment` API has only a string overload. It does not sniff meta declarations, accept UTF-16 BOMs or apply windows-1252 fallback. The resulting node is a detached fragment with a fresh owner. Stream decoding checks cancellation between reads; the synchronous library string fragment parse, CSS selection, and individual string serialization have entry/exit or inter-result checks but no mid-call cancellation API. Ctrl-C uses the operation token for APIs that accept it. This limitation is specific to the existing synchronous APIs.

## CSS grammar and failures

Selector lists, universal/type selectors, ID/class selectors and attribute presence, `=`, `~=`, `|=`, `^=`, `$=`, `*=` are supported. Combinators are descendant whitespace, child `>`, adjacent sibling `+`, general sibling `~`. CSS identifiers/string escapes use the library's CSS syntax.

Supported pseudos: `:root`, `:scope`, `:empty`, `:first-child`, `:last-child`, `:only-child`, `:nth-child(an+b)`, `:nth-last-child(an+b)`, `:first-of-type`, `:last-of-type`, `:only-of-type`, `:nth-of-type(an+b)`, `:nth-last-of-type(an+b)`, and `:not(simple-selector-list)`. Negation permits compounds without combinators or nested `:not`. nth accepts odd/even, signed integer, an+b; the Level 4 `of selector` extension rejects.

Pseudo-elements, `:is/:where/:has`, `:lang/:dir`, state/UI/link/form pseudos, column combinators, attribute i/s flags and every unlisted feature reject the entire compilation with a zero-based UTF-16 offset. There are no ignored branches or partial results. `--css-namespace PREFIX=URI` is repeatable; each prefix must be unique. Empty prefix denotes the default type namespace. `prefix|name`, `*|name` and `|name` are supported; an unbound prefix rejects. Without a default mapping, unqualified types match any namespace; unqualified attributes always mean no namespace. HTML type/attribute name matching folds only ASCII in the HTML namespace; foreign names, ID/class/value and namespace comparisons remain ordinal. Traversal observes actual children, excluding template content implicitly.

## Limits, ownership and exit codes

Every command starts with finite library defaults: decoded document scalars 16,777,216; depth 256; per-token/aggregate attribute characters 1,048,576; attributes per element 256; interned names 65,536; expanded characters 16,777,216; diagnostics 128. `--max-characters N`, `--max-depth N`, `--max-token-characters N` replace those three limits with positive integers (depth/token must fit Int32; document limit fits Int64). Other limits retain their defaults. The same limits apply independently to a context document and its fragment operation. Fragment UTF-16 staging is bounded to twice the scalar quota plus two code units; the parser enforces exact scalar accounting.

CLI composition intentionally allocates retained DOMs/results, selector snapshots, the fragment string where required, and a final UTF-8 memory result. This is no streaming or whole-operation zero-allocation claim. Failed parsing/querying/serialization never publishes its staged result. Explicit output files use a same-directory temporary file followed by replacement, allowing input/output to name the same path and preserving an existing output on rejection. Publication to stdout can encounter I/O after writing begins. Caller-owned standard/borrowed streams remain open; only explicit file streams and staging resources are disposed.

| Exit | Meaning |
| --- | --- |
| 0 | Success, including no selector matches and prescribed HTML recovery |
| 1 | Selector syntax, quotas, context-match rejection, invalid fragment UTF-8 |
| 2 | Invalid invocation/options or explicit primary/context/output file I/O |
| 3 | Cancellation, standard-stream I/O, unexpected internal fault |

HTML failures go to stderr, with `HTML.CLI.<category>:` and a message; CSS syntax includes `offset=N`, structured legacy parse diagnostics include original code/line/column. Verified HTML5 engines currently expose quota faults as exact `InvalidOperationException` messages; CLI maps only the known quota messages to category `Quota`/exit 1. Unrelated algorithm faults remain exit 3. No stack trace is printed. XML command implementations and their existing messages are unchanged.

The owned compiled sample links the static registry and both CLI modules, exercises four HTML commands plus XML preservation, stream ownership through success/rejection/cancellation/I/O, and asserts `.NET 10.0.11`. Run its built DLL with the pinned host's `--fx-version 10.0.11`. The CLI adds zero library public API members, so there is no `docs/api/M7-cli.txt` fragment. Its command/output inventory is this document; the approved compiled API union verifies all real public fragments.
