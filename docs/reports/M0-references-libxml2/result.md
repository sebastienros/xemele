# libxml2 reference result

Source: `150ed0e3eeceea0dfe030186cdd9dbbb59211aca`. Upstream libxml2 2.15.4 at
`96498992efa48d52b0e8b83058bd88dbdaf153c1`. See `provenance.json` for all flags,
actual enabled feature macros and source/license/artifact hashes. CMake 3.31.6,
Apple Clang 17.0.0 (clang-1700.6.3.2), GNU Make 3.81, macOS SDK26.2 arm64,
deployment target15.0. No system libxml2 dependency; bridge depends on libSystem.
Two fresh native builds produced identical static-library and bridge SHA256;
`reproducibility.json` records the comparison.

With repo-pinned .NET10.0.400 on PATH/DOTNET_ROOT, run:

```
python3 -B docs/reports/M0-references-libxml2/verify.py
```

Actual check results (full transcript in `verification.txt`):

- 11 native/BCL normalized fixture comparisons and 5 supported envelope cases.
- Direct production UTF16 ordinal regression, including supplementary/BMP order.
- Direct native-loss reproduction plus 16 affected envelope cases explicitly
  unsupported; UTF8/BOM, UTF16LE/BE, Latin1, declarations, Misc/comments/PIs/DTD,
  CRLF and inside-root text. BCL remains the oracle for unsupported v1 cases.
- 14 malformed/unsupported/oversized requests with lifecycle recovery; 14 XML
  rejections; 65 resolver cases including mapped success, nested/canonical URLs,
  absent maps, file/network denial and no subsequent request resource leakage.
- Native entity amplification and adapter event quota failures are limit failures,
  not syntax verdicts; raw malformed UTF8 wire exits2 without response; huge
  response identity falls back to bounded failure. Unsupported encoding is explicit.
- Preserved protocol verifier: 51 focused tests and 54 solution infrastructure
  passes; 3 expected controlled-failure exits2, CLI/build/pack/API checks,
  13 discovery tests, 2 independent corpus fixtures; 101-task validation passes.

Reference capability limits are explicit in the runner README. Document-level
whitespace unsupported responses do not remove any Xemele whitespace requirement.
Native non-ASCII namespace IRI rejection is preserved as an actual engine difference.
Only macOS arm64 was exercised. No parser allocation/performance timing applies
and no native runtime package dependency was added. Integration verification is
left to the coordinator; only task-owned files and the task record changed.
