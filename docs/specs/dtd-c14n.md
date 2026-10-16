# DTD validation and canonicalization

This contract specifies association,
outcomes, finite budgets, cancellation, diagnostics, exact node-set membership,
all three canonicalization algorithms, and error/lifetime semantics.

Validation uses `Xemele.Validation.DtdSchema.Compile(string, resolver?, limits?, cancellationToken)`
or `FromDocument(XmlDocument)`, and `Validate(XmlDocument, ValidationOptions?)`.
Shared `ValidationResult` owns a diagnostic snapshot and distinguishes complete invalid
from incomplete quota/provenance outcomes. Shared `ValidationOptions` has Limits,
nullable positive MaxOperations (10M default), and CancellationToken. The shared
validation model is independent of schema implementations. DTD validation consumes the XML
parsed model and current DOM, without silent augmentation or expansion.

Canonicalization uses `Xemele.Serialization.XmlCanonicalizer.Canonicalize(XmlDocument, Stream, options?)`
and the independent `IReadOnlyList<Xemele.XPath.XPathItem>` overload. Modes are Inclusive10,
Inclusive11, Exclusive10. Options specify comments, exclusive inclusive-prefix list,
finite shared Limits/output-byte cap and cancellation. Tree input and XPath identity/model
are the only required feature dependencies. Selection is exact membership, not subtree
closure. Streams remain caller-owned; byte output follows the frozen W3C algorithms.

## Shared validation outcome and budget

`ValidationResult(bool isComplete, IEnumerable<XmlDiagnostic> diagnostics)` snapshots
non-null diagnostics in order and rejects a null sequence or member. IsValid is
computed: completion and no Error/Fatal diagnostics. Warnings may accompany success.
Incomplete requires at least one Fatal; it cannot represent validity. Getter-only
properties prevent inconsistent `with` expressions. Results own diagnostics; later
caller edits cannot change them. No schema-specific types appear in shared options.

Options snapshot Limits and MaxOperations at operation entry; defaults are finite
XmlResourceLimits and 10,000,000 operations. Nullable positive MaxOperations permits
explicit unlimited; zero/negative and null Limits fail before work. The effective
work cap is the minimum of non-null MaxOperations and Limits.EvaluationWork. Work
charges each visited node/member, scalar inspected, automaton transition, declaration
lookup and ID/IDREF bookkeeping step, including preprocessing. No unchecked counter
overflow. Exactly at any quota succeeds. Diagnostic budget defaults to 128; reserve
one slot for a terminal Fatal when truncated, preserving total <= cap, even at cap 1.

Invalid instances return complete false-valid outcomes; resource refusal returns
incomplete false-valid with `VALIDATION.Limit`. Missing parse completeness/provenance
returns incomplete with `DTD.IncompleteInput`. Cancellation throws
OperationCanceledException with the operation token; it is never invalidity. Argument,
I/O and resolver callback exceptions propagate. Compilation syntax failures are
XmlParseException with DTD-prefixed diagnostics; declaration validity failures use
DTD-prefixed diagnostics in that exception. Instance diagnostics use `DTD.*` stable
codes. Locations preserve declaration SourceUri and original positions from the XML
model; instance nodes without retained locations use (null,0,0,null), never fabricated
positions. A declaration error reports its declaration location; instance errors use
instance location. Shared result types need no global code enum or schema dependency.

## DTD association and validation

Compile accepts a DTD external-subset body, not a DOCTYPE wrapper. It parses through
the existing XML model, with the provided resolver as the only external access route,
finite limits and cancellation. No implicit network/file access, catalogs or cache.
Returned schema owns an immutable declaration snapshot and compiled content models;
compilation checks declaration validity, deterministic content models, and lexical
name/token rules. Compile does not alter a document or infer a root-name declaration.

FromDocument consumes the retained combined internal/external XML model directly;
no reparse or resolver calls. Missing model throws InvalidOperationException. Internal
subset precedence and first binding follow the XML parser, not a second ad hoc merge.
The schema retains originating DOCTYPE root name when created this way. Validate
requires that root name when present; a standalone compiled subset uses the instance
DOCTYPE name if present. No DOCTYPE on a programmatic tree requires a declared root
matching the actual root QName. Matching is lexical XML QName, not expanded namespace
name. Namespace declaration attributes participate in DTD attribute validity rules.

Validate traverses the current DOM without rewriting, re-expanding entities or adding
attributes. Existing parser defaults count as actual attributes. Missing ordinary
default attributes are allowed by attribute validity; REQUIRED/FIXED/type/enumeration
and ID/IDREF checks still apply. Defaults are not silently materialized. Checks cover
undeclared elements/attributes, all content models, permitted whitespace, ID uniqueness,
IDREF(S) resolution, unparsed ENTITY(IES)/NOTATION and standalone dependencies. Unresolved
entity/attribute sidecars cannot be treated as empty values; report incomplete input.
A narrow source-invalid exception applies when every unresolved general-entity
occurrence is proven undeclared against the fully read original associated DTD,
complete nonrecovering input, and its nonnegative unchanged completion revision.
FromDocument may compile this retained model and Validate returns complete invalid
DTD.UndeclaredEntity at the original occurrence; symbolic sidecars remain intact,
with no hypothetical empty replacement, resolution or augmentation. Missing proof,
unread declarations, declared-but-unexpanded references, incomplete attributes,
mutation or a different associated schema remain incomplete. This does not alter
nonvalidating XML well-formedness behavior for parameter-entity-dependent documents.

