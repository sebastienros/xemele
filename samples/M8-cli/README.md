# Schema CLI

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
