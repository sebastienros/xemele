# Reproduce and interpret

Use the pinned SDK10.0.400 on PATH/DOTNET_ROOT, then
`DOTNET_CLI_USE_MSBUILD_SERVER=0 python3 -B docs/reports/M0-references-xmloxide/verify.py`.
The verifier explicitly builds the integrated BCL runner before comparisons; no
peer branch/build wiring is needed. It also runs the integrated protocol verifier,
including preserved foundation checks, and tasks.py validate.

The normalization baseline fixture has independent XML1.0 expectations from
[section2.11](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-line-ends) and
[section3.3.3](https://www.w3.org/TR/2008/REC-xml-20081126/#AVNormalize), cross-checked
against BCL. Native/BCL outputs are recorded separately. Two adversarial oracle
checks ensure the native divergence and a mutated BCL result fail the independent
expectation. This is reference bookkeeping evidence, not product parser testing;
zero Xemele parser cases are run. No product test/exclusion/expectation was changed.
See the runner README for limits and the source-level capability assessment.
