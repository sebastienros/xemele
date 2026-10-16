# Compiled API comparison, version 1

`tools/ApiBaseline/discover.py` owns inventory validation. Its canonical
`xemele-api-v1` JSONL schema is unchanged: sorted id/member/declaration objects,
XML documentation IDs, no duplicates, UTF-8, alphabetic keys, compact JSON.
The compiled comparison tool is `tools/ApiBaseline/Checker`.
It uses the pinned SDK's Roslyn assemblies, not an additional package pin.

Build a Release assembly; run `ApiCheck emit <assembly> <fragment>` only when
reviewing a new approved baseline, and `compare <assembly> <fragment>` in checks.
CI must compose validated API fragments in memory, sort once by member ID, and
compare to the full actual assembly. Never update a baseline just to make CI
pass. Producers assign id equal to member for new fragments. Existing inventory
permits other unique ids; composition preserves id provenance but compares the
member/declaration pair to actual output. Fragment names and assembly names are
not encoded into member identity. Duplicate member rows fail before comparison.
Fixture golden fragments are deliberately outside docs/api, so declaration
models are not advertised as shipped APIs.

Extraction loads metadata into a Roslyn compilation with net10 runtime references
and all metadata imports. Walk externally visible (public/protected/protected
internal) declared types and members, including compiler-generated record
members/default constructors. Exclude property/event accessor rows because their
accessor surface is carried by the property/event display. Inherited members are
represented once on their declaring type. Emit symbol documentation IDs and a
fixed SymbolDisplayFormat with containing namespace/types, access/modifiers,
return/member type, parameter names/types/ref/out/params/defaults, generic
parameters/variance/constraints, nullable reference annotations, delegate
signatures and property read/write descriptors, explicit getter/setter accessibility,
init and required flags. Use fully qualified framework
names, escaped identifiers and no source whitespace. Append direct base type and
sorted direct interfaces, type flags (abstract/sealed/readonly/ref-like), member
virtual/abstract/override/sealed flags, JSON constant values, and sorted observable
non-compiler attributes. The exact code is the versioned normalization algorithm;
it is deterministic contract text, not intended as compilable C# source. Pin SDK
and checker together; reviewing a new normalizer version requires intentional
baseline migration. Missing/added/changed rows fail even for additive changes
until an intentional API change updates the corresponding fragment.

Nullable/tuple/init/ref-like/record compiler attributes inform Roslyn symbol
interpretation. Arbitrary System.Runtime.CompilerServices attributes are currently
excluded from appended display, so custom compiler attributes, ABI marshalling,
layout/interop metadata, assembly forwarding, explicit interface dispatch and
per-accessor attributes/event visibility need expanded probes before any corresponding API is shipped.
Version 1 approval covers the foundation fixture family, not unrestricted future
unsafe/interop/custom-attribute surfaces. The fixture extraction checks include
base/interfaces, nullable fields, init properties, generic result, readonly
handles, enum constants, delegates, defaults, record-generated members and the
resolver's virtual method. The actual runtime assembly is compared separately from declaration models. Source usage compilation complements metadata
comparison; it cannot prove behavioral compatibility. Documentation/comments,
limits' initializer defaults and ownership semantics require separate contract
tests because assemblies do not encode all of them as public signatures.

CI integrates the checker with fragment discovery, the runtime Release assembly,
and source usage projects. Add negative probes and compatibility smoke tests
for new signature families before relying on their coverage.
