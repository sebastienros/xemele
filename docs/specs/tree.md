# Tree and DOM contract — M1

Decision owner: M1-contracts. Runtime tree owner: M1-tree. Exact declarations are
[Tree.cs.fixture](../../tests/Contracts/Fixtures/M1/Tree.cs.fixture), compiled
without input, syntax, DOM or serialization. These declaration bodies are
nonpackaged compilation models, not runtime implementations. `XmlDocument` and
`readonly struct XmlNode` replace the obsolete illustrative Document/Node names.
[Approved M0 ownership and identity](../decisions/M0/foundation.md) remains binding.

## Document, nodes and identity

`XmlDocument()` creates its document node but no root. `DocumentNode` is always
valid; `RootElement` is the default XmlNode when no root exists.
The additional XmlDocument(XmlResourceLimits limits) constructor snapshots and
validates limits; independent construction enforces InternedNames and finite
arena/counter capacity. Input-only depth/character/token limits do not restrict
manually constructed trees. Parameterless uses default limits; null intern quota
explicitly opts out. Capacity/quota failure in construction is
InvalidOperationException (OutOfMemoryException propagates). Cloned documents
retain source limits; imported nodes use destination limits. Missing Parent,
FirstChild, LastChild, PreviousSibling and NextSibling are default handles, never
nullable handles. Default handles expose IsDefault and support equality/hash;
all other access throws InvalidOperationException. OwnerDocument is a strong
owner reference. Node identity is owner reference plus Int32 slot, slot zero
valid, -1 internal missing, never reused; no generation counter. ImportNode and
Clone always allocate new identity. Detaching leaves identity, value and owner
valid. There is no destructive deletion or disposable document.

XmlNodeKind has exactly Document, Element, Text, CData, Comment,
ProcessingInstruction and Fragment, in declaration order starting at zero.
M2 may append kinds in its own ordered contract; M1 never declares DTD/entity
metadata or later types. CreateElement takes localName, nullable namespaceUri
and prefix; null means empty string, returned names are owned strings.
CreateText, CreateComment and CreateCData take owned values; CreateProcessingInstruction
uses target as LocalName and data as Value. CreateFragment makes a detached
container. Document/Fragment/character nodes have empty Prefix/NamespaceUri;
non-element/non-PI LocalName is empty. Element, document and fragment Value is
empty and its setter throws InvalidOperationException. Other Value setters own
and validate their string. Names are immutable after construction.