Element Valid source witnesses erased by expansion retain only one first internal
owned diagnostic and the applicable immutable declaration reference. A complete,
nonrecovering unchanged source completion revision and an applicable original
associated declaration profile are required. EMPTY references remain invalid even
when expansion is empty; children-content character-reference whitespace remains
invalid, including references reparsed from entity replacement text. Literal entity
values whose declaration character references already became literal XML S remain
legal. A changed declaration category on otherwise unchanged input cannot silently ignore
the one first witness: later erased restrictions may still apply, so this case is
incomplete. Mutations/restoration, clones and manual trees do not inherit these
source constraints. Current-tree checks still apply; no raw
lexical input or per-node provenance collection is retained.

Mutations during validation fail with InvalidOperationException; immutable schemas
are reusable across independent operations, without shared mutable budgets.

Standalone validity remains mandatory. XML already rejects source-level external entity,
external default/normalization and external element-whitespace dependencies at parse.
Postparse validation must also apply standalone to the current DOM, including mutations.
## Canonical input, output and options

Modes have fixed values Inclusive10=0, Inclusive11=1, Exclusive10=2; default Inclusive10,
comments false, empty inclusive-prefix list, default finite Limits, MaxOutputBytes
16,777,216. Snapshot options/list, reject null members, unknown mode, nonpositive
non-null output cap, or nonempty prefixes for inclusive modes before output. Exclusive
prefix entries are XML NCNames or `#default`; duplicates are collapsed ordinally.
They identify prefixes, not namespace URIs. Null output cap is explicit unlimited.
Cancellation is checked at entry and bounded work intervals. Limits.EvaluationWork
charges traversal, comparison and escaping work; depth and token/scalar quotas apply
with the shared units. Output cap counts actual UTF-8 bytes, without BOM.

Document overload selects the entire XPath document, all descendant content,
attributes and in-scope namespace nodes. Node-set overload is an exact mathematical
selection: no implicit descendants, attributes or namespace closure when an element
is selected. Membership does not suppress traversal through excluded ancestors.
Selected attributes/namespaces are rendered only on selected owner elements; omitted
ancestors still supply algorithm-mandated context. Deduplicate by XPath identity, sort
in document order, preserve attribute and namespace identity rather than string values.
Empty node set emits zero bytes. Default/stale/detached/fragment or mixed-owner items
are ArgumentException before any output. Unresolved sidecars/incomplete documents are
InvalidOperationException before output. Snapshot identities and detect DOM mutation;
never continue with stale XPath metadata. Ancestor context is read from the same owner.

Caller supplies a writable Stream. Do not close, flush or reposition it. Append UTF-8
bytes at its current position. No BOM, XML/DOCTYPE declaration or pretty printing.
I/O errors propagate; cancellation/limits/mutation may leave partial output, with no
transaction or rewind promise. Output limit throws InvalidOperationException; invalid
options/relative namespace names throw ArgumentException. Rejection found during
preflight must not write bytes. No resolver or DTD fetch at canonicalization time;
input must already have expanded/normalized characters and explicit parser defaults.
Canonicalization does not manufacture missing DTD defaults in a manually built tree.

Canonical algorithms are frozen to [C14N 1.0, 15 March 2001](https://www.w3.org/TR/2001/REC-xml-c14n-20010315),
[C14N 1.1, 2 May 2008](https://www.w3.org/TR/2008/REC-xml-c14n11-20080502/),
and [Exclusive C14N 1.0, 18 July 2002](https://www.w3.org/TR/2002/REC-xml-exc-c14n-20020718/).
Inclusive modes process inherited namespace/XML context for document subsets;
1.1 excludes inherited xml:id and fixes xml:base through omitted ancestors. Exclusive
uses visibly utilized prefixes plus InclusivePrefixes, excluding ordinary inherited
XML attributes. Preserve prefixes; no namespace rewriting. Suppress redundant namespace
bindings and handle default undeclarations under each algorithm. Relative namespace
names are rejected. Namespace declarations sort by prefix; attributes sort by namespace
URI then local name with unqualified attributes first, using ordinal scalar ordering.
Text escapes &, <, > and CR; attributes escape &, <, double quote, tab, LF, CR;
comments/PI escape CR with algorithm-specific outer-root LF placement. CDATA becomes
text; empty elements use explicit start/end tags. IncludeComments changes only comment
membership, not PIs. XML 1.0 input only; 1.1 mode is not XML 1.1 parsing support.
DTD validity is pinned to [XML 1.0 Fifth Edition](https://www.w3.org/TR/2008/REC-xml-20081126/).

Tests must prove exact-byte document/subset cases for all three algorithms,
both comment modes, namespace/XML inheritance, omitted ancestors, exclusive prefixes,
xml:id/base differences, normalization/escaping/sorting, outer-root nodes, invalid
node sets, stream lifetime, cancellation, quotas and mutation. This contract does not
claim those runtime checks passed. Preserve unrelated runtime behavior.

Retain nullable standalone declaration and parse provenance with the document;
DTD validation must distinguish incomplete provenance from semantic invalidity.
