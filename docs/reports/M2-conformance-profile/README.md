# Effective XML 1.0 profile and offline comparison contract

This contract classifies all 2585 original IDs and preserves all 3386 original
resource byte/hash identities. `m2-applicability.json` is additive; the M1
inventory, classifications and inputs remain unchanged. No case is excluded
because Xemele or a reference currently fails it. `productStatus` in the
classification is deliberately not execution evidence. `observations.json`
contains actual independent driver/reference probes and remaining gaps.

Run `python3 -B docs/reports/M2-conformance-profile/classify.py --check` to
regenerate all expectations in memory and verify every original resource hash.
The generator consumes only original metadata/bytes and these normative rules;
reference or product responses never determine classification.

## Normative interpretation

[XML 1.0 Fifth Edition](https://www.w3.org/TR/2008/REC-xml-20081126/),
[Namespaces 1.0 Third Edition](https://www.w3.org/TR/2009/REC-xml-names-20091208/)
and the pinned [suite metadata](https://www.w3.org/XML/Test/xmlconf-20130923.html)
are distinct inputs. Each row retains historical TYPE/EDITION/VERSION/purpose
and gives its effective expectation, exact rule and normative links.

Earlier edition name restrictions are superseded by Fifth Edition [4]/[4a].
Seven original IBM P89 cases still have two document elements: their edition
annotation cannot erase that separate [1] violation. The ledger preserves both
historical and Fifth Edition IDs even when their bytes are identical.

XML §2.8 [26] permits `1.` followed by digits. Compatible 1.x declarations are
processed using 1.0 rules. A VERSION=1.1 annotation is never an exclusion.
XML1.1 C1 literal restrictions/version ordering do not apply; XML1.0 Char, S,
name, newline and namespace rules do. C0 references prohibited by Char reject;
NEL/LSEP remain characters rather than line ends or S. Original XML1.1 canonical
outputs cannot substitute for effective 1.0 models.

TYPE=invalid is a validity test, not necessarily a well-formedness failure.
The frozen nonvalidating profile does not check content models, duplicate IDs,
required attributes or enumerated values. Read declarations still supply default
attributes, normalize non-CDATA values and expand internal entities (XML §5.1).
The existing M2 contract explicitly checks standalone dependencies; those rows
have a separate profile rule. Parameter-entity nesting validity cases remain
visible gaps if Xemele treats them as mandatory syntax rejection.

TYPE=error allows either normal acceptance or reported rejection under XML
§1.2. A runner failure, quota denial or missing fixture is never either outcome.
Errors whose concrete bytes violate mandatory Char become required rejection.
Namespace URI-reference checking is optional under NS §8; the profile permits
relative/IRI names without dereferencing them. Namespace-disabled suite metadata
never disables the product's QName/NCName constraints (NS §7), and prefix
undeclaration remains forbidden (NS §5).

Optional encoding labels are exactly the seven labels in the ledger, matched
case-insensitively. Unsupported labels require profile rejection rather than
exclusion. Unicode signature/declaration disagreement and invalid bytes remain
separate mandatory rejection conditions.

## Offline source and map

Each primary source URI is `https://xemele.invalid/corpus/` plus escaped inventory
path. The host is an identity only. `options.sourceUri` is the sole new BCL
option: well-formed absolute URI string without fragment. Missing/empty options
retain `urn:xemele:input`; unknown, nonstring, relative, whitespace or fragmented
options are protocol runner failures. Existing request/response shapes and
encoding-hint semantics remain unchanged. Source identity does not grant access.

All corpus bytes are verified against inventory length/hash before provisioning.
Absolute filesystem paths, traversal, backslashes, siblings, missing/corrupt
files and symlinks fail as infrastructure errors. Nested resource URIs resolve
against the final containing resource URI. Offline keys contain only explicit
verified resources; both HTTP and file URI resolution use memory, never network
or filesystem fallback. Lexical external identifier closure is a provisioning
superset, not a claim that unused/unparsed entities are read. E18 explicitly
includes the internal-PE declaration-origin path in subdir1 as well as its lexical
subdir2 candidate and exact root path. The root path is a verified source identity,
not an alias; a reference using it produces a visible semantic disagreement. Missing actual map requests fail the run; notation identifiers
alone do not trigger reads. Corpus bytes are not republished.

## Semantic and disagreement contract

`effective-xml10-events-v1` compares only the document element and descendants:
ordered start/end events keyed by expanded namespace/local name; attributes
sorted ordinally by namespace then local name, values exact after XML processing;
adjacent text and CDATA coalesced, Unicode scalars unmodified after XML1.0
newline/reference normalization. Empty elements produce both start and end.
Namespace declarations, preferred prefixes, attribute insertion order, comments,
PI, declarations and DOCTYPE are not fields of this semantic model. These lexical
behaviors require separate tests; a semantic match is not their verification.
Outside-root XML S is omitted with an explicit depth-aware normalization. A
non-whitespace outside-root event or unbalanced sequence is infrastructure failure.
No general whitespace trimming, Unicode normalization or entity-text rewriting.

`audit.py` executes independently compiled product and BCL children on exact
bytes, binds case identity, and compares this model on mutual success. It retains
counts and exact-ID/input-hash diagnostic/comparison rows, never raw payloads.
Full source and binary hashes, SDK/runtime and engine/build identities accompany
every run. Runner-failure remains infrastructure/capability evidence, never XML
rejection. Every new disagreement requires its own normative assessment;
System.Xml's three previously pinned malformed VersionNum suffix acceptances
remain the exact M1 ledger, not a wildcard waiver. Its older name/version
capabilities can disagree with Fifth Edition and cannot define applicability.
Xmloxide's external DTD/nested-relative restrictions likewise are reference
capability limits, never product exclusions.

The test driver uses the verified byte decoder/core/DOM sink with an explicit
primary source URI, default finite limits and an explicit offline resolver. Its
assembly uses an existing friend identity solely for independent test composition.
Public DOM byte entry points currently lack a primary source-URI parameter; the
harness does not claim that additive public behavior exists. No runtime hot path,
allocation or timing change is made by this task.
