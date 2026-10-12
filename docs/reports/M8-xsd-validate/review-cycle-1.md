# Consolidated correction cycle 1

Source `8c4cabcdef573b1fe131fa9f1639ea396984c348` implements the
independent assessment/accounting corrections from the coordinator's first
consolidated review: unknown lax nodes use open assessment and resolve xsi:type;
nil presence requires nillability; simple derivation traverses union members and
restriction blocking; fixed comparison parses the declaration type/context;
selected empty/element-only content applies a default constraint only when the
default is used; per-element wildcard ID rules include optional declared ID uses;
schemaLocation has a charged typed list; owned trim/separator scans poll each
inspected UTF-16 unit; simple text enforces accumulated scalar token limits.

The first text fragment borrows DOM-owned string storage. A later fragment
reserves a run-owned char buffer, charges every old/new copied unit before
writing, and reserves/charges a separate final string copy. Independent ledger
tests prove 0 copy work/components for one borrowed fragment; 6 work/1 component
for joining `ab` and `cde`; another 6 work/1 component for final materialization;
12 exact versus 11 one-short; cancellation during materialization and during a
second scan after normalization; 6 exact trim inspections versus 5; and 4,097
separator inspections versus 4,096. These are correctness work ledgers, not
allocation counters or timing results.

Actual check at that source: 828 selected tests, 827 passed and one failed
(`DynamicDefaultRequiresCanonicalDeclaredValue`), zero errors/skips/not-run.
133 owned cases plus 695 predecessor cases were selected. The failure remains
unskipped pending the ordered canonical-constraint helper. Independent API and
normal/XML-only consumers passed: 1,403 public and two frozen evaluator members,
both consumers successful, Release builds zero warnings/errors; planning 180
and diff checks passed. The all-in-one check stopped at the failing regression,
so safe-resolver/API/consumer commands after that point were not attributed to
that script run; API/consumers were run separately against the same source.

Earlier correction source `354b24d3e0763e3050556396473c5b64affeedfc`
passed 823/825; the second failure was a now-obsolete diagnostics overflow
fixture whose unknown attributes became valid under corrected lax assessment.
The fixture was replaced by an undeclared root plus unknown xsi attribute.
`bee83ea854ab8bad55f46d799b364a3c20519f94` passed 824/825, with only
the canonical-default regression failing. No failures were excluded or marked
as passes.

The canonical helper join must parse/validate a constraint using its declared
type and captured context, retain that owned value, and provide the prescribed
XSD 1.0 canonical lexical representation for actual-type validation. Declared
int `01` must supply `1` to a selected one-digit pattern restriction; declared
decimal `1` must supply `1.0`, which selected int rejects. Fixed comparisons use
the declaration-typed value. QName/NOTATION and temporal varieties without a
prescribed XSD 1.0 canonical form preserve validated normalized authored
lexical/context. No frozen interface or unverified helper is used here.

Normative reference: [XSD 1.0 Structures, element assessment 3.3.4 and simple
derivation 3.14.6](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/).
All acceptance criteria remain pending corrected-source review, canonical join,
passing checks and fresh approved allocation evidence. Probe preparation and
measurements are held; older counter-disabled preparation is not frozen evidence
for this corrected source.
