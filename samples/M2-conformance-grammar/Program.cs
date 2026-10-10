using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele;

// Assessment probe, not an acceptance test: no fabricated replacement value.
string pe = "<!DOCTYPE r [<!ENTITY % p ''>%p;]>";
string[] inputs = [
    "<r a='&missing;'/>",
    "<!DOCTYPE r [<!ENTITY % p ''>]><r a='&missing;'/>",
    pe + "<r a='&missing;'/>",
    "<?xml version='1.0' standalone='yes'?>" + pe + "<r a='&missing;'/>",
    "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA '&missing;'>]><r/>",
    "<!DOCTYPE r SYSTEM 'urn:subset'><r a='&missing;'/>",
    "<!DOCTYPE r SYSTEM 'urn:subset'><r/>"
];
for (int i = 0; i < inputs.Length; i++)
{
    string input = inputs[i];
    bool success = XmlDocument.TryParse(input, out var document, out var errors,
        new() { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new Offline() });
    Console.WriteLine(JsonSerializer.Serialize(new {
        id = "unknown-attribute-" + i,
        inputSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))),
        classification = i is 0 or 1 or 3 ? "EntityDeclared-WFC" : "EntityDeclared-VC-only; representation-required",
        observed = success ? "accepted" : "rejected", diagnostics = errors.Select(e => e.Code).ToArray(),
        build = typeof(XmlDocument).Assembly.GetCustomAttributesData().First(a => a.AttributeType.Name == "AssemblyInformationalVersionAttribute").ConstructorArguments[0].Value
    }));
}
sealed class Offline : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Uri != new Uri("urn:subset")) throw new IOException("Ungranted resource");
        return new(new MemoryStream(Encoding.UTF8.GetBytes("<!ATTLIST r a CDATA '&missing;'>"), false), request.Uri);
    }
}
