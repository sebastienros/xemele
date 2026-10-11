using System.Runtime.InteropServices;
using Xemele;
using Xemele.Validation;

var source = new Uri("https://fixture.invalid/explicit.xml");
using var input = new MemoryStream("<r/>"u8.ToArray());
var document = XmlDocument.Load(input, null, source, CancellationToken.None);
var schema = DtdSchema.Compile("<!ELEMENT r EMPTY>", null, null, source, CancellationToken.None);
if (!schema.Validate(document).IsValid || !input.CanRead) throw new InvalidOperationException("Source-aware sample failed.");
Console.WriteLine(RuntimeInformation.FrameworkDescription);
Console.WriteLine("Source-aware DOM and named DTD passed; caller stream remains open.");
