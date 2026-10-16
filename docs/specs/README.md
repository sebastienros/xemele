# Product specifications

These documents describe Xemele's architecture, supported profiles, API contracts,
ownership, and failure behavior. Runtime source and executable tests establish
implemented behavior.
[Common rules](common.md) apply throughout the runtime.

| Specification | Boundary |
| --- | --- |
| [Foundation](foundation.md) | Build conventions, corpus manifests, reference protocol |
| [Runtime foundation](foundation-runtime.md) | Storage, identity, decoding, numeric limits, allocation boundaries |
| [API compatibility](api-compatibility.md) | Compiled API normalization and baseline comparison |
| [Conformance](conformance.md) | Test outcomes, fixture provenance, exclusions |
| [Common](common.md) | Diagnostics, limits, options, lifecycle |
| [Tree](tree.md) | Managed document/node model and mutation |
| [Input/parser](parser.md) | Decoding, resumable grammar, DTD parsing, recovery |
| [XML resources](xml-resources.md) | DTD model, entities, resolution, encoding, recovery |
| [Undeclared references](undeclared-references.md) | Incomplete content and attribute/default provenance |
| [DTD composition](dtd-composition.md) | External framing and shared budgets |
| [Serialization](serialization.md) | Document output and forward writer |
| [Streaming/SAX](streaming.md) | Callbacks, push, pull, asynchronous transport |
| [Streaming internals](streaming-writer.md) | Attribute metadata, resource continuations, shared pump |
| [XPath](xpath.md) | Grammar, evaluation, node identities |
| [DTD/C14N](dtd-c14n.md) | Validation and canonical byte output |
| [Resolution/integration](integration.md) | Catalogs, XInclude, adapters, CLI |
| [Source identity](source-uri.md) and [resolver cancellation](resolver-cancellation.md) | Explicit source URIs and per-call cancellation |
| [HTML/CSS](html.md) | Separate HTML engines and selector grammar |
| [HTML model](html-model.md), [processing instructions](html-processing-instructions.md), [legacy recovery](legacy-root-recovery.md) | Tree representation and exact parsing profiles |
| [XML selectors](css-xml.md) and [default selector](css-default-constructor.md) | Two-graph selection semantics |
| [Schemas](schemas.md) | Datatypes, RELAX NG, XSD, Schematron |
| [Schema inventory](schema-inventory.json) and [sources](schema-sources.json) | Required features, exclusions, normative editions |
| [Resource outcomes](resource-outcomes.md), [missing components](missing-components.md), [instance hints](instance-hint-ordering.md) | Schema acquisition and incomplete assessment |
| [Release requirements](release.md) | Compatibility, performance, packaging, robustness |
