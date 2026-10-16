using System.Reflection;
using System.Text;
using Xemele;

var resolver = new OfflineResolver();
const string source = "<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd' [<!ENTITY % x \"<!ENTITY e SYSTEM 'body.ent'>\"><!ENTITY % y '&#37;x;'>]><r>&e;</r>";
var document = XmlDocument.Parse(source, new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
if (document.RootElement.LocalName != "r" || document.RootElement.GetTextContent() != "right!" || document.DocumentType!.Name != "r")
    throw new Exception("Independent sample document/doctype oracle failed.");
if (resolver.Requests != 2 || resolver.Disposals != 2) throw new Exception("Exactly two resource opens/cleanup expected.");
Console.WriteLine("DTD base sample passed: text=right!;requests=2;disposed=2;product=" + typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);

sealed class OfflineResolver : XmlResourceResolver
{
    internal int Requests, Disposals;
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        string body;
        Uri final;
        if (request.Uri.AbsoluteUri == "https://fixture.invalid/request/ext.dtd")
        {
            if (request.Kind != XmlResourceKind.ExternalSubset || request.SystemId != "https://fixture.invalid/request/ext.dtd" || request.Referrer is not null || request.PublicId is not null)
                throw new Exception("Exact subset request oracle failed.");
            final = new("https://fixture.invalid/final/ext.dtd");
            body = "<?xml encoding='UTF-8'?>%y;";
        }
        else
        {
            if (request.Uri.AbsoluteUri != "https://fixture.invalid/final/body.ent" || request.Referrer?.AbsoluteUri != "https://fixture.invalid/final/ext.dtd" || request.Kind != XmlResourceKind.ExternalEntity || request.SystemId != "body.ent" || request.PublicId is not null)
                throw new Exception("Exact declaration-base request oracle failed.");
            final = new("https://fixture.invalid/content/body.ent");
            body = "right!";
        }
        return new(new OwnedStream(this, Encoding.UTF8.GetBytes(body)), final);
    }
    private sealed class OwnedStream(OfflineResolver owner, byte[] bytes) : MemoryStream(bytes, false)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                owner.Disposals++;
            base.Dispose(disposing);
        }
    }
}
