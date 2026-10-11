using Xemele.Streaming;
// Callback infrastructure is usable independently of later parser transports.
var handler = new XmlSaxCallbacks
{
    OnStartDocument = () => Console.WriteLine("start"),
    OnText = text => Console.WriteLine(text.ToString()),
    OnEndDocument = () => Console.WriteLine("end")
};
IXmlSaxHandler sink = handler;
sink.StartDocument(); sink.Text("borrowed text Ω"); sink.EndDocument();
sink.Comment("optional callback");
var subclass = new TextHandler(); subclass.Text("subclass text");
sealed class TextHandler : XmlSaxHandler
{
    public override void Text(ReadOnlySpan<char> text) => Console.WriteLine(text.ToString());
}
