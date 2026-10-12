# Schematron corpus provenance

Original `upstream/core/*.xml` and `upstream/svrl/*.xml` come unmodified from
[David Maus / Schematron conformance](https://github.com/Schematron/schematron-conformance),
revision `1d73a16524814e934eb76a18ee5c47b2c1727d11`, Maven version 1.0.2.
The original `pom.xml` declares the MIT License and names David Maus as developer;
its license declaration and original README are retained verbatim. This archive
contains no separate LICENSE file or copyright notice; none is invented here.
[MIT license terms](https://opensource.org/licenses/MIT) apply as declared upstream.

`inventory.json` pins the downloaded archive digest and each original testcase
resource digest. Each testcase contains its schema, input, secondary include
resources and original expectations. All 50 testcases / 51 schema variants are
retained and executed; no missing applicable case is an exclusion.

`applicability.json` retains the original expected outcome, edition/binding and
bounded reviewed interpretation for each testcase. It distinguishes 38 ordinary
applicable cases, three let oracle disagreements, one name oracle disagreement,
one frozen SVRL representation difference and seven unsupported execution cases
(eight variants). The cross-pattern abstract-rule case remains applicable with
its original schema-error oracle and requires the coordinator's owning repair.

The frozen profile is ISO2016 with absent/exact `xslt`, XPath1 evaluated directly
by M4. Unsupported `documents`, content-valued lets and executable XSLT are
explicit `SCH.QueryBinding` errors. Report-only upstream "invalid" means findings
present; it does not mean frozen `ValidationResult.IsValid=false`: reports are
warnings. Original failed output predicates remain failed, separately from
assertions that the selected frozen output is correct. These checks do not claim
full upstream conformance or completion of all normative ISO tests.

`profile.json` contains 19 authored cases with independently specified complete
structured rows and XML expectations; `generate_profile.py` in the owned report
folder reproduces those bytes without evaluating Xemele. The differential suite
runs 32 expressions on three input documents using .NET BCL XPath, recording one
Unicode length reference limitation in `reference-limitations.json`. The original
BCL result is asserted, and the normative product expectation is asserted.
