# DTD value helper contract

`DtdValueDecoder` is a single-literal incremental, disposable operation. `Decode`
accepts borrowed UTF-16 input and caller-owned output (capacity >=2), excludes
literal delimiters, and returns exact per-call accepted/written UTF-16 counts.
Output is meaningful only through Written. It decodes numeric references at
declaration time and preserves general reference spelling. It permits markup in
replacement text. Caller composes parameter entities and normalizes source
newlines first; the helper does not normalize numeric-reference output again.

NeedInput consumes the entire nonfinal input; NeedOutput owns at most one pending
scalar and resumes with the unconsumed suffix/final flag and fresh output.
No numeric spelling is buffered, including unbounded leading zeros. Numeric
accumulation is bounded to U+10FFFF. Every output capacity >=2 makes progress;
one scalar is never partly emitted. Final is sticky, completion/rejection/fault
terminal, capacity misuse happens before mutation, reentrancy is rejected.
Rejection owns a diagnostic; cancellation/ledger exceptions propagate and fault
the decoder. Caller supplies the reference/literal location; this helper does
not track source positions, byte mappings, raw source or retained names.

`DtdValueProcessor.NormalizeAttributeDefault` applies an unnormalized literal
only with the supplied effective general-entity table. Declaration ReplacementText
already contains decoded declaration character references. Iterative expansion
preserves direct character-reference whitespace; literal TAB/LF/CR becomes space.
All non-CDATA types trim/collapse #x20 only, leaving referenced TAB/LF/CR intact.
Predefined references yield their scalar. Literal less-than is rejected in all
attribute replacement frames; referenced less-than is legal. Undeclared,
external/unparsed/parameter and active-name cycles reject. Model defaults are
never mutated. The caller selects declarations/precedence and remains table owner.
No source string or table is retained after this synchronous operation returns.
The final string, frame list, active-name set and builder are explicit owned
allocation boundaries. An ordinal borrowed table probe avoids temporary name
strings; no custom hash, SearchValues or SIMD optimization is introduced.

The unchanged optional `IDtdBudget` owns aggregate expansion and depth when supplied;
otherwise the helper owns local counters using a validated limits snapshot.
Numeric/predefined references enter/charge one scalar/exit. General entities
enter, charge every scalar of replacement source actually traversed (including
reference spelling), expand nested references, and exit. Thus nested levels
charge their performed work, and cancellation/refusal unwinds entered frames.
Raw primary-source and name charges always belong to grammar. TokenCharacters
bounds decoder emitted replacement scalars and final normalized attribute scalars
(after #x20 trim/collapse). UTF-16 output capacity uses checked Int32 arithmetic;
null quotas do not disable capacity or cycle safety. Cancellation is checked
per source scalar/digit/table probe and at entry.

These are internal helpers, not DtdParser/source-stack/public stubs. Standalone
sample/benchmark projects compile only Common diagnostics/options, verified
IDtdBudget/model, and owned values. Parser-composition and x64/AOT measurements
remain enforcing integration/gate work. No end-to-end parser benchmark exists.
