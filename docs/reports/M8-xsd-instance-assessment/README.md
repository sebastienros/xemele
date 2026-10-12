# Wildcard and nil identity instance assessment

Four unchanged official witnesses are byte-preserving copies from W3C xsdtests
revision `7bc3365c652a322f3d762021b3879eb92dae7e30`. Owned provenance.json records
original case-sensitive paths, group/instance indices, validity and SHA256;
00COPYRIGHT preserves the source notice. No resolver, schema-hint loading or
network fallback is enabled in either independent BCL or product tests.

Baseline witness revision `6e03b7b1c1ccb081e3421ffe6da3476e693b19e9` on
SDK10.0.400/.NET10.0.11: all four BCL expectations passed; all four product
assertions failed, with complete product results. attZ002.v, addB034.v and
addB136.v were rejected despite matching lax local wildcard (official/BCL valid).
idF018.i was accepted despite the second nilled field retaining xs:anyType
(official/BCL invalid). Actual baseline:4tests/4failures/0errors/skips/not-run;
Release build:0warnings/errors. Original bytes and expected validity stay fixed.

[Structures Second Edition §3.2.2](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#cAttribute_Declarations)
maps a prohibited declaration to no positive attribute use. Values.cs now skips
prohibited candidate uses and lets the existing wildcard path select any global
attribute declaration. Compiler markers, missing defaults, local-use precedence,
strict missing-declaration refusal, global datatype and typed fixed checks remain.

[Structures Second Edition §3.11.4 clause3](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#cvc-identity-constraint)
requires the selected field to have a simple effective type before tuple
qualification. Session.cs grants nil exemption only when the existing effective
XsdValidatedValue.Type resolves to a simple datatype or complex simple content.
The immutable graph lookup charges the shared operation before registry lookup
and at every complex type candidate; it adds no collection/string ownership.
No frozen API/interface or later dependency changes.

Owned44 inventory:4originals+40neighbors. Neighbors independently specify wildcard
presence/namespace/policy, global datatype/fixed, local precedence, absent default,
all nil type forms, effective xsi:type, unique exemption, key/nillable prohibition,
exact precharged work/components, source diagnostics and mid-lookup cancellation.
One explicit BCL oracle difference is retained: BCL accepts xsi:nil=false on a
key field declared nillable, while Second Edition §3.11.4 clause4.2.3 and the
unchanged product behavior reject it. That neighbor asserts both outcomes; it
cannot be conflated with the four original BCL agreements.

Historical preparation issues: first neighbor test build failed with one
XmlAttributes assertion type error and three cancellation analyzer errors.
A subsequent attempt ran the unchanged witness binary after that failed build;
its four failures are stale baseline output, not a repaired-source test run.
After correction the fresh build had0warnings/errors. The first fresh44 run
passed43 and exposed the independent BCL key/nillable difference above; that
assumption was corrected explicitly. The next fresh44 run passed44 with
0errors/failures/skips/not-run.

Correctness: `python3 docs/reports/M8-xsd-instance-assessment/check.py`.
Owned inventory is exact and nonempty; separate syntax1146, fixed45, safe42,
public1417 and frozen316+139/257/24/9/2/7, normal+true source-linked XML-only
checks are required. Allocation evidence remains pending coordinator source
review, frozen preparation and a fresh host-wide PARK/durable grant. Workers
never launch measurements. No raw logs/results are committed or archived.
