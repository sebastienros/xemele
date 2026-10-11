using Xemele.Streaming;

using var parser = new XmlPushParser(new XmlSaxCallbacks
{
    OnStartElement = e => Console.WriteLine($"start {e.LocalName}"),
    OnText = text => Console.WriteLine($"text {text}"),
    OnEndElement = e => Console.WriteLine($"end {e.LocalName}"),
    OnEndDocument = () => Console.WriteLine("complete")
});
parser.Append("<root>hel"u8);
parser.Append("lo</root>"u8);
parser.Complete();
