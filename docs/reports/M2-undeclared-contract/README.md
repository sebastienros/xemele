# Undeclared content-reference contract verification

Source-only candidate under coordinator HOST HOLD. No executed checks yet.

The proposal and consumer are independently authored, explicitly nonpackaged
fixtures. The sample compiles them against frozen integrated declarations, not
against a runtime feature or an active peer branch. The scripted model models
event ordering and owned retention; it does not parse XML, create a real DOM,
measure allocation, or demonstrate runtime pause/cancellation/conformance.

After HOST RELEASE run `python3 -B docs/reports/M2-undeclared-contract/verify.py`.
The verifier uses temporary owned report directories for compiler outputs,
inspects failures before cleanup, and preserves only concise counts and source/
binary identities. It verifies original E13 bytes/ledger identity, actual frozen
product/public and independently compiled model preservation, four event view ABI
shapes, old sink compatibility, explicit owned retention and targeted negative
controls. Planning validation/tests and diff ownership are required. Counts are
derived from actual output, never copied from a predecessor's summary.

No runtime test suite or corpus import is needed for this contract-only change;
subsequent implementation must supply real runtime acceptance and performance
evidence. All current criteria remain pending until the actual checks pass.
