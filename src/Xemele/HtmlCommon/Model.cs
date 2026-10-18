using System.Collections.ObjectModel;
using System.Text;
using Xemele;
using Xemele.Internal;
namespace Xemele.Html;

/// <summary>Identifies the kind of a node in an HTML tree.</summary>
public enum HtmlNodeKind
{
    /// <summary>The document container.</summary>
    Document,
    /// <summary>A document fragment containing a sequence of child nodes.</summary>
    Fragment,
    /// <summary>An HTML or foreign-namespace element.</summary>
    Element,
    /// <summary>A text node.</summary>
    Text,
    /// <summary>A comment node.</summary>
    Comment,
    /// <summary>A document-type declaration.</summary>
    DocumentType,
    /// <summary>A processing-instruction node created through the tree model.</summary>
    ProcessingInstruction = 6
}
/// <summary>Identifies the document compatibility mode selected by HTML parsing.</summary>
public enum HtmlQuirksMode
{
    /// <summary>The standards, or no-quirks, mode.</summary>
    NoQuirks,
    /// <summary>The limited-quirks compatibility mode.</summary>
    LimitedQuirks,
    /// <summary>The full quirks compatibility mode.</summary>
    Quirks
}
/// <summary>Stores an HTML attribute's expanded name, prefix, and owned value.</summary>
/// <param name="LocalName">The attribute's local name.</param>
/// <param name="Value">The attribute's string value.</param>
/// <param name="NamespaceUri">The namespace URI, or an empty string for no namespace.</param>
/// <param name="Prefix">The lexical prefix, or an empty string for no prefix.</param>
public sealed record HtmlAttribute(string LocalName, string Value, string NamespaceUri = "", string Prefix = "");
/// <summary>Stores the name, identifiers, and tokenizer quirks flag of an HTML doctype.</summary>
/// <param name="Name">The doctype name.</param>
/// <param name="PublicIdentifier">The public identifier, or null when absent.</param>
/// <param name="SystemIdentifier">The system identifier, or null when absent.</param>
/// <param name="ForceQuirks">Whether the token requires quirks-mode processing.</param>
public sealed record HtmlDoctype(string Name, string? PublicIdentifier, string? SystemIdentifier, bool ForceQuirks);
/// <summary>Configures the resource limits of the legacy HTML parser.</summary>
public sealed record HtmlParseOptions
{
    /// <summary>Gets the limits snapshotted at the start of each parsing operation.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
}
/// <summary>Configures HTML5 parsing and its resource limits.</summary>
public sealed record Html5ParseOptions
{
    /// <summary>Gets the limits snapshotted at the start of each parsing operation.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets whether HTML5 tree construction uses scripting-enabled rules; the default is false.</summary>
    /// <remarks>This flag affects parsing rules; it does not execute scripts.</remarks>
    public bool ScriptingEnabled
    {
        get; init;
    }
}
/// <summary>Provides the context used to parse an HTML5 fragment.</summary>
/// <param name="ContextElement">The element whose name, attributes, and ancestry are snapshotted for parsing.</param>
/// <param name="QuirksMode">The compatibility mode of the fragment context.</param>
public sealed record HtmlFragmentContext(HtmlNode ContextElement, HtmlQuirksMode QuirksMode = HtmlQuirksMode.NoQuirks);
/// <summary>Selects the initial HTML5 tokenizer text state.</summary>
public enum HtmlTokenizerState
{
    /// <summary>Normal data with markup and character-reference recognition.</summary>
    Data,
    /// <summary>RCDATA text with character references and matching end-tag recognition.</summary>
    RcData,
    /// <summary>Raw text with matching end-tag recognition.</summary>
    RawText,
    /// <summary>Script data with HTML5 script-specific escape handling.</summary>
    ScriptData,
    /// <summary>Plain text without markup recognition.</summary>
    PlainText
}
/// <summary>Configures the initial state and resource limits of HTML5 tokenization.</summary>
public sealed record HtmlTokenizerOptions
{
    /// <summary>Gets the resource limits snapshotted for the tokenization operation.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets the initial text state; the default is <see cref="HtmlTokenizerState.Data"/>.</summary>
    public HtmlTokenizerState InitialState { get; init; } = HtmlTokenizerState.Data;
    /// <summary>Gets the last start-tag name used to recognize an appropriate end tag in text states.</summary>
    /// <remarks>RCDATA, raw-text, and script-data initial states require a nonempty name; ASCII case is folded.</remarks>
    public string LastStartTagName { get; init; } = "";
}
/// <summary>Identifies an owned HTML token.</summary>
public enum HtmlTokenKind
{
    /// <summary>A document-type declaration token.</summary>
    Doctype,
    /// <summary>An element start-tag token.</summary>
    StartTag,
    /// <summary>An element end-tag token.</summary>
    EndTag,
    /// <summary>A comment token.</summary>
    Comment,
    /// <summary>A character-data token.</summary>
    Character,
    /// <summary>The end-of-input token.</summary>
    EndOfFile,
    /// <summary>A processing-instruction token supported by the shared token model.</summary>
    ProcessingInstruction = 6
}
/// <summary>Represents an immutable HTML token with owned strings and a read-only attribute snapshot.</summary>
public sealed class HtmlToken
{
    private static readonly ReadOnlyCollection<HtmlAttribute> NoAttributes = Array.AsReadOnly(Array.Empty<HtmlAttribute>());
    private HtmlToken(HtmlTokenKind kind, string name, string data, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing, HtmlDoctype? doctype, XmlLocation location, bool trusted)
    {
        Kind = kind;
        Name = name;
        Data = data;
        Attributes = attributes;
        SelfClosing = selfClosing;
        Doctype = doctype;
        Location = location;
    }
    // Tokenizer-built tokens: arguments are already validated and the attribute array is not retained elsewhere.
    internal static HtmlToken Trusted(HtmlTokenKind kind, string name, string data, HtmlAttribute[]? attributes, bool selfClosing, HtmlDoctype? doctype, XmlLocation location)
     => new(kind, name, data, attributes is null || attributes.Length == 0 ? NoAttributes : Array.AsReadOnly(attributes), selfClosing, doctype, location, true);
    /// <summary>Creates an HTML token, validating its metadata and copying the supplied attribute list.</summary>
    /// <param name="kind">The token kind.</param>
    /// <param name="name">The token's name, or an empty string when unnamed.</param>
    /// <param name="data">The token's character, comment, or processing-instruction data.</param>
    /// <param name="attributes">The tag attributes, copied in their supplied order.</param>
    /// <param name="selfClosing">Whether the token has a self-closing flag.</param>
    /// <param name="doctype">The doctype metadata, or null for tokens without it.</param>
    /// <param name="location">The source location associated with the token.</param>
    /// <exception cref="ArgumentNullException">A required string, attribute list, or attribute component is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The token kind is undefined.</exception>
    public HtmlToken(HtmlTokenKind kind, string name, string data, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing, HtmlDoctype? doctype, XmlLocation location)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(attributes);
        var copy = new HtmlAttribute[attributes.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            HtmlDocument.ValidateAttribute(attributes[i]);
            copy[i] = attributes[i];
        }
        HtmlDocument.ValidateDoctype(doctype);
        Kind = kind;
        Name = name;
        Data = data;
        Attributes = Array.AsReadOnly(copy);
        SelfClosing = selfClosing;
        Doctype = doctype;
        Location = location;
    }
    /// <summary>Gets the token kind.</summary>
    public HtmlTokenKind Kind
    {
        get;
    }
    /// <summary>Gets the token's name, or an empty string when unnamed.</summary>
    public string Name
    {
        get;
    }
    /// <summary>Gets the token's owned character, comment, or processing-instruction data.</summary>
    public string Data
    {
        get;
    }
    /// <summary>Gets the read-only attribute snapshot in token order.</summary>
    public IReadOnlyList<HtmlAttribute> Attributes
    {
        get;
    }
    /// <summary>Gets whether the token's self-closing flag is set.</summary>
    public bool SelfClosing
    {
        get;
    }
    /// <summary>Gets the doctype metadata, or null when none applies.</summary>
    public HtmlDoctype? Doctype
    {
        get;
    }
    /// <summary>Gets the source location associated with the token.</summary>
    public XmlLocation Location
    {
        get;
    }
}
/// <summary>Owns a mutable HTML tree, its interned names, resource limits, and parsing metadata.</summary>
public sealed class HtmlDocument
{
    internal const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
    internal readonly XmlResourceLimits Limits;
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private bool htmlNsInterned;
    /// <summary>Creates an empty HTML document with a validated snapshot of the resource limits.</summary>
    /// <param name="limits">The resource limits, or null for defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException">A resource limit is invalid.</exception>
    public HtmlDocument(XmlResourceLimits? limits = null)
    {
        if (limits is null || ReferenceEquals(limits, HtmlOperationSnapshots.DefaultLimits))
        {
            Limits = HtmlOperationSnapshots.DefaultLimits;
        }
        else
        {
            Limits = limits with { };
            Limits.Validate();
        }
        DocumentNode = new(this, HtmlNodeKind.Document);
    }
    /// <summary>Gets the document container to which a root element and doctype may be attached.</summary>
    public HtmlNode DocumentNode
    {
        get;
    }
    // Bumped whenever a node that may have descendants is attached or detached; invalidates cached element depths.
    internal int DepthEpoch = 1;
    /// <summary>Gets the document's attached root element.</summary>
    /// <exception cref="InvalidOperationException">The document has no root element.</exception>
    public HtmlNode RootElement
    {
        get
        {
            foreach (var n in DocumentNode.RawChildren)
            {
                if (n.Kind == HtmlNodeKind.Element)
                {
                    return n;
                }
            }

            throw new InvalidOperationException("Document has no root element.");
        }
    }
    private bool scriptingEnabled;
    /// <summary>Gets whether the document was parsed using scripting-enabled tree-construction rules.</summary>
    public bool ScriptingEnabled => scriptingEnabled;
    private HtmlQuirksMode quirksMode;
    /// <summary>Gets the compatibility mode recorded by the parser.</summary>
    public HtmlQuirksMode QuirksMode => quirksMode;
    internal void SetMetadata(HtmlQuirksMode quirks, bool scripting)
    {
        if (!Enum.IsDefined(quirks))
        {
            throw new ArgumentOutOfRangeException(nameof(quirks));
        }

        quirksMode = quirks;
        scriptingEnabled = scripting;
    }
    internal static void ValidateAttribute(HtmlAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(attribute.LocalName);
        ArgumentNullException.ThrowIfNull(attribute.Value);
        ArgumentNullException.ThrowIfNull(attribute.NamespaceUri);
        ArgumentNullException.ThrowIfNull(attribute.Prefix);
    }
    internal static void ValidateDoctype(HtmlDoctype? doctype)
    {
        if (doctype is not null)
        {
            ArgumentNullException.ThrowIfNull(doctype.Name);
        }
    }
    internal void CheckNames(string a, string b = "", string c = "")
    {
        var missing = 0;
        if (a.Length != 0 && !(htmlNsInterned && ReferenceEquals(a, HtmlNamespace)) && !names.ContainsKey(a))
        {
            missing++;
        }

        if (b.Length != 0 && b != a && !(htmlNsInterned && ReferenceEquals(b, HtmlNamespace)) && !names.ContainsKey(b))
        {
            missing++;
        }

        if (c.Length != 0 && c != a && c != b && !(htmlNsInterned && ReferenceEquals(c, HtmlNamespace)) && !names.ContainsKey(c))
        {
            missing++;
        }

        if (Limits.InternedNames is int max && (long)names.Count + missing > max)
        {
            throw new InvalidOperationException("HTML interned-name quota exceeded.");
        }
    }
    internal string Intern(string value)
    {
        if (value.Length == 0)
        {
            return "";
        }

        if (htmlNsInterned && ReferenceEquals(value, HtmlNamespace))
        {
            return HtmlNamespace;
        }

        if (names.TryGetValue(value, out var found))
        {
            return found;
        }

        if (value == HtmlNamespace)
        {
            htmlNsInterned = true;
            names.Add(HtmlNamespace, HtmlNamespace);
            return HtmlNamespace;
        }

        names.Add(value, value);
        return value;
    }
    internal string InternSingle(string value)
    {
        if (value.Length == 0)
        {
            return "";
        }

        if (names.TryGetValue(value, out var found))
        {
            return found;
        }

        if (Limits.InternedNames is int max && names.Count >= max)
        {
            throw new InvalidOperationException("HTML interned-name quota exceeded.");
        }

        if (value == HtmlNamespace)
        {
            htmlNsInterned = true;
            names.Add(HtmlNamespace, HtmlNamespace);
            return HtmlNamespace;
        }

        names.Add(value, value);
        return value;
    }
    internal void InternTriplet(string a, string b, string c, out string ia, out string ib, out string ic)
    {
        if (b == HtmlNamespace && c.Length == 0)
        {
            if (htmlNsInterned)
            {
                ia = InternSingle(a);
                ib = HtmlNamespace;
                ic = "";
                return;
            }
            if (a.Length == 0)
            {
                ia = "";
                ib = InternSingle(HtmlNamespace);
                ic = "";
                return;
            }
        }
        if (b.Length == 0 && c.Length == 0)
        {
            ia = InternSingle(a);
            ib = "";
            ic = "";
            return;
        }
        CheckNames(a, b, c);
        ia = Intern(a);
        ib = Intern(b);
        ic = Intern(c);
    }
    internal void CheckToken(string value)
    {
        if (Limits.TokenCharacters is not int max || value.Length <= max)
        {
            return;
        }

        var count = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (++count > max)
            {
                throw new InvalidOperationException("HTML token quota exceeded.");
            }
        }
    }
    // Parser-only: localName is already folded and charged against TokenCharacters when in HtmlNamespace.
    internal HtmlNode CreateParsedElement(string localName, string namespaceUri)
    {
        if (namespaceUri == HtmlNamespace && htmlNsInterned)
        {
            var internedLocal = InternSingle(localName);
            var n = new HtmlNode(this, HtmlNodeKind.Element, internedLocal, HtmlNamespace, "");
            if (internedLocal == "template")
            {
                n.templateContent = CreateFragment();
                n.templateContent.TemplateHost = n;
            }
            return n;
        }
        InternTriplet(localName, namespaceUri, "", out var iLocal, out var iNs, out var iPrefix);
        var node = new HtmlNode(this, HtmlNodeKind.Element, iLocal, iNs, iPrefix);
        if (namespaceUri == HtmlNamespace && iLocal == "template")
        {
            node.templateContent = CreateFragment();
            node.templateContent.TemplateHost = node;
        }
        return node;
    }
    /// <summary>Creates a detached element owned by this document.</summary>
    /// <param name="localName">The local name; HTML-namespace names are ASCII-lowercased.</param>
    /// <param name="namespaceUri">The namespace URI; the default is the HTML namespace.</param>
    /// <param name="prefix">The lexical prefix, or an empty string for no prefix.</param>
    /// <returns>The new element; HTML template elements also own a separate content fragment.</returns>
    /// <exception cref="ArgumentNullException">A name component is null.</exception>
    /// <exception cref="InvalidOperationException">A token or interned-name limit is exceeded.</exception>
    public HtmlNode CreateElement(string localName, string namespaceUri = HtmlNamespace, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(localName);
        ArgumentNullException.ThrowIfNull(namespaceUri);
        ArgumentNullException.ThrowIfNull(prefix);
        if (namespaceUri == HtmlNamespace)
        {
            localName = HtmlNames.Fold(localName);
        }

        CheckToken(localName);
        InternTriplet(localName, namespaceUri, prefix, out var iLocal, out var iNs, out var iPrefix);
        var node = new HtmlNode(this, HtmlNodeKind.Element, iLocal, iNs, iPrefix);
        if (namespaceUri == HtmlNamespace && iLocal == "template")
        {
            node.templateContent = CreateFragment();
            node.TemplateContent!.TemplateHost = node;
        }
        return node;
    }
    /// <summary>Creates a detached text node owned by this document.</summary>
    /// <param name="value">The owned text value.</param>
    /// <returns>The new text node.</returns>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The token-character limit is exceeded.</exception>
    public HtmlNode CreateText(string value) => CreateValue(HtmlNodeKind.Text, value);
    // Parser-only: token/input/expansion quotas have already been charged before
    // adjacent character tokens are merged. A persisted text node is not a token.
    internal HtmlNode CreateParsedText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(this, HtmlNodeKind.Text, value: value);
    }
    /// <summary>Creates a detached comment node owned by this document.</summary>
    /// <param name="value">The comment data, without delimiters.</param>
    /// <returns>The new comment node.</returns>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="InvalidOperationException">The token-character limit is exceeded.</exception>
    public HtmlNode CreateComment(string value) => CreateValue(HtmlNodeKind.Comment, value);
    /// <summary>Creates a detached processing-instruction node owned by this document.</summary>
    /// <param name="target">The ASCII target name; xml and xml-stylesheet are reserved.</param>
    /// <param name="data">The instruction data, without delimiters.</param>
    /// <returns>The new processing-instruction node.</returns>
    /// <exception cref="ArgumentNullException">The target or data is null.</exception>
    /// <exception cref="ArgumentException">The target is invalid or reserved.</exception>
    /// <exception cref="InvalidOperationException">A token or interned-name limit is exceeded.</exception>
    public HtmlNode CreateProcessingInstruction(string target, string data)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(data);
        if (target.Length == 0 || !IsTargetStart(target[0]))
        {
            throw new ArgumentException("Invalid HTML processing-instruction target.", nameof(target));
        }

        for (var i = 1; i < target.Length; i++)
        {
            if (!IsTargetStart(target[i]) && target[i] is not (>= '0' and <= '9') && target[i] != '-')
            {
                throw new ArgumentException("Invalid HTML processing-instruction target.", nameof(target));
            }
        }

        if (target.AsSpan().Equals("xml", StringComparison.OrdinalIgnoreCase) || target.AsSpan().Equals("xml-stylesheet", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Reserved HTML processing-instruction target.", nameof(target));
        }

        CheckToken(target);
        CheckToken(data);
        CheckNames(target);
        return new(this, HtmlNodeKind.ProcessingInstruction, Intern(target), value: data);
    }
    private static bool IsTargetStart(char value) => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '_';
    private HtmlNode CreateValue(HtmlNodeKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        CheckToken(value);
        return new(this, kind, value: value);
    }
    /// <summary>Creates a detached document-type node owned by this document.</summary>
    /// <param name="doctype">The doctype metadata to retain.</param>
    /// <returns>The new document-type node, attachable only to a document container.</returns>
    /// <exception cref="ArgumentNullException">The metadata or doctype name is null.</exception>
    /// <exception cref="InvalidOperationException">The token-character limit is exceeded.</exception>
    public HtmlNode CreateDocumentType(HtmlDoctype doctype)
    {
        ArgumentNullException.ThrowIfNull(doctype);
        ValidateDoctype(doctype);
        CheckToken(doctype.Name);
        return new(this, HtmlNodeKind.DocumentType, doctype.Name, doctype: doctype);
    }
    /// <summary>Creates an empty detached fragment owned by this document.</summary>
    /// <returns>The fragment, whose children can later be moved into another container.</returns>
    public HtmlNode CreateFragment() => new(this, HtmlNodeKind.Fragment);
    /// <summary>Copies a node into this document without modifying its original tree.</summary>
    /// <param name="node">The node to import; document nodes cannot be imported.</param>
    /// <param name="deep">Whether to copy descendants and template contents as well as the node and its attributes.</param>
    /// <returns>A detached copy owned by this document.</returns>
    /// <exception cref="ArgumentNullException">The node is null.</exception>
    /// <exception cref="ArgumentException">The node is a document.</exception>
    /// <exception cref="InvalidOperationException">Copying exceeds a document resource limit.</exception>
    public HtmlNode Import(HtmlNode node, bool deep = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Kind == HtmlNodeKind.Document)
        {
            throw new ArgumentException("Cannot import a document.", nameof(node));
        }

        return Copy(node, deep);
    }
    internal HtmlNode Copy(HtmlNode node, bool deep) => CopyCore(node, deep, false);
    internal HtmlNode ImportParsed(HtmlNode node, bool deep = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Kind == HtmlNodeKind.Document)
        {
            throw new ArgumentException("Cannot import a document.", nameof(node));
        }

        return CopyCore(node, deep, true);
    }
    private HtmlNode CopyCore(HtmlNode node, bool deep, bool parsedText)
    {
        var copy = CopyShallow(node, parsedText);
        if (deep)
        {
            var work = new Stack<(HtmlNode Source, HtmlNode Target)>();
            work.Push((node, copy));
            while (work.TryPop(out var pair))
            {
                foreach (var child in pair.Source.RawChildren)
                {
                    var c = CopyShallow(child, parsedText);
                    pair.Target.AppendChild(c);
                    work.Push((child, c));
                }
                if (pair.Source.TemplateContent is { } template)
                {
                    work.Push((template, pair.Target.TemplateContent!));
                }
            }
        }
        return copy;
    }
    private HtmlNode CopyShallow(HtmlNode node, bool parsedText)
    {
        var result = node.Kind switch
        {
            HtmlNodeKind.Element => CreateElement(node.LocalName, node.NamespaceUri, node.Prefix),
            HtmlNodeKind.Text => parsedText ? CreateParsedText(node.Value) : CreateText(node.Value),
            HtmlNodeKind.Comment => CreateComment(node.Value),
            HtmlNodeKind.ProcessingInstruction => CreateProcessingInstruction(node.LocalName, node.Value),
            HtmlNodeKind.DocumentType => CreateDocumentType(node.Doctype!),
            HtmlNodeKind.Fragment => CreateFragment(),
            HtmlNodeKind.Document => new HtmlNode(this, HtmlNodeKind.Document),
            _ => throw new ArgumentException("Unsupported node kind.")
        };
        foreach (var a in node.RawAttributes)
        {
            result.SetAttribute(a);
        }

        return result;
    }
}
/// <summary>Represents a mutable node owned by one HTML document.</summary>
/// <remarks>Child and attribute collections are live read-only views. Template contents form a separate fragment.</remarks>
public sealed class HtmlNode
{
    private static readonly List<HtmlNode> NoChildren = [];
    private static readonly List<HtmlAttribute> NoAttributes = [];
    // Lists are allocated on first write (or first public view access); the shared empty instances are never mutated.
    private List<HtmlNode> children = NoChildren;
    private List<HtmlAttribute> attributes = NoAttributes;
    private ReadOnlyCollection<HtmlNode>? childrenView;
    private ReadOnlyCollection<HtmlAttribute>? attributesView;
    private List<HtmlNode> MutableChildren() => ReferenceEquals(children, NoChildren) ? children = [] : children;
    private List<HtmlAttribute> MutableAttributes() => ReferenceEquals(attributes, NoAttributes) ? attributes = [] : attributes;
    private string value;
    internal HtmlNode(HtmlDocument owner, HtmlNodeKind kind, string local = "", string uri = "", string prefix = "", string value = "", HtmlDoctype? doctype = null)
    {
        OwnerDocument = owner;
        Kind = kind;
        LocalName = local;
        NamespaceUri = uri;
        Prefix = prefix;
        this.value = value;
        Doctype = doctype;
        if (kind == HtmlNodeKind.Element)
        {
            Flags = HtmlElementFlagTable.Of(uri, local);
        }
    }
    internal HtmlElementFlags Flags
    {
        get;
    }
    /// <summary>Gets the document that owns this node.</summary>
    public HtmlDocument OwnerDocument
    {
        get;
    }
    /// <summary>Gets the immutable node kind.</summary>
    public HtmlNodeKind Kind
    {
        get;
    }
    /// <summary>Gets the element, doctype, or processing-instruction local name, or an empty string when unnamed.</summary>
    public string LocalName
    {
        get;
    }
    /// <summary>Gets the element's namespace URI, or an empty string when no namespace applies.</summary>
    public string NamespaceUri
    {
        get;
    }
    /// <summary>Gets the lexical name prefix, or an empty string when absent.</summary>
    public string Prefix
    {
        get;
    }
    /// <summary>Gets or sets the data of a text, comment, or processing-instruction node.</summary>
    /// <exception cref="ArgumentNullException">The assigned value is null.</exception>
    /// <exception cref="InvalidOperationException">The node kind is not writable or the token-character limit is exceeded.</exception>
    public string Value
    {
        get => value; set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Kind is not (HtmlNodeKind.Text or HtmlNodeKind.Comment or HtmlNodeKind.ProcessingInstruction))
            {
                throw new InvalidOperationException("Only text, comments and processing instructions have mutable values.");
            }

