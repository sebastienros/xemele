# Command-line interface

## Linting XML and HTML

```sh
xemele lint document.xml
xemele lint pages/ schemas/ another.xml
cat document.xml | xemele lint
cat page.html | xemele lint --type html
xemele lint --type xml document.txt
```

Like [markdownlint's `mdl`](https://github.com/markdownlint/markdownlint),
`lint` accepts positional files, recursively scanned directories, and stdin when
no paths are given. `-` explicitly reads stdin and may be mixed with file paths
once. Use `--` before paths beginning with `-`. Explicit files may have any
extension. Directory discovery includes `.xml`, `.xhtml`, `.svg`, `.xsd`, `.rng`,
`.sch`, `.xsl`, `.xslt`, `.html`, and `.htm` (case-insensitive), including hidden
files, but does not follow symlinks. Directory results are sorted ordinally;
explicit inputs retain their order, and repeated absolute file paths are checked
only once. A directory with no matching files succeeds silently.

`--type auto` (default) selects the HTML5 parser for `.html`/`.htm` and XML for
everything else, including stdin. `--type xml|html` overrides parser selection.
XML reports recoverable syntax issues and terminal parser diagnostics. HTML reports
the existing HTML5 **tokenizer** parse errors using the tree builder's parsing
context (including raw text, SVG, and MathML); it is not a full HTML conformance,
structural, accessibility, schema, or style-rule validator. Schema files are
checked as XML, not compiled. No Markdown rules, mdl configuration files, Ruby
styles, custom rulesets, or automatic fixes are supported.

Findings go to **stdout**, one per line, ordered by source line and column:

```text
page.html:3: HTML.duplicate-attribute duplicate attribute
```

Stdin is labeled `(stdin)`; an unknown source line is `0`. Embedded newlines in
messages and paths are escaped. Clean inputs produce no output and files are never
rewritten. Invocation/file errors, cancellation, and internal failures go to
stderr without stack traces. Findings in one file do not stop subsequent files;
file failures also allow remaining inputs to be checked.

Exit codes: **0** clean; **1** findings (including parser quotas); **2** invalid
arguments or file/I/O failure; **3** cancellation or unexpected failure. The
highest applicable code wins. `xemele lint --help` succeeds with usage information.
Unlike other Xemele commands, lint findings intentionally use stdout to match mdl.

Finite parser limits and byte-input encoding detection remain in effect. HTML
diagnostics are bounded to 128 per file, reserving the final slot for an explicit
limit notice; XML uses its existing bounded diagnostic buffer. `--allow-dtd`
enables XML DTD syntax only; it does not permit external resource access.
Neither parser downloads resources or executes scripts. Ctrl-C cancels the run,
and stdin remains open.

## Schema validation

```
xemele validate-rng --schema order.rng --input order.xml
xemele validate-xsd --schema order.xsd --input order.xml --diagnostics json
xemele validate-schematron --schema order.sch --input order.xml --phase production --svrl order.svrl.xml
```

All commands require `--schema PATH`; without `--input PATH` they read stdin.
`--schema-base-uri URI` must be absolute and overrides the schema file's base.
Explicit primary input/schema/catalog files do not grant access to nested resources.
Use `--allow-root DIRECTORY` for the existing guarded local-file policy and
`--catalog PATH` for catalog routing through that policy. Network access is denied.

Diagnostics go to stderr in ordered text (default) or `--diagnostics json` form.
JSON has `inputUri`, `isComplete`, `isValid`, and `diagnostics`; each diagnostic
contains `code`, `severity`, `message`, and `location` with `sourceUri`,
`lineNumber`, `linePosition`, and `byteOffset`. Nullable source/byte positions
remain null; a named input URI is recorded separately, never substituted for
a diagnostic source. Library stream results may have no source URI.

Positive finite integer budgets are independent: `--compile-max-operations`
and `--validation-max-operations` default to 10,000,000; `--compile-max-components`
defaults to 100,000. Nested schemas share `--compile-max-resources` (64) and
`--compile-max-resource-bytes` (16,777,216). Other frozen parser/XPath limits
retain their finite defaults. There is no unlimited option.

Schematron accepts `--phase ID` and `--svrl PATH`; `--svrl -` writes UTF-8 SVRL
to stdout. A complete valid or warning-only operation stages the full report
before replacing a requested file atomically. Invalid, incomplete, cancelled,
or failed operations leave an existing target unchanged. Observable stdout write
exceptions return3 and may have emitted bytes that cannot be rolled back. On the
reviewed Unix/.NET runtime, standard console output deliberately suppresses a
broken pipe (`EPIPE`); exit0 reports validation success and cannot guarantee that
the downstream consumer received the staged SVRL.

Exit codes: 0 complete valid/warning-only; 1 schema/instance/resource/quota
rejection or incomplete result; 2 invocation/file failure or unknown phase;
3 cancellation or unexpected failure. Ctrl-C cancels the current operation.

These commands use existing public validation APIs and internal static registry
hooks; no library API rows are added.
