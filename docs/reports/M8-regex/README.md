# M8-regex branch implementation

`XmlSchemaRegexEngine` implements the frozen compiler/matcher interfaces without
adding public runtime members. The opaque program owns a private immutable
Thompson automaton and character predicates; input spans are consumed synchronously.
Whole-string language semantics, literal ^/$, all Appendix F productions, nullable
loops, finite/unbounded repeats, categories, blocks, XML name escapes, negation and
nested subtraction are implemented. Neither implementation nor correctness oracle
uses System.Text.RegularExpressions.

The parser constructs temporary owned syntax nodes, charging each node/term/state
before construction; scalar scanning, graph emission, Unicode block lookup,
predicate comparisons, epsilon transitions and matching share the caller's work
ledger. Programs retain no compiler, caller operation, pattern, mutable builder or
run state. Runs use fixed stack scratch through 128 states and a single returned
pool lease above that threshold. Scratch is five state-count-sized slices; seen
sets bound nullable epsilon closure without recursion or backtracking.

Resource refusals return false with an incomplete matching operation, or throw an
owned compile exception. Syntax rejection uses REGEX.Syntax and the supplied
location. Cancellation is polled through charge calls at entry, during scans and
transitions, and before success. TokenCharacters counts scalars separately for a
pattern/input; depth counts the enclosing expression/class nesting. The compiler's
finite recursive representation has a 512-level safety bound even when policy
Depth is unlimited; expanded state/scratch indices and repeat counts have checked
Int32 bounds. Exceeding representation capacity is SCHEMA.Limit, never syntax or
a successful truncated compilation. Large repeats stop before state allocation
when the aggregate component/work ledger refuses.

Unicode category classification follows the pinned .NET runtime's Unicode database,
as the specification permits later Unicode versions. Blocks use Unicode 17 data,
including supplementary-plane blocks, and historical Greek,
CombiningMarksforSymbols and PrivateUse aliases. Specials includes the historical
FEFF point plus its XML-valid modern range. XML name escapes use the referenced
XML 1.0 Second Edition's explicit productions, not current CLR identifier rules.
Source URL/digest provenance and independently read block boundary vectors are in
`tests/fixtures/M8-regex/`; `generate-tables.py` reproduces factual tables from
ignored downloads. The owned tests cover every pinned block, all 29 scalar
categories, independent finite-language concatenation enumeration, invalid syntax,
exact/one-over quotas, cancellation and concurrent stack/pool run state.

`checks.py` runs only the owned regex and affected model correctness classes, then
frozen declaration comparison, the prior public API union and a real XML-only
consumer preserving M5. It asserts positive test counts; it never selects resolver
allocation tests or runs measurement. Gate-level comprehensive corpus and x64
release confirmation remain coordinator/gate responsibilities.

Measurement preparation and results are recorded separately. Warm borrowed matches
promise zero steady-state managed allocation; compiler/program construction,
operation/result ownership, static initialization and pool growth are distinct
boundaries. No throughput optimization is claimed: straightforward span loops are
used; no SearchValues, hashing or custom SIMD is introduced.
