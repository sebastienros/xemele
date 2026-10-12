# Approved internal RNG adapter protocol

The coordinator approved this convention after bounded review of the frozen
contracts. It adds no frozen interface, record property or public API.

RNG XSD parameter occurrences are deliberately represented by a fresh
XmlDatatypeDefinition object with Name=(RNG XSD-datatypes library URI,built-in
local name), BaseType=(canonical XSD URI,the same local name), matching canonical
Variety, ItemType=null and an empty MemberTypes list. This is a restriction-only
wrapper, including for built-in lists. The evaluator validates the complete
marker shape and known library/type before selecting conjunctive RNG pattern
semantics. Malformed/unknown markers fail closed. Arbitrary synthetic names,
Anonymous, source locations and registry reference identity never determine an
XSD/RNG origin. Ordinary XSD restrictions retain sibling pattern alternatives.

RngDatatypeFacetAdapter.ValidateParameters creates this explicit per-occurrence
object, snapshots the parameter collection and retains each facet's context and
location. Object identity distinguishes occurrences with equal alias names, and
the weak prepared cache separates both definition and registry identity. The
facade maps the accepted ten parameter names to XmlFacetKind with Fixed=false;
unknown names, whiteSpace/enumeration and repeated scalar parameters reject.
Repeated pattern parameters conjoin. Pattern strings stay authored; other values
are normalized with XML whitespace collapse while retaining their original
owned facet strings and contexts.

For the empty datatype library, the explicit adapter checks known string/token
and requires zero parameters. A successful null parameter-definition means use
the verified existing empty library directly, never a definitions-only library
or guessed no-target-namespace type. Generic detached/unregistered XSD definitions
with no target namespace remain valid inputs to the frozen evaluator; they are
not interpreted as RNG empty-library objects.

The RNG facade must run this finalization on every retained compiler checkpoint
before publishing a schema, including detached occurrences and unreachable/
simplified graph checkpoints. Shared limits and cancellation use one operation;
no quota is reset per parameter, regex, member or occurrence. Generic errors and
regex syntax failures must be reported as RNG datatype/schema diagnostics at
that facade boundary while caller exceptions propagate unchanged.

Required NOTATION enumeration and declaration checks remain XSD prepublication
obligations, as documented in this task's README. Bare/parameterized RNG NOTATION
retains the accepted QName-like datatype semantics.
