using Xunit;
using Xemele.Html;
namespace Xemele.Tests.HtmlParsedText;
public sealed class ParsedTextTests
{
 [Fact] public void MergedTextDoesNotBecomeAnotherQuotaToken()
 {
  var owner=new HtmlDocument(new(){TokenCharacters=1});
  var text=owner.CreateParsedText("😀xy");Assert.Same(owner,text.OwnerDocument);Assert.Equal("😀xy",text.Value);
  text.SetParsedText("😀xy😀");Assert.Equal("😀xy😀",text.Value);
  Assert.Throws<InvalidOperationException>(()=>owner.CreateText("😀x"));
  Assert.Throws<InvalidOperationException>(()=>text.Value="😀x");Assert.Equal("😀xy😀",text.Value);
  text.Value="😀";Assert.Equal("😀",text.Value);
 }
 [Fact] public void InternalLaneRejectsNullAndOtherKindsAtomically()
 {
  var owner=new HtmlDocument();var text=owner.CreateText("old");
  Assert.Throws<ArgumentNullException>(()=>owner.CreateParsedText(null!));
  Assert.Throws<ArgumentNullException>(()=>text.SetParsedText(null!));Assert.Equal("old",text.Value);
  foreach(var node in new[]{owner.DocumentNode,owner.CreateFragment(),owner.CreateElement("p"),owner.CreateComment("old"),owner.CreateProcessingInstruction("p","old"),owner.CreateDocumentType(new("html",null,null,false))})
  {string old=node.Value;Assert.Throws<InvalidOperationException>(()=>node.SetParsedText("new"));Assert.Equal(old,node.Value);}
 }
 [Fact] public void ParserCopyOnlyExemptsMergedTextAndPreservesTemplatesOwners()
 {
  var source=new HtmlDocument();var template=source.CreateElement("template");template.TemplateContent!.AppendChild(source.CreateText("😀long"));
  var target=new HtmlDocument(new(){TokenCharacters=8});var copy=target.ImportParsed(template);var text=Assert.Single(copy.TemplateContent!.Children);Assert.Same(target,text.OwnerDocument);Assert.Equal("😀long",text.Value);Assert.Same(copy.TemplateContent,text.Parent);
  var strict=new HtmlDocument(new(){TokenCharacters=1});var large=source.CreateText("😀long");
  Assert.Equal(large.Value,strict.ImportParsed(large).Value);Assert.Throws<InvalidOperationException>(()=>strict.Import(large));Assert.Throws<InvalidOperationException>(()=>strict.ImportParsed(source.CreateComment("long")));Assert.Throws<InvalidOperationException>(()=>strict.ImportParsed(source.CreateProcessingInstruction("p","long")));Assert.Throws<InvalidOperationException>(()=>strict.ImportParsed(source.CreateElement("long")));
  var element=source.CreateElement("p");element.SetAttribute(new("a","long"));Assert.Throws<InvalidOperationException>(()=>strict.ImportParsed(element));
  Assert.Throws<ArgumentNullException>(()=>target.ImportParsed(null!));Assert.Throws<ArgumentException>(()=>target.ImportParsed(source.DocumentNode));Assert.Empty(target.ImportParsed(template,false).TemplateContent!.Children);
 }
 [Fact] public void ParsedTextStillObeysAttachmentAndDepthRules()
 {
  var owner=new HtmlDocument(new(){Depth=1,TokenCharacters=1});var root=owner.CreateElement("p");owner.DocumentNode.AppendChild(root);var text=owner.CreateParsedText("long");
  Assert.Throws<InvalidOperationException>(()=>root.AppendChild(text));Assert.Null(text.Parent);Assert.Empty(root.Children);
  var other=new HtmlDocument();Assert.Throws<ArgumentException>(()=>other.DocumentNode.AppendChild(text));Assert.Same(owner,text.OwnerDocument);
 }
}
