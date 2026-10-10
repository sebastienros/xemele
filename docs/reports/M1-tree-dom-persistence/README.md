# Syntax-prevalidated persistence contract

This internal boundary is for verified syntax sinks and independently validated
owned model replay only. It has no parser dependency or public API addition.
The caller proves the EXACT immutable strings supplied: XML 1.0 scalar legality;
NCNames and expanded names; reserved xml/xmlns bindings; PI XML Name target and
case-insensitive non-xml restriction; comment/CDATA/PI delimiter legality; unique
expanded attribute keys and namespace prefixes on each element. Passing invalid
syntax to this trust boundary is misuse, not a validation experiment. Existing
public factories/mutation and checked builder members continue to validate.

Signatures on XmlDocument.DomBuilder (all internal):

- CreateSyntaxValidatedNode(XmlNodeKind kind, string localName, string namespaceUri,
  string prefix, string value, CancellationToken cancellationToken)
- AppendSyntaxValidatedChild(XmlNode parent, XmlNode child, CancellationToken cancellationToken)
- AppendSyntaxValidatedAttribute(XmlNode element, string localName, string value,
  string namespaceUri, string prefix, CancellationToken cancellationToken)
- AppendSyntaxValidatedNamespaceDeclaration(XmlNode element, string prefix,
  string namespaceUri, CancellationToken cancellationToken)

Start BeginDomConstruction on a fresh private document. Create then immediately
attach the newest sole detached leaf to a previously constructed container.
Element has nonempty localName and empty value. PI has nonempty target localName,
empty URI/prefix. Text/CDATA/comment have empty localName/URI/prefix. Other kinds
reject. Strings are required nonnull; no nullable normalization is offered.
Container/owner/freshness/root checks reuse the verified builder. Creation and
members reuse original private primitives for intern quota/capacity, version
preflight and postcommit notifications. No references survive arena growth.
Caller must not structurally mutate outside the builder or append duplicate keys.

Each entry checks cancellation before work. There is no custom string/name/scalar
or delimiter loop in this boundary. Platform string hashing/comparison, interning
copy on miss, and arena allocations can scale with input size; these are not
claimed bounded or cancellable. Cancellation abandons private construction; no
allocation rollback. Full accumulated comment/CDATA/PI strings must be validated,
including delimiters spanning callback fragments. Persist fragments by explicit
ownership, never by retaining borrowed views. Probe borrowed names before miss
ToString; sample model uses ordinal alternate lookup and owns every stored value.

The independent sample parses a complete owned event model with verified syntax,
disposes parser, then replays only that model. It is not the later DOM adapter.
Allocation probe separates first-use setup, fixture validation/materialization,
warm attachment, arena growth, complete owned construction and retained estimate.
Prepared capacity warm persistence is also tested at zero allocated bytes.
Measurements compare identical prevalidated owned replay: checked factory/member
validation versus trusted persistence, with the same builder attachment in both.
Fixtures/validation are outside measurement; values and names cover short/long
sizes and all supported kinds. No timing starts until exclusive coordinator grant.
