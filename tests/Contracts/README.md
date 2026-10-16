# Contract checks

`Consumers/` contains nonpackaged internal and source-subset consumers, grouped by
`xml` and `validation`. These are compatibility checks, not public API examples.
Projects declaring an XML-only property can also compile against the corresponding
`tests/SourceSubsets` project; preserve that isolation when changing dependencies.

`Foundation/` and `Fixtures/foundation/` exercise compiled API normalization and
its rejecting mutation controls. CI runs them through:

```sh
python3 -B tools/ApiBaseline/verify.py
```

The single API extractor lives in `tools/ApiBaseline/Checker`; runtime fragments
live in `docs/api`. XPath execution vectors have one canonical inventory at
`tests/corpora/xpath/inventory.json`.

The remaining `Fixtures/` inputs support isolated compatibility or provenance
checks. Do not treat declaration bodies as runtime implementations, and do not
regenerate a golden baseline to hide an unintended change.