            OwnerDocument.CheckToken(value);
            this.value = value;
        }
    }
    // Only tree builders may persist merged, already-charged character tokens.
    internal void SetParsedText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Kind != HtmlNodeKind.Text)
        {
            throw new InvalidOperationException("Parsed text requires a text node.");
        }

        this.value = value;
    }
    internal HtmlNode? TemplateHost;
    // Set only while the node is on the stack of open elements of the tree builder that created it;
    // the byte fits existing object padding, so the node does not grow.
    internal bool Open;
    internal HtmlNode? StructuralParent => Parent ?? TemplateHost;
    private HtmlNode? parent;
    /// <summary>Gets the parent container, or null when detached or when this is a template-content fragment.</summary>
    public HtmlNode? Parent => parent;
    /// <summary>Gets the first child, or null when the node has no children.</summary>
    public HtmlNode? FirstChild => children.Count == 0 ? null : children[0];
    /// <summary>Gets the last child, or null when the node has no children.</summary>
    public HtmlNode? LastChild => children.Count == 0 ? null : children[^1];
    private HtmlNode? previousSibling;
    /// <summary>Gets the preceding sibling, or null when none exists.</summary>
    public HtmlNode? PreviousSibling => previousSibling;
    private HtmlNode? nextSibling;
    /// <summary>Gets the following sibling, or null when none exists.</summary>
    public HtmlNode? NextSibling => nextSibling;
    /// <summary>Gets a live read-only view of the ordinary children in tree order.</summary>
    public IReadOnlyList<HtmlNode> Children => childrenView ??= MutableChildren().AsReadOnly();
    /// <summary>Gets a live read-only view of the attributes in insertion order.</summary>
    public IReadOnlyList<HtmlAttribute> Attributes => attributesView ??= MutableAttributes().AsReadOnly();
    internal List<HtmlAttribute> RawAttributes => attributes;
    internal List<HtmlNode> RawChildren => children;
    /// <summary>Gets the metadata of a document-type node, or null for other kinds.</summary>
    public HtmlDoctype? Doctype
    {
        get;
    }
    internal HtmlNode? templateContent;
    /// <summary>Gets the separate content fragment of an HTML template element, or null for other nodes.</summary>
    public HtmlNode? TemplateContent => templateContent;
    /// <summary>Concatenates this node's ordinary descendant text in tree order.</summary>
    /// <returns>An owned string; comments, processing instructions, and separate template contents are not included.</returns>
    public string GetTextContent()
    {
        using var b = new ValueStringBuilder();
        var stack = new Stack<HtmlNode>();
        stack.Push(this);
        while (stack.TryPop(out var n))
        {
            if (n.Kind == HtmlNodeKind.Text)
            {
                b.Append(n.Value);
            }

            for (var i = n.children.Count - 1; i >= 0; i--)
            {
                stack.Push(n.children[i]);
            }
        }
        return b.ToString();
    }
    private string AttributeName(string name, string uri) => NamespaceUri == HtmlDocument.HtmlNamespace && uri.Length == 0 ? HtmlNames.Fold(name) : name;
    /// <summary>Looks up an attribute by its local name and namespace URI.</summary>
    /// <param name="localName">The name, matched ASCII-case-insensitively for unnamespaced attributes on HTML elements.</param>
    /// <param name="namespaceUri">The namespace URI, or an empty string for no namespace.</param>
    /// <returns>The attribute value, or null when no attribute matches.</returns>
    /// <exception cref="ArgumentNullException">The local name or namespace URI is null.</exception>
    public string? GetAttribute(string localName, string namespaceUri = "")
    {
        ArgumentNullException.ThrowIfNull(localName);
        ArgumentNullException.ThrowIfNull(namespaceUri);
        foreach (var a in attributes)
        {
            if (SameAttributeName(a.LocalName, localName, namespaceUri) && a.NamespaceUri == namespaceUri)
            {
                return a.Value;
            }
        }

        return null;
    }
    /// <summary>Adds or replaces an element attribute by its expanded name.</summary>
    /// <param name="attribute">The attribute to persist; unnamespaced names on HTML elements are ASCII-lowercased.</param>
    /// <exception cref="ArgumentNullException">The attribute or a required component is null.</exception>
    /// <exception cref="InvalidOperationException">The node is not an element or a resource limit is exceeded.</exception>
    public void SetAttribute(HtmlAttribute attribute)
    {
        HtmlDocument.ValidateAttribute(attribute);
        if (Kind != HtmlNodeKind.Element)
        {
            throw new InvalidOperationException("Attributes require an element.");
        }

        var name = AttributeName(attribute.LocalName, attribute.NamespaceUri);
        var index = AttributeIndex(name, attribute.NamespaceUri);
        if (index < 0 && OwnerDocument.Limits.AttributesPerElement is int max && attributes.Count >= max)
        {
            throw new InvalidOperationException("HTML attribute quota exceeded.");
        }

        OwnerDocument.CheckToken(name);
        OwnerDocument.CheckToken(attribute.Value);
        OwnerDocument.InternTriplet(name, attribute.NamespaceUri, attribute.Prefix, out var iName, out var iUri, out var iPrefix);
        var saved = ReferenceEquals(iName, attribute.LocalName) && ReferenceEquals(iUri, attribute.NamespaceUri) && ReferenceEquals(iPrefix, attribute.Prefix)
            ? attribute
            : new HtmlAttribute(iName, attribute.Value, iUri, iPrefix);
        if (index < 0)
        {
            MutableAttributes().Add(saved);
        }
        else
        {
            attributes[index] = saved;
        }
    }
    // Parser-only: appends a unique, already-folded unnamespaced attribute on a freshly created HTML element.
    internal void AppendParsedHtmlAttribute(HtmlAttribute attribute)
    {
        if (OwnerDocument.Limits.AttributesPerElement is int max && attributes.Count >= max)
        {
            throw new InvalidOperationException("HTML attribute quota exceeded.");
        }
        var iName = OwnerDocument.InternSingle(attribute.LocalName);
        var saved = ReferenceEquals(iName, attribute.LocalName) && attribute.NamespaceUri.Length == 0 && attribute.Prefix.Length == 0
            ? attribute
            : new HtmlAttribute(iName, attribute.Value);
        MutableAttributes().Add(saved);
    }
    private bool SameAttributeName(string stored, string candidate, string uri)
    {
        if (NamespaceUri != HtmlDocument.HtmlNamespace || uri.Length != 0)
        {
            return stored == candidate;
        }

        if (stored.Length != candidate.Length)
        {
            return false;
        }

        for (var i = 0; i < stored.Length; i++)
        {
            var c = candidate[i];
            if (c is >= 'A' and <= 'Z')
            {
                c = (char)(c + 32);
            }

            if (stored[i] != c)
            {
                return false;
            }
        }
        return true;
    }
    private int AttributeIndex(string name, string uri)
    {
        for (var i = 0; i < attributes.Count; i++)
        {
            if (SameAttributeName(attributes[i].LocalName, name, uri) && attributes[i].NamespaceUri == uri)
            {
                return i;
            }
        }

        return -1;
    }
    /// <summary>Removes an attribute by its local name and namespace URI.</summary>
    /// <param name="localName">The name, matched ASCII-case-insensitively for unnamespaced attributes on HTML elements.</param>
    /// <param name="namespaceUri">The namespace URI, or an empty string for no namespace.</param>
    /// <returns>Whether a matching attribute was removed.</returns>
    /// <exception cref="ArgumentNullException">The local name or namespace URI is null.</exception>
    public bool RemoveAttribute(string localName, string namespaceUri = "")
    {
        ArgumentNullException.ThrowIfNull(localName);
        ArgumentNullException.ThrowIfNull(namespaceUri);
        var i = AttributeIndex(localName, namespaceUri);
        if (i < 0)
        {
            return false;
        }

        attributes.RemoveAt(i);
        return true;
    }
    /// <summary>Moves a node, or all children of a fragment, to the end of this container.</summary>
    /// <param name="node">The node or fragment owned by this document.</param>
    /// <exception cref="ArgumentNullException">The node is null.</exception>
    /// <exception cref="ArgumentException">The node belongs to a different document.</exception>
    /// <exception cref="InvalidOperationException">The insertion would create a cycle, violate tree shape, or exceed the depth limit.</exception>
    public void AppendChild(HtmlNode node) => Insert(node, null);
    /// <summary>Moves a node, or all children of a fragment, before an existing child.</summary>
    /// <param name="node">The node or fragment owned by this document.</param>
    /// <param name="reference">The current child before which to insert.</param>
    /// <exception cref="ArgumentNullException">The node or reference is null.</exception>
    /// <exception cref="ArgumentException">The reference is not a child or the node belongs to another document.</exception>
    /// <exception cref="InvalidOperationException">The insertion would create a cycle, violate tree shape, or exceed the depth limit.</exception>
    public void InsertBefore(HtmlNode node, HtmlNode reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Parent != this)
        {
            throw new ArgumentException("Reference is not a child.", nameof(reference));
        }

        Insert(node, reference);
    }
    private void Insert(HtmlNode node, HtmlNode? reference)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Kind is not (HtmlNodeKind.Document or HtmlNodeKind.Fragment or HtmlNodeKind.Element))
        {
            throw new InvalidOperationException("Invalid container.");
        }

        if (node.OwnerDocument != OwnerDocument)
        {
            throw new ArgumentException("Import foreign nodes before attachment.", nameof(node));
        }

        if (node == reference)
        {
            return;
        }

        for (var p = this; p != null; p = p.StructuralParent)
        {
            if (p == node)
            {
                throw new InvalidOperationException("Cycle.");
            }
        }

        var fragment = node.Kind == HtmlNodeKind.Fragment;
        if (fragment)
        {
            foreach (var n in node.children)
            {
                PreflightChild(n);
            }
        }
        else
        {
            PreflightChild(node);
        }

        if (Kind == HtmlNodeKind.Document)
        {
            int elements = 0, doctypes = 0;
            foreach (var n in children)
            {
                if (fragment || n != node)
                {
                    if (n.Kind == HtmlNodeKind.Element)
                    {
                        elements++;
                    }

                    if (n.Kind == HtmlNodeKind.DocumentType)
                    {
                        doctypes++;
                    }
                }
            }

            if (fragment)
            {
                foreach (var n in node.children)
                {
                    if (n.Kind == HtmlNodeKind.Element)
                    {
                        elements++;
                    }

                    if (n.Kind == HtmlNodeKind.DocumentType)
                    {
                        doctypes++;
                    }
                }
            }
            else
            {
                if (node.Kind == HtmlNodeKind.Element)
                {
                    elements++;
                }

                if (node.Kind == HtmlNodeKind.DocumentType)
                {
                    doctypes++;
                }
            }
            if (elements > 1 || doctypes > 1)
            {
                throw new InvalidOperationException("Duplicate document root or doctype.");
            }
        }
        var added = fragment ? node.children.Count : 1;
        MutableChildren().EnsureCapacity(checked(children.Count + added));
        OwnerDocument.DepthEpoch++;
        if (fragment)
        {
            while (node.FirstChild is { } child)
            {
                Attach(child, reference);
            }
        }
        else
        {
            Attach(node, reference);
        }

        OwnerDocument.DepthEpoch++;
    }
    // Parser-only append of a freshly created leaf node: the cycle, container and document-shape checks of Insert cannot fail for it.
    internal void AppendParsed(HtmlNode node)
    {
        if ((node.Kind is HtmlNodeKind.Element or HtmlNodeKind.Text or HtmlNodeKind.Comment) && (Kind is HtmlNodeKind.Element or HtmlNodeKind.Fragment) && node.parent == null && node.TemplateHost == null && node.children.Count == 0 && node.templateContent == null && node != this && node.OwnerDocument == OwnerDocument)
        {
            if (OwnerDocument.Limits.Depth is int max && ElementDepth() + (node.Kind == HtmlNodeKind.Element ? 1 : 0) > max)
            {
                throw new InvalidOperationException("HTML depth quota exceeded.");
            }

            node.depthEpoch = 0;
            var list = MutableChildren();
            list.Add(node);
            node.parent = this;
            Relink(list.Count - 1);
        }
        else
        {
            Insert(node, null);
        }
    }
    private int depthEpoch;
    private int depthCache;
    private int ElementDepth()
    {
        var epoch = OwnerDocument.DepthEpoch;
        if (depthEpoch == epoch)
        {
            return depthCache;
        }

        var d = 0;
        for (var p = this; p != null; p = p.StructuralParent)
        {
            if (p != this && p.depthEpoch == epoch)
            {
                d += p.depthCache;
                break;
            }
            if (p.Kind == HtmlNodeKind.Element)
            {
                d++;
            }
        }
        depthEpoch = epoch;
        depthCache = d;
        return d;
    }
    private void PreflightChild(HtmlNode n)
    {
        if (n.Kind is HtmlNodeKind.Document or HtmlNodeKind.Fragment)
        {
            throw new InvalidOperationException("Invalid child.");
        }

        for (var p = this; p != null; p = p.StructuralParent)
        {
            if (p == n)
            {
                throw new InvalidOperationException("Cycle.");
            }
        }

        if (Kind == HtmlNodeKind.Document && n.Kind == HtmlNodeKind.Text)
        {
            throw new InvalidOperationException("Document text.");
        }

        if (Kind != HtmlNodeKind.Document && n.Kind == HtmlNodeKind.DocumentType)
        {
            throw new InvalidOperationException("Doctype requires document.");
        }

        CheckDepth(n);
    }
    private void Attach(HtmlNode node, HtmlNode? reference)
    {
        node.Remove();
        var index = reference == null ? children.Count : children.IndexOf(reference);
        MutableChildren().Insert(index, node);
        node.parent = this;
        Relink(index);
    }
    // Templates add a private ownership edge while their content's public Parent is null.
    internal static HtmlNode? NextStructural(HtmlNode current, HtmlNode root)
    {
        if (current.TemplateContent is { } template)
        {
            return template;
        }

        if (current.FirstChild is { } first)
        {
            return first;
        }

        while (current != root)
        {
            if (current.NextSibling is { } sibling)
            {
                return sibling;
            }

            var parent = current.StructuralParent!;
            if (current.TemplateHost != null && parent.FirstChild is { } child)
            {
                return child;
            }

            current = parent;
        }
        return null;
    }
    private void CheckDepth(HtmlNode node)
    {
        if (OwnerDocument.Limits.Depth is not int max)
        {
            return;
        }

        var parentDepth = ElementDepth();
        if (node.children.Count == 0 && node.templateContent == null)
        {
            if (parentDepth + (node.Kind == HtmlNodeKind.Element ? 1 : 0) > max)
            {
                throw new InvalidOperationException("HTML depth quota exceeded.");
            }

            return;
        }
        for (var n = node; n != null; n = NextStructural(n, node))
        {
            var depth = parentDepth;
            for (var p = n; p != node.StructuralParent; p = p!.StructuralParent)
            {
                if (p!.Kind == HtmlNodeKind.Element)
                {
                    depth++;
                }
            }

            if (depth > max)
            {
                throw new InvalidOperationException("HTML depth quota exceeded.");
            }
        }
    }
    private void Relink(int index)
    {
        var n = children[index];
        n.previousSibling = index == 0 ? null : children[index - 1];
        n.nextSibling = index + 1 == children.Count ? null : children[index + 1];
        n.PreviousSibling?.nextSibling = n;
        n.NextSibling?.previousSibling = n;
    }
    /// <summary>Detaches this node from its parent without changing its owner or descendants.</summary>
    /// <remarks>Removing an already detached node has no effect.</remarks>
    public void Remove()
    {
        if (Parent == null)
        {
            return;
        }

        if (children.Count != 0 || templateContent != null)
        {
            OwnerDocument.DepthEpoch++;
        }
        else
        {
            depthEpoch = 0;
        }

        var oldParent = Parent;
        var i = oldParent.children.IndexOf(this);
        oldParent.children.RemoveAt(i);
        PreviousSibling?.nextSibling = NextSibling;
        NextSibling?.previousSibling = PreviousSibling;
        parent = null;
        previousSibling = null;
        nextSibling = null;
    }
    /// <summary>Creates a detached copy owned by the same document.</summary>
    /// <param name="deep">Whether to copy descendants and template contents as well as the node and its attributes.</param>
    /// <returns>The copied node.</returns>
    /// <exception cref="InvalidOperationException">Copying exceeds a document resource limit.</exception>
    public HtmlNode Clone(bool deep = true) => OwnerDocument.Copy(this, deep);
}
internal static class HtmlNames
{
    internal static string Fold(string value)
    {
        var first = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is >= 'A' and <= 'Z')
            {
                first = i;
                break;
            }
        }

        if (first < 0)
        {
            return value;
        }

        return string.Create(value.Length, value, static (output, input) => { for (var i = 0; i < input.Length; i++) { output[i] = input[i] is >= 'A' and <= 'Z' ? (char)(input[i] + 32) : input[i]; } });
    }
}
