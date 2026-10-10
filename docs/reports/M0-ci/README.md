# M0 CI deliverable

Implementation: `.github/workflows/build.yml` and
`tools/ApiBaseline/discover.py`. Versioned producer conventions and the precise
boundary between discovery and compiled API verification are documented in
`tools/ApiBaseline/README.md`. Focused stdlib tests live in
`tests/Xemele.TestUtilities/Build/test_discovery.py`.

The implementation consumes only the verified M0-build bootstrap. No runtime
APIs, corpus manifests, shared project edits or later milestone dependencies are
introduced. Actual checkout discovery reports zero declarations/manifests and
an empty-runtime bootstrap exemption. Default discovery fails on this checkout
until a real API fragment exists. No conformance/API compatibility pass is
inferred from that empty inventory.

The task JSON records exact tested revisions and outcomes. Windows/Linux and
hosted CI runs remain unexecuted locally. Runtime allocation/performance checks
are inapplicable: this deliverable is offline Python validation and CI wiring.
