# RELAX NG evaluator and facade

M8-rng-validate owns the public `RelaxNgSchema` and the private evaluation join.
The compiler retains dense immutable IDs. Finalization checks every datatype/value
checkpoint, replaces the definitions-only registry, prepares explicit per-occurrence
RNG facet definitions and parses schema value literals before publication. The
runtime traverses Start only. Parameters retain their original lexical strings,
contexts and source coordinates. Shared facet diagnostics become RNG.Datatype at
this private boundary; external resolver/stream exceptions propagate unchanged.

Each run owns residual expressions, canonicalization, nullable results, element
match memoization, namespace snapshots, coalesced text and diagnostics. The
compiled graph, datatype values and prepared facet plans are immutable. Runs do
not mutate instance nodes. ContentRevision and cancellation are checked at
bounded transitions and before returning. Namespace declarations, comments and
processing instructions do not become instance attributes/children; comments and
PIs do not split adjacent character data. Weak matching accepts whitespace for
nullable content, and missing character data can match an empty lexical string.
Lists scan borrowed spans and XML whitespace; NBSP is not a token delimiter.

Work charges cover graph transitions, names, scalar scanning, copies, conversions,
regex, value comparisons and retained/scratch growth. Nested checks use the same
operation. Matching has a 1024-active-frame representational guard independent of
nullable XML depth quotas; overflow produces an incomplete VALIDATION.Limit result.
No matcher recursion can escape as stack overflow. Scratch is bounded by charged
work/component growth and finite index/counter capacities. Results and schema
objects necessarily own allocations; no full-validator zero-allocation or speed
claim is made. Allocation evidence is pending a fresh bounded host grant.

The true XML-only project includes foundations plus verified M8 common, regex,
datatype, facet and RNG compiler source. It excludes XSD, Schematron, HTML, CSS,
XSLT, CLR serializers and later milestones. `samples/M8-rng-validate/Consumer.csproj`
uses that assembly with `-p:RngXmlOnly=true`, and the normal assembly otherwise.
The frozen comparison checks the full approved RNG public/internal declaration
fixture against the emitted runtime. The task API fragment contains only its
seven public facade declarations, preserving predecessor fragments.
