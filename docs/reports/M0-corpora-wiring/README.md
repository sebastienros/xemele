# Root corpus command wiring

The root CorpusManager entry point calls the verified internal `CorpusCommand.Run`
in the same assembly. It retains default/help aliases and exit 2 for unknown or
invalid commands. Import validates pinned local bundles and provision uses cache
only; nonempty all-provisioned inventories exit 0. Missing/corrupt cache,
unsupported profiles, exclusions, infrastructure errors and empty inventories exit
1. Reports preserve manifest provenance and distinguish all six provisioning
outcomes; parser execution and standards passes remain zero.

Run with SDK 10.0.400 on PATH (and DOTNET_ROOT where the installation requires it):

```sh
python3 -B docs/reports/M0-corpora-wiring/check_cli.py
python3 -B tests/Xemele.TestUtilities/Corpora/verify_bootstrap.py
```

The first command explicitly restores/builds the root CLI and outside-solution
focused MTP app, verifies 24 actual test results, then exercises the root executable
with positive and negative cases. Only the existing symlink capability test may
explicitly skip when unavailable; its reason is recorded. All fixture/cache
mutations happen under disposable ignored artifacts. Commands, host, revision,
stdout/stderr and exact exits are recorded in
`artifacts/M0-corpora-wiring/verification.txt`; CI uploads this folder and fresh
focused XML alongside the existing test/package artifacts. SDK paths are supplied
by the caller, with no committed host-specific installation path.

Preservation verification also runs `docs/reports/M0-corpora/verify.py`: solution
and focused-app builds, independent bootstrap checks, discovery tests, existing
MTP tests and controlled failures, shell help/error checks, managed-only package,
and task validation. This is provisioning infrastructure evidence, with no parser
conformance, hot-path allocation, benchmark, or unrun hosted-platform claim.
