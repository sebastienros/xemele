using System.Text;
using Xemele;
foreach (var (label, body, expected) in new[] {("US-ASCII",new byte[]{65},"A"),("ISO-8859-1",new byte[]{0xE9},"é"),("Windows-1252",new byte[]{0x80},"€")})
{
    byte[] xml=[..Encoding.ASCII.GetBytes($"<?xml version='1.0' encoding='{label}'?><r>"),..body,.."</r>"u8];
    var document=XmlDocument.Parse(xml);
    if(document.RootElement.GetTextContent()!=expected)throw new Exception("Incorrect legacy conversion");
    Console.WriteLine($"{label}: {document.RootElement.GetTextContent()}");
}
byte[] invalidBytes=[.."<?xml version='1.0' encoding='Windows-1252'?><r>"u8,0x81,.."</r>"u8];
if(XmlDocument.TryParse(invalidBytes.AsSpan(),out _,out var invalid))throw new Exception("Undefined byte accepted");
if(invalid[0].Code!="INPUT.InvalidEncoding")throw new Exception("Undefined codepage byte accepted");
Console.WriteLine(invalid[0].Code);
