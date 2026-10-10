# Full XML suite import and M1 metadata classification

The pinned official W3C 20130923 archive contains 3,697 members (3,386 regular
files, 311 directory entries; 2,722,978 source bytes). Every file is in the
verified offline transport bundle: primary XML, external entities/DTDs, output
oracles, catalogs, stylesheets, contributor notices and other supporting files.
No filtering/deduplication of bytes or metadata identities occurs.

`tests/corpora/xml/inventory.json` is the parent consumer contract (schema 1).
Each case has `id = metadataPath#sourceId`, original attributes/defaults,
metadata source line, description, original contained input URI and SHA-256,
output paths, and `m1.outcome/reasons/contractRejection`. The unique source
identities retain all 2,585 tests even when bytes are shared. Source counts:
812 valid, 242 invalid, 1,498 not-well-formed, 33 optional-error. There are 323
external-entity metadata cases; VERSION attributes include 271 XML 1.1,
396 XML 1.0 and 1,918 unspecified/all. All primary/output metadata references
exist in the archive. Every archive member and per-file checksum is recorded.

Catalog discovery expands only contained local catalog/DTD entities, applies
entity document base URIs and nested xml:base, and reads catalog DTD defaults.
No product parser is referenced. A separate BCL catalog-only reader independently
checks the complete authoritative identity/type set. The importer never parses
malformed test documents to generate an outcome oracle.

The inventory profile provisions bytes only. Existing schema requires every
file to have an accepted/rejected label; `manifest.json` labels all files
`accepted` as **transport inventory**, including resource/catalog/oracle files.
These labels are not parser expectations. Consumers MUST use `inventory.json`
case `m1` metadata for product expectations. No exclusions occur in transport;
the infrastructure verifies/provisions all 3,386 files. Its `Provisioned` means
checksum-verified bytes, not accepted XML or standards success.

M1 classification uses the frozen XML 1.0 Fifth Edition/namespaced UTF-8/UTF-16
subset. It records unsupported-profile for DTD, external entity, validity,
XML 1.1/NS 1.1, historical edition, namespace-disabled, other encoding and
unspecified optional-error oracles. Reason counts overlap. `contractRejection`
is a separate anticipated scope-rejection diagnostic for DTD cases without
encoding/version/signature gaps. When declaration or byte-decoder failure can
precede DOCTYPE, it remains null; metadata inspection does not predict the first
runtime error. In particular the EUC-JP Japanese case does not assert
XML.DtdProhibited. This field never turns a standards gap into a pass. Pure
malformed M1 syntax,
unknown general references and malformed encoding names remain supported
rejections. Comments/CDATA/PIs do not create false DTD requirements. Local
`xmlns` element/ordinary prefixed attribute names are not feature exclusions.
All upstream valid tests contain DOCTYPE, so no authoritative supported accepted
cases remain in this strict subset. Bootstrap acceptance coverage stays intact.
No feature decision uses a Xemele outcome. Zero product cases executed and zero
standards passes, including provisioning/check tests.

The archive and base64 bundle are ignored local data because source
redistribution terms conflict; see the retained [notice](../../../tests/corpora/xml/NOTICE.md).
The immutable archive is 641,522 bytes (SHA-256 recorded in notice/inventory).
The local bundle duplicates about 2.7 MB as base64 solely to use the existing
verified schema/adapter without shared-code changes. It is reproducible and is
not committed. Complete generated metadata and all per-file hashes are committed.

Use pinned SDK 10.0.400 with `DOTNET_CLI_USE_MSBUILD_SERVER=0`, and prepend
`/Users/sebastienros/Library/Application Support/dotnetup/dotnet` to PATH.

```sh
python3 -B docs/reports/M1-corpus-import/provision.py --download
python3 -B docs/reports/M1-corpus-import/import_suite.py --check
python3 -B docs/reports/M1-corpus-import/verify.py
```

`--download` only accesses the pinned official URL when no local or preserved
archive exists. All checks are offline once it is present. `verify.py` builds
owned runner/test apps against the **actual verified core**, tests discovery,
checksums and positive/negative provisioning, checks bootstrap discovery, and
preserves a verified cache outside this disposable worktree:

`/private/tmp/xemele-corpus-cache/w3c-xml-20130923/`

That directory contains original archive, manifest/inventory/notice/bundle,
actual infrastructure hash-addressed source/fixture cache, and `resources/`
with **all original URI paths**. The runtime join reads an input at
`resources/<case.inputPath>` and resolves relative resources from that URI;
resource maps use `inventory.files` and each SHA-256. No network resolution is
required. Check every required byte against its inventory hash before execution.
`provision.py --preserve` recreates it after an owned runner Release build.
To restore a deleted checkout's ignored data, `provision.py` copies the archived
cache back and regenerates/verifies metadata/bundle without download.

For integration verification, pass `verify.py --audit-base <pre-merge-main-commit>`.
The default is the original worker claim. The verifier resolves that commit,
checks whitespace and owned paths across its committed `base..HEAD` delta, and
separately checks working/staged/untracked paths. This prevents unrelated root
coordination changes since the original claim from entering an integration audit.
`--help` and invalid CLI arguments exit before changing verification transcripts.

Redistribution clarification remains a pre-publication follow-up. No package,
runtime, public API or benchmark implementation is changed by this task.
