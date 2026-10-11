# Source locations for schema compilation

Source: `0315f407f6e15091932d90c498759d3e80612baf`.

`XmlDocument` retains copied `XmlLocation` values in a lazy document-owned dictionary keyed by entry kind and stable arena slot. The document also retains its original source URI independently of an XML declaration. DomSink records original callbacks for nodes, attributes and namespace declarations, including the beginning of coalesced text. No input buffer, callback span, parser or borrowed storage survives. Entries are bounded by parsed DOM slots and existing input quotas; cloned/imported/programmatic entries receive none.

`SchemaDocumentScope.Location(XmlNode)`, `.Location(XmlAttribute)` and `.Location(XmlNamespaceDeclaration)` return copied original positions only for complete strict input with an unchanged source content revision. Each scope checks cancellation and mutation before and after lookup. Foreign/default handles fail; recovered, partial, mutated and untracked entries have unknown coordinates. Known origin survives unknown coordinates. HTML uses a separate DOM and gains no invented XML positions. Consumers must use these helpers for local declaration/particle/constraint locations, not a document-level diagnostic fallback.

Read-only source review accepted the implementation. Ten owned cases cover character scalar coordinates, byte offsets with chunk sizes 1/2/7, async input, redirected origins without declarations, mutation before/during traversal, clone/import/programmatic nodes, foreign/default handles, cancellation, recovery and partial input. Combined with affected DOM, model, DTD provenance, metadata and async cases, 177 actual Arm64 .NET 10.0.11 tests passed with zero failures/errors/skips/not-run. Full public API remained 1393 members; frozen model public/internal surface remained 316. The integrated command script preserves this exact selection and checks positive counts.

Preparation failures were corrected before measurement: an initial test compile used LINQ on a ref-struct collection and omitted required test cancellation; the first executed ten-case run had nine passes and one erroneous expectation that `<r>` recovery stays incomplete. The corrected fatal-NUL fixture asserts actual incomplete status. Neither failed attempt is reported as passing.

No parser scan or token-matching optimization was introduced. Allocation evidence is in allocations.md. Original baseline and candidate use the identical consumed probe binary; only Xemele.dll differs. Comprehensive milestone corpus coverage remains the M8 gate's responsibility.
