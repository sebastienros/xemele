# Remaining instance triage

Diagnostic handoff from the immutable full product/BCL source `80cd6bac0e740947cc3809fef4300068d8b4e51b`, with only product Annotations/Group/IdentityConstraint/Saxon XmlVersions replaced by saved exact-ID observations at `09b0f5e54807f99efa94d92cda891f002e8dff4f`. This is an explicit mixed-revision analysis, not a new full run. Original journals stay transient; this report retains concise findings and exact witnesses, not raw logs.

136 original instance failures: 32 unexpected valid, 24 complete invalid, 60 schema-unavailable/unexecuted, 17 incomplete refusals, two staging exceptions and one authored hint namespace mismatch. No timeout/resource/preparation refusal or unavailable schema counts as an invalid-instance pass. Independent BCL agreement/disagreement is diagnostic evidence, not an oracle rewrite. The 16 original Saxon instance mismatches retain original outcomes but have separately accepted profile agreements or unassessed substrate/dependency dispositions.

Priority beyond queued attZ002/groupN007: restricted-union xsi:type `stZ073b.i`; unique nil fields `idF018.i`; thirteen attribute/wildcard rejects including addB034/addB136/attZ015 and four ComplexType cases; time seconds `time016_2024.i`; two legacy gMonth lexical cases (standards triage); eighteen supplementary regex category accepts (standards triage); ten finite-work particle refusals. Seven Additional instance-hint composition cases also accept in BCL and need standards/adaptation triage rather than an assumed runtime repair.

The drafted malformed-hint correction at `1a6d9df8c46e7cf800b280d83895385f2a244784` is not overlaid here: addB138/addB139 original failures remain as their executed baseline staging exceptions until focused engine execution is reviewed. Actual unchanged malformed-schema controls in both engines give schema-unavailable and no invalid-instance pass.

## harness-exception / no semantic diagnostic (2 IDs)

Likely source / follow-up: Owned hint namespace staging; original schema validity distinct from instance assessment.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Additional_w3c.xml#g143/i0:addB138.i | invalid / harness-exception | instance-syntax-error | original mismatch; triage | msData/additional/test84002_a.xml |
| msMeta/Additional_w3c.xml#g144/i0:addB139.i | invalid / harness-exception | instance-syntax-error | original mismatch; triage | msData/additional/test84002_b.xml |

## incomplete-refusal / SCHEMA.Syntax (7 IDs)

Likely source / follow-up: Original parser/syntax or failed hinted-schema precondition; no instance-invalid inference.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| saxonMeta/XmlVersions.testSet#g2/i0:xv003.v01.xml | valid / incomplete-refusal | schema-unavailable | unassessed substrate | saxonData/XmlVersions/xv003.v01.xml |
| saxonMeta/XmlVersions.testSet#g5/i0:xv006.v01.xml | valid / incomplete-refusal | schema-unavailable | unassessed substrate | saxonData/XmlVersions/xv006.v01.xml |
| saxonMeta/XmlVersions.testSet#g7/i0:xv008.v01.xml | valid / incomplete-refusal | schema-unavailable | unassessed substrate | saxonData/XmlVersions/xv008.v01.xml |
| saxonMeta/XmlVersions.testSet#g8/i2:xv009.n01.xml | invalid / incomplete-refusal | schema-unavailable | unassessed substrate | saxonData/XmlVersions/xv009.n01.xml |
| sunMeta/Wildcard.testSet#g20/i1:Negative | invalid / incomplete-refusal | instance-syntax-error | original mismatch; triage | sunData/Wildcard/psContents/psContents00201m/psContents00201m1_n.xml |
| sunMeta/Wildcard.testSet#g23/i1:Negative | invalid / incomplete-refusal | instance-syntax-error | original mismatch; triage | sunData/Wildcard/psContents/psContents00301m/psContents00301m2_n.xml |
| sunMeta/Wildcard.testSet#g25/i1:Negative | invalid / incomplete-refusal | instance-syntax-error | original mismatch; triage | sunData/Wildcard/psContents/psContents00302m/psContents00302m2_n.xml |

## incomplete-refusal / VALIDATION.Limit (10 IDs)

