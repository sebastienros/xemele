using System.Text;
using System.Text.Json;
using Xemele;

// No network or filesystem fallback: only these two resources are granted.
var resolver = new OfflineResources();
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
var document = XmlDocument.Parse("<?xml version='1.10'?><!DOCTYPE r SYSTEM 'urn:demo:dtd'><r xmlns='urn:root' xmlns:p='urn:part' p:id='7'>&item;</r>", options);
var root = document.RootElement;
Require(root.NamespaceUri == "urn:root" && root.GetAttribute("id", "urn:part") == "7", "expanded names");
Require(root.GetAttribute("mode") == "alpha beta", "tokenized default");
Require(root.FirstChild.LocalName == "item" && root.FirstChild.NamespaceUri == "urn:part" && root.GetTextContent() == "A&B", "external entity model");
Require(resolver.Opens.SequenceEqual(new[] { "urn:demo:dtd", "urn:demo:entity" }) && resolver.Streams.All(s => s.Disposals == 1), "explicit grants and disposal");
Require(document.ToXmlString().Contains("A&amp;B", StringComparison.Ordinal), "complete output");

// VC-only references retain identity; neither text nor output may silently lose them.
var partial = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a NMTOKENS ' before &unknown; after '>]><r>left&missing;right</r>", options);
var applied = partial.IncompleteAttributeValues.Single();
Require(ReferenceEquals(applied.Default, partial.IncompleteAttributeDefaults.Single()) && applied.Parts[1] is XmlUnknownAttributeReference { Name: "unknown" }, "symbolic default identity");
var reference = partial.UnresolvedReferences.Single();
Require(reference.Name == "missing" && reference.Parent == partial.RootElement && reference.ChildBoundary == 1 && reference.PreviousSibling.Value == "left", "unresolved anchor");
Refuses(() => partial.RootElement.GetAttribute("a"));
Refuses(() => partial.RootElement.GetTextContent());
Refuses(() => partial.ToXmlString());
Console.WriteLine(JsonSerializer.Serialize(new { profile = "XML 1.0 Fifth Edition / Namespaces 1.0 Third Edition; nonvalidating", root = "{urn:root}r", attribute = "{urn:part}id=7", tokenizedDefault = "alpha beta", entity = "{urn:part}item:A&B", resourceOpens = resolver.Opens.Count, resourceDisposals = resolver.Streams.Sum(s => s.Disposals), symbolicDefaults = partial.IncompleteAttributeDefaults.Count, unresolvedContent = partial.UnresolvedReferences.Count, lossRefusals = 3 }));

static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
static void Refuses(Func<string?> action)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Missing loss refusal");
}
sealed class OfflineResources : XmlResourceResolver
{
    internal List<string> Opens = [];
    internal List<OwnedStream> Streams = [];
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? text = request.SystemId switch
        {
            "urn:demo:dtd" => "<?xml version='1.7' encoding='UTF-8'?><!ELEMENT r EMPTY><!ATTLIST r mode NMTOKENS ' alpha   beta '><!ENTITY item SYSTEM 'urn:demo:entity'>",
            "urn:demo:entity" => "<?xml version='1.00' encoding='UTF-8'?><p:item xmlns:p='urn:part'>A&amp;B</p:item>",
            _ => null
        };
        if (text is null) return null;
        Opens.Add(request.SystemId!);
        var stream = new OwnedStream(Encoding.UTF8.GetBytes(text)); Streams.Add(stream);
        return new(stream, new Uri(request.SystemId!));
    }
}
sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes, false)
{
    internal int Disposals;
    protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
}
