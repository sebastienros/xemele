# Complete datatype composition

`XmlDatatypeRegistry` owns one `XsdDatatypeLibrary` provider set and two explicit
RNG namespace adapters. `Find` recognizes only the canonical XSD URI, the RNG
XSD-datatypes URI, and the empty library URI. The empty library parses only
string/token. Names and namespace URIs use ordinal identity; unknown names never
fall back. Alias values retain the requested declared name through core Retype,
while atomic primitive/provider identity and canonical list-item types remain
unchanged. These are internal implementations of the frozen interfaces, with no
new public members.

The registry publishes every one of core's 45 built-in metadata names. Its
immutable definitions include immediate bases, list items, effective whitespace
and fixed flags, list minLength=1, integer fractionDigits=0 and integer ranges.
`anySimpleType` has core's intrinsic absent primitive/variety metadata and uses
the accepted textual representation; it is not a placeholder. Built-in atomic
lexical restrictions are enforced by the verified providers, not inferred from
CLR types. Registered schema definitions own their M8-model snapshots and take
precedence over namespace alias fallback, including no-target-namespace types
named string/token. A private dictionary is assigned once after charged ingestion;
no mutable map alias is exposed or retained from a caller. Setup costs are
47+N work and 366+N components for N distinct caller definitions. No opaque final
freezing/hash/copy pass exists. Schema-defined restriction/list/union/facet
validation remains the explicit M8-facets join, which can use FindType and the
core CreateList/CreateUnion/Retype hooks. There is no fake extension library.

TryParse first charges one metadata dispatch and measures normalized capacity.
Core TryNormalize measures again and writes into bounded stack scratch (<=256
UTF-16 units) or a finally-returned pooled buffer sized to normalized capacity.
Thus successful normalization costs 3N scalar work, explicitly including removed
XML whitespace; it does not allocate an owned normalized string. A pooled scratch
boundary reserves one work/one component before renting. Public Normalize keeps
the core's owned-string, two-pass contract. No custom SIMD, token hashes or
SearchValues optimization is introduced; scalar token scans retain bounded
per-scalar cancellation, including multilingual/malformed input.

NMTOKENS/IDREFS/ENTITIES reject empty normalized input. One scalar scan counts
items; a charged temporary reference array reserves 1+count components before
allocation. A separately charged atomic metadata lookup precedes the scalar
split scan. Each item is parsed through its verified NMTOKEN/IDREF/ENTITY provider,
then one work unit precedes its reference store. No successful prefix is
published on failure. Core CreateList produces the owned private immutable item
snapshot; the temporary array, original lexical buffer and pool storage are not
retained. Built-in identity/declaration/document constraints remain downstream.

Core's list/retype boundary throws on returned quota refusal. The temporary
DatatypeStructuredOperation adapter reserves one work/one component before
construction and throws a private sentinel only for an inner Charge or
ChargeComponents returning false. Only that sentinel is translated to Stopped;
caller exceptions, including a caller-thrown SchemaCompileException after
incompletion, propagate unchanged. All provider parsing and value/comparison/
hash/measure/composite/retype behavior delegates the accepted family/core joins.
No cross-primitive numeric promotion, provider mixing or total temporal order is
introduced. Each provider's existing evidence/source is preserved.

The later ordered composition owner corrected core's list hash foreach after an
actual composition zero-allocation failure: indexed item traversal preserves the
exact core fold/work semantics without obtaining an interface enumerator. The
coordinator authorized this narrow change; child historical evidence is intact.
New composition measurements, rather than old child evidence, cover this path.
