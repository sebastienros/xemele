The pinned metadata identifies nine exact external DTD resources from the W3C
2013-09-23 XML test-suite archive, with original case IDs, byte counts and SHA-256.
Bodies remain in the private offline cache and retain their upstream licenses;
this directory does not redistribute or relicense them. Provision through
`python3 -B docs/reports/M1-corpus-import/provision.py` when needed.

Expected declaration models and subset accept/reject outcomes are authored in
`PinnedTests.cs`. Every short UTF-16 split is exercised. These tests parse the
identified external subset resources, not the containing XML documents; they
make no full XML conformance claim. Authored join tests cover composed default
application and framing/source protocols separately.
