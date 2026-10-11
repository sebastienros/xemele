# Compiler finalization join

The internal compiler owns a dense immutable graph and publishes no public schema
facade or executable datatype library. `Datatypes.Find/FindType` return null in
the definition-only registry. The verified validation facade must replace it
with a sealed registry using the verified real implementations before publication.

Every authored data/value occurrence surviving include overrides is retained,
including disconnected checkpoints for unreachable definitions and patterns
removed by empty/notAllowed simplification. Checkpoints retain their original
datatype, parsed lexical text, parameter order/occurrences, namespace/base snapshot
and source location; detached data checkpoints have no matching-except edge.
Instance traversal follows Start only; facade finalization checks all Patterns.
Do not treat disconnected checkpoints as alternative instance start patterns.

The RNG XSD library URI stays `http://www.w3.org/2001/XMLSchema-datatypes` in
Datatype. The real join maps its type identifiers to the shared XSD type names
`http://www.w3.org/2001/XMLSchema`. Original lexical value and param strings are
owned unchanged. For value only, Context.DefaultNamespace is the inherited RNG
ns value; Prefixes and BaseUri remain the original resource context. Param
contexts are their own original contexts.

The supported profile accepts ten XSD parameter names: length, minLength,
maxLength, pattern, maxInclusive, maxExclusive, minInclusive, minExclusive,
totalDigits, fractionDigits. Each maps to the same XmlFacetKind, with Fixed=false.
Built-in string/token accept none. Excluded whiteSpace/enumeration and unknown
or differently cased parameter names reject as RNG.Datatype at the param source.
NCName lexical errors are schema restrictions. Unsupported libraries reject.

The facade must complete type existence, parameter applicability/lexical values,
constraint consistency and value lexical correctness before returning a schema.
Repeated pattern parameters mean conjunction (AND), unlike sibling XSD pattern
facets. A repeated non-pattern parameter is a schema error. Normalize non-pattern
parameter values with XML whitespace collapse at that join without replacing
the retained original string/context. Parse/compile regex with the shared budget.

Primary sources: [RELAX NG 2001 sections 4.16 and 6.2.9](https://relaxng.org/spec-20011203.html),
[RNG XSD datatype guidelines section 2](https://relaxng.org/xsd-20010907.html).
