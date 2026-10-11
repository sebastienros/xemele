using System.Text;
using Xemele;
using Xemele.Html;

var html = Html5Parser.Parse("<!doctype html><body><input disabled><template><?marker ?><p>é &amp; 尾</template><script>if(a<b)c()</script>");
var html5 = new HtmlSaveOptions { Mode = HtmlSerializationMode.Html5 };
var legacy = new HtmlSaveOptions { Mode = HtmlSerializationMode.Legacy };
const string expected = "<!DOCTYPE html><html><head></head><body><input disabled=\"\"><template><?marker ?><p>é &amp; 尾</p></template><script>if(a<b)c()</script></body></html>";
if (HtmlSerializer.Serialize(html, html5) != expected) throw new Exception("HTML5 document serialization.");
if (HtmlSerializer.Serialize(html.RootElement.Children[1].FirstChild!, html5) != "<input disabled=\"\">") throw new Exception("Outer node serialization.");
string native = HtmlSerializer.Serialize(html, legacy);
if (!native.StartsWith("<!DOCTYPE html>\n", StringComparison.Ordinal) || !native.Contains("<input disabled>", StringComparison.Ordinal) || !native.Contains("<?marker >", StringComparison.Ordinal) || !native.EndsWith("\n", StringComparison.Ordinal)) throw new Exception("Legacy explicit mode.");
using var stream = new MemoryStream(); HtmlSerializer.Save(html, stream, html5);
if (!stream.CanWrite || !stream.ToArray().AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected))) throw new Exception("Caller-owned UTF8 stream.");
var fragment = Html5Parser.ParseFragment("<tr><td>&lt;&amp;", new(new HtmlDocument().CreateElement("tbody")));
if (HtmlSerializer.Serialize(fragment) != "<tr><td>&lt;&amp;</td></tr>") throw new Exception("Detached fragment.");
var xml = XmlDocument.Parse("<x>&lt;&amp;</x>");
if (XmlSerializer.Serialize(xml, options: new() { OmitXmlDeclaration = true }) != "<x>&lt;&amp;</x>") throw new Exception("Earlier XML.");
Console.WriteLine(expected); Console.WriteLine("All three APIs, both modes, outer node/template/fragment, UTF8 caller stream and earlier XML passed.");
