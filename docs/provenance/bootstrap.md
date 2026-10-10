# Original bootstrap corpus

`tests/corpora/bootstrap/` contains two original UTF-8 XML byte sequences authored
for corpus infrastructure checks. No external corpus or implementation is copied.
The source URL identifies this repository; the immutable revision is the SHA-256
content identity of the checked-in source bundle, not a claim of an upstream tag
or a successful network download. The manifest pins that bundle and each fixture.
The fixture MIT grant and attribution are in `NOTICE.txt` alongside the data.

Expectations were authored independently: `<root/>` closes its only element;
`<root>` lacks the required end tag. These classifications do not constitute
execution of Xemele, a reference parser, or an XML standards test suite.

The additive `sourcePath` field selects a local immutable bundle relative to the
manifest. Adapter `xemele-offline-bundle-v1` reads a version 1 JSON object whose
`files` array contains exact manifest-relative `path` and base64 `base64` bytes.
The bundle's inventory and every fixture hash must match before import/provision.
Import checks the source SHA-256 and stages it under
`artifacts/corpora/sources/<source-sha256>.json`. Provision uses that cache only;
it never silently substitutes checkout fixtures or fetches a missing source.
Verified fixture bytes go to
`artifacts/corpora/fixtures/<source-sha256>/<fixture-sha256>`.

`exclusion` is an optional nonempty per-fixture reason. It is a reported exclusion,
never a pass; no bootstrap fixture is excluded. Unsupported profiles, missing or
corrupt source cache, and infrastructure failures are distinct per-case outcomes.
Provenance metadata is carried into the report, not independently certified by
the tool. Future external modules must supply their own license review and pinned
sources; this deliverable does not download future standards corpora.
