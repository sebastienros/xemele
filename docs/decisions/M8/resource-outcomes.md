# Additive schema resource outcomes

Exact frozen declarations and executable representative usage live in
`tests/Contracts/Fixtures/M8/ResourceOutcomes/`. The isolated fixture compiled
and executed on Release SDK 10.0.400/runtime 10.0.11 before runtime edits.
`Public.golden.txt` freezes the entire representative resolver surface;
`docs/api/M8-schema-resource-outcomes.txt` contains only additive public rows.
All 1,417 earlier public rows and frozen schema lane interfaces are preserved.

`XmlResourceResolutionStatus` is Denied=0, Unavailable=1, Resolved=2.
The readonly non-disposable result has private construction, getter-only Status
and Resource, static Denied/Unavailable and FromResource(non-null). Default is
Denied/null. Only FromResource creates Resolved and rejects null. Copying a result
neither disposes nor transfers its resource. Returning Resolved across the
resolver boundary transfers the existing wrapper to its receiving loader once.

The virtual synchronous ResolveResult polls cancellation then invokes legacy
Resolve exactly once with identical request/token, maps null to Denied and
catches nothing. It deliberately does not poll after acquisition: cleanup must
first be protected by the receiving loader. Resolve and ResolveAsync declarations
and all non-schema consumers stay unchanged.

Concrete internal TryLoadOptional(reference, effectiveBaseUri, operation,
out XmlDocument? document) reuses mandatory Load acquisition/parsing/cleanup.
Only explicit Unavailable returns false/null, after polling the complete shared
operation. Missing resolver, invalid base, legacy null, default and Denied refuse;
mandatory Load also refuses Unavailable. Nothing recovers after Refuse or turns
caller/parser/I/O/quota/cancellation errors into absence. Every optional resolution
attempt charges one shared work unit before the resolver, including unavailable
attempts. Mandatory parsing retains its established scalar debit. Resource capacity still guards before callback. Absence consumes no
successful external-resource slot or bytes, creates no document/cache entry and
never resets the shared parsing/work allowance. Inclusion scope balances on all
paths. Redirected source/base and exactly-once cleanup remain unchanged; cleanup
failure cannot replace a primary exception.

This task supplies the seam only. Ordered XSD compiler ownership decides optional
include/import and empty or annotation-only redefine; substantive redefine is
mandatory. No async, compiler, corpus, DTD/entity/catalog/XInclude call sites or
other language composition semantics change here. Allocation preparation and
measurement require coordinator source acceptance and a fresh exclusive grant.
