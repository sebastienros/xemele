# Windows test path construction repair

Actual Windows run38454912150 failed 38 original schema cases and four allocation parent tests at path guards, before packaging. `Path.Combine(root, "tests/fixtures/...")` and `Path.Combine(root, "artifacts/.../child-runs")` preserved forward slashes inside segments, while the guard compared against native normalized paths. The four child logs confirm the confinement guard rejected the request; these were not timeouts.

Ten path constructions across five test files now pass each directory segment separately to `Path.Combine`. Every other source byte in those files is unchanged, verified against the parent revision. All canonical path, descendant boundary, token, parent identity, fingerprint, exact child invocation, real/positive allocation thresholds and original schema expectations remain unchanged. No runtime, fixture or API source changed.

Pinned SDK10.0.400/.NET10.0.11 local build and 43 focused checks passed on macOSarm64: 39 unchanged original schema/hash checks plus four allocation parent facts, each requiring its actual real and positive child. Seven commands completed with clean direct-child/process-group/log receipts. The first sandboxed offline restore timed out; that failed receipt is retained, and an elevated read-only inspection confirmed its process group absent. The normal-permission bounded retry passed.

Windows execution remains unrun after this repair. Its cumulative suite and package/trim/NativeAOT/tool results still require a separately authorized fresh run. Valid Linux/macOS hosted package evidence is preserved; this harness-only change does not alter package payloads. M9 distribution and the final release gate remain blocked; no extra push/run or fuzz work occurred.
