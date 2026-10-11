# M5 corpus partial execution and repair handoff

This task remains in progress. Frozen expectations 7dc2a3c9cd60f2a0ce373cde9b2724b414ee1051
preceded execution; adapter 9d37897 ran original bytes on actual .NET10.0.11/Arm64,
all 2585 IDs and 3386 SHA256-verified resources. Driver exit 1, process exited and
own group cleaned. No benchmarks/allocation counters were run. Product source
is verified base 9314ee6be13a9a1959623dfc17878db4f286cf97; no runtime edits or
unverified peer assemblies. Retained source-context byte/declaration driver
comes from verified M2 grammar support, then invokes public DtdSchema facade.

Frozen applicability: 976 source-associated DTD valid/invalid cases; 1609 prior
effectiveXML10 grammar cases, including 78 valid/invalid source documents with
no DOCTYPE and 1531 original not-wf/error cases. Every original ID remains.
First product counts: associated valid 799, invalid 97, rejected 79, incomplete 1;
prior grammar accepted 453/rejected 1156, all prior expectations pass. Total
first failure count 24: 20 DTD expectations and 4 canonical reference-preparation
mismatches. All 32 original product canonical bytes already matched.

| Original case | First expected | Product | Diagnostic | Resolution |
|---|---|---|---|---|
| xmlconf/xmltest/xmltest.xml#invalid--002 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/xmltest/xmltest.xml#invalid--005 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/xmltest/xmltest.xml#invalid--006 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/xmltest/xmltest.xml#invalid-not-sa-022 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/ibm/ibm_oasis_invalid.xml#ibm-invalid-P49-ibm49i01.xml | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/ibm/ibm_oasis_invalid.xml#ibm-invalid-P50-ibm50i01.xml | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/ibm/ibm_oasis_invalid.xml#ibm-invalid-P51-ibm51i01.xml | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-9a | valid | rejected | DTD.InvalidDefault | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-14 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-15a | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-15g | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-15h | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/xml-1.1/xml11.xml#rmt-031 | valid | invalid | DTD.AttributeType | Explicit source-profile expectation corrected; original observation preserved |
| xmlconf/eduni/xml-1.1/xml11.xml#rmt-033 | valid | invalid | DTD.AttributeType | Explicit source-profile expectation corrected; original observation preserved |
| xmlconf/eduni/xml-1.1/xml11.xml#rmt-047 | valid | invalid | DTD.Content | Explicit source-profile expectation corrected; original observation preserved |
| xmlconf/eduni/xml-1.1/xml11.xml#rmt-049 | valid | invalid | DTD.Content | Explicit source-profile expectation corrected; original observation preserved |
| xmlconf/eduni/namespaces/1.0/rmt-ns10.xml#rmt-ns10-045 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/namespaces/1.0/rmt-ns10.xml#rmt-ns10-046 | invalid | valid | none | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-3e/errata3e.xml#rmt-e3e-06i | valid | rejected | DTD.InvalidDefault | Owning runtime repair required; expected unchanged |
| xmlconf/eduni/errata-3e/errata3e.xml#rmt-e3e-13 | invalid | incomplete | DTD.IncompleteInput | Owning runtime repair required; expected unchanged |

The reference audit has thirteen repair-related label differences. Three E15
witnesses have equal but normatively incorrect product/reference validity
results, bringing the total to sixteen runtime witnesses. These remain: 8 Proper Declaration/PE Nesting, 2 unused
ENTITY defaults incorrectly checked semantically at schema compilation, 3 E15
lexical ElementValid references erased by DOM expansion, 2 namespace-invalid
ID/IDREF tokens with colons, 1 undeclared general entity after fully read internal
parameter declarations reporting incomplete rather than proved source-invalid.
These are outside corpus ownership and are not exclusions. Default validation
must enforce syntax regardless of use, but semantic declared-entity constraints
only when defaulted. Name-typed namespace tokens require NCNames; NMTOKEN types
retain colons where allowed. A bounded first lexical source witness must apply
only to an unchanged, complete source profile; current-tree checks still run.
Known-invalid undeclared entity evidence must not classify unread or merely
unexpanded declared references as complete. No general incomplete rule change requested.