Likely source / follow-up: ContentModels/Session.cs work consumption; finite budget refusal, never semantic pass.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Particles_w3c.xml#g742/i0:particlesQ005.v | valid / incomplete-refusal | instance-valid | original mismatch; triage | msData/particles/particlesQ005.xml |
| msMeta/Particles_w3c.xml#g844/i0:particlesZ034_a1.v | valid / incomplete-refusal | instance-valid | original mismatch; triage | msData/particles/particlesZ034_a1.xml |
| msMeta/Particles_w3c.xml#g845/i0:particlesZ034_a2.i | invalid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ034_a2.xml |
| msMeta/Particles_w3c.xml#g846/i0:particlesZ034_a3.i | invalid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ034_a3.xml |
| msMeta/Particles_w3c.xml#g847/i0:particlesZ034_b.i | invalid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ034_b1.xml |
| msMeta/Particles_w3c.xml#g848/i0:particlesZ035_a.i | invalid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ035_a.xml |
| msMeta/Particles_w3c.xml#g849/i0:particlesZ036_a.i | invalid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ036_a.xml |
| msMeta/Particles_w3c.xml#g850/i0:particlesZ036_b1.i | valid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ036_b1.xml |
| msMeta/Particles_w3c.xml#g851/i0:particlesZ036_b2.i | valid / incomplete-refusal | instance-invalid | original mismatch; triage | msData/particles/particlesZ036_b2.xml |
| msMeta/Particles_w3c.xml#g852/i0:particlesZ036_c.v | valid / incomplete-refusal | instance-valid | original mismatch; triage | msData/particles/particlesZ036_c.xml |

## instance-hint-namespace-mismatch / no semantic diagnostic (1 IDs)

Likely source / follow-up: Owned hint namespace staging; original schema validity distinct from instance assessment.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Schema_w3c.xml#g6/i0:schA8.i | invalid / instance-hint-namespace-mismatch | instance-hint-namespace-mismatch | original mismatch; triage | msData/schema/schA8.xml |

## instance-invalid / XSD.Attribute (13 IDs)

Likely source / follow-up: Evaluation/Values.cs:47,54; effective attribute/wildcard mapping in Compilation/Compiler.cs.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Additional_w3c.xml#g141/i0:addB136.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/additional/test84188.xml |
| msMeta/Additional_w3c.xml#g42/i0:addB034.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/additional/test71818.xml |
| msMeta/Attribute_w3c.xml#g243/i0:attP031.i | valid / instance-invalid | instance-invalid | original mismatch; triage | msData/attribute/attP031.xml |
| msMeta/Attribute_w3c.xml#g265/i0:attZ002.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/attribute/attZ002.xml |
| msMeta/Attribute_w3c.xml#g289/i0:attZ015.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/attribute/attZ015.xml |
| msMeta/ComplexType_w3c.xml#g247/i0:ctE019.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/complexType/ctE019.xml |
| msMeta/ComplexType_w3c.xml#g260/i0:ctF013.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/complexType/ctF013.xml |
| msMeta/ComplexType_w3c.xml#g265/i0:ctG001.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/complexType/ctG001.xml |
| msMeta/ComplexType_w3c.xml#g277/i0:ctG013.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/complexType/ctG013.xml |
| msMeta/Particles_w3c.xml#g809/i0:particlesZ002.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/particles/particlesZ002.xml |
| sunMeta/suntest.testSet#g10/i4:test.11.v | valid / instance-invalid | instance-valid | original mismatch; triage | sunData/combined/009/test.11.v.xml |
| sunMeta/suntest.testSet#g10/i7:test.3.v | valid / instance-invalid | instance-valid | original mismatch; triage | sunData/combined/009/test.3.v.xml |
| sunMeta/suntest.testSet#g10/i9:test.5.v | valid / instance-invalid | instance-valid | original mismatch; triage | sunData/combined/009/test.5.v.xml |

## instance-invalid / XSD.Content (1 IDs)

Likely source / follow-up: Compilation/Compiler.cs:613; Evaluation/Evaluator.cs:118 (queued component mapping).

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Group_w3c.xml#g172/i0:groupN007v.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/group/groupN007.xml |

## instance-invalid / XSD.Datatype.Facet (6 IDs)

