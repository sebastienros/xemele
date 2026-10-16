using System.Text;
using Xemele.Streaming;
using var input = new MemoryStream(Encoding.UTF8.GetBytes("<r xmlns='urn:r' a='v'>hello<c/></r>"));
using var reader = XmlReader.Create(input);
int structural = 0;
var text = new StringBuilder();
while (reader.Read())
{
    Console.WriteLine($"{reader.TokenKind}: {reader.LocalName.ToString()}{reader.ValueSpan.ToString()}");
    if (reader.TokenKind == XmlTokenKind.StartElement)
        foreach (var attribute in reader.Attributes)
            Console.WriteLine($"  {attribute.LocalName.ToString()}={attribute.Value.ToString()}");
    if (reader.TokenKind == XmlTokenKind.Text)
        text.Append(reader.ValueSpan);
    else
        structural++;
}
if (structural != 6 || text.ToString() != "hello" || !input.CanRead || reader.TokenKind != XmlTokenKind.None) throw new Exception("Reader sample invariant failed.");
