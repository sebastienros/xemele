using System.Text;
using Xemele;
using Xemele.Streaming;
using Reader = Xemele.Streaming.XmlReader;

using var input = new MemoryStream(Encoding.UTF8.GetBytes("<r>async</r>"));
await XmlSaxParser.ParseAsync(input, new XmlSaxCallbacks { OnText = text => Console.WriteLine(text.ToString()) });
input.Position = 0;
await using (var reader = Reader.Create(input))
    while (await reader.ReadAsync())
        Console.WriteLine(reader.TokenKind);
input.Position = 0;
var document = await XmlDocument.LoadAsync(input);
Console.WriteLine(document.RootElement.GetTextContent());
input.Position = 0;
var result = await XmlDocument.LoadWithDiagnosticsAsync(input);
if (!result.IsWellFormed) throw new InvalidOperationException("Sample input must be well formed.");
input.Position = 0;
var completion = await XmlSaxParser.ParseWithDiagnosticsAsync(input, new XmlSaxCallbacks());
if (!completion.IsComplete || !input.CanRead) throw new InvalidOperationException("Input must remain open.");