Names follow XML 1.0 Fifth Edition NCName for element/attribute local names and
prefixes; PI targets use XML Name and cannot be any case spelling of xml.
Values must contain legal XML 1.0 scalar values, without unpaired surrogates.
Comments reject -- and a trailing hyphen, CDATA rejects ]]>, PI data rejects ?>.
ArgumentNullException handles required null strings; invalid names, values,
reserved bindings and foreign/cyclic/illegal insertion use ArgumentException.
For wrong node kinds, value/attribute/namespace mutations throw InvalidOperationException.
Validation precedes observable mutation. Reserved xml/xmlns names follow
[Namespaces in XML](https://www.w3.org/TR/xml-names/#ns-decl); arbitrary namespace
URI strings are preserved ordinally rather than URI-normalized. Prefix requires
nonempty namespace URI; xml binds only its reserved URI and that URI uses xml;
xmlns and its URI cannot be ordinary element/attribute names.

## Construction and mutation

Document children may be comments, PIs and at most one element; no text/CDATA,
fragment wrapper or document child. Elements/fragments accept elements, text,
CDATA, comments and PIs. AppendChild/InsertBefore with a fragment transfers its
children in order and empties it; the fragment never becomes a child. Validate
the complete transfer before mutation (including a document's root count).
InsertBefore requires a current direct referenceChild; it cannot be default.
Moving a child within its owner detaches then inserts, preserving identity.
InsertBefore(child, child) is a validated no-op. Cross-owner moves require
ImportNode. DocumentNode cannot move or Remove. Remove on an already detached
non-document node is a no-op. New empty documents are legal; serialization
requires exactly one root. No implicit adjacent-text coalescing occurs.

Clone(false) copies the node's value, attributes and declarations but no children;
Clone(true) also copies descendants. Document cloning returns the new document's
DocumentNode. ImportNode rejects a Document node (use its children), accepts all
other M1 kinds and returns a detached copy. Both use iterative checked traversal.
GetTextContent for containers concatenates descendant Text/CData in order,
ignoring comment/PI; character nodes return Value. SetTextContent for containers
removes children and creates one Text child, or none for empty value; document
nodes reject it. Character nodes set Value. Traversal, cloning, import and text
aggregation must be iterative at adversarial depth.

## Attributes and namespace declaration handles

XmlAttribute and XmlNamespaceDeclaration are readonly owner-plus-slot handles
with IsDefault, OwnerDocument, OwnerElement, IsAttached, mutable Value, Remove,
equality/hash and ==/!=. They are never node children. Attribute identity uses
its own never-reused slot domain; namespace identity uses another domain.
Removed handles retain their original OwnerElement and editable owned value;
IsAttached becomes false. There is no reattachment API. Re-adding the same key
creates a new slot; updating an attached key preserves identity and position.
Default handle semantics match XmlNode. XmlAttribute has LocalName, NamespaceUri,
Prefix; namespace declarations have Prefix, and Value is the namespace URI.
Remove on a detached handle is a no-op. Handle Value setters enforce the same
scalar/binding validity as insertion. Attribute name fields never mutate except
SetAttribute may replace the prefix of an existing expanded-name key.

SetAttribute returns the handle, keyed ordinally by (namespaceUri, localName).
GetAttribute returns the owned value or null; RemoveAttribute returns whether
one existed. Namespace declarations are not Attributes: SetNamespaceDeclaration
returns a declaration keyed by prefix, RemoveNamespaceDeclaration returns bool.
Empty prefix declares the default namespace; empty URI undeclares only the
default namespace. Nonempty prefix cannot bind empty URI under Namespaces 1.0.
A redundant xml declaration is permitted only with its reserved URI; the xml
binding otherwise exists implicitly. Namespace URI/prefix consistency with
ancestors is checked by strict serialization, rather than silently mutating
constructed trees. A namespaced attribute with empty prefix is constructible
for Repair output, but Strict output rejects it (default namespaces do not apply
to attributes).

XmlNodeChildren, XmlAttributes, XmlNamespaceDeclarations each expose Count,
zero-based integer indexer, GetEnumerator and a mutable concrete struct Enumerator
with Current and MoveNext; there are no IEnumerable/interface enumerators, Reset
or boxing-dependent fallbacks. Index outside Count throws ArgumentOutOfRangeException.
Current outside a successful MoveNext throws InvalidOperationException. Empty
collections are returned for valid node kinds lacking the corresponding content;
mutation APIs on those kinds reject. Enumeration order is child order or first
insertion order. Set/update preserves attribute/declaration position; remove and
re-add appends. Indexers may walk links; no O(1) indexing promise is made.

Every structural insertion/removal (including attribute/declaration membership)
increments the checked document-wide Int64 MutationVersion before observable
mutation; detached-tree changes count. All three enumerators capture it and fail
on MoveNext/Current after any structural change anywhere in the owner. Existing
value/prefix updates do not invalidate structural enumeration. Overflow rejects
before mutation. Concurrent reads require no concurrent mutation. Creation of a
detached node alone does not change structural enumeration version.

## Internal extension hook

Exact internal XmlTreeMutationKind/XmlTreeMutation declarations and XmlDocument's
NotifyMutation(in XmlTreeMutation), MutationVersion and optional
`partial void OnMutationCommitted(in XmlTreeMutation mutation)` are in the fixture.
NotifyMutation follows each committed primitive change, including changes to
detached trees; Kind identifies node insert/remove, attribute/namespace insert/remove,
value or name changes. Node identifies the affected node or owning element;
OldParent/NewParent are default if absent; unrelated attribute/namespace handles
are default. A move reports remove then insert; compound text replacement or
fragment transfer reports primitive changes in order. Value and Name notifications
occur even without structural version increments. A later index module supplies
the optional partial body in its own file, without requiring any index/DTD type
in M1. Hooks are internal bookkeeping, cannot invoke user code or reenter
mutation, and must invalidate/rebuild their index if post-commit maintenance
cannot complete. Hooks do not promise transaction rollback after allocation
failure. Validation errors always leave the tree unchanged.

## Composition boundaries

M1-dom alone supplies [Dom.cs.fixture](../../tests/Contracts/Fixtures/M1/Dom.cs.fixture)
partial factories: Parse(string/ReadOnlySpan<char>/ReadOnlySpan<byte>), Load(Stream)
and TryParse for the same three memory input forms. All take nullable options and
an operation CancellationToken with the exact defaults shown there. TryParse
returns a nullable out document and owned out diagnostics; success has a nonnull
complete document and empty diagnostics, rejection null and nonempty diagnostics.
Only structured syntax/quota/unsupported rejection returns false; argument/I/O/
cancellation/callback exceptions propagate unchanged. No M1 filename, async,
recovery or resolver overload is implied. Parsing factories belong in Parsing/Dom;
tree files contain no parser references. M1-serialize supplies ToXmlString/Save
in Serialization/Document; those wrappers appear only in that compiled stage.

Required runtime tests: stable identity/growth/detach, imports/clones, fragment
atomic validation, reserved names/bindings, all default/missing navigation,
attribute/declaration replacement identity, all enumerator invalidations, deep
iterative operations, constructed-tree output and later DOM/event equivalence.