Likely source / follow-up: Datatype facet/regex profile; Saxon cases adjudicated separately.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Regex_w3c.xml#g2589/i0:reZ006i.i | valid / instance-invalid | instance-invalid | original mismatch; triage | msData/regex/invalid.c.xml |
| saxonMeta/XmlVersions.testSet#g10/i0:xv100c.i.xml | valid / instance-invalid | instance-invalid | agrees | saxonData/XmlVersions/xv100.i.xml |
| saxonMeta/XmlVersions.testSet#g10/i1:xv100c.c.xml | valid / instance-invalid | instance-invalid | agrees | saxonData/XmlVersions/xv100.c.xml |
| saxonMeta/XmlVersions.testSet#g4/i0:xv005.v01.xml | valid / instance-invalid | schema-unavailable | agrees | saxonData/XmlVersions/xv005.v01.xml |
| saxonMeta/XmlVersions.testSet#g6/i0:xv007.v01.xml | valid / instance-invalid | schema-unavailable | agrees | saxonData/XmlVersions/xv007.v01.xml |
| saxonMeta/XmlVersions.testSet#g9/i0:xv100i.i.xml | valid / instance-invalid | instance-invalid | agrees | saxonData/XmlVersions/xv100.i.xml |

## instance-invalid / XSD.Datatype.Lexical (4 IDs)

Likely source / follow-up: Datatype lexical/date parser; gMonth originals need Second Edition standards review.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/DataTypes_w3c.xml#g2061/i0:gMonth002_2061.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/datatypes/gMonth002.xml |
| msMeta/DataTypes_w3c.xml#g2063/i0:gMonth004_2063.v | valid / instance-invalid | instance-valid | original mismatch; triage | msData/datatypes/gMonth004.xml |
| saxonMeta/XmlVersions.testSet#g1/i0:xv002.v01.xml | valid / instance-invalid | schema-unavailable | agrees | saxonData/XmlVersions/xv002.v01.xml |
| saxonMeta/XmlVersions.testSet#g8/i1:xv009.v02.xml | valid / instance-invalid | schema-unavailable | agrees | saxonData/XmlVersions/xv009.v02.xml |

## instance-valid / XML name/datatype profile (adjudicated) (4 IDs)

Likely source / follow-up: Frozen XML1.0 Fifth / XSD1.0 Second profile; no runtime waiver.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| saxonMeta/XmlVersions.testSet#g5/i2:xv006.n02.xml | invalid / instance-valid | schema-unavailable | agrees | saxonData/XmlVersions/xv006.n02.xml |
| saxonMeta/XmlVersions.testSet#g5/i3:xv006.n03.xml | invalid / instance-valid | schema-unavailable | agrees | saxonData/XmlVersions/xv006.n03.xml |
| saxonMeta/XmlVersions.testSet#g7/i1:xv008.n01.xml | invalid / instance-valid | schema-unavailable | agrees | saxonData/XmlVersions/xv008.n01.xml |
| saxonMeta/XmlVersions.testSet#g7/i2:xv008.n02.xml | invalid / instance-valid | schema-unavailable | agrees | saxonData/XmlVersions/xv008.n02.xml |

## instance-valid / instance hint composition / standards triage (7 IDs)

Likely source / follow-up: InstanceInputs ordered precompilation adaptation; original fixtures unchanged.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Additional_w3c.xml#g159/i0:addB156.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_2.xml |
| msMeta/Additional_w3c.xml#g161/i0:addB158.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_4.xml |
| msMeta/Additional_w3c.xml#g162/i0:addB159.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_5.xml |
| msMeta/Additional_w3c.xml#g164/i0:addB161.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_7.xml |
| msMeta/Additional_w3c.xml#g165/i0:addB162.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_8.xml |
| msMeta/Additional_w3c.xml#g167/i0:addB164.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_10.xml |
| msMeta/Additional_w3c.xml#g169/i0:addB166.i | invalid / instance-valid | instance-valid | original mismatch; triage | msData/additional/test93490_12.xml |

## instance-valid / regex supplementary category / standards triage (18 IDs)

