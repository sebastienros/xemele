# XPath contract

This contract specifies grammar, semantics, lifetimes, quotas, and diagnostics.
Tree identities use XmlNode, XmlAttribute, and XmlDocument.

The profile is XPath 1.0 expressions, with all 13 axes, 14 binary operators,
unary minus and all 27 core functions. No XPath 2.0 sequences, types, `if`,
`for`, comments, scientific numeric literals, or implicit extension library.
The grammar and lexical rules follow below. The pinned
normative reference is the [16 November 1999 Recommendation](https://www.w3.org/TR/1999/REC-xpath-19991116/).
`tests/corpora/xpath/inventory.json` contains an explicit acceptance ID and concrete expected
behavior for every axis/operator/function and grammar/edge case. These are
expected acceptance vectors; inventory presence alone is not a test pass.

Prefix bindings are copied at Compile, including expanded variable and function
names. Unprefixed names use the empty namespace. XPathContext contains expanded
variable/function bindings, limits and cancellation; it has no dynamic namespace
property. Compile copies extension signatures; Evaluate copies bindings and
snapshots the input tree under quotas before dispatch. Compiled expressions are
immutable, reentrant and reusable. Result membership, order, names and values are
owned snapshots; exposed tree handles retain their existing live tree semantics.

## Grammar and lexical rules

Support the entire XPath 1.0 expression grammar. Here is the implementation
baseline (QName/NCName use XML Namespaces name characters; S is XML whitespace).
Each repeated infix precedence level associates left; unary minus recurses.

```text
Expr := OrExpr
OrExpr := AndExpr ('or' AndExpr)*
AndExpr := EqualityExpr ('and' EqualityExpr)*
EqualityExpr := RelationalExpr (('=' | '!=') RelationalExpr)*
RelationalExpr := AdditiveExpr (('<' | '<=' | '>' | '>=') AdditiveExpr)*
AdditiveExpr := MultiplicativeExpr (('+' | '-') MultiplicativeExpr)*
MultiplicativeExpr := UnaryExpr (('*' | 'div' | 'mod') UnaryExpr)*
UnaryExpr := '-' UnaryExpr | UnionExpr
UnionExpr := PathExpr ('|' PathExpr)*
PathExpr := LocationPath | FilterExpr | FilterExpr ('/' | '//') RelativeLocationPath
FilterExpr := PrimaryExpr Predicate*
PrimaryExpr := '$' QName | '(' Expr ')' | Literal | Number | FunctionCall
FunctionCall := FunctionName '(' (Expr (',' Expr)*)? ')'
LocationPath := RelativeLocationPath | AbsoluteLocationPath
AbsoluteLocationPath := '/' RelativeLocationPath? | '//' RelativeLocationPath
RelativeLocationPath := Step (('/' | '//') Step)*
Step := AxisSpecifier NodeTest Predicate* | '.' | '..'
AxisSpecifier := AxisName '::' | '@' | empty
NodeTest := NameTest | NodeType '(' ')' | 'processing-instruction' '(' Literal ')'
NameTest := '*' | NCName ':*' | QName
NodeType := 'node' | 'text' | 'comment' | 'processing-instruction'
Predicate := '[' Expr ']'
Literal := single-quoted characters other than single quote
         | double-quoted characters other than double quote
Number := Digits ('.' Digits?)? | '.' Digits
FunctionName := QName other than the four NodeType names
```

AxisName is exactly the 13 entries in tests/corpora/xpath/inventory.json. Expand empty axis to child,
@ to attribute, . to self::node(), .. to parent::node(), and each // to
/descendant-or-self::node()/. Absolute // begins at the root. No empty relative
step, trailing /, variable name wildcard, literal escape or exponent notation.
Whitespace may separate tokens but not split a QName, decimal number or ::.
Longest lexical match wins. A '*' is multiplication only in the operator
position; otherwise a principal-node wildcard. 'and', 'or', 'div', 'mod' are
operators only following an operand (not after @, ::, (, [, comma, or another
operator). Names such as child::div remain names. Name before :: is an axis;
name before ( is a function unless a NodeType in node-test position. '-' within
NCName remains a name character. Input is already decoded text, without XML
entity unescaping; caller supplies decoded attribute content. Reject invalid
surrogates and invalid XML name characters with spans. Numbers are invariant
IEEE 754 doubles; overflow gives positive infinity; arithmetic uses IEEE rules.

## AST and component interfaces

XPathAst variants are literal,
variable, unary minus, binary, call, filter and path. A path distinguishes
absolute root from relative focus and may start with a filter expression.
Steps retain axis, expanded node test, ordered predicates and source span.
Expand abbreviations before returning AST, retain original diagnostic spans.
Every production constructor snapshots incoming collections, validates spans,
nulls and node-test payloads, and exposes read-only owned arrays without mutable
aliases. Records do not imply a deep snapshot by themselves. No mutable AST
cache or per-evaluation focus/budget lives in a compiled expression.

IXPathCompiler receives a validated immutable XPathStaticContext and returns AST.
IXPathNavigator is a single-document evaluation snapshot. SelectAxis returns
unique items in axis proximity order (reverse for ancestor, ancestor-or-self,
preceding and preceding-sibling); other axes use document order. It performs no
node-test filtering. CompareDocumentOrder returns negative/zero/positive and
rejects foreign snapshot identities. SelectId returns elements in document
order; FindLanguage starts with item element/owner/parent and walks xml:lang
ancestors, preserving empty declarations. Root is the document root item.

IXPathFunctionLibrary.Signatures exposes exactly the 27 immutable core signatures
listed in inventory, all in the empty namespace. Invoke validates argument count
and focus (1 <= Position <= Size), uses XPath conversion rules, and charges the
same evaluation budget as navigation. No extension dispatch inside the core
library. Evaluator resolves variables, predicates, operators, calls and context
changes; composition implements Compile/Evaluate and public diagnostics.
The internal model defines the compiler, navigator and function interfaces.
Extension calls are known by
compile-time signatures and dispatched from per-evaluation functions.

## Public values and item identities

XPathExpandedName is ordinal (NamespaceUri, LocalName), without prefix; require
nonnull URI and valid NCName local name at all public API boundaries. Default
expanded names are invalid. No URI normalization. XPathValue has precisely
NodeSet, Boolean, Number and String. default(XPathValue) is the empty node-set.
FromString rejects null; FromNumber accepts NaN/infinities and signed zero;
FromNodeSet enumerates once into owned immutable storage, rejects defaults,
normalizes duplicates and document order, and rejects mixed documents. It is an
explicit materialization outside an Evaluate budget; Enumerable user code is
caller-controlled, not bounded engine work. AsNumber/AsString/AsBoolean perform
XPath conversions. AsNodeSet requires NodeSet, otherwise InvalidOperationException.
AsString uses XPath number formatting (NaN, Infinity, -Infinity, zero for both
zeros, no exponent, shortest round-tripping decimal subject to XPath integral
form). Scalar string conversions are culture-independent and Unicode scalar
string operations do not split surrogate pairs. Boolean of empty string,
empty node-set, NaN and either zero is false; nonempty string 'false' is true.
String-to-number trims only XML S, accepts optional leading minus and decimal
Number grammar, rejects plus/exponent/Infinity/non-XML whitespace as NaN.

FromNode and evaluation XmlNode overload require nondefault, document-connected
nodes; reject Fragment/detached subtrees with ArgumentException. A document node
itself is supported even with no root element. FromAttribute requires an attached
attribute on a document-connected element. FromNamespace requires an element
and currently in-scope prefix; absent prefix is ArgumentException. Factories
snapshot item metadata/strings at construction; Evaluate rebinds their identities
to its entry snapshot. An identity absent from that snapshot is XPATH-CONTEXT
(for context) or XPATH-NODESET (for variables). Variables' node-sets must belong
to the same input document and are rebound similarly; scalar values are copied.

Root/element/comment/PI identity is XmlNode identity. Attribute identity is its
actual stable XmlAttribute slot handle, with owner/expanded-name metadata; remove
and re-add produces a different identity. Node is null for attribute/namespace;
Attribute is present only for attribute; OwnerElement is present only for
attribute/namespace. Namespace identity is (owner XmlNode, ordinal prefix), never
a declaration handle: inherited bindings on different owners are distinct.
Namespace local name and Name are the prefix, NamespaceUri and Prefix are empty,
and StringValue is its bound URI. xml is always present; xmlns never is; no
empty undeclared default binding is emitted. Namespace declarations are not
attributes. Order after an element is namespaces by ordinal prefix, attributes
by (ordinal NamespaceUri, LocalName), then children; this deterministic choice
fixes XPath's otherwise implementation-dependent namespace/attribute order.

Adjacent Text/CData siblings are one logical text item, ignoring no intervening
node. Empty aggregate runs are omitted. Node points to the first physical member
of the maximal run (even if it is empty); identity uses that handle. FromNode of
any member maps to that same logical run. Text item string is concatenated run
values; names are empty. Element/root string is descendant logical text in order,
excluding comments/PIs. PI LocalName/Name is target, NamespaceUri/Prefix empty;
comment/root/text have empty names. Element/attribute Name is lexical prefix:local
or local; NamespaceUri is expanded URI. Item equality ignores snapshot metadata,
uses above identities; default items equal each other, IsDefault=true, metadata
access throws InvalidOperationException. Nondefault values and collections own
snapshots; Node/Attribute handles remain live and retain their whole document.

All axes follow the XPath data model. Attribute/namespace parent is owner;
they have no children or siblings. Following/preceding never include attributes,
namespaces or (respectively) descendants/ancestors; following from an attribute
or namespace includes its owner's children. Namespace/attribute principal node
kind is their own kind; all other axes principal kind is element. Name tests
match expanded names with unprefixed element/attribute names in empty namespace,
not the document's default namespace. p:* matches the compiled URI. node() matches
all logical kinds. Numeric predicate is equality to proximity position; other
values use boolean conversion. Predicates filter sequentially with updated size;
reverse-axis step predicates use reverse proximity, parenthesized filter uses
document order. Path/union/filter final node-sets are sorted and deduplicated.

## Static and dynamic contexts; mutation

Compile parses the complete grammar before static name/function resolution, so
trailing XPath 2.0 syntax is XPATH-SYNTAX even if a preceding function name is
unknown. Compile copies options, prefix map, extension signatures and limits. Only xml
is implicitly bound to http://www.w3.org/XML/1998/namespace; reject xmlns, wrong
xml binding, empty prefix, empty URI or malformed prefix. Unprefixed variables
and functions are in empty namespace, as are unprefixed name tests. Unknown
prefix is compile error; variable existence is checked dynamically. Extension
signatures require nonempty namespace, valid expanded name, nonnegative minimum,
maximum null (variadic) or >= minimum, no duplicates; core names cannot be
replaced. Unknown functions and wrong arity are compile errors. Context functions
are keyed by expanded name; missing used extension is evaluation error. Unused
extra bindings are permitted but must be valid; no dynamic QName resolution.

Evaluate snapshots context dictionaries/limits at entry, then the entire source
document's logical items, values, topology, namespace scope and ID metadata under
input quotas before evaluating AST. Snapshot construction requires exclusive
caller access to the document: concurrent mutation/callback enumeration mutation
while copying is unsupported, documented caller error (no thread-safety claim).
The engine invokes no user extensions before snapshot completion. Mutation by an
extension after that point affects the live tree but not the running query or
owned results. Subsequent Evaluate calls see new state. An expression supports
concurrent evaluations on distinct trees or on a shared tree with no mutation
while their snapshots copy; extension implementations are caller-thread-safe.
Results preserve identity/membership/order/string/name snapshots after mutations;
re-evaluating an old context/item resolves its identity in the new snapshot.
An implementation may reuse the snapshot of a document across evaluations while its
content revision and DTD model are unchanged, and may hold it weakly. Each reuse still
enforces the input, string and operation quotas and cancellation, and charges exactly the
operations a fresh capture would; any possible limit failure takes the metered capture.
Node-set results are always returned as owned copies, never as views of a shared snapshot.

id() recognizes attributes declared ID in retained XML DtdModel by lexical element
and attribute QName, including defaulted attributes where applicable. It does
not infer ID from names 'id' or xml:id and does not require schema validation.
Tokenize argument strings by XML S; node-set argument uses every member string.
Return matching elements once in document order. Duplicate ID values in a
nonvalidating tree yield all matches. Do not fetch DTD/resources. Trees with
unresolved references/incomplete attribute values/defaults reject snapshot with
XPATH-INCOMPLETE; recovery lexical placeholders cannot become XPath text.

## Operations, sizes and cancellation

All nullable limits must be positive or null (explicit unlimited); invalid
options give ArgumentOutOfRangeException before work. Null options means finite
documented defaults. Checked counters; exactly at a limit succeeds, one-over fails
before growing destination. Representational Int32/array/double limits remain.
Limits copy once per operation. MaxExpressionCharacters/MaxStringCharacters/
MaxInputCharacters count UTF-16 code units; MaxTokens counts non-whitespace
lexical tokens including punctuation, excluding EOF; MaxAstNodes counts all AST
variants plus steps and node tests; MaxDepth counts parser expression/step/predicate
nesting with outer Expr at 1 (implement iteratively or reject before recursion).
Expression characters checked before tokenization, tokens before storing, AST
nodes before creation. Namespace/function option entry and string data copying
also consumes compile MaxTokens (one per binding/signature) and
MaxExpressionCharacters (aggregate UTF-16 prefix/URI/name chars plus expression).
This bounds compile context setup, not just expression text. These context
charges are in addition to lexical tokens; documentation/tests must distinguish.

Input items count root, logical nodes, attributes and each in-scope namespace on
each element. Snapshot input characters aggregate each item's string and name
fields once (StringValue, LocalName, NamespaceUri, Prefix, Name), including
repeated root/element text and inherited URI materialization. A physical text
member visited while coalescing also charges an operation even when omitted.
MaxInputItems and MaxInputCharacters are pre-growth checks during snapshot.
MaxNodeSetItems applies to every scratch/intermediate/result node-set, including
variable and extension returns. MaxStringCharacters bounds every engine-owned
string (literals/variables/item strings/conversions/function intermediates/returns),
not solely final scalar values. Reject over-limit incoming scalars before AST work.

Charge one operation for each physical snapshot node/member visit; each inherited
namespace scope entry inspected; each UTF-16 code unit scanned/copied in snapshot,
comparison, conversion or string function; each AST evaluation, predicate candidate,
axis candidate (including rejected tests), node identity/hash/order comparison,
ID/lang candidate, variable/argument/return entry copied or inspected; and before
each extension dispatch. Sorting/deduplication/equality pairs are charged per
comparison/identity inspection; lazy axes may not bypass work charges. Check
cancellation at Compile/Evaluate entry, before external callback/extension work,
and at most every 256 charged tokens/characters/operations including copying.
Compile loops use the same bounded polling cadence even with unlimited quotas.
No partial result on limits/cancellation. Long counters are checked, overflow
is an appropriate compile/evaluation limit diagnostic.

Extensions get immutable argument arrays/focus and shared ChargeOperations,
CancellationToken. Negative charges are ArgumentOutOfRangeException; zero is a
cancellation checkpoint. Engine checks cancellation and charges before dispatch,
then checks cancellation, node-set/string caps and return identities after return.
Node-set returns must contain only identities present in the entry snapshot;
metadata is rebound to that snapshot. Extension code can mutate tree and allocate,
but must cooperate by charging/polling its own work. Quotas cannot preempt arbitrary
user code or bound allocations performed inside it. All callback exceptions,
including engine-shaped exceptions thrown by callbacks, propagate by identity;
only engine-owned failures become structured diagnostics. Top-level Compile
cancellation is an explicit parameter; Evaluate reads context cancellation.

## Errors and numerical/operator behavior

XPathSourceSpan uses zero-based UTF-16 Start/Length in original expression; EOF
is (expression.Length, 0). Diagnostic strings are owned, stable code/category,
no shared mutable lists; one terminal diagnostic per exception. Engine messages
are at most 256 UTF-16 units and never embed unbounded expression/variable text. CompileException
requires Syntax/StaticContext/CompileLimit; EvaluationException requires
Evaluation/EvaluationLimit. Nonnull diagnostic and message/code validation;
constructors reject invalid category/span arguments.

Codes: XPATH-SYNTAX (Syntax: bad token/grammar), XPATH-PREFIX,
XPATH-FUNCTION, XPATH-ARITY (StaticContext), XPATH-COMPILE-LIMIT
(CompileLimit); XPATH-VARIABLE, XPATH-EXTENSION, XPATH-TYPE,
XPATH-CONTEXT, XPATH-NODESET, XPATH-INCOMPLETE (Evaluation);
XPATH-EVALUATION-LIMIT (EvaluationLimit). Failure span is failing AST/operator/
call/variable span; entry/snapshot failures span entire expression. A malformed
public argument, null/default handle, invalid option or invalid registration
throws argument exception, not language exception. Cancellation throws
OperationCanceledException with supplied token. No catch-all callback translation.

and/or short-circuit left-to-right. Equality follows XPath's boolean-first,
then number, then string scalar coercion; node-set pairs use existential string
comparison, node-set vs number existential numeric comparison, node-set vs string
existential string comparison, node-set vs boolean boolean conversion of whole
set. != is an independent existential comparison, not negation of =. Relational
operators use existential numeric pairs. Arithmetic uses numbers; mod is truncated
division remainder, not IEEE remainder; sign follows dividend. Union/path/filter
inputs require node-set (XPATH-TYPE). NaN equals nothing and all relational
comparisons with it are false; -0 preserves IEEE arithmetic but string '0'.
round() ties toward positive infinity and returns negative zero for [-0.5,0).
substring uses rounded one-based scalar positions and rounded length boundaries;
NaN boundaries yield empty; infinite bounds follow XPath comparisons. normalize-
space uses only XML S; translate replaces first occurrence mapping, drops chars
without corresponding replacement, and ignores duplicate source mapping tails.
lang() case-insensitive exact match or requested prefix followed by '-'; empty
xml:lang stops ancestor search. sum(empty)=0. No locale/culture dependencies.
All function arities, examples and results are enumerated in tests/corpora/xpath/inventory.json.

## Contract and conformance checks

The conformance suite executes the canonical inventory against the production
facade and checks its hash, count and unique IDs. API comparison uses the shared
compiled checker. Production checks must execute all vectors plus the
mutation/cancellation/limits/static-context
requirements above. Differential reference: .NET System.Xml.XPath XPathNavigator
on equivalent fully resolved XML for core expressions (excluding chosen ordering,
ID inference, extension and quota differences); pin .NET 10.0.11 and invariant
culture. Expected mismatches require explicit vector classification.
