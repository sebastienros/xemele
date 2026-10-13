# Borrowed name lookup experiments

The user's proposed design is viable: use a quick key to select one candidate, verify the entire name, and treat collisions as misses. A table index, hash or prefix must never establish name identity. Keep the cache bounded and operation-owned; fixed known literals can be shared globally.

Current XML already probes borrowed UTF-16 and creates a string only for a previously unseen name. Its 64-entry front cache checks length/first character and full ordinal equality before the randomized dictionary fallback. HTML5 instead calls `name.ToString()` at `HtmlTokenizer.EmitTag` and `attrName.ToString()` in `FinishAttribute`, so repeated HTML token names still allocate before DOM interning. The tokenizer has already folded ASCII case; SVG/MathML adjustment and custom/non-ASCII names must retain their existing behavior. Legacy HTML already has an ordinal borrowed-span name pool.

`BorrowedNames` implements three relevant prototypes: a span switch returning 23 fixed HTML tag/attribute literals; a mixed four-character UTF-16 prefix key with exact equality; and packed short names plus XXH3 for longer names. Four UTF-16 code units or eight UTF-8 bytes fit in 64 bits. The prototype reads only available units and includes length in selection. It does not re-encode strings to UTF-8: XXH3 hashes the existing UTF-16 bytes directly. Names above 64 units bypass the small cache. Fixed matching remains exact and case-sensitive; the tokenizer's existing ASCII folding belongs before it.

Apple M4 Pro ARM64, .NET 10.0.11, SDK 10.0.400, BDN 0.15.8 ShortRun, one launch, three warmups and three 250 ms iterations; tiering, PGO and ReadyToRun disabled. Inputs/table initialization are outside timing. Results below are ns per borrowed name, 128 requests per invocation, from the final 30-case micro run.

| Shape | Existing cache | Packed prefix | Known HTML + fallback | XXH3 + short packing | Hybrid secondary hash |
|---|---:|---:|---:|---:|---:|
| Common HTML names | 9.08 | 5.72 | 2.85 | 7.18 | 6.66 |
| XML names with numeric suffixes | 15.81 | 17.41 | 17.49 | 10.18 | 12.62 |
| Identical four-character prefixes | 15.44 | 17.10 | 17.66 | 12.82 | 14.39 |
| Unicode names | 13.92 | 9.74 | 11.67 | 8.05 | 10.18 |
| Long names, cache bypass | 24.39 | 23.86 | 24.56 | 24.64 | 24.27 |

All warmed cache/matcher methods allocate 0 B. Creating a new string per name allocates about 32 B for the common HTML set and takes 6.10 ns in this primitive workload. This is not an end-to-end HTML tokenizer measurement and excludes StringBuilder/chunk extraction. The setup checks every candidate against an equal-but-distinct input string, plus unknown/case/accent variants and literal reference identity; `name-check` runs those checks independently. Cold dictionary/cache setup and first unknown-name ownership still allocate.

The no-package hybrid uses .NET's `string.GetHashCode(ReadOnlySpan<char>)` only after the existing cheap probe collides, checks full equality again, and lazily adds 64 secondary slots. It passed 10,780 unit tests, including exact colliding names, distinct-name quotas and warmed 0 B dispatch. However, seven alternating whole-document runs against revision `6e534cdc183f864b26c905740a44fba88ef34a23` show geomean latency 0.9966× before (only 0.3% change, mixed fixture results) and 2.6% more allocations on feed-small. It was reverted; no XML throughput improvement is claimed. XXH3 is a benchmark-only dependency, not a new runtime dependency.

A separate post-allocation identity-cache experiment covered 8/32/128 names, equal-but-distinct strings, and same-local/different-URI triples. A single direct slot improved larger vocabularies but regressed the eight-name identity and namespace sets severely when collisions caused long-URI dictionary hashing. It does not invalidate the pre-allocation cache proposal; it rules out replacing the existing eight-entry DOM cache indiscriminately.

The actionable HTML follow-up is a fixed lowercase literal matcher first, then an operation-owned bounded cache for custom names with full equality, and normal owned-string fallback. It must preserve name quotas, duplicate attributes, foreign-name adjustment, source locations, split/chunked names and retainable callback tokens. HTML tokenizer paths are outside this XML task's ownership, so this lane provides the measured prototype and a precise owning-task request.

Reproduce with the owned runner commands `symbols`, `names` and `name-check`. Raw reports are in ignored `artifacts/M9-xml-performance/{hybrid-names-bdn,name-caches-bdn,hybrid-paired.json}`. Runtime changes are kept only after end-to-end evidence.
