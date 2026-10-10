# Incomplete attribute contract checks

Run `python3 -B docs/reports/M2-undeclared-attribute-contract/verify.py` from this
branch after committing source. This compiles the actual library for metadata
inspection only, an independent proposal assembly and a separate consumer. It
compares the 290-member current public API, seven frozen internal shapes and
seven public handle/content shapes, checks authored byte identities and the
original inventory/ledger, exercises contract policy models, compiler negatives,
compiled ABI mismatch and proposal/consumer mismatch controls, then planning and
ownership checks. `checks.json` binds committed sources, actual binary and pinned
SDK 10.0.400/runtime 10.0.11 identities. All build output is temporary and removed.

Policy-model execution and historical seven-row observations are explicitly
separate from product execution. This task never parses those inputs with the
product, runs the corpus or claims parser conformance/allocation/timing results.
The exact ordered runtime ownership and mandatory conformance-join prerequisite
are in the decision document and task handoff. Integration evidence belongs to
the coordinator; branch status can only become implemented here.
