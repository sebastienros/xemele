using System.Collections.ObjectModel;
using System.Text;
using Xemele;
namespace Xemele.Html;
public enum HtmlNodeKind { Document, Fragment, Element, Text, Comment, DocumentType }
public enum HtmlQuirksMode { NoQuirks, LimitedQuirks, Quirks }
public sealed record HtmlAttribute(string LocalName, string Value, string NamespaceUri = "", string Prefix = "");
public sealed record HtmlDoctype(string Name, string? PublicIdentifier, string? SystemIdentifier, bool ForceQuirks);
public sealed record HtmlParseOptions { public XmlResourceLimits Limits { get; init; } = new(); }
public sealed record Html5ParseOptions { public XmlResourceLimits Limits { get; init; } = new(); public bool ScriptingEnabled { get; init; } }
public sealed record HtmlFragmentContext(HtmlNode ContextElement, HtmlQuirksMode QuirksMode = HtmlQuirksMode.NoQuirks);
public enum HtmlTokenizerState { Data, RcData, RawText, ScriptData, PlainText }
public sealed record HtmlTokenizerOptions
{
 public XmlResourceLimits Limits { get; init; } = new();
 public HtmlTokenizerState InitialState { get; init; } = HtmlTokenizerState.Data;
 public string LastStartTagName { get; init; } = "";
}
public enum HtmlTokenKind { Doctype, StartTag, EndTag, Comment, Character, EndOfFile }
public sealed class HtmlToken
{
 public HtmlToken(HtmlTokenKind kind, string name, string data, IReadOnlyList<HtmlAttribute> attributes, bool selfClosing, HtmlDoctype? doctype, XmlLocation location)
 {
  if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
  ArgumentNullException.ThrowIfNull(name); ArgumentNullException.ThrowIfNull(data); ArgumentNullException.ThrowIfNull(attributes);
  var copy = new HtmlAttribute[attributes.Count];
  for (int i=0;i<copy.Length;i++) { HtmlDocument.ValidateAttribute(attributes[i]); copy[i]=attributes[i]; }
  HtmlDocument.ValidateDoctype(doctype);
  Kind=kind; Name=name; Data=data; Attributes=Array.AsReadOnly(copy); SelfClosing=selfClosing; Doctype=doctype; Location=location;
 }
 public HtmlTokenKind Kind { get; }
 public string Name { get; }
 public string Data { get; }
 public IReadOnlyList<HtmlAttribute> Attributes { get; }
 public bool SelfClosing { get; }
 public HtmlDoctype? Doctype { get; }
 public XmlLocation Location { get; }
}
public sealed class HtmlDocument
{
 internal const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
 internal readonly XmlResourceLimits Limits;
 private readonly Dictionary<string,string> names = new(StringComparer.Ordinal);
 public HtmlDocument(XmlResourceLimits? limits = null) { Limits=(limits ?? new()) with {}; Limits.Validate(); DocumentNode=new(this,HtmlNodeKind.Document); }
 public HtmlNode DocumentNode { get; }
 public HtmlNode RootElement { get { foreach(var n in DocumentNode.Children) if(n.Kind==HtmlNodeKind.Element) return n; throw new InvalidOperationException("Document has no root element."); } }
 private bool scriptingEnabled;
 public bool ScriptingEnabled => scriptingEnabled;
 private HtmlQuirksMode quirksMode;
 public HtmlQuirksMode QuirksMode => quirksMode;
 internal void SetMetadata(HtmlQuirksMode quirks, bool scripting) { if (!Enum.IsDefined(quirks)) throw new ArgumentOutOfRangeException(nameof(quirks)); quirksMode=quirks; scriptingEnabled=scripting; }
 internal static void ValidateAttribute(HtmlAttribute attribute) { ArgumentNullException.ThrowIfNull(attribute); ArgumentNullException.ThrowIfNull(attribute.LocalName); ArgumentNullException.ThrowIfNull(attribute.Value); ArgumentNullException.ThrowIfNull(attribute.NamespaceUri); ArgumentNullException.ThrowIfNull(attribute.Prefix); }
 internal static void ValidateDoctype(HtmlDoctype? doctype) { if(doctype is not null) ArgumentNullException.ThrowIfNull(doctype.Name); }
 internal void CheckNames(string a, string b="", string c="")
 {
  int missing=0;
  if(a.Length!=0&&!names.ContainsKey(a))missing++;
  if(b.Length!=0&&b!=a&&!names.ContainsKey(b))missing++;
  if(c.Length!=0&&c!=a&&c!=b&&!names.ContainsKey(c))missing++;
  if(Limits.InternedNames is int max && (long)names.Count+missing>max) throw new InvalidOperationException("HTML interned-name quota exceeded.");
 }
 internal string Intern(string value) { if(value.Length==0) return ""; if(names.TryGetValue(value,out var found)) return found; names.Add(value,value); return value; }
 internal void CheckToken(string value) { if(Limits.TokenCharacters is not int max)return; int count=0; foreach(var rune in value.EnumerateRunes()) if(++count>max) throw new InvalidOperationException("HTML token quota exceeded."); }
 public HtmlNode CreateElement(string localName, string namespaceUri=HtmlNamespace, string prefix="")
 {
  ArgumentNullException.ThrowIfNull(localName); ArgumentNullException.ThrowIfNull(namespaceUri); ArgumentNullException.ThrowIfNull(prefix);
  if(namespaceUri==HtmlNamespace) localName=HtmlNames.Fold(localName);
  CheckToken(localName); CheckNames(localName,namespaceUri,prefix);
  var node=new HtmlNode(this,HtmlNodeKind.Element,Intern(localName),Intern(namespaceUri),Intern(prefix));
  if(namespaceUri==HtmlNamespace && localName=="template") { node.templateContent=CreateFragment(); node.TemplateContent!.TemplateHost=node; }
  return node;
 }
 public HtmlNode CreateText(string value) => CreateValue(HtmlNodeKind.Text,value);
 public HtmlNode CreateComment(string value) => CreateValue(HtmlNodeKind.Comment,value);
 private HtmlNode CreateValue(HtmlNodeKind kind,string value) { ArgumentNullException.ThrowIfNull(value); CheckToken(value); return new(this,kind,value:value); }
 public HtmlNode CreateDocumentType(HtmlDoctype doctype) { ArgumentNullException.ThrowIfNull(doctype); ValidateDoctype(doctype); CheckToken(doctype.Name); return new(this,HtmlNodeKind.DocumentType,doctype.Name,doctype:doctype); }
 public HtmlNode CreateFragment() => new(this,HtmlNodeKind.Fragment);
 public HtmlNode Import(HtmlNode node,bool deep=true)
 {
  ArgumentNullException.ThrowIfNull(node); if(node.Kind==HtmlNodeKind.Document) throw new ArgumentException("Cannot import a document.",nameof(node));
  return Copy(node,deep);
 }
 internal HtmlNode Copy(HtmlNode node,bool deep)
 {
  var copy=CopyShallow(node);
  if(deep) { var work=new Stack<(HtmlNode Source,HtmlNode Target)>(); work.Push((node,copy)); while(work.TryPop(out var pair)) { foreach(var child in pair.Source.Children) {var c=CopyShallow(child); pair.Target.AppendChild(c); work.Push((child,c));} if(pair.Source.TemplateContent is { } template) work.Push((template,pair.Target.TemplateContent!)); } }
  return copy;
 }
 private HtmlNode CopyShallow(HtmlNode node)
 {
  HtmlNode result=node.Kind switch {HtmlNodeKind.Element=>CreateElement(node.LocalName,node.NamespaceUri,node.Prefix),HtmlNodeKind.Text=>CreateText(node.Value),HtmlNodeKind.Comment=>CreateComment(node.Value),HtmlNodeKind.DocumentType=>CreateDocumentType(node.Doctype!),HtmlNodeKind.Fragment=>CreateFragment(),HtmlNodeKind.Document=>new HtmlNode(this,HtmlNodeKind.Document),_=>throw new ArgumentException("Unsupported node kind.")};
  foreach(var a in node.Attributes) result.SetAttribute(a); return result;
 }
}
public sealed class HtmlNode
{
 private readonly List<HtmlNode> children=new();
 private readonly List<HtmlAttribute> attributes=new();
 private string value;
 internal HtmlNode(HtmlDocument owner,HtmlNodeKind kind,string local="",string uri="",string prefix="",string value="",HtmlDoctype? doctype=null)
 {OwnerDocument=owner;Kind=kind;LocalName=local;NamespaceUri=uri;Prefix=prefix;this.value=value;Doctype=doctype;Children=children.AsReadOnly();Attributes=attributes.AsReadOnly();}
 public HtmlDocument OwnerDocument {get;}
 public HtmlNodeKind Kind {get;}
 public string LocalName {get;}
 public string NamespaceUri {get;}
 public string Prefix {get;}
 public string Value {get=>value;set {ArgumentNullException.ThrowIfNull(value);if(Kind is not (HtmlNodeKind.Text or HtmlNodeKind.Comment)) throw new InvalidOperationException("Only text and comments have mutable values.");OwnerDocument.CheckToken(value);this.value=value;}}
 internal HtmlNode? TemplateHost;
 internal HtmlNode? StructuralParent => Parent ?? TemplateHost;
 private HtmlNode? parent;
 public HtmlNode? Parent => parent;
 public HtmlNode? FirstChild => children.Count==0?null:children[0];
 public HtmlNode? LastChild => children.Count==0?null:children[^1];
 private HtmlNode? previousSibling;
 public HtmlNode? PreviousSibling => previousSibling;
 private HtmlNode? nextSibling;
 public HtmlNode? NextSibling => nextSibling;
 public IReadOnlyList<HtmlNode> Children {get;}
 public IReadOnlyList<HtmlAttribute> Attributes {get;}
 public HtmlDoctype? Doctype {get;}
 internal HtmlNode? templateContent;
 public HtmlNode? TemplateContent => templateContent;
 public string GetTextContent() {var b=new StringBuilder();var stack=new Stack<HtmlNode>();stack.Push(this);while(stack.TryPop(out var n)) {if(n.Kind==HtmlNodeKind.Text)b.Append(n.Value);for(int i=n.children.Count-1;i>=0;i--)stack.Push(n.children[i]);}return b.ToString();}
 private string AttributeName(string name,string uri) => NamespaceUri==HtmlDocument.HtmlNamespace && uri.Length==0?HtmlNames.Fold(name):name;
 public string? GetAttribute(string localName,string namespaceUri="") {ArgumentNullException.ThrowIfNull(localName);ArgumentNullException.ThrowIfNull(namespaceUri);foreach(var a in attributes)if(SameAttributeName(a.LocalName,localName,namespaceUri) && a.NamespaceUri==namespaceUri)return a.Value;return null;}
 public void SetAttribute(HtmlAttribute attribute)
 {
  HtmlDocument.ValidateAttribute(attribute);if(Kind!=HtmlNodeKind.Element)throw new InvalidOperationException("Attributes require an element.");
  string name=AttributeName(attribute.LocalName,attribute.NamespaceUri); int index=AttributeIndex(name,attribute.NamespaceUri);
  if(index<0 && OwnerDocument.Limits.AttributesPerElement is int max && attributes.Count>=max)throw new InvalidOperationException("HTML attribute quota exceeded.");
  OwnerDocument.CheckToken(name);OwnerDocument.CheckToken(attribute.Value);OwnerDocument.CheckNames(name,attribute.NamespaceUri,attribute.Prefix);
  var saved=attribute with {LocalName=OwnerDocument.Intern(name),NamespaceUri=OwnerDocument.Intern(attribute.NamespaceUri),Prefix=OwnerDocument.Intern(attribute.Prefix)};
  if(index<0)attributes.Add(saved);else attributes[index]=saved;
 }
 private bool SameAttributeName(string stored,string candidate,string uri)
 {
  if(NamespaceUri!=HtmlDocument.HtmlNamespace || uri.Length!=0)return stored==candidate;
  if(stored.Length!=candidate.Length)return false;
  for(int i=0;i<stored.Length;i++){char c=candidate[i];if(c is >= 'A' and <= 'Z')c=(char)(c+32);if(stored[i]!=c)return false;}return true;
 }
 private int AttributeIndex(string name,string uri) {for(int i=0;i<attributes.Count;i++)if(SameAttributeName(attributes[i].LocalName,name,uri)&&attributes[i].NamespaceUri==uri)return i;return -1;}
 public bool RemoveAttribute(string localName,string namespaceUri="") {ArgumentNullException.ThrowIfNull(localName);ArgumentNullException.ThrowIfNull(namespaceUri);int i=AttributeIndex(localName,namespaceUri);if(i<0)return false;attributes.RemoveAt(i);return true;}
 public void AppendChild(HtmlNode node) => Insert(node,null);
 public void InsertBefore(HtmlNode node,HtmlNode reference) {ArgumentNullException.ThrowIfNull(reference);if(reference.Parent!=this)throw new ArgumentException("Reference is not a child.",nameof(reference));Insert(node,reference);}
 private void Insert(HtmlNode node,HtmlNode? reference)
 {
  ArgumentNullException.ThrowIfNull(node);if(Kind is not (HtmlNodeKind.Document or HtmlNodeKind.Fragment or HtmlNodeKind.Element))throw new InvalidOperationException("Invalid container.");
  if(node.OwnerDocument!=OwnerDocument)throw new ArgumentException("Import foreign nodes before attachment.",nameof(node));
  if(node==reference)return;
  for(HtmlNode? p=this;p!=null;p=p.StructuralParent)if(p==node)throw new InvalidOperationException("Cycle.");
  bool fragment=node.Kind==HtmlNodeKind.Fragment;
  if(fragment) {foreach(var n in node.children)PreflightChild(n);} else PreflightChild(node);
  if(Kind==HtmlNodeKind.Document)
  {
   int elements=0,doctypes=0;
   foreach(var n in children)if(fragment || n!=node){if(n.Kind==HtmlNodeKind.Element)elements++;if(n.Kind==HtmlNodeKind.DocumentType)doctypes++;}
   if(fragment){foreach(var n in node.children){if(n.Kind==HtmlNodeKind.Element)elements++;if(n.Kind==HtmlNodeKind.DocumentType)doctypes++;}}
   else {if(node.Kind==HtmlNodeKind.Element)elements++;if(node.Kind==HtmlNodeKind.DocumentType)doctypes++;}
   if(elements>1||doctypes>1)throw new InvalidOperationException("Duplicate document root or doctype.");
  }
  int added=fragment?node.children.Count:1;
  children.EnsureCapacity(checked(children.Count+added));
  if(fragment){while(node.FirstChild is {} child)Attach(child,reference);} else Attach(node,reference);
 }
 private void PreflightChild(HtmlNode n)
 {
  if(n.Kind is HtmlNodeKind.Document or HtmlNodeKind.Fragment)throw new InvalidOperationException("Invalid child.");
  for(HtmlNode? p=this;p!=null;p=p.StructuralParent)if(p==n)throw new InvalidOperationException("Cycle.");
  if(Kind==HtmlNodeKind.Document && n.Kind==HtmlNodeKind.Text)throw new InvalidOperationException("Document text.");
  if(Kind!=HtmlNodeKind.Document && n.Kind==HtmlNodeKind.DocumentType)throw new InvalidOperationException("Doctype requires document.");
  CheckDepth(n);
 }
 private void Attach(HtmlNode node,HtmlNode? reference) {node.Remove();int index=reference==null?children.Count:children.IndexOf(reference);children.Insert(index,node);node.parent=this;Relink(index);}
 // Templates add a private ownership edge while their content's public Parent is null.
 internal static HtmlNode? NextStructural(HtmlNode current,HtmlNode root)
 {
  if(current.TemplateContent is {} template)return template;
  if(current.FirstChild is {} first)return first;
  while(current!=root) {if(current.NextSibling is {} sibling)return sibling;var parent=current.StructuralParent!;if(current.TemplateHost!=null && parent.FirstChild is {} child)return child;current=parent;}
  return null;
 }
 private void CheckDepth(HtmlNode node)
 {
  if(OwnerDocument.Limits.Depth is not int max)return;
  int parentDepth=0;for(var p=this;p!=null;p=p.StructuralParent)if(p.Kind==HtmlNodeKind.Element)parentDepth++;
  for(HtmlNode? n=node;n!=null;n=NextStructural(n,node)) {int depth=parentDepth;for(var p=n;p!=node.StructuralParent;p=p!.StructuralParent)if(p!.Kind==HtmlNodeKind.Element)depth++;if(depth>max)throw new InvalidOperationException("HTML depth quota exceeded.");}
 }
 private void Relink(int index) {var n=children[index];n.previousSibling=index==0?null:children[index-1];n.nextSibling=index+1==children.Count?null:children[index+1];if(n.PreviousSibling!=null)n.PreviousSibling.nextSibling=n;if(n.NextSibling!=null)n.NextSibling.previousSibling=n;}
 public void Remove() {if(Parent==null)return;var oldParent=Parent;int i=oldParent.children.IndexOf(this);oldParent.children.RemoveAt(i);if(PreviousSibling!=null)PreviousSibling.nextSibling=NextSibling;if(NextSibling!=null)NextSibling.previousSibling=PreviousSibling;parent=null;previousSibling=null;nextSibling=null;}
 public HtmlNode Clone(bool deep=true) => OwnerDocument.Copy(this,deep);
}
internal static class HtmlNames
{
 internal static string Fold(string value) {int first=-1;for(int i=0;i<value.Length;i++)if(value[i] is >= 'A' and <= 'Z'){first=i;break;}if(first<0)return value;return string.Create(value.Length,value,static (output,input)=>{for(int i=0;i<input.Length;i++)output[i]=input[i] is >= 'A' and <= 'Z'?(char)(input[i]+32):input[i];});}
}
