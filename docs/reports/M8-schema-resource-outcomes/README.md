# Schema resource outcomes handoff

The exact fixture compiled/executed before runtime edits at contract commit
2164e612eeae9e0d3ead82aba6131ae7ceebdadc.
Runtime adds only result/status/virtual ResolveResult plus a concrete optional
SchemaResourceLoader seam. Default/legacy null remains Denied; mandatory
Unavailable refuses. Optional explicit Unavailable returns false/null only
with a complete polled operation. Frozen ISchemaResourceLoader stays unchanged.

The accepted compatibility decision preserves mandatory Charge(0) plus accepted
scalar parsing debits. Verified M8-model test
`ParsingDebitsAcceptedScalarsAcrossPrincipalAndExternalInputs` still proves
principal4 + external4 succeeds at MaxOperations=8. Every optional attempt
charges one work unit before callback; optional1 + parse4 is an exact five-unit
load. Shared-principal test proves principal4 + two unavailable attempts +
one available attempt/parse4 succeeds at work11, with no fresh budget. Missing
resources consume no successful resource slot, bytes, document/cache entry or
parse allowance. Existing capacity guard remains before callback.

Correctness commands are `python3 docs/reports/M8-schema-resource-outcomes/check.py`
and the focused namespace command recorded in the task. The driver reuses nearby
M8 correctness/API/consumer support; it excludes WarmBorrowed and performs no
allocation counters, heap readings or benchmark work. First focused runtime
build exposed an incorrect test property and cancellation analyzer requirement;
both were fixed. First driver used a schema-only extractor for resolver types;
changed it to the established public extractor. No production failure or shared
test/API baseline weakening occurred.

The initial working-tree checks passed 37 focused, 1,529 affected and 42
legacy resolver cases, zero failed/errors/skipped/not-run. Public union matched
1,430 rows (1,417 old plus 13 additive); exact representative surface matched
46 rows; all eight frozen lanes retained all member metadata. Normal and true
XML-only consumers passed. Planning validated 194 tasks; diff check passed.
A further exact optional-five-unit/one-over witness has been added for the final
revision check; evidence records its actual outcome separately.

Allocation remains pending source review/preparation and fresh root grant.
The source-only probe has five owned routes, three zero-byte primitive targets,
cold setup/positive/retained controls, and reused bounded supervisor. See
`allocation-plan.md`. No allocation measurement is claimed. Compiler directive
semantics remain with the ordered XSD repair. No outside ownership requests.
