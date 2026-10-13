# First root check failure

Tested source: `0099205c7b20c2ccc865a87d341d861db4f842a3`.
Root preparation passed all8 commands with zero warnings/errors and verified
source/runtime/output bindings before issuing its preparation certificate.
The first normal probe in the seven-command check phase failed with
`System.InvalidOperationException: report file failure` at `Probe.cs.fixture:111`.
The remaining six check commands were unrun. Root observed exit1, both owned
groups absent, inputs matched, no deadline failure, and inner raw output deleted.

Observed public boundaries before the failure: RNG compile2129/components474/
validation83; XSD27466/3765/157; Schematron1315/50/904. These are partial probe
observations, not completion of the acceptance checks.

Source diagnosis: failure to create a temporary report in a nonexistent output
directory becomes `InvocationException` (exit2), but unconditional cleanup then
calls `File.Delete` in that missing directory and throws a second I/O exception.
The generic failure catch consequently returns exit3. The narrow fix applies the
existing XML command cleanup guard: delete only an existing temporary file.
The authored expected exit2 remains unchanged; failed output creation publishes
no report. The assertion now includes observed exit/error for bounded diagnosis.

Worker work remains source-only. No follow-up build/check/counter has run here;
successful preparation receipts and the root certificate remain untouched until
root replaces or consumes them. Task criteria/status are unchanged.
