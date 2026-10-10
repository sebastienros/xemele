# Serialization contract — M1

Owner M1-contracts; implementation owner M1-serialize. Exact public declarations
are [Serialization.cs.fixture](../../tests/Contracts/Fixtures/M1/Serialization.cs.fixture).
Compile with common/tree only; no input, parser, DOM factory, M2 type, M3 writer
or async method exists in this stage. Serialize(XmlDocument, options?, token)
returns an owned string; the Stream overload writes incrementally and returns
void. XmlDocument.ToXmlString/Save wrappers are in Serialization/Document, not
base tree files. All signatures/defaults are fixed by the compiled fixture.

## Options and transport

XmlSaveOptions is an immutable record. Indent=false, OmitXmlDeclaration=false,
Encoding=new UTF8Encoding(false, true), EmitByteOrderMark=false,
NamespaceHandling=XmlNamespaceHandling.Strict (enum Strict=0, Repair=1).
Snapshot/validate before output. Null options use defaults; null Encoding,
unsupported encoding or enum value is ArgumentException. M1 supports strict
UTF-8 and UTF-16LE/BE only (encoding CodePage 65001, 1200, 1201); it chooses
exception fallback regardless of caller replacement fallback settings, so
unencodable/invalid sequences never silently become replacement characters.
Encoding objects are cloned/snapshotted; mutations by another caller cannot
alter an operation. Indentation is two spaces per depth and LF, with no trailing
newline and no configurable later-writer option.

String output has no BOM, ignores byte Encoding/EmitByteOrderMark after option
validation, and emits `<?xml version="1.0"?>` unless omitted. Byte output uses
actual Encoding, declaration `<?xml version="1.0" encoding="utf-8"?>`, `utf-16le`
or `utf-16be` as applicable, followed immediately by the root/prolog nodes unless
indentation adds permitted structural whitespace. Byte-order mark is written
exactly once iff EmitByteOrderMark=true: EF BB BF, FF FE, FE FF respectively,
independent of Encoding.GetPreamble defaults. Explicit utf-16le/be labels permit
BOM-less output; no generic utf-16 label is generated without a BOM. Declaration
encoding must match the emitted byte order and never reuses input declarations.
XmlDocument does not store original declaration/version/standalone metadata in
M1. OmitXmlDeclaration omits the declaration in both transports; BOM remains
controlled by its own option. The saved result is XML 1.0 only.

Validate document/options/stream nulls and CanWrite before any output. Exactly
one root is required; document comments/PIs are preserved in child order. Caller
streams always remain open on success/rejection/I/O/cancellation. Serializer does
not close or explicitly Flush the caller stream; its own buffered encoder bytes
are drained before return. Cancellation checked at entry, before writes and at
most every 4096 characters/work steps. I/O/cancellation exceptions propagate
unchanged; prior bytes may remain. No rollback or atomic stream output guarantee.
Local scratch is disposed/returned on all paths. Streaming never materializes
the full result string. No filesystem or Task.Run behavior is implied.

## Namespace output policy

Strict checks every element/attribute QName against the effective ancestor
bindings plus that element's declarations, with implicit xml binding. Unprefixed
unqualified elements under a nonempty default namespace require an explicit
empty default declaration; unprefixed ordinary attributes have no namespace.
A namespaced ordinary attribute needs a nonempty bound prefix. Prefix collisions,
missing/mismatched declarations or forbidden reserved binding produce
InvalidOperationException; output is never silently repaired with Strict.
Neither policy mutates the tree or handle identity.

Repair computes a temporary output scope iteratively. Preserve explicit local
declarations and their insertion order, and preserve each node's preferred
prefix when it binds its intended URI. If an element's preferred binding is
missing, synthesize it; if an explicit local binding conflicts, choose a fresh
prefix rather than changing the explicit declaration. An unprefixed namespaced
element may synthesize a default binding if no conflicting explicit default
exists, otherwise gets a fresh prefix. An unqualified element under a nonempty
inherited default emits xmlns=""; an explicitly conflicting nonempty local
default cannot coexist with an unqualified element and is replaced in output
with an empty default declaration (tree remains unchanged). Namespaced
attributes with missing/empty/conflicting prefix first reuse an existing
nonempty prefix for that URI (ordinally smallest if several); otherwise synthesize
ns1, ns2, ... using a checked operation-wide counter and skipping names already
in scope or preferred by any name/local declaration on that element. xml is
never synthesized under another spelling. Namespaced elements needing fresh
prefixes follow the same ns counter. Generated declarations follow explicit ones
in first-use order (element then ordinary attributes in insertion order).
Conflicting unused explicit declarations remain valid unless the unqualified
element rule above replaces the default. Duplicate/reserved illegal declarations
are rejected, never repaired into acceptance. Repair resolves binding conflicts,
not invalid XML names/scalars/structure.

Ordinary attributes serialize in insertion order; declarations precede them in
explicit insertion order followed by generated order. Value updates preserve
order; remove/re-add appends. Names are ordinal/case-sensitive. Output order is
deterministic but is not XML canonicalization and has no M5 dependency.

## Text, lexical validity and formatting

Use double quotes around all attributes/declaration values. Text escapes & and <;
escape > only when it would finish ]]> across adjacent text segments. Attribute
values escape &, < and double quote; emit TAB/LF/CR as &#x9;/&#xA;/&#xD; to
preserve their values under attribute normalization. Text CR emits &#xD; so a
round-trip does not normalize it away. Text LF/TAB remain literal. Empty element
output uses `<name/>` exactly (not a space before slash). Adjacent Text nodes
serialize contiguously without changing tree identity. Namespace URIs use the
same attribute escaping. Comment, CDATA and PI delimiters preserve their
lexical kinds. Comment rejects --/trailing hyphen; CDATA rejects ]]>; PI rejects
?> and an xml target; no automatic CDATA splitting is selected. Validate all
XML scalar values and names even for independently constructed trees. Invalid
serializable tree state throws InvalidOperationException (factories/setters
already reject invalid arguments as ArgumentException). No parse exception is
used for output validation.

Indentation may insert LF/indent only between children of a container with no
Text or CData direct child; even whitespace-only Text makes it mixed for this
purpose. Insert before each child and before closing tag for a nonempty such
element. Document comments/PIs/root may be separated by LF; no leading LF except
a separator after an emitted declaration. Never insert or trim whitespace beside
Text/CData within mixed content. A descendant element can indent its own
nonmixed contents independently. Existing whitespace is preserved exactly.
Traversal and scope maintenance are iterative and checked at adversarial depth;
no recursive call stack proportional to tree depth.

Known-output vectors are
[serialization-vectors.json](../../tests/Contracts/Fixtures/M1/serialization-vectors.json).
They specify independent tree construction actions and exact string/byte output,
not parser-generated oracles. Runtime lane executes them plus strict/repair
collision/inherited/undeclaration tests, mixed whitespace, all lexical illegal
sequences/scalars, UTF encodings/BOMs/declarations, deterministic attributes,
stream ownership/partial I/O/cancellation and allocation/retention evidence.
Contract validation verifies vector encoding arithmetic, not serializer behavior.

M3's contract task may add a standalone forward XmlWriter and async transport
with explicit ownership/state/flush behavior. Neither their declarations nor
unfinished implementations appear in M1; the synchronous serializer is usable
and testable without them.
