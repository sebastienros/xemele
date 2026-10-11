# HTML shared model

The shared model preserves permanent owner and node identity, ordered live read-only
collections, expanded attribute identity, owned token snapshots, namespace and
unrestricted HTML data, template contents and detached fragment metadata. Public
RootElement throws InvalidOperationException when there is no root; it never
returns null through the frozen nonnullable declaration. Import rejects Document;
Clone remains in the same owner, including detached document-node copies. Structural template ownership participates
in acyclicity and depth checks, but actual DOM Parent ancestry alone supplies
fragment context. No XML factories or validation rules are changed.

The normative named-reference bytes and extracted foreign tables are pinned in
owned fixtures with SHA256/source/date/license provenance. 2231 references include
106 semicolonless spellings and all two-scalar replacements. Ordinal borrowed
binary search and bounded longest-prefix matching allocate no candidate strings;
attribute ambiguity rejects semicolonless matches followed by ASCII alphanumeric
or equals. Foreign matchers use the required length-dispatched span equality
baseline, return null on unknown borrowed names, and return static canonical names
on hits. A string wrapper preserves the caller-owned name on misses. No hashing,
SearchValues or custom SIMD is introduced. Relevant allocation/BDN evidence is
pending an exclusive coordinator lease; timings remain provisional microbenchmarks
until HTML engine composition, and no x64 claim is made.

Node, list, attribute, import and snapshot allocations persist as owned results.
Mutation preflight and fixed probes avoid temporary arrays, captured delegates and
per-insertion stacks. Deep copy/text-content materialize owned operation results;
intermediate stacks belong to that operation. Limits snapshot/validate on document
creation. DOM names, values, attributes and depth are bounded; parser input,
expansion and transport accounting remain engine-owned.
