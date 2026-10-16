using System.Globalization;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using BenchmarkDotNet.Attributes;
using Xemele.XPath;
using BclDocument = System.Xml.XmlDocument;
using XDocument = Xemele.XmlDocument;
using XExpression = Xemele.XPath.XPathExpression;

namespace XmlComparison;

internal static class Fixture
{
    public static readonly string Xml = "<catalog>" + string.Concat(
        Enumerable.Range(1, 128).Select(i =>
            $"<item id=\"{i}\" price=\"{i}\"><title>Book {i} &amp; XML</title></item>")) + "</catalog>";
    public const string Query = "sum(/catalog/item/@price)";
    public const double Sum = 8256;
    public static readonly byte[] Bytes = Encoding.UTF8.GetBytes(Xml);

    public static System.Xml.XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = System.Xml.DtdProcessing.Prohibit,
        XmlResolver = null
    };

    public static BclDocument ParseBcl()
    {
        var document = new BclDocument { PreserveWhitespace = true, XmlResolver = null };
        using var input = new StringReader(Xml);
        using var reader = System.Xml.XmlReader.Create(input, ReaderSettings());
        document.Load(reader);
        return document;
    }

    public static System.Xml.XPath.XPathExpression CompileBcl(XPathNavigator navigator) =>
        navigator.Compile(Query);
}

[MemoryDiagnoser, ShortRunJob]
public class DomBenchmarks
{
    private BclDocument bcl = null!;
    private System.Xml.Linq.XDocument linq = null!;
    private XDocument xemele = null!;
    private XPathNavigator navigator = null!;
    private XPathNavigator linqNavigator = null!;
    private System.Xml.XPath.XPathExpression bclQuery = null!;
    private System.Xml.XPath.XPathExpression linqQuery = null!;
    private XExpression query = null!;

    [GlobalSetup]
    public void Setup()
    {
        bcl = ParseXmlDocument();
        linq = ParseXDocument();
        xemele = ParseXemele();
        navigator = bcl.CreateNavigator()!;
        linqNavigator = linq.CreateNavigator();
        bclQuery = Fixture.CompileBcl(navigator);
        linqQuery = Fixture.CompileBcl(linqNavigator);
        query = XExpression.Compile(Fixture.Query);
        if (SerializeXmlDocument() != Fixture.Xml || SerializeXDocument() != Fixture.Xml ||
            SerializeXemele() != Fixture.Xml ||
            XPathXmlDocument() != Fixture.Sum || XPathXDocument() != Fixture.Sum || XPathXemele() != Fixture.Sum)
        {
            throw new InvalidOperationException("XML output or compiled XPath workload differs.");
        }

        string[] expected = bcl.DocumentElement!.ChildNodes.Cast<System.Xml.XmlElement>()
            .Select(n => $"{n.GetAttribute("id")}:{n.GetAttribute("price")}:{n.InnerText}").ToArray();
        var actual = new List<string>();
        foreach (var node in xemele.RootElement.Children)
            actual.Add($"{node.GetAttribute("id")}:{node.GetAttribute("price")}:{node.GetTextContent()}");
        string[] fromLinq = linq.Root!.Elements()
            .Select(n => $"{n.Attribute("id")!.Value}:{n.Attribute("price")!.Value}:{n.Value}").ToArray();
        if (!expected.SequenceEqual(actual) || !expected.SequenceEqual(fromLinq))
        {
            throw new InvalidOperationException("Parsed ordered XML values differ.");
        }
        Console.WriteLine($"Fixture: {Fixture.Xml.Length.ToString(CultureInfo.InvariantCulture)} characters, 128 items.");
    }

    [Benchmark] public BclDocument ParseXmlDocument() => Fixture.ParseBcl();
    [Benchmark] public System.Xml.Linq.XDocument ParseXDocument() =>
        System.Xml.Linq.XDocument.Parse(Fixture.Xml, LoadOptions.PreserveWhitespace);
    [Benchmark] public XDocument ParseXemele() => XDocument.Parse(Fixture.Xml);
    [Benchmark] public string SerializeXmlDocument() => bcl.OuterXml;
    [Benchmark] public string SerializeXDocument() => linq.ToString(SaveOptions.DisableFormatting);
    [Benchmark] public string SerializeXemele() => xemele.ToXmlString(new() { OmitXmlDeclaration = true });
    [Benchmark] public double XPathXmlDocument() => (double)navigator.Evaluate(bclQuery);
    [Benchmark] public double XPathXDocument() => (double)linqNavigator.Evaluate(linqQuery);
    [Benchmark] public double XPathXemele() => query.Evaluate(xemele.RootElement).AsNumber();
}

[MemoryDiagnoser, ShortRunJob]
public class ReaderBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        if (BclReader() != 257 || XemeleReader() != 257)
        {
            throw new InvalidOperationException("Reader element counts differ.");
        }
    }

    [Benchmark]
    public int BclReader()
    {
        using var input = new MemoryStream(Fixture.Bytes, false);
        using var reader = System.Xml.XmlReader.Create(input, Fixture.ReaderSettings());
        int count = 0;
        while (reader.Read())
            if (reader.NodeType == System.Xml.XmlNodeType.Element)
                count++;
        return count;
    }

    [Benchmark]
    public int XemeleReader()
    {
        using var input = new MemoryStream(Fixture.Bytes, false);
        using var reader = Xemele.Streaming.XmlReader.Create(input);
        int count = 0;
        while (reader.Read())
            if (reader.TokenKind == Xemele.Streaming.XmlTokenKind.StartElement)
                count++;
        return count;
    }
}
