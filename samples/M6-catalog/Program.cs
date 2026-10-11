using System.Text;
using Xemele;
var catalog = XmlCatalog.Parse("<catalog xmlns='urn:oasis:names:tc:entity:xmlns:xml:catalog'><system systemId='public.xml' uri='https://offline/example.xml'/></catalog>");
var request = new XmlResourceRequest(new("https://upstream/example.xml"), null, XmlResourceKind.ExternalEntity) { SystemId = "public.xml" };
Console.WriteLine(catalog.Resolve(request)?.AbsoluteUri ?? throw new Exception("Missing mapping"));
var composed = new CatalogResolver(catalog, new OfflineResolver());
using var resource = composed.Resolve(request, default) ?? throw new Exception("Offline refusal");
Console.WriteLine(XmlDocument.Load(resource.Content).ToXmlString());
sealed class OfflineResolver : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.Uri.AbsoluteUri == "https://offline/example.xml" ? new(new MemoryStream(Encoding.UTF8.GetBytes("<offline/>")), new("https://final/example.xml")) : null;
    }
}