Likely source / follow-up: Regex Unicode scalar/category profile; independent Unicode16 facts before any oracle change.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Regex_w3c.xml#g1040/i0:reT63.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reT63.xml |
| msMeta/Regex_w3c.xml#g1061/i0:reT84.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reT84.xml |
| msMeta/Regex_w3c.xml#g391/i0:reJ11.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ11.xml |
| msMeta/Regex_w3c.xml#g393/i0:reJ13.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ13.xml |
| msMeta/Regex_w3c.xml#g399/i0:reJ19.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ19.xml |
| msMeta/Regex_w3c.xml#g401/i0:reJ21.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ21.xml |
| msMeta/Regex_w3c.xml#g403/i0:reJ23.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ23.xml |
| msMeta/Regex_w3c.xml#g405/i0:reJ25.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ25.xml |
| msMeta/Regex_w3c.xml#g409/i0:reJ29.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ29.xml |
| msMeta/Regex_w3c.xml#g411/i0:reJ31.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ31.xml |
| msMeta/Regex_w3c.xml#g413/i0:reJ33.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ33.xml |
| msMeta/Regex_w3c.xml#g415/i0:reJ35.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ35.xml |
| msMeta/Regex_w3c.xml#g440/i0:reJ61.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ61.xml |
| msMeta/Regex_w3c.xml#g448/i0:reJ69.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ69.xml |
| msMeta/Regex_w3c.xml#g453/i0:reJ75.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ75.xml |
| msMeta/Regex_w3c.xml#g455/i0:reJ77.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reJ77.xml |
| msMeta/Regex_w3c.xml#g916/i0:reS21.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reS21.xml |
| msMeta/Regex_w3c.xml#g937/i0:reS42.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/regex/reS42.xml |

## instance-valid / time lexical (1 IDs)

Likely source / follow-up: Datatype date/time lexical parser; exact 13:20:60 standards review.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/DataTypes_w3c.xml#g2024/i0:time016_2024.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/datatypes/time016.xml |

## instance-valid / unique nil field (1 IDs)

Likely source / follow-up: Identity/Session.cs:169-175 nil field capture / Evaluator integration.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/IdentityConstraint_w3c.xml#g264/i0:idF018.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/identityConstraint/idF018.xml |

## instance-valid / xsi:type restricted union (1 IDs)

Likely source / follow-up: Evaluation/Values.cs:181-267 and Evaluator.cs:181 (xsi:type restricted union).

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/SimpleType_w3c.xml#g337/i0:stZ073b.i | invalid / instance-valid | instance-invalid | original mismatch; triage | msData/simpleType/stZ073.xml |

## schema-unavailable / REGEX.Syntax (3 IDs)

Likely source / follow-up: Regex parser; dependent instance unexecuted.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Regex_w3c.xml#g2572/i0:HighSurrogates.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/regex/HighSurrogates.xml |
| msMeta/Regex_w3c.xml#g694/i0:reM77.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/regex/reM77.xml |
| msMeta/Regex_w3c.xml#g789/i0:reN75.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/regex/reN75.xml |

## schema-unavailable / SCHEMA.ResourceCycle (3 IDs)

Likely source / follow-up: Compilation/Resources.cs cycle policy; unexecuted instance.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Schema_w3c.xml#g108/i0:schU3.i | invalid / schema-unavailable | instance-valid | unexecuted | msData/schema/schU3.xml |
| msMeta/Schema_w3c.xml#g109/i0:schU4.i | invalid / schema-unavailable | instance-valid | unexecuted | msData/schema/schU4.xml |
| msMeta/Schema_w3c.xml#g110/i0:schU5.i | invalid / schema-unavailable | instance-valid | unexecuted | msData/schema/schU5.xml |

## schema-unavailable / SCHEMA.ResourceDenied (4 IDs)

Likely source / follow-up: Compilation/Resources.cs / nullable resolver integration; unexecuted instance.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/DataTypes_w3c.xml#g1337/i0:anyURI_a002_1337.v | valid / schema-unavailable | instance-valid | unexecuted | msData/datatypes/Facets/anyURI/anyURI_a002.xml |
| msMeta/DataTypes_w3c.xml#g1339/i0:anyURI_a004_1339.i | invalid / schema-unavailable | instance-valid | unexecuted | msData/datatypes/Facets/anyURI/anyURI_a004.xml |
| msMeta/Schema_w3c.xml#g19/i0:schD7.v | valid / schema-unavailable | instance-valid | unexecuted | msData/schema/schD7.xml |
| msMeta/Schema_w3c.xml#g45/i0:schG8.v | valid / schema-unavailable | instance-valid | unexecuted | msData/schema/schG8.xml |

## schema-unavailable / SCHEMA.Syntax (2 IDs)

Likely source / follow-up: Original parser/syntax or failed hinted-schema precondition; no instance-invalid inference.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Additional_w3c.xml#g130/i0:addB124.i | invalid / schema-unavailable | schema-unavailable | unexecuted | msData/additional/test79253.xml |
| msMeta/Additional_w3c.xml#g69/i0:addB063.i | invalid / schema-unavailable | instance-unassessed | unexecuted | msData/additional/test72702.xml |

