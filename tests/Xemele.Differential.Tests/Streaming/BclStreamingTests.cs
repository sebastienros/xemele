using System.Text;
using System.Text.Json;
using Xemele.Streaming;
using Xunit;
using BclReader = System.Xml.XmlReader;
namespace Xemele.Differential.Tests.Streaming;

public sealed class BclStreamingTests
{
    [Theory]
    [InlineData("<r a='a&#x9;b' xmlns:p='urn:p'><p:e/>α😀&amp;<![CDATA[x]]><!--c--><?pi data?></r>")]
    [InlineData("<!DOCTYPE r [<!ATTLIST r a CDATA 'v'><!ENTITY e 'text'>]><r>&e;</r>")]
    public void IndependentBclReaderAgreesOnSupportedContent(string xml)
    {
        var expected = new Tape();
        using (var reader = BclReader.Create(new StringReader(xml), new()
        {
            DtdProcessing = System.Xml.DtdProcessing.Parse,
            XmlResolver = null,
            IgnoreWhitespace = false
        }))
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case System.Xml.XmlNodeType.Element:
                        string name = reader.Name, uri = reader.NamespaceURI;
                        var empty = reader.IsEmptyElement;
                        List<string> attrs = [];
                        while (reader.MoveToNextAttribute())
                        {
                            attrs.Add(Tape.Row(reader.Name, reader.NamespaceURI, reader.Value));
                        }

                        reader.MoveToElement();
                        expected.Event("start", name, uri, attrs.ToArray());
                        if (empty)
                        {
                            expected.Event("end", name, uri);
                        }

                        break;
                    case System.Xml.XmlNodeType.EndElement:
                        expected.Event("end", reader.Name, reader.NamespaceURI);
                        break;
                    case System.Xml.XmlNodeType.Text:
                    case System.Xml.XmlNodeType.Whitespace:
                    case System.Xml.XmlNodeType.SignificantWhitespace:
                        expected.Text(reader.Value);
                        break;
                    case System.Xml.XmlNodeType.CDATA:
                        expected.Event("cdata");
                        expected.Text(reader.Value);
                        expected.Event("/cdata");
                        break;
                    case System.Xml.XmlNodeType.Comment:
                        expected.Event("comment", reader.Value);
                        break;
                    case System.Xml.XmlNodeType.ProcessingInstruction:
                        expected.Event("pi", reader.Name, reader.Value);
                        break;
                }
            }
        }
        expected.Flush();
        var actual = new Tape();
        XmlSaxParser.Parse(Encoding.UTF8.GetBytes(xml), actual, new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, TestContext.Current.CancellationToken);
        actual.Flush();
        Assert.Equal(expected.Events, actual.Events);
    }
    private sealed class Tape : XmlSaxHandler
    {
        public List<string> Events = [];
        private readonly StringBuilder text = new();
        public static string Row(params object[] values) => JsonSerializer.Serialize(values);
        public void Flush()
        {
            if (text.Length != 0)
            {
                Events.Add(Row("text", text.ToString()));
                text.Clear();
            }
        }
        public void Event(params object[] values)
        {
            Flush();
            Events.Add(Row(values));
        }
        public override void Text(ReadOnlySpan<char> value) => text.Append(value);
        public override void StartElement(XmlStartElement e)
        {
            List<string> attrs = [];
            foreach (var a in e.Attributes)
            {
                attrs.Add(Row(Name(a.Prefix, a.LocalName), a.NamespaceUri.ToString(), a.Value.ToString()));
            }

            foreach (var n in e.NamespaceDeclarations)
            {
                attrs.Add(Row(n.Prefix.IsEmpty ? "xmlns" : "xmlns:" + n.Prefix.ToString(), "http://www.w3.org/2000/xmlns/", n.NamespaceUri.ToString()));
            }

            Event("start", Name(e.Prefix, e.LocalName), e.NamespaceUri.ToString(), attrs.ToArray());
        }
        public override void EndElement(XmlEndElement e) => Event("end", Name(e.Prefix, e.LocalName), e.NamespaceUri.ToString());
        private static string Name(ReadOnlySpan<char> p, ReadOnlySpan<char> n) => p.IsEmpty ? n.ToString() : p.ToString() + ":" + n.ToString();
        public override void StartCData() => Event("cdata");
        public override void EndCData() => Event("/cdata");
        public override void Comment(ReadOnlySpan<char> value) => Event("comment", value.ToString());
        public override void ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> value) => Event("pi", target.ToString(), value.ToString());
    }
}
