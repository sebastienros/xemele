# Public API review

The reviewed release candidate composes55canonical task fragments with1,430
unique externally visible declarations. The approved M0 Roslyn metadata extractor
compares the actual Release assembly, including nullable annotations, parameter
and return types, defaults, constraints, ref-like/delegate signatures, property
access/init/required flags, enum constants and public inheritance. No baseline
is regenerated to accept a mismatch. Missing/changed declaration controls must
fail. The first-preview gate's222compiled declarations must be an exact subset.

Documentation and behavior review uses the actual public entry points and prior
source-applicable contract tests, complemented by the runnable migration example:

| Surface | Review boundary and evidence |
| --- | --- |
| XML/tree/serializer | Parsing/Dom/Factories.cs, Recovery.cs; Tree/Node.cs; existing M1 gate and M8 cumulative tests. Null/false/default-handle, strict recovery rejection, detachment and stream ownership. |
| Streaming | Streaming/Sax/XmlSaxParser*.cs and existing reader/push/writer samples/contracts. Borrowed views, sequential callbacks, caller exceptions, finite states and cancellation. |
| XPath | Approved fragments retain all typed results/namespace and attribute identities; M4 module contracts and corpus inventory. Reusable compilation and explicit context; migration variable example. |
| DTD/C14N/catalog/XInclude/adapters/CLI | Existing frozen signatures and module tests remain unchanged; explicit resource policy and budgets, no native lifetime hooks or global initialization. |
| HTML/CSS | Distinct legacy/WHATWG entry points and explicit serialization modes; existing M7 corpus and builder contracts, migration selector example. |
| RNG/XSD/Schematron | SchemaCommon/Model.cs and public facades; owned immutable results, nullable authored IDs, finite operation/resource/component limits, compile exceptions and complete-vs-valid distinction. M8 exact approved availability/nullability transitions preserved. |

The documentation [migration guide](../migration/README.md) describes user-visible
choices and exception/ownership contracts. It does not claim every member has an
XML documentation comment or that metadata comparison alone proves behavior.
No unsafe/native ABI or blanket libxml2/xmloxide parity is advertised.

The normalizer's historical exclusions (custom compiler/interop attributes,
layout/forwarding/per-accessor attributes) remain explicit. Public source review
must detect introduction of those unsupported families before accepting this
checker's coverage. No such new family is introduced by M9. Earlier verification
records retain their limits; the current actual assembly comparison and first
preview subset are the release compatibility evidence.
