# Measured feature and representation costs

A private artifact-only variant of checkpoint `9fca5c136` disables durable source-record persistence and capacity hints in `Document.SourceLocations.cs`. All other source files are frozen at that checkpoint. It still maintains parser diagnostics and byte mapping. Default production behavior is unchanged. This experimental build intentionally fails the source-location contract and is never credited as a default-compatible parity result.

Seven alternating local 200 ms runs are compared with the same checkpoint. Time ratios below divide each Xemele/BCL ratio by its baseline Xemele/BCL ratio to reduce host drift. There are no controlled confidence intervals; small differences are informational.

| Fixture | No durable records/default time, BCL-normalized | Allocation ratio |
|---|---:|---:|
| feed-small | 0.881 | 0.923 |
| svg-small | 0.918 | 0.864 |
| deep-medium | 0.919 | 0.894 |
| entity-medium | 0.952 | 0.913 |
| xhtml-medium | 0.908 | 0.878 |
| build-medium | 0.918 | 0.868 |
| soap-large | 0.898 | 0.874 |
| configuration-large | 0.895 | 0.863 |
| business-large | 0.912 | 0.872 |
| text-large | 0.986 | 0.998 |

Durable locations cost roughly 9–14% of allocated bytes on structural fixtures and 5–12% of their BCL-normalized time in these screenings. The large-text fixture barely changes. SOAP's independent retained root layout identifies 907,200 bytes of coordinate arrays in a 7,235,800-byte graph (12.5%). The default compact-coordinate implementation already reduced this cost substantially from the original full-width 32-byte slot representation. Suppressing coordinates alone does not achieve BCL parity.

A public opt-out could be useful for callers that never need later schema/validation source diagnostics. Its contract would need to cover original-node/member locations, source URI, incomplete entity provenance and imported/changed trees; this lane does not silently change those defaults. A contract owner must handle that additive API.

A conditional `NodeMembers` heap payload experiment shrank general node slots and moved six attribute/namespace head/count fields to nodes that acquire members. It passed functional cases but failed two earlier prepared-persistence allocation contracts: each warmed 1,000-node sequence allocated 56,000 B rather than 0 B. It was reverted before end-to-end timing, so no throughput or allocation benefit is claimed. The arena representation remains. This is a concrete reason not to copy a smaller class representation based solely on fresh-heap size.

The existing rooted layouts also show why native memory cannot be inferred from speed: libxml2 and xmloxide payload retention exceeds managed BCL on several frozen fixtures. They use different byte-string representations and tree payload shapes, and xmloxide duplicates name/namespace strings across nodes rather than retaining the same canonical managed name triples. Their uninstrumented timing paths remain separate from allocation sidecars.

Raw variant logs/results and the two failing experimental allocation cases are preserved under ignored `artifacts/M9-xml-performance/`. Experimental sources are in `/private/tmp/xemele-no-coordinates`; no experimental source-location suppression is merged into the default library.
