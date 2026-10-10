# Validated fresh-tree construction boundary

`XmlDocument.BeginDomConstruction()` returns an internal `XmlDocument.DomBuilder`
only when the document owns exactly its empty document node and no members or
structural history. Construction is single-threaded and exclusive. Public general
mutation is unchanged; outside structural mutation or an unexpected arena count
invalidates the builder. Multiple builders acquired before work do not cooperate:
the first commit invalidates the other.

The consumer creates one node using existing validated `Create*` APIs, then calls
`AppendChild(parent, child)` immediately. The child must be the new final slot,
same owner, detached, with no children or members. Parent must be the document
node or a previously appended element. This excludes cycles without parent walks,
fragments, foreign/default handles, reuse and detached subtrees. The builder keeps
one root bit, so arbitrarily many document comments/PIs cause no repeated root
scan. Text/CDATA are legal under elements only. Empty documents remain legal.

`AppendAttribute(element, localName, value, namespaceUri = null, prefix = null)`
and `AppendNamespaceDeclaration(element, prefix, namespaceUri)` accept unique
expanded attribute keys / declaration prefixes guaranteed by validated syntax.
**Uniqueness is the caller's internal precondition**, not a public unchecked API.
They still validate XML names/scalars and reserved namespace bindings. They call
the original `AppendMember` primitive, retaining its version/capacity/name-quota
preflight and ordered identity / postcommit `NotifyMutation` behavior. Child
attachment calls the original `Link` primitive and notification. Tree owns no
parser type and ignores input-only depth/character/member limits.

The builder retains only its owner, scalar arena counts/version/root flag and
diagnostic counters. All work per child/member is bounded apart from linear
name/value validation and amortized arena/dictionary growth. Existing intern
lookup uses .NET's ordinal dictionary and borrowed alternate probes; owned names
are allocated only on actual misses. The future DOM consumer must probe borrowed
callback names before materializing missing strings, and own any text at its
persistence boundary. No borrowed spans are retained by this boundary.

`ChildChecks` counts child validations reaching primitive attachment;
`MemberChecks` counts validated member attempts reaching primitive append.
The count tests cover 500/12000-depth trees, 64/1024 member pairs and 4001 document
children. They complement direct source review: no ancestor, root-list or member
key loops exist in the boundary or selected primitives. Failure attempts can
increment counters but never commit structural membership.

The independent sample first records an owned syntax-event model, disposes the
parser, then replays it into Tree. It is an explicit model probe, not DOM factories
or the future streaming DOM adapter. No input/DOM/serializer peer code is needed.

Benchmark cases compare identical public/validated construction at 256/2048 deep
nodes, member pairs and document comments. Fixture names are outside timing;
returned documents consume results. Cold setup, warm helper, owned arena growth,
full owned construction and retained memory are separately probed. Timing is
provisional single-arm64 evidence pending the coordinator's exclusive grant and
later end-to-end DOM confirmation / x64 release checks. Check results will be
recorded at the committed source revision after actual execution.
