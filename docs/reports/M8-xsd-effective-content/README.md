# Effective complex content mapping repair

Source ade942a4 was independently accepted. Actual branch checks at
2d92341a passed all282 cases (28 new mapping cases plus254 predecessors),
with zero failures/errors/skips/not-run. Frozen119+257/public1393 members,
both real XML-only consumers,179-task planning validation and whitespace
checks passed. Release builds had zero warnings/errors.

The five-route probe built and passed counters-disabled consumed controls
and elevated process-cleanup preflight after the external parser RELEASE.
No allocation measurement has run yet.

The exact syntactic empty-content mapping from Structures Second Edition
§3.4.2 precedes extension/restriction composition. Empty effective extensions
inherit the entire base kind and particle, even with authored mixed=false.
Mixed empty syntax owns a 1..1 empty sequence; an already-identical sequence
is reused. Group references and general nullability do not trigger this shortcut.
Authored particle tracking remains separate so mapped absence cannot hide
a duplicate body. Occurrences/grammar still validate before mapping.

Owned allocation probe covers: empty baseline, synthesized
mixed empty content, inherited mixed empty all, zero-occurrence mapping and
invalid duplicate. Inputs are parsed during fixture setup, then each call uses
a fresh real schema operation and compiler. No timing or optimization claim.
Builds and disabled controls passed. A frozen identity manifest and a fresh
all-host grant are required before actual allocation measurement.

Independent source review accepted ade942a4. The nonempty0..0 absence mapped
to effective-empty is an explicit cross-section inference from pinned XSD1.0
§3.4.2/§3.7.2/§3.8.2; do not import the later XSD1.1 expanded synthesis.
No applicable Second Edition erratum was found.
