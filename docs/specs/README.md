# Implementation contracts

Read only specs listed in your task. [Common rules](common.md) apply to runtime work; the [task workflow](../tasks/README.md) defines coordination.

| Spec | Boundary |
| --- | --- |
| [Foundation](foundation.md) | Builds, evidence, corpus/reference protocol, contract approval |
| [Common](common.md) | Diagnostics, limits, options, lifecycle, additive APIs |
| [Tree](tree.md) | Managed document/node model and mutation |
| [Input/parser](parser.md) | Decoding, resumable grammar, DTD parsing, recovery |
| [Serialization](serialization.md) | Document output and forward writer |
| [Streaming/SAX](streaming.md) | Callback contract, push, pull, async transport |
| [XPath](xpath.md) | Compilation, evaluation, node identities |
| [DTD/C14N](dtd-c14n.md) | Validation and canonical byte output |
| [Resolution/integration](integration.md) | Resolver, catalogs, XInclude, adapters, CLI |
| [HTML/CSS](html.md) | Separate HTML engines and selectors |
| [Schemas](schemas.md) | Datatypes, RELAX NG, XSD, Schematron |
| [Release](release.md) | Performance, API review, package consumption |

Shapes in these files are C# declaration sketches: bodies are omitted, and supporting members explicitly assigned to contract tasks must be expanded there. Each spec names its decision owner. `approved` contract evidence means concrete signatures and executable usage tests have been recorded; it does not mean the library is implemented. Public APIs appear in source only in the task that implements them.
