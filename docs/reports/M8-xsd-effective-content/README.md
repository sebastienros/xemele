# Effective complex content mapping repair

Source/test draft only; no builds or checks have run during the active external
parser measurement phase. Expected owned suite:28 cases.

The exact syntactic empty-content mapping from Structures Second Edition
§3.4.2 precedes extension/restriction composition. Empty effective extensions
inherit the entire base kind and particle, even with authored mixed=false.
Mixed empty syntax owns a 1..1 empty sequence; an already-identical sequence
is reused. Group references and general nullability do not trigger this shortcut.
Authored particle tracking remains separate so mapped absence cannot hide
a duplicate body. Occurrences/grammar still validate before mapping.

Owned allocation probe is prepared as source only: empty baseline, synthesized
mixed empty content, inherited mixed empty all, zero-occurrence mapping and
invalid duplicate. Inputs are parsed during fixture setup, then each call uses
a fresh real schema operation and compiler. No timing or optimization claim.
Builds, disabled controls, accepted-source manifests and actual allocation
measurement all remain pending final host release and a future fresh grant.

Independent source review accepted ade942a4. The nonempty0..0 absence mapped
to effective-empty is an explicit cross-section inference from pinned XSD1.0
§3.4.2/§3.7.2/§3.8.2; do not import the later XSD1.1 expanded synthesis.
No applicable Second Edition erratum was found. No execution evidence yet.
