using System.Text;
using Xemele;
var resolver = new Resolver();
var document = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>",
    new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver });
if (document.RootElement.FirstChild.LocalName != "foo" || resolver.Stream!.Disposals != 1)
    throw new Exception("Existing external source composition/ownership changed.");
Console.WriteLine("Actual source/package external version1.1 composition and disposal passed.");
sealed class Resolver : XmlResourceResolver
{
    internal OwnedStream? Stream;
    public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream = new(Encoding.UTF8.GetBytes("<?xml version='1.1' encoding='UTF-8'?><foo/>"));
        return new(Stream, new Uri("urn:final"));
    }
}
sealed class OwnedStream(byte[] bytes) : MemoryStream(bytes, false)
{
    internal int Disposals;
    protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
}
