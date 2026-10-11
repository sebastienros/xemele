using System.Text.Json;
using Xunit;
using Xemele.Html;
namespace Xemele.Tests.HtmlModel;
public sealed class ModelTests
{
 private static string Fixture(string name) { var path=Directory.GetCurrentDirectory();while(!File.Exists(Path.Combine(path,"tests/fixtures/M7-html-model",name))) path=Directory.GetParent(path)?.FullName ?? throw new InvalidOperationException("Fixture unavailable");return Path.Combine(path,"tests/fixtures/M7-html-model",name); }
 [Fact] public void AllAuthoritativeReferencesAndUnknownPrefixes()
 {
  using var json=JsonDocument.Parse(File.ReadAllText(Fixture("entities.json")));int count=0;
  foreach(var e in json.RootElement.EnumerateObject()) {var name=e.Name[1..];var expected=e.Value.GetProperty("characters").GetString();Assert.True(HtmlNamedReferences.TryGet(name,out var actual));Assert.Equal(expected,actual);Assert.Equal(e.Value.GetProperty("codepoints").EnumerateArray().Select(v=>v.GetInt32()),actual.EnumerateRunes().Select(r=>r.Value));Assert.True(HtmlNamedReferences.Match(name+"!",out int consumed,out actual));Assert.Equal(name.Length,consumed);Assert.Equal(expected,actual);Assert.False(HtmlNamedReferences.TryGet(name+"!",out _));Assert.False(HtmlNamedReferences.TryGet("\u00e9"+name,out _));count++; }
  Assert.Equal(2231,count);Assert.False(HtmlNamedReferences.TryGet(new string('a',33),out _));Assert.False(HtmlNamedReferences.Match("unknown;",out _,out _));Assert.True(HtmlNamedReferences.Match("notin;",out var used,out var value));Assert.Equal(6,used);Assert.Equal("∉",value);
 }
 [Fact] public void AmbiguousAttributeReferencesRemainLiteral()
 {
  foreach(var suffix in new[]{"a","Z","0","="}) {Assert.False(HtmlNamedReferences.Match("amp"+suffix,true,out var n,out var value));Assert.Equal(0,n);Assert.Equal("",value);Assert.True(HtmlNamedReferences.Match("amp"+suffix,false,out _,out _));}
  Assert.True(HtmlNamedReferences.Match("amp;=",true,out var consumed,out var decoded));Assert.Equal(4,consumed);Assert.Equal("&",decoded);Assert.True(HtmlNamedReferences.Match("ampé",true,out _,out _));
 }
 [Fact] public void FragmentContextAndOptionsAreValidatedSnapshots()
 {
  var d=new HtmlDocument();var form=d.CreateElement("form");var context=d.CreateElement("textarea");form.AppendChild(context);context.SetAttribute(new("x","original"));var snapshot=HtmlOperationSnapshots.Snapshot(new HtmlFragmentContext(context,HtmlQuirksMode.LimitedQuirks));context.SetAttribute(new("x","changed"));form.Remove();Assert.Equal("original",snapshot.Ancestry[0].Attributes[0].Value);Assert.Equal("form",snapshot.Ancestry[1].LocalName);Assert.Equal(HtmlQuirksMode.LimitedQuirks,snapshot.QuirksMode);Assert.Throws<ArgumentException>(()=>HtmlOperationSnapshots.Snapshot(new HtmlFragmentContext(d.CreateText("x"))));Assert.Throws<ArgumentOutOfRangeException>(()=>HtmlOperationSnapshots.Snapshot(new HtmlFragmentContext(context,(HtmlQuirksMode)9)));Assert.Throws<ArgumentException>(()=>HtmlOperationSnapshots.Snapshot(new HtmlTokenizerOptions{InitialState=HtmlTokenizerState.RawText}));Assert.Equal("script",HtmlOperationSnapshots.Snapshot(new HtmlTokenizerOptions{InitialState=HtmlTokenizerState.ScriptData,LastStartTagName="SCRIPT"}).LastStartTagName);var t=d.CreateElement("template");form.AppendChild(t);var inner=d.CreateElement("textarea");t.TemplateContent!.AppendChild(inner);Assert.Single(HtmlOperationSnapshots.Snapshot(new HtmlFragmentContext(inner)).Ancestry);var options=new Html5ParseOptions{ScriptingEnabled=true};var copy=HtmlOperationSnapshots.Snapshot(options);Assert.True(copy.ScriptingEnabled);Assert.NotSame(options.Limits,copy.Limits);d.SetMetadata(HtmlQuirksMode.Quirks,true);Assert.True(d.ScriptingEnabled);Assert.Equal(HtmlQuirksMode.Quirks,d.QuirksMode);
 }
 [Fact] public void ForeignTablesAreExhaustiveAndOrdinal()
 {
  using var json=JsonDocument.Parse(File.ReadAllText(Fixture("foreign.json")));
  foreach(var e in json.RootElement.GetProperty("svgElements").EnumerateObject()){Assert.Equal(e.Value.GetString(),HtmlForeignNames.AdjustSvgElement(e.Name));Assert.Null(HtmlForeignNames.AdjustSvgElement((e.Name+"x").AsSpan()));}
  foreach(var e in json.RootElement.GetProperty("svgAttributes").EnumerateObject()){Assert.Equal(e.Value.GetString(),HtmlForeignNames.AdjustSvgAttribute(e.Name));Assert.Null(HtmlForeignNames.AdjustSvgAttribute(e.Name.ToUpperInvariant().AsSpan()));}
  Assert.Equal("definitionURL",HtmlForeignNames.AdjustMathMlAttribute("definitionurl"));Assert.Null(HtmlForeignNames.AdjustMathMlAttribute("definitionURL".AsSpan()));
  foreach(var e in json.RootElement.GetProperty("foreignAttributes").EnumerateObject()){var a=HtmlForeignNames.AdjustForeignAttribute(new(e.Name,"v"));Assert.Equal(e.Value[0].GetString(),a.LocalName);Assert.Equal(e.Value[1].GetString(),a.NamespaceUri);Assert.Equal(e.Value[2].GetString(),a.Prefix);Assert.Equal("v",a.Value);}
 }
 [Fact] public void OwnershipLinksFragmentAtomicityAndReadOnlyViews()
 {
  var d=new HtmlDocument();Assert.Throws<InvalidOperationException>(()=>d.RootElement);var root=d.CreateElement("DIV");d.DocumentNode.AppendChild(root);Assert.Equal("div",d.RootElement.LocalName);
  var fragment=d.CreateFragment();var a=d.CreateText("a");var b=d.CreateComment("--");fragment.AppendChild(a);fragment.AppendChild(b);root.AppendChild(fragment);Assert.Empty(fragment.Children);Assert.Same(a,root.FirstChild);Assert.Same(b,a.NextSibling);Assert.Same(a,b.PreviousSibling);Assert.Same(root,a.Parent);b.Remove();Assert.Null(a.NextSibling);Assert.Null(b.Parent);
  Assert.Throws<ArgumentException>(()=>root.AppendChild(new HtmlDocument().CreateText("x")));Assert.Throws<InvalidOperationException>(()=>root.AppendChild(root));Assert.Throws<InvalidOperationException>(()=>d.DocumentNode.AppendChild(d.CreateText("x")));
  var invalid=d.CreateFragment();invalid.AppendChild(d.CreateElement("x"));invalid.AppendChild(d.CreateElement("y"));Assert.Throws<InvalidOperationException>(()=>d.DocumentNode.AppendChild(invalid));Assert.Equal(2,invalid.Children.Count);Assert.Same(root,d.RootElement);var cloned=d.DocumentNode.Clone();Assert.Same(d,cloned.OwnerDocument);Assert.Null(cloned.Parent);Assert.NotSame(root,cloned.FirstChild);Assert.Throws<ArgumentException>(()=>d.Import(d.DocumentNode));
  Assert.Throws<NotSupportedException>(()=>((IList<HtmlNode>)root.Children).Add(a));var enumerator=root.Children.GetEnumerator();Assert.True(enumerator.MoveNext());root.AppendChild(b);Assert.Throws<InvalidOperationException>(()=>enumerator.MoveNext());
 }
 [Fact] public void TemplateEdgesArePrivateAndParticipateInCyclesAndCopies()
 {
  var d=new HtmlDocument();var t=d.CreateElement("template");var content=t.TemplateContent!;Assert.Null(content.Parent);Assert.Empty(t.Children);content.AppendChild(d.CreateText("hidden"));Assert.Equal("",t.GetTextContent());Assert.Throws<InvalidOperationException>(()=>content.AppendChild(t));var nested=d.CreateElement("template");content.AppendChild(nested);Assert.Throws<InvalidOperationException>(()=>nested.TemplateContent!.AppendChild(t));
  var target=new HtmlDocument();var imported=target.Import(t);Assert.NotSame(t,imported);Assert.Same(target,imported.OwnerDocument);Assert.Equal("hidden",imported.TemplateContent!.GetTextContent());Assert.Null(imported.TemplateContent.Parent);Assert.Empty(t.Clone(false).TemplateContent!.Children);var receiver=d.CreateElement("div");receiver.AppendChild(content);Assert.Empty(content.Children);Assert.Equal("hidden",receiver.GetTextContent());Assert.Null(content.Parent);
 }
 [Fact] public void AttributesPreserveExpandedIdentityOrderSnapshotsAndHtmlData()
 {
  var d=new HtmlDocument();var e=d.CreateElement("bad name\0");e.SetAttribute(new("CLASS","one"));e.SetAttribute(new("other","two"));var old=e.Attributes[0];e.SetAttribute(new("class","three"));Assert.Equal("one",old.Value);Assert.Equal("class",e.Attributes[0].LocalName);Assert.Equal("three",e.GetAttribute("CLASS"));e.SetAttribute(new("CLASS","foreign","urn:ns","p"));Assert.Equal(3,e.Attributes.Count);Assert.Equal("foreign",e.GetAttribute("CLASS","urn:ns"));Assert.True(e.RemoveAttribute("CLASS"));Assert.Null(e.GetAttribute("class"));
  Assert.Equal("--\0-",d.CreateComment("--\0-").Value);var type=d.CreateDocumentType(new("html","","",true));Assert.Equal("",type.Doctype!.PublicIdentifier);Assert.Equal("",type.Doctype.SystemIdentifier);
 }
 [Fact] public void QuotasAndPreflightPreserveLinks()
 {
  Assert.Throws<ArgumentOutOfRangeException>(()=>new HtmlDocument(new(){Depth=0}));var d=new HtmlDocument(new(){Depth=1,AttributesPerElement=1,TokenCharacters=2,InternedNames=4});var e=d.CreateElement("p");d.DocumentNode.AppendChild(e);e.AppendChild(d.CreateText("😀x"));Assert.Throws<InvalidOperationException>(()=>d.CreateText("😀xy"));Assert.Throws<InvalidOperationException>(()=>e.AppendChild(d.CreateElement("q")));e.SetAttribute(new("a","v"));Assert.Throws<InvalidOperationException>(()=>e.SetAttribute(new("b","v")));Assert.Single(e.Attributes);Assert.Single(e.Children);
 }
 [Fact] public void TokenOwnsImmutableSnapshotsLocationsAndDoctypePresence()
 {
  var attrs=new List<HtmlAttribute>{new("x","one")};var location=new XmlLocation(new Uri("https://example.test/a"),3,4,17);var token=new HtmlToken(HtmlTokenKind.StartTag,"p","",attrs,true,null,location);attrs[0]=new("x","two");Assert.Equal("one",token.Attributes[0].Value);Assert.Equal(location,token.Location);Assert.True(token.SelfClosing);Assert.Throws<NotSupportedException>(()=>((IList<HtmlAttribute>)token.Attributes).Clear());Assert.Throws<ArgumentOutOfRangeException>(()=>new HtmlToken((HtmlTokenKind)99,"","",[],false,null,default));Assert.Throws<ArgumentNullException>(()=>new HtmlToken(HtmlTokenKind.StartTag,"","",new HtmlAttribute[]{null!},false,null,default));Assert.Null(new HtmlToken(HtmlTokenKind.EndOfFile,"","",[],false,null,new(null,1,1,null)).Location.ByteOffset);
 }
}
