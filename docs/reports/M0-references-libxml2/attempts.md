# Check history

The final source check is at `150ed0e3eeceea0dfe030186cdd9dbbb59211aca`;
`verification.txt` supersedes draft attempts. No unrun check is marked passing.

- Initial sandbox fetch failed DNS; authorized fetch succeeded from GNOME at
  exact `96498992efa48d52b0e8b83058bd88dbdaf153c1`. Branch switch initially lacked
  shared Git administration write access; authorized switch succeeded.
- Draft native dependency check misread the bridge's own install name as host
  libxml2 linkage; corrected. macOS deployment targets aligned and explicit SDK,
  architecture/compiler flags pinned. Clean builds now have identical hashes.
- Draft BCL comparison exposed missing DTD-default attributes; enabled native
  XML_PARSE_DTDATTR, then verified real default values against BCL.
- Draft broad probes exposed positive native reads with namespace errors; the
  per-reader structured handler now preserves native errors and rejects them.
- Known-revision d695593 verifier failed the supplementary namespace IRI equality
  assertion after that error capture. Upstream parser reports XML_WAR_NS_URI via
  xmlNsErr. Final verifier preserves native rejection and BCL acceptance rather
  than suppressing native errors. Production ordinal sorting is directly tested.
- Draft preserved .NET tests were blocked by sandbox test IPC (exit -6); the
  authorized rerun passed. Expected controlled-failure smoke tests still exit2
  and are asserted as failures by the inherited verifier.
- Native-loss probe reproduced omitted top-level whitespace. Coordinator approved
  explicit unsupported capability reporting without changing v1/BCL semantics.
  Final post-native envelope detector rejects affected/ambiguous cases, while
  supported responses and inside-root text still equal BCL.

No native parser performance/allocation, other platform, or Xemele parser
conformance claim is made. This is bounded reference infrastructure.
