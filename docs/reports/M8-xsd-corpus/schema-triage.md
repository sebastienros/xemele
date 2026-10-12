# Remaining schema triage at accepted harness source80cd6bac

This is a concise diagnostic handoff, not raw execution output. The immutable full run is at `80cd6bac0e740947cc3809fef4300068d8b4e51b`; original journals are transient under ignored artifacts. Every listed schema differs from its original declared oracle while independent .NET10.0.11 System.Xml agrees with that oracle. Status is retained; agreement is evidence for investigation, not a standards adjudication. Standalone schema predicates do not depend on instance hints. Excludes exact approved time exception, adjudicated errF001, and three assigned syntax witnesses. Some neighboring annotation/global-group/identity cases may be fixed by the newly verified syntax integration; affected-set rerun determines this before another repair.

42 original schema test IDs; 36 distinct schema fixtures.

Current product overlay at `09b0f5e54807f99efa94d92cda891f002e8dff4f`
reran only Annotations, Group, IdentityConstraint and Saxon XmlVersions after
verified syntax integration and the original namespace-fact staging correction.
`annotF003`, `groupD003`, and `idJ208` now pass. This leaves 39 listed original
schema IDs / 33 distinct fixtures for further triage; the immutable table below
preserves its full-run provenance. `xv004.xsd` is already schema-invalid and
agrees with the independently derived profile; `xv001.xsd` still accepts an
invalid component name. The Saxon set has 44 profile agreements, one profile
failure and six unassessed substrate/dependency cases, with zero harness errors.

