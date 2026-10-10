# Source/API assessment

All observations are for jonwiggins/xmloxide
555055f1ea6bf9944488fd96415b6f03b5b43696, package0.5.0. Cache provisioned from
https://github.com/jonwiggins/xmloxide; the build independently verifies Cargo's
actual checkout HEAD, clean tracked files, immutable dependency source and tree.
Upstream MIT license is retained task-privately; lockfile has registry checksums.
The final provenance artifact records actual toolchain, target, resolved features
and dependency license/source inventory. No upstream source is patched or vendored.

Pinned upstream relevant symbols:

- src/parser/mod.rs ExternalEntityRequest (lines29–41): name, system_id,
  public_id only. EntityResolver is Arc<Fn + Send + Sync>; ParseOptions owns the
  callback (lines43–92). No source/base URI parameter exists. parse_str_with_options
  is the actual entry point used with recovery off and blanks retained.
- src/parser/xml.rs parse_doctype (lines204–238): external IDs set
  has_external_dtd, with no external subset resolver invocation. Lines280–359 parse
  the internal subset and serialize_dtd; parameter declarations can disappear from
  DocumentType.internal_subset. The adapter uses real DocumentType fields for
  subset capability rejection, plus raw prolog-only lexical parameter detection.
- src/parser/xml.rs parse_entity_ref_in_content (lines1088–1140): the actual
  resolver is invoked for declared external general entities; unavailable external
  DTD/PE declarations may instead yield unexpanded EntityRef nodes. The adapter
  rejects these instead of silently omitting content. Replacement child nodes are
  traversed; stored replacement value is not emitted again. Subparsers share options
  and the request's callback, so denied nested relative references remain failures.
- src/encoding/mod.rs decode_to_utf8 (line168 onward): actual XML decoding and
  declaration/BOM handling. Explicit utf-8 hint uses strict std UTF8 validation.
- src/parser/input.rs increment_depth, parse_name, count_entity_expansion:
  known fixed or numeric diagnostic shapes. src/parser/xml.rs amplification and
  nesting guards and its entity-error wrapper; src/validation/dtd.rs
  MAX_CONTENT_MODEL_DEPTH256/content-model guard. All matched guard errors are
  infrastructure limit failures, including wrapped replacement-text name limits.
- src/parser/xml.rs parse_attribute, parse_cdata and initial prolog parsing explain
  observed CRLF/prolog divergences recorded by actual native/BCL process checks.
  ParseOptions max_attributes/max_text_length/max_attribute_length are not used
  by this XML parser; the runner does not claim otherwise.

URI policy: absolute mapped general entities supported; absent mappings deny
file/network/URN access. No I/O is performed by the closure. Relative references,
external DTD subsets and parameter DTD entities explicitly fail protocol capability
checks. URL normalization is provided by locked url2.5.8; full System.Uri equivalence
for every unusual URI syntax is not established by this task. Consumers needing
unsupported capability must use a capable reference, never relax Xemele requirements.

The exact known v1 XML fixture and additional supported fixtures compare normalized
events with BCL. Native CRLF/initial whitespace differences remain observations;
independent XML expectations are retained in fixtures/normalization-baseline.json.
The verifier rejects native wrong normalization and a mutated correct response as
standard expectations. No Xemele runtime parser or product exclusion was added.
Subprocess duration is not parser performance; no runtime hot-path/allocation or
comparative timing experiment applies to this reference infrastructure task.
