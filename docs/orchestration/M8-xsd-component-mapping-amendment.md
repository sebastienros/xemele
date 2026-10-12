# Component mapping scope amendment

Coordinator authorization, 2026-10-12. The active M8-xsd-component-mapping
worker may add the following bounded acceptance criterion to its own task
record. Ownership remains Compiler.cs and existing owned test/report paths;
there is no new shared-file ownership or peer dependency.

## Empty namespace sets

Preserve unchanged original fixtures and reproduce these accepted schema
tests before changing runtime behavior:

- `msMeta/Wildcards_w3c.xml#g175/s:wildK001`,
  `msData/wildcards/wildK001.xsd`.
- `msMeta/Wildcards_w3c.xml#g28/s:wildC030`,
  `msData/wildcards/wildC030.xsd`.
- `msMeta/Wildcards_w3c.xml#g306/s:wildZ010`,
  `msData/wildcards/wildZ010_a.xsd`, including its original import.

Pinned upstream: `7bc3365c652a322f3d762021b3879eb92dae7e30`.
Accepted corpus run `80cd6bac0e740947cc3809fef4300068d8b4e51b` records
original VALID, independent System.Xml 10.0.11 schema-valid, and product
`XSD.SchemaRestriction: Empty wildcard namespace list.` for all three.

The Second Edition schema-for-schemas defines namespaceList with an
unrestricted list alternative. Section 3.10.2 maps its tokens to a set;
an empty list consequently maps to the empty positive set. The source at
Compiler.cs:828 incorrectly rejects it. Existing ContentNamespaces supports
positive empty sets, including Allow=false and vacuous subset semantics.

Sources inspected by coordinator:

- https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#declare-openness
- https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#namespaceList

Implement the smallest compiler mapping correction for empty and XML
whitespace-only namespace lists. Preserve the distinction from an omitted
attribute (##any), ##local, ##targetNamespace, and ##other. Invalid special
tokens and processContents must still reject. Cover both any/anyAttribute,
original import resolution, graph shape, optional wildcard validation and
required empty-set particle rejection. Preserve work charging, locations,
cancellation and budget refusals. Include this owned mapping route in the
prepared allocation evidence. No new public or frozen API.

## Baseline precision

The worker observed xv004 already rejects at facet compilation. Retain that
fact; its relevant defect is acceptance of its component name before the
later failure, not public-facade acceptance. xv001 still wrongly compiles.
Do not relabel the xv004 baseline as a public acceptance failure.