Primary bases: [XML Fifth Edition VC ElementValid](https://www.w3.org/TR/2008/REC-xml-20081126/#elementvalid),
[Second Edition E15](https://www.w3.org/XML/xml-V10-2e-errata#E15),
[Third Edition E06](https://www.w3.org/XML/xml-V10-3e-errata#E06),
[Namespaces 1.0 namespace validity](https://www.w3.org/TR/2009/REC-xml-names-20091208/#Conformance).
E15a forbids even empty entity references in EMPTY; E15g literal character
reference-to-space and E15h entity replacement text consisting of a character
reference differ from a legal entity literal whose declaration character
reference has already become literal S. The earlier standalone provenance test
asserting source '&#32;' element-only validity needs owning reassessment.

Four original XML1.1 valid-only NEL/LSEP attribute/content cases 031/033/047/049
are invalid under the frozen effectiveXML10 S/NMTOKENS/content profile.
Correction 21808e2 explicitly hash-binds those source witnesses in
`effective10-validity-overrides.json`; no cases removed. Regeneration --check
passes. Original failed expectations are preserved, not retroactively passing.

Reference audit first execution: 831 unequal raw labels, 441 associated DTD;
zero reference infrastructure errors. Every ID and source/normative basis is
accounted in first-reference-audit.json. Invalid/rejected reporting stages are
separated from acceptance disagreement. Legacy BCL name/token classes and
native version support do not implement the frozen effectiveXML10/FifthEdition
profile; existing M2 grammar disagreements are identified. Unused-default and
empty-NMTOKENS reference omissions are disclosed. The LegalNameChar Unicode
range vector has 45 distinct source declarations (same base prefixes, distinct
valid FifthEdition suffixes); BCL reports a duplicate bare LegalNameChar, a
reference parser name-profile discrepancy. This task does not claim all 831
reference outputs agree; independent source review of this classification remains.

Correction 21808e2 captures parse/product/reference exceptions per canonical row,
continues and serializes every vector; 3 offline injection tests prove 32 rows
with one injected failure for each stage. 5 refusal/quota/incomplete controls
prove XML.ResourceDenied/ResourceCycle/DTD.ExternalInput never satisfy invalidity.
No full DTD repeat has occurred before verified repairs. Typed attribute reference
normalization now uses a validating XmlReader-prepared DOM (example validity
errors are irrelevant to canonical input and ignored); product pins unchanged.

Affected canonical-only check passed 32 exact vectors and all 26 declared matching
algorithm reference comparisons (14 inclusive 1.0, 12 exclusive 1.0). All referenceModes
on prior six vectors execute; cross-mode PrefixList applies only to exclusive
mode, since it is that algorithm's parameter. BCL 1.1 is not claimed. Libxml 2.9.13
xmllint --c14n11 passed 6 source-applicable whole-document 1.1 cases; product mode 1
maps to libxml XML_C14N_1_1=2. CLI includes comments, so applicability requires
with-comments or no physical comments, and no subset expression. All other 1.1
vectors remain exact normative fixture tests, including 3.8 E11-01 bar/foo.
/usr/bin/xmllint SHA256 42ce847ac57ff55f50be12d96faffbf4626398043dcefee0fc9b7f3af42ec6f6.
No unavailable subset API or additional coverage is claimed.

10 conformance manifest/driver/refusal controls and 12 authored differential
cases passed; Release driver/conformance/differential builds 0 warnings/errors.
Approved API union helper matched 948 compiled members. Task validation 156 passed.
No product API changes. Final task acceptance awaits verified owning repairs
and applicable conformance rerun; old performance evidence remains applicable.

Ignored raw retained for coordinator inspection:
artifacts/M5-corpus/corpus.json SHA256 aed98a4f25920a9981d24132d0e97e4e1cd10f1251b6227b0899e6385424fa38;
canonical-corrected.json SHA256 b9fdf9bfcaf488d2407ed3983a5a5cf369f56bb2cf1186da9806bc6a06487185;
libxml.json SHA256 46f2df2474eb73377cceec7dba339550baac59d140d89f9b22816dc8dd4271c5.
No raw cleanup before root ACK; no counter reruns.

Adapter-only robustness checkpoint 266a3504fb213043d9e1027d4124d3660bc49128
catches timeout/OSError per libxml vector, retains an explicit failed infrastructure
row, continues all six rows and exits nonzero on any failure. Binary identity and
version infrastructure errors also survive serialization and fail the run. Four
Python-only injected vector/version timeout/OSError controls passed at that revision;
no xmllint, runtime or corpus processes were launched. The earlier actual six
libxml observations remain evidence at 21808e2, not a run of this new adapter.

Coordinator independent source-only review approved 21808e2 and 7d6160c within
the disclosed limits: all 831 unequal labels accounted, 441 associated DTD, no
reference consensus claim, and sixteen runtime witnesses still pending. This
review does not replace execution or claim repaired runtime behavior.
