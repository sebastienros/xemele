# Exact schema ambiguity inputs

Prepared from original W3C revision `7bc3365c652a322f3d762021b3879eb92dae7e30` and product/BCL baseline `80cd6bac0e740947cc3809fef4300068d8b4e51b`. No adjudication, exception, or profile change is made. These are twelve original schema test IDs, seven distinct schema documents. Dependent instances remain unexecuted; their declared validity is preserved.

| Schema case ID | Original expected / actual | Independent BCL | Original schema document / SHA256 |
| --- | --- | --- | --- |
| msMeta/Particles_w3c.xml#g248/s:particlesHa070 | valid / schema-invalid | schema-valid | msData/particles/particlesHa070.xsd / 9863c8a2a7ecd3895113ab219041e5056476ea1a928bafccc8aac60716b5ea73 |
| msMeta/Particles_w3c.xml#g250/s:particlesHa080 | valid / schema-invalid | schema-valid | msData/particles/particlesHa080.xsd / 8c9e298d1ef42b9c7daba23e9c3f11bf3a93e32b804fa52d054ba0b176d93cd2 |
| msMeta/Particles_w3c.xml#g254/s:particlesHa084 | valid / schema-invalid | schema-valid | msData/particles/particlesHa084.xsd / 4ba323f36c97b2cc59d12139cc314780cb516b05b9d7f8df8c801329b7ee605b |
| msMeta/Particles_w3c.xml#g705/s:particlesR013 | valid / schema-invalid | schema-valid | msData/particles/particlesR013.xsd / fba2337df737b4b9212d85471bb3d2323b3c2a1f675ff7ac6fde58c0a0754f46 |
| msMeta/Particles_w3c.xml#g748/s:particlesQ013 | valid / schema-invalid | schema-valid | msData/particles/particlesQ013.xsd / 1621b3d5b1826424225ca3940f45d9ba47e0ab521b36a98be8585299cb009122 |
| msMeta/SimpleType_w3c.xml#g121/s:stE053 | valid / schema-invalid | schema-valid | msData/simpleType/stE053.xsd / a6a08198ad43ede42fa8f2e0a48d98273fded20308895a829f0d25f8a566e536 |
| msMeta/Wildcards_w3c.xml#g309/s:wildZ013a | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |
| msMeta/Wildcards_w3c.xml#g310/s:wildZ013b | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |
| msMeta/Wildcards_w3c.xml#g311/s:wildZ013c | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |
| msMeta/Wildcards_w3c.xml#g312/s:wildZ013d | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |
| msMeta/Wildcards_w3c.xml#g313/s:wildZ013e | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |
| msMeta/Wildcards_w3c.xml#g314/s:wildZ013f | valid / schema-invalid | schema-valid | msData/wildcards/test328873.xsd / 30126d76a297ef26677aad8cdd4d831a5df6fecfb4e036ca7995d8c130bb8881 |

| Dependent instance ID | Original expected / actual | Original instance / SHA256 |
| --- | --- | --- |
| msMeta/Particles_w3c.xml#g705/i0:particlesR013.v | valid / schema-unavailable (unexecuted, originalPassed=false) | msData/particles/particlesR013.xml / 5a13f7df2cd15370a181fc799de86e448a5ab6b3352217040d60205618391f13 |
| msMeta/Particles_w3c.xml#g748/i0:particlesQ013.v | valid / schema-unavailable (unexecuted, originalPassed=false) | msData/particles/particlesQ013.xml / 61f71605220352cb9896208b61a063ef0b753a08ffbc4f0e522021644c56a3df |
| msMeta/SimpleType_w3c.xml#g121/i0:stE053.i | invalid / schema-unavailable (unexecuted, originalPassed=false) | msData/simpleType/stE053.xml / 06e041298b1171edf4447ee1b00eb3019d70c3a49f702761ade7300d89628a9d |
| msMeta/Wildcards_w3c.xml#g309/i0:wildZ013a.i | invalid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873a.xml / be5fe02f1c690f6a3c3832327651d9ad6e468834457f89439d961fc246aa7a74 |
| msMeta/Wildcards_w3c.xml#g310/i0:wildZ013b.v | valid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873b.xml / 5659f43ebdc20b7f2b2eee95f838eea5b21d084cd595b877528741cb10752c15 |
| msMeta/Wildcards_w3c.xml#g311/i0:wildZ013c.v | valid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873c.xml / a1808cd0873a1bf50c0e96b83c2d47b74b4428fb4ec128d893579f646bbc0ea0 |
| msMeta/Wildcards_w3c.xml#g312/i0:wildZ013d.i | invalid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873d.xml / 786b64d419bf03ce238fe5de21c45c6ad851d6986b34922b305b05e1a10b942f |
| msMeta/Wildcards_w3c.xml#g313/i0:wildZ013e.i | invalid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873e.xml / 419c6a2849401670b6b2763e0694bb193c56b57dbd4e2174a24dfa4f8f568d60 |
| msMeta/Wildcards_w3c.xml#g314/i0:wildZ013f.i | invalid / schema-unavailable (unexecuted, originalPassed=false) | msData/wildcards/test328873f.xml / f67050948bf9a91429e14c1b72165593a06851db2c23abb567d7ba4dd8216e1a |

9 dependent original instances; none were assessed or counted as invalid-instance passes. Original schema diagnostics are XSD.SchemaRestriction: content restriction, extension wildcard preservation, or union member validity respectively. Decision remains with the coordinator and independent standards review.
