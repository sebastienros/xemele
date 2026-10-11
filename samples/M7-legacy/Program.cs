using Xemele;
using Xemele.Html;
using System.Runtime.InteropServices;
if(RuntimeInformation.FrameworkDescription!=".NET 10.0.11")throw new Exception("Pinned actual runtime required.");
var legacy=HtmlParser.Parse("<ul><li>one<li>two</ul>");if(legacy.RootElement.GetTextContent()!="onetwo")throw new Exception("Legacy recovery");
var encoded=HtmlParser.Parse("<meta charset=utf-8><p>é"u8);if(encoded.RootElement.GetTextContent()!="é")throw new Exception("Legacy charset");
using var stream=new MemoryStream("<p>&copy;"u8.ToArray());var loaded=HtmlParser.Load(stream);if(loaded.RootElement.GetTextContent()!="©"||!stream.CanRead)throw new Exception("Stream ownership");
var xml=new XmlDocument();xml.DocumentNode.AppendChild(xml.CreateElement("root"));if(xml.RootElement.LocalName!="root")throw new Exception("Earlier XML behavior");
Console.WriteLine("Legacy recovery, byte charset, incremental stream ownership and earlier XML passed; "+RuntimeInformation.FrameworkDescription);
