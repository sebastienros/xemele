# M4 compiler implementation

`XPathCompiler : IXPathCompiler` is internal and stateless. Each operation uses a
borrowed offset lexer, explicit parser frames, unresolved syntax drafts and an
explicit postorder resolution stack. Unlimited depth does not use recursive CLR
calls. Syntax must reach EOF before prefix/function/arity resolution; malformed
XPath 2.0 suffixes therefore produce XPATH-SYNTAX first.

Tokens retain original UTF-16 offsets, including zero-length EOF. The lexer
validates XML scalars and NCName characters using integrated XML classification.
Operator words and `*` acquire their meaning from parser position. The Pratt
precedence levels implement the frozen grammar, including union binding more
strongly than unary minus. Repeated binary operations associate left.

Paths expand @, ., .. and // to frozen axis/node-test forms. Groups introduce no
AST variant; enclosing operations retain parentheses in their source spans.
Unknown-function and arity errors use the complete call span, variable-prefix
errors use the variable span, and name-test prefix errors use the name token.
Node-test and abbreviation step spans retain the source that produced them.
Messages are fixed bounded strings and never echo caller expression data.

Compile quotas include the integrated static context's ContextTokens and
ContextCharacters. Implicit xml consumes neither; explicit caller xml is a
charged entry. EOF and XML whitespace consume no lexical tokens. AST counts are
final variants + steps + node tests, including expanded // nodes, with no group
node. Depth is outer Expr=1; nested group/function argument/unary expression adds
one, step adds one, step predicate adds a further predicate and nested Expr
(two), and filter predicate adds predicate + Expr (two). Binary precedence frames
stay in the same Expr nesting. Every quota is checked before its destination
entry/variant is created. Expression strings are checked before lexer setup.

The final AST owns literal strings and expanded local names, snapshots child
collections and retains no expression buffer/draft. All these are persistence
allocations, distinct from the zero-allocation borrowed lexer and static matcher
primitive target. Resolution probes namespace keys with borrowed ordinal spans. Colon discovery
and registration URI/local-name comparisons poll during each at most 256-unit
chunk. Function registrations are enumerated rather than hashed; enumeration
bookkeeping is included in owned full-compile costs, not in the borrowed zero
allocation primitive target.
No evaluator, public facade, mutable AST cache or later milestone code is used.
The simple literal scalar loop preserves Unicode validation/cancellation; the
prepared delimiter suite evaluates loop, IndexOf, IndexOfAny and cached
SearchValues with an identical delimiter-search oracle. Its measurements cannot
alone justify replacing the validating lexical loop.

Verified model construction repair 73636914659607df5f7bc20ecfa8767b54a97a96 is
adopted. Every name/PI validator and AST collection construction boundary now
receives the operation cancellation token through its internal overload. Owned
string copying and namespace comparisons operate in at most 256-unit chunks.
Number conversion scans with bounded polling and feeds at most 1101 significant
digits plus exponent to the framework converter, preserving rounding through a
sticky tail digit. A fixed 1132-character stack buffer belongs to one nonrecursive
number-helper call; it is not stackallocated repeatedly in the resolution loop.
Existing public constructors and signatures remain intact.

# Actual output/API inventory

- `src/Xemele/XPath/Compilation/Lexer.cs`: internal borrowed XPathLexer/token kinds.
- `src/Xemele/XPath/Compilation/XPathCompiler.cs`: internal compiler + frame/draft implementation.
- `src/Xemele/XPath/Compilation/Resolution.cs`: polled colon/name/prefix/function probes.
- `src/Xemele/XPath/Compilation/Number.cs`: bounded decimal conversion + cancellation.
- `docs/api/M4-compile.txt`: valid empty xemele-api-v1 public fragment; no public additions.
- `samples/M4-compile/Program.cs`: compiled friend-assembly demonstration using
  the existing Xemele.Benchmarks friend identity, not a new public entry point.
- `samples/M4-compile/PackedConsumer.cs.fixture`: independent packed net10.0
  consumer exercises the internal compiler by reflection and the public model
  normally; this is demonstration code, not an added public access path.
- `docs/reports/M4-compile/check_consumer.py`: reused verified consumer workflow,
  isolated temporary global.json/package cache and local feed.
- `tests/Xemele.Tests/M4-compile/`: focused AST, lexical, grammar, diagnostics,
  static resolution, quotas, deep-stack, cancellation, 105 inventory compile
  outcomes and 333 independent pinned framework scalar-parser comparisons.
- `benchmarks/Xemele.Benchmarks/M4-compile/Probe.cs.fixture`: prepared 25-case
  single ShortRun NoEmit suite, semantic-only controls and leased allocation gate.
- `docs/reports/M4-compile/Compiler.Probe.csproj` and `freeze.py`: owned driver
  and source/generated/binary/runtime identity capture, adapted from model support.

Sample output:

```text
XPathPath absolute=True steps=3 span=0:24
DescendantOrSelf Node predicates=0 span=0:2
Child Name predicates=1 span=2:16
Child Name predicates=0 span=19:5
XPATH-ARITY 0:7
```

Framework reference is SDK 10.0.400 / runtime 10.0.11, invariant XPath semantics.
Inventory execution checks compilation only; navigation/function/evaluation
expected values are intentionally left to their own lanes. Scalar comparison
uses a test-only interpreter, never a production evaluator dependency.
