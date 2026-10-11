# Additive HTML processing instructions

Owner: M7-html-pi-contract; implementation: M7-html-pi-model, M7-tokenizer,
M7-treebuilder and M7-html-serialize in dependency order. This repairs an omission
in the original six-kind freeze without changing any existing public member.

## Authority and complete algorithm boundary

The [WHATWG parsing standard](https://html.spec.whatwg.org/multipage/parsing.html)
last updated 9 October 2026 is pinned by its retrieved UTF-8 source SHA256
`34e193906e6898ae7faf576a8d4c0f8de619a86ec99d85a3baeb2713171f1dbf`.
Sections 13.2.5.6 and 13.2.5.72–76 require processing instructions in ordinary
HTML Data parsing; this is mandatory, with no feature flag. Other initial states
retain their own lexical rules. Target spelling preserves case. Invalid targets
and ASCII-insensitive xml/xml-stylesheet targets become bogus comments. EOF in
these PI states emits EOF without an unfinished PI. Data preserves NUL; named
references are not decoded in PI data. The complete normative transitions govern
whitespace, question marks and either terminating delimiter; implementations may
not simplify these to XML PI rules.

## Exact additive declarations and model semantics

Append ProcessingInstruction=6 to both HtmlTokenKind and HtmlNodeKind. The existing
six values in each enum remain 0–5. Add only
`HtmlNode HtmlDocument.CreateProcessingInstruction(string target, string data)`.
No new option, token constructor or public tokenizer state is added.

For emitted PI tokens, Name is target (the same exact target spelling held in a corresponding node LocalName) and Data is data, Attributes is empty,
SelfClosing=false, Doctype=null. Existing immutable ownership, normalized source
location, callback retention, cancellation and cleanup contracts apply. The public
HtmlToken constructor retains its earlier structural validations; it does not
newly impose kind-specific semantic validation on arbitrary programmatic tokens.

The factory rejects null target/data with ArgumentNullException; empty or invalid
target throws ArgumentException for target. Its target grammar is ASCII letter or
underscore first, followed by ASCII letters/digits/hyphen/underscore; xml and
xml-stylesheet are rejected ASCII-insensitively. This deliberately matches the
parsed HTML target domain, without XML-name coercion. Target case remains exact.
Data is an arbitrary owned immutable string, including NUL, control characters,
`>` and `?>`; neither factory nor Value setter sanitizes it. Output reparsing
identity is not promised for programmatic malformed markup.

A PI node has immutable LocalName=target, mutable Value=data, empty NamespaceUri
and Prefix, empty Attributes/Children, null Doctype/TemplateContent and its
permanent OwnerDocument. It cannot contain children or accept attributes. Value
null and quota failures leave the previous value unchanged. Target and data each
obey TokenCharacters in Unicode scalars; the target is interned and obeys
InternedNames. Validate target, data and all quotas before changing the owner's
name table or creating the node. No new quota unit or token-count quota is added.
Input/expanded-document counters remain parser-owned; factory creation follows
existing model factory limits rather than inventing cumulative input accounting.

PI nodes may attach to Document, Fragment or Element with ordinary owner, depth,
acyclicity, ordering and atomic fragment-transfer checks; they do not count as an
element root or doctype. Clone and Import preserve kind, target and data (including
case/NUL); deep and shallow PI copies are identical detached leaves. Import uses
the target owner's limits and permanent ownership. Copying containing trees and
template contents includes their PIs. GetTextContent excludes PI data, even on the
PI itself, matching the existing descendant-text-only model method. CSS :empty
ignores PIs and structural selector positions count elements only.

## Tree construction, fragments and output

Every insertion mode and foreign-content rule handles PI tokens as prescribed by
its current normative rule, including document-level positions before the root
and after it. The insertion helper creates a PI with token target/data at the
algorithm's adjusted insertion location; this includes template content and
foster-parenting when applicable. PI insertion neither implies element stack
changes nor contributes character data. Fragment parsing uses the real frozen
context and returns the ordinary fresh-owner fragment; PIs appear in their
prescribed order and do not leak into the supplied context owner.

Html5 serialization emits `<?`, target, one U+0020 space, data, and `?>`, including
empty data. No escaping or target folding is performed. Existing document/outer
node/fragment/template traversal and streaming ownership apply. Legacy parse
produces comments for '<?' per its pinned native parser, so no legacy lexical PI
reinterpretation is introduced. Legacy serialization of programmatic PI nodes
must receive its own native differential vectors; it cannot silently drop them.

## Corpus and declaration evidence boundary

Pinned html5lib-tests revision `c777c408b61078ea2eb4acefc2535f54dbc8b28a`
already expects PIs. test2.test zero-based cases 31–33 expect namespace/empty,
foo/bar, foo/bar? respectively. test3.test 1158–1162 expect no unfinished PI
at EOF. Original inputs, output and errors remain unchanged and required. The
owned report retains their exact content/hashes and independent normative
witnesses. There is no presumed stale-corpus exception. Only a demonstrated
case-specific expectation difference may receive a ledger naming original case,
state, normative rule and independently authored expected witness; this contract
creates no exclusions.

Three nonpackaged declaration families compile and compare their canonical API
fragments. The tokenizer consumer references both new enum members, factory,
Name/Data, mutation and clone/import signatures; bodies are never invoked.
Declaration lanes alone use NoWarn=CS0436 for intentional duplicate model types.
The separate ActualModels project excludes Model.cs.fixture and verifies only
already shipped HTML/XML preservation with normal warnings-as-errors. Actual PI
model behavior belongs to the later ordered implementation, never this repair.