| Original case ID | Original / actual | Status | Fixture | Diagnostic / likely owner |
| --- | --- | --- | --- | --- |
| msMeta/Additional_w3c.xml#g186/s:addB183 | valid / schema-invalid | accepted | msData/additional/test113911.xsd | XSD.SchemaRestriction: Derivation changes an inherited fixed attribute use.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Additional_w3c.xml#g192/s:addB194 | valid / schema-invalid | stable | msData/additional/addB194.xsd | XSD.SchemaRestriction: Content model violates unique particle attribution.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Additional_w3c.xml#g273/s:isDefault078 | valid / schema-invalid | accepted | msData/additional/isdefault078.xsd | XSD.SchemaRestriction: Undefined schema component reference.; Compilation/Compiler.cs |
| msMeta/Additional_w3c.xml#g7/s:addC001 | valid / schema-invalid | accepted | msData/additional/adhocAddC001.xsd | XSD.SchemaRestriction: Undefined schema component reference.; Compilation/Compiler.cs |
| msMeta/Additional_w3c.xml#g83/s:addB077 | valid / schema-invalid | accepted | msData/additional/addB077.xsd | XSD.SchemaRestriction: Circular model group.; Compilation/Compiler.cs |
| msMeta/Annotations_w3c.xml#g70/s:annotF003 | invalid / schema-valid | accepted | msData/annotations/annotF003.xsd | Unexpected schema acceptance; Compilation/Compiler.cs |
| msMeta/DataTypes_w3c.xml#g1356/s:anyURI_b006_1356 | invalid / schema-valid | stable | msData/datatypes/Facets/anyURI/anyURI_b006.xsd | Unexpected schema acceptance; Datatype anyURI/facets (standards triage) |
| msMeta/Element_w3c.xml#g341/s:elemZ027_c | invalid / schema-valid | queried | msData/element/elemZ027c.xsd | Unexpected schema acceptance; Compilation/Compiler.cs |
| msMeta/Element_w3c.xml#g342/s:elemZ027_e | valid / schema-invalid | accepted | msData/element/elemZ027e.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Element_w3c.xml#g343/s:elemZ027_f | valid / schema-invalid | accepted | msData/element/elemZ027f.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Group_w3c.xml#g44/s:groupD003 | invalid / schema-valid | accepted | msData/group/groupD003.xsd | Unexpected schema acceptance; Compilation/Compiler.cs |
| msMeta/Group_w3c.xml#g85/s:groupH007v | invalid / schema-valid | accepted | msData/group/groupH007.xsd | Unexpected schema acceptance; Compilation/Compiler.cs |
| msMeta/IdentityConstraint_w3c.xml#g705/s:idJ208 | invalid / schema-valid | accepted | msData/identityConstraint/idJ208.xsd | Unexpected schema acceptance; Identity compiler/XPath |
| msMeta/Particles_w3c.xml#g169/s:particlesEb041 | valid / schema-invalid | accepted | msData/particles/particlesEb041.xsd | XSD.SchemaRestriction: Circular type derivation.; Compilation/Compiler.cs |
| msMeta/Particles_w3c.xml#g248/s:particlesHa070 | valid / schema-invalid | accepted | msData/particles/particlesHa070.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g250/s:particlesHa080 | valid / schema-invalid | accepted | msData/particles/particlesHa080.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g254/s:particlesHa084 | valid / schema-invalid | accepted | msData/particles/particlesHa084.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g705/s:particlesR013 | valid / schema-invalid | accepted | msData/particles/particlesR013.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g748/s:particlesQ013 | valid / schema-invalid | accepted | msData/particles/particlesQ013.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g812/s:particlesZ007 | valid / schema-invalid | accepted | msData/particles/particlesZ007.xsd | XSD.SchemaConstraint: NOTATION requires an effective enumeration restriction.; Xsd/XmlSchema.cs finalization |
| msMeta/Particles_w3c.xml#g813/s:particlesZ008 | valid / schema-invalid | accepted | msData/particles/particlesZ008.xsd | XSD.SchemaRestriction: Content is not a valid restriction of its base type.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Particles_w3c.xml#g815/s:particlesZ010 | valid / schema-invalid | accepted | msData/particles/particlesZ010.xsd | XSD.SchemaRestriction: Circular model group.; Compilation/Compiler.cs |
| msMeta/Particles_w3c.xml#g817/s:particlesZ012 | valid / schema-invalid | accepted | msData/particles/particlesZ012.xsd | XSD.SchemaRestriction: Substitution type does not derive from the head type.; Compilation/Compiler.cs |
| msMeta/Particles_w3c.xml#g820/s:particlesZ015 | valid / schema-invalid | accepted | msData/particles/particlesZ015.xsd | XSD.SchemaRestriction: Substitution type does not derive from the head type.; Compilation/Compiler.cs |
| msMeta/Particles_w3c.xml#g824/s:particlesZ020 | valid / schema-invalid | accepted | msData/particles/particlesZ020.xsd | XSD.SchemaRestriction: Substitution type does not derive from the head type.; Compilation/Compiler.cs |
| msMeta/Particles_w3c.xml#g837/s:particlesZ031 | valid / schema-invalid | accepted | msData/particles/particlesZ031.xsd | XSD.SchemaRestriction: Complex content requires a complex base.; Compilation/Compiler.cs |
| msMeta/Regex_w3c.xml#g2572/s:HighSurrogates | valid / schema-invalid | accepted | msData/regex/HighSurrogates.xsd | REGEX.Syntax: Invalid XML Schema regular expression.; Regex compiler/parser |
| msMeta/Regex_w3c.xml#g694/s:reM77 | valid / schema-invalid | accepted | msData/regex/reM77.xsd | REGEX.Syntax: Invalid XML Schema regular expression.; Regex compiler/parser |
| msMeta/Regex_w3c.xml#g789/s:reN75 | valid / schema-invalid | accepted | msData/regex/reN75.xsd | REGEX.Syntax: Invalid XML Schema regular expression.; Regex compiler/parser |
| msMeta/SimpleType_w3c.xml#g121/s:stE053 | valid / schema-invalid | accepted | msData/simpleType/stE053.xsd | XSD.SchemaRestriction: Invalid or final union member datatype.; Compilation/Compiler.cs |
| msMeta/SimpleType_w3c.xml#g272/s:stZ007 | valid / schema-invalid | accepted | msData/simpleType/stZ007.xsd | XSD.SchemaRestriction: A user atomic type cannot restrict anySimpleType directly.; Compilation/Compiler.cs |
| msMeta/SimpleType_w3c.xml#g312/s:stZ047 | valid / schema-invalid | accepted | msData/simpleType/test107331_e.xsd | XSD.SchemaRestriction: A user atomic type cannot restrict anySimpleType directly.; Compilation/Compiler.cs |
| msMeta/SimpleType_w3c.xml#g320/s:stZ055 | valid / schema-invalid | accepted | msData/simpleType/test107331_e.xsd | XSD.SchemaRestriction: A user atomic type cannot restrict anySimpleType directly.; Compilation/Compiler.cs |
| msMeta/Wildcards_w3c.xml#g175/s:wildK001 | valid / schema-invalid | accepted | msData/wildcards/wildK001.xsd | XSD.SchemaRestriction: Empty wildcard namespace list.; Compilation/Compiler.cs |
| msMeta/Wildcards_w3c.xml#g28/s:wildC030 | valid / schema-invalid | accepted | msData/wildcards/wildC030.xsd | XSD.SchemaRestriction: Empty wildcard namespace list.; Compilation/Compiler.cs |
| msMeta/Wildcards_w3c.xml#g306/s:wildZ010 | valid / schema-invalid | accepted | msData/wildcards/wildZ010_a.xsd | XSD.SchemaRestriction: Empty wildcard namespace list.; Compilation/Compiler.cs |
| msMeta/Wildcards_w3c.xml#g309/s:wildZ013a | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Wildcards_w3c.xml#g310/s:wildZ013b | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Wildcards_w3c.xml#g311/s:wildZ013c | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Wildcards_w3c.xml#g312/s:wildZ013d | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Wildcards_w3c.xml#g313/s:wildZ013e | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |
| msMeta/Wildcards_w3c.xml#g314/s:wildZ013f | valid / schema-invalid | accepted | msData/wildcards/test328873.xsd | XSD.SchemaRestriction: Extension does not preserve its base attribute wildcard.; ContentModels/Derivation.cs (UPA also ContentModels/Compiler.cs) |

Prioritize the compiler cluster: three empty-namespace wildcards `wildK001`, `wildC030`, `wildZ010` (common `Compilation/Compiler.cs:828`); mixed-content/zero-particle `groupN007v.v` (an instance witness); and independently adjudicated component-name schema `xv001.xsd`, whose original VALID expectation remains preserved but frozen Second Edition component-name profile is INVALID. Its product schema acceptance violates the profile and is not waived. The paired `xv004.xsd` already rejects and agrees with the profile.

Resource refusal, work limits, failed-schema instance preconditions, ordered hint limitations and unavailable inputs remain separate. Queried cases require focused standards review rather than blanket removal. No product source was edited by this worker.

Coordinator later explicitly adjudicated HighSurrogates/reM77/reN75 and
particlesZ007 INVALID under the frozen Second Edition normative profile.
Their original VALID/schema-invalid failures remain in this table; derived profile
agreement and the four unassessed invalid-schema dependents are separate. These
four rows require no runtime repair under that profile. Other disputed rows remain
pending; the exact approved time case is still the only exception.
