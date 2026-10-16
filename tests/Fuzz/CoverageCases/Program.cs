using System.Text;
using Xemele;

// Fixed named grammar cases, not generated mutations or a fuzz entry point.
string[] rejected =
[
    "<!DOCTYPEr><r/>", "<!DOCTYPE 1bad><r/>", "<!DOCTYPE r WRONG 'x'><r/>",
    "<!DOCTYPE r SYSTEM><r/>", "<!DOCTYPE r SYSTEM unquoted><r/>",
    "<!DOCTYPE r PUBLIC 'abc'><r/>", "<!DOCTYPE r PUBLIC 'abc' unquoted><r/>",
    "<!DOCTYPE r PUBLIC 'a^b' 'urn:empty'><r/>",
    "<!DOCTYPE r PUBLIC 'a\tb' 'urn:empty'><r/>",
    "<!DOCTYPE r SYSTEM 'urn:empty' junk><r/>",
    "<!DOCTYPE r PUBLIC 'unfinished><r/>", "<!DOCTYPE r SYSTEM 'unfinished><r/>"
];
var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = new EmptyDtd() };
int checks = 0;
foreach (string xml in rejected)
{
    foreach (bool bytes in new[] { false, true })
    {
        var result = bytes ? XmlDocument.ParseWithDiagnostics(Encoding.UTF8.GetBytes(xml), options) : XmlDocument.ParseWithDiagnostics(xml, options);
        if (result.IsComplete || !result.Diagnostics.Any(d => d.Code == "XML.Syntax"))
            throw new InvalidDataException("Malformed DOCTYPE grammar not rejected: " + xml);
        checks++;
    }
}
string publicId = "  AZ az 09\r\n -'()+,./:=?;!*#@$_%  ";
string expected = "AZ az 09 -'()+,./:=?;!*#@$_%";
foreach (string value in new[] { publicId, "   ", "single", "a\rb\nc\r\nd" })
{
    string xml = "<!DOCTYPE r PUBLIC \"" + value + "\" 'urn:empty'><r/>";
    foreach (bool bytes in new[] { false, true })
    {
        var document = bytes ? XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), options) : XmlDocument.Parse(xml, options);
        string normalized = value == publicId ? expected : value == "   " ? "" : value == "single" ? "single" : "a b c d";
        if (document.DocumentType?.PublicId != normalized || document.DocumentType.SystemId != "urn:empty" || document.RootElement.LocalName != "r")
            throw new InvalidDataException("PUBLIC identifier normalization differs.");
        checks++;
    }
}
Console.WriteLine("PASS " + checks + " fixed character/byte DOCTYPE rejection and public-identifier normalization cases");
sealed class EmptyDtd : XmlResourceResolver
{
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Uri != new Uri("urn:empty"))
            throw new InvalidDataException("Unexpected resource request.");
        return new(new MemoryStream([], writable: false), request.Uri);
    }
}
