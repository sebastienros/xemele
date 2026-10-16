using System.Reflection;
using System.Text;
using Xemele;
var resolver = new Resolver();
var doc = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'https://fixture.invalid/request/ext.dtd'><r>&e;</r>", new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
if (doc.RootElement.LocalName != "r" || doc.RootElement.GetTextContent() != "right!" || doc.RootElement.GetAttribute("v") != "ok" || doc.DocumentType!.Name != "r" || resolver.Requests != 3 || resolver.Disposals != 3) throw new Exception("independent document/default/resource oracle");
Console.WriteLine("Nonvalidating sample passed: text=right!;default=ok;requests=3;disposed=3;product=" + typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
sealed class Resolver : XmlResourceResolver
{
    private static readonly Uri Parent = new("https://fixture.invalid/final/ext.dtd");
    internal int Requests, Disposals;
    public override XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        string body;
        Uri final;
        if (r.Uri.AbsoluteUri == "https://fixture.invalid/request/ext.dtd")
        {
            if (r.Referrer is not null || r.Kind != XmlResourceKind.ExternalSubset || r.SystemId != r.Uri.AbsoluteUri || r.PublicId is not null)
                throw new Exception("subset request");
            body = "<!ENTITY % p SYSTEM '../payload/close.dtd'><!ENTITY % mode 'INCLUDE['><!ELEMENT r (#PCDATA)><![%mode;<!ATTLIST r v CDATA 'ok'>]]><!ENTITY e %p;";
            final = Parent;
        }
        else if (r.Uri.AbsoluteUri == "https://fixture.invalid/payload/close.dtd")
        {
            if (r.Referrer != Parent || r.Kind != XmlResourceKind.ExternalEntity || r.SystemId != "../payload/close.dtd" || r.PublicId is not null)
                throw new Exception("PE request");
            body = "SYSTEM 'body.ent'>";
            final = new("https://fixture.invalid/actual/close.dtd");
        }
        else
        {
            if (r.Uri.AbsoluteUri != "https://fixture.invalid/final/body.ent" || r.Referrer != Parent || r.Kind != XmlResourceKind.ExternalEntity || r.SystemId != "body.ent" || r.PublicId is not null)
                throw new Exception("opener base request");
            body = "right!";
            final = new("https://fixture.invalid/content/body.ent");
        }
        return new(new OwnedStream(this, Encoding.UTF8.GetBytes(body)), final);
    }
    private sealed class OwnedStream(Resolver owner, byte[] bytes) : MemoryStream(bytes, false)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                owner.Disposals++;
            base.Dispose(disposing);
        }
    }
}