## schema-unavailable / XSD.SchemaConstraint (1 IDs)

Likely source / follow-up: Xsd/XmlSchema.cs NOTATION finalization; unexecuted instance.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| msMeta/Particles_w3c.xml#g812/i0:particlesZ007.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/particles/particlesZ007.xml |

## schema-unavailable / XSD.SchemaRestriction (47 IDs)

Likely source / follow-up: Compilation/Compiler.cs / ContentModels/Derivation.cs; dependent instance unexecuted.

| Original case ID | Expected / product actual | BCL actual | Profile | Original instance fixture |
| --- | --- | --- | --- | --- |
| ibmMeta/anyAttribute.testSet#g3/i0:s3_10_6v01i | invalid / schema-unavailable | schema-unavailable | unexecuted | ibmData/instance_invalid/S3_10_6/s3_10_6ii01.xml |
| ibmMeta/anyAttribute.testSet#g4/i0:s3_10_6v02i | invalid / schema-unavailable | schema-unavailable | unexecuted | ibmData/instance_invalid/S3_10_6/s3_10_6ii02.xml |
| ibmMeta/anyAttribute.testSet#g6/i0:s3_10_6v04i | invalid / schema-unavailable | schema-unavailable | unexecuted | ibmData/instance_invalid/S3_10_6/s3_10_6ii04.xml |
| ibmMeta/wildcard.testSet#g13/i0:s3_10_1ii08i | invalid / schema-unavailable | schema-unavailable | unexecuted | ibmData/instance_invalid/S3_10_1/s3_10_1ii08.xml |
| ibmMeta/wildcard.testSet#g14/i0:s3_10_1ii09i | invalid / schema-unavailable | schema-unavailable | unexecuted | ibmData/instance_invalid/S3_10_1/s3_10_1ii09.xml |
| msMeta/Additional_w3c.xml#g126/i0:addB120.i | invalid / schema-unavailable | schema-unavailable | unexecuted | msData/additional/test73456.xml |
| msMeta/Additional_w3c.xml#g190/i0:addB191.v | invalid / schema-unavailable | instance-valid | unexecuted | msData/additional/addB191.xml |
| msMeta/Additional_w3c.xml#g273/i0:isDefault078.v | valid / schema-unavailable | instance-valid | unexecuted | msData/additional/isdefault078.xml |
| msMeta/Additional_w3c.xml#g7/i0:addC001.v | valid / schema-unavailable | instance-valid | unexecuted | msData/additional/adhocAddC001.xml |
| msMeta/Errata10_w3c.xml#g18/i0:errF001.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/errata10/errF001.xml |
| msMeta/Particles_w3c.xml#g169/i0:particlesEb041.v | valid / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesEb041.xml |
| msMeta/Particles_w3c.xml#g705/i0:particlesR013.v | valid / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesR013.xml |
| msMeta/Particles_w3c.xml#g748/i0:particlesQ013.v | valid / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesQ013.xml |
| msMeta/Particles_w3c.xml#g808/i0:particlesZ001.i | invalid / schema-unavailable | schema-unavailable | unexecuted | msData/particles/particlesZ001.xml |
| msMeta/Particles_w3c.xml#g813/i0:particlesZ008.v | valid / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesZ008.xml |
| msMeta/Particles_w3c.xml#g817/i0:particlesZ012.v | valid / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesZ012.xml |
| msMeta/Particles_w3c.xml#g820/i0:particlesZ015.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/particles/particlesZ015.xml |
| msMeta/Particles_w3c.xml#g830/i0:particlesZ026.v | indeterminate / schema-unavailable | instance-valid | unexecuted | msData/particles/particlesZ026.xml |
| msMeta/SimpleType_w3c.xml#g121/i0:stE053.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/simpleType/stE053.xml |
| msMeta/SimpleType_w3c.xml#g164/i0:ste110.i | invalid / schema-unavailable | schema-unavailable | unexecuted | msData/simpleType/ste100.xml |
| msMeta/SimpleType_w3c.xml#g272/i0:stZ007.v | valid / schema-unavailable | instance-valid | unexecuted | msData/simpleType/stZ007.xml |
| msMeta/SimpleType_w3c.xml#g312/i0:stZ047.v | valid / schema-unavailable | instance-valid | unexecuted | msData/simpleType/test107331_4.xml |
| msMeta/SimpleType_w3c.xml#g320/i0:stZ055.v | valid / schema-unavailable | instance-valid | unexecuted | msData/simpleType/test107331_9.xml |
| msMeta/Wildcards_w3c.xml#g306/i0:wildZ010.v | invalid / schema-unavailable | instance-valid | unexecuted | msData/wildcards/wildZ010.xml |
| msMeta/Wildcards_w3c.xml#g309/i0:wildZ013a.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/wildcards/test328873a.xml |
| msMeta/Wildcards_w3c.xml#g310/i0:wildZ013b.v | valid / schema-unavailable | instance-valid | unexecuted | msData/wildcards/test328873b.xml |
| msMeta/Wildcards_w3c.xml#g311/i0:wildZ013c.v | valid / schema-unavailable | instance-valid | unexecuted | msData/wildcards/test328873c.xml |
| msMeta/Wildcards_w3c.xml#g312/i0:wildZ013d.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/wildcards/test328873d.xml |
| msMeta/Wildcards_w3c.xml#g313/i0:wildZ013e.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/wildcards/test328873e.xml |
| msMeta/Wildcards_w3c.xml#g314/i0:wildZ013f.i | invalid / schema-unavailable | instance-invalid | unexecuted | msData/wildcards/test328873f.xml |
| saxonMeta/Missing.testSet#g0/i0:missing001.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.v1.xml |
| saxonMeta/Missing.testSet#g0/i1:missing001.n1.xml | invalid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.n1.xml |
| saxonMeta/Missing.testSet#g1/i0:missing001.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.v1.xml |
| saxonMeta/Missing.testSet#g1/i1:missing001.n1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.n1.xml |
| saxonMeta/Missing.testSet#g2/i0:missing003.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.v1.xml |
| saxonMeta/Missing.testSet#g2/i1:missing003.n1.xml | invalid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.n1.xml |
| saxonMeta/Missing.testSet#g5/i0:missing006.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.v1.xml |
| saxonMeta/Missing.testSet#g5/i1:missing006.n1.xml | invalid / schema-unavailable | schema-unavailable | unexecuted | saxonData/Missing/missing001.n1.xml |
| saxonMeta/VC.testSet#g0/i0:vc001.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc001.v1.xml |
| saxonMeta/VC.testSet#g0/i1:vc001.n1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc001.n1.xml |
| saxonMeta/VC.testSet#g1/i0:vc002.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc002.v1.xml |
| saxonMeta/VC.testSet#g1/i1:vc002.n1.xml | invalid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc002.n1.xml |
| saxonMeta/VC.testSet#g13/i0:vc014.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc014.v1.xml |
| saxonMeta/VC.testSet#g13/i1:vc014.n1.xml | invalid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc014.n1.xml |
| saxonMeta/VC.testSet#g18/i0:vc024.v1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc001.v1.xml |
| saxonMeta/VC.testSet#g18/i1:vc024.n1.xml | valid / schema-unavailable | schema-unavailable | unexecuted | saxonData/VC/vc001.n1.xml |
| saxonMeta/XmlVersions.testSet#g3/i0:xv004.v01.xml | valid / schema-unavailable | schema-unavailable | unassessed invalid-schema dependency | saxonData/XmlVersions/xv004.v01.xml |


Later coordinator explicit Second Edition profile adjudication covers the three
REGEX.Syntax schema-unavailable dependencies and particlesZ007.i's NOTATION
schema-unavailable prerequisite. All four original expected INVALID/actual
unexecuted failures remain unchanged; separate profile outcomes are unassessed
invalid-schema dependencies, never invalid-instance passes. No new full run is
represented by this annotation.

Coordinator later adjudicated all eighteen supplementary-category original
instance mismatches VALID under exact pinned Unicode16 facts, and the four exact
lexical cases time016 VALID/gMonth002 INVALID/gMonth004 INVALID/attP031 INVALID.
These22 rows retain their original failed outcomes and BCL disagreements here;
separate profile agreement is not a runtime repair waiver. The owned independent
reproduction check validates original statuses/hashes/patterns and57 exact scalar
categories, without using product/reference outcomes. Lexical standards reasons
remain explicitly coordinator source interpretation, including the time inference.
