# Tree and DOM contract — M1

Decision owner: `M1-contracts`, after the M0 representation decision. Dependencies: common primitives only. Tree construction/mutation works without a parser.

## Shapes

```csharp
namespace Xemele;

public sealed partial class Document
{
    public Document();
    public Node DocumentNode { get; }
    public Node? RootElement { get; }
    public Node CreateElement(string localName, string? namespaceUri = null, string? prefix = null);
    public Node CreateText(string value);
    public Node CreateComment(string value);
    public Node CreateCData(string value);
    public Node CreateProcessingInstruction(string target, string data);
    public Node ImportNode(Node source, bool deep = true);
}
public readonly struct Node : IEquatable<Node>
{
    public Document OwnerDocument { get; }
    public NodeKind Kind { get; }
    public string LocalName { get; }
    public string NamespaceUri { get; }
    public string Prefix { get; }
    public Node? Parent { get; }
    public Node? FirstChild { get; }
    public Node? NextSibling { get; }
    public NodeChildren Children { get; }
    public XmlAttributes Attributes { get; }
    public string GetTextContent();
    public void SetTextContent(string value);
    public string? GetAttribute(string localName, string? namespaceUri = null);
    public void SetAttribute(string localName, string value, string? namespaceUri = null, string? prefix = null);
    public void AppendChild(Node child);
    public void InsertBefore(Node child, Node referenceChild);
    public void Remove();
    public Node Clone(bool deep = true);
}
```

`NodeKind` covers document, element, text, CDATA, comment, processing instruction, document type, entity reference, and fragment. Contract fixtures spell out creation/access for each supported M1 kind. DTD-associated metadata is added in M2 through a separate internal attachment, not a dependency of the base tree. The Node value shape remains conditional on the M0 experiment; no dependent task starts before that choice is frozen.

`NodeChildren` and `XmlAttributes` expose allocation-free concrete enumerators. Attributes and namespace bindings have their own handles with owner element, name/value and identity; they are not children. Contract review must define the exact collection and handle shapes now, so XPath can later adapt them without changing Node identity. Empty namespace/prefix uses an empty string consistently.

## Invariants

Handles remain stable across storage growth. Identity includes document and slot/generation; default handles throw on access. Remove detaches, preserving handle validity. No public destructive deletion in M1; it need not be reserved. Cross-document append is rejected; import explicitly copies. Cycles, multiple roots in a strict document, invalid names, and illegal child kinds are rejected. Fragments can hold multiple children. Namespace declaration APIs must permit creating serializable trees without parsing XML first.

Cloning, text aggregation, and traversal are iterative for adversarial depth. Mutation invalidates active enumerators. Attribute updates replace by expanded name. Namespace repair is an output policy, not silent mutation. Concurrent reads require no concurrent mutation. The document owns managed memory and is not disposable.

## DOM composition

`M1-dom` alone owns partial `Document` parsing factories in `Parsing/Dom/`: `Parse(string)`, `Parse(ReadOnlySpan<byte>)`, and `Load(Stream)` with implemented options/cancellation. Tree files never reference parser types. `M1-serialize` adds `ToXmlString`/`Save` partial wrappers in `Serialization/Document/`. M3 adds `LoadAsync` in its own file. Later features use standalone services; they do not expand a monolithic Document file.

Tests: tree invariants, namespace/attribute identity, imports/clones, default handles, enumerator invalidation, constructed-tree output, and DOM/event semantic equivalence. APIs for ID lookup arrive with the entity/DTD work and maintain indexes via an internal mutation hook specified in M1.
