using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.Dom;

public sealed class PersistenceTests
{
    [Theory]
    [InlineData("<xmlns/>", "", "", 0)]
    [InlineData("<xmlns xmlns='urn:d'/>", "urn:d", "", 0)]
    [InlineData("<p:xmlns xmlns:p='u'/>", "u", "p", 0)]
    [InlineData("<xmlns xmlns:p='u' p:xmlns='v'/>", "", "", 1)]
    public void LegalXmlnsLocalNamesRemainOrdinaryNamesOverAllTransports(string source, string uri, string prefix, int attributes)
    {
        void Check(XmlDocument document)
        {
            var root = document.RootElement;
            Assert.Equal("xmlns", root.LocalName);
            Assert.Equal(uri, root.NamespaceUri);
            Assert.Equal(prefix, root.Prefix);
            Assert.Equal(attributes, root.Attributes.Count);
            if (attributes != 0)
            {
                var attribute = root.Attributes[0];
                Assert.Equal("xmlns", attribute.LocalName);
                Assert.Equal("u", attribute.NamespaceUri);
                Assert.Equal("p", attribute.Prefix);
                Assert.Equal("v", attribute.Value);
            }
            // The test-only BCL oracle confirms the same namespace expansion.
            var expected = System.Xml.Linq.XElement.Parse(source);
            Assert.Equal(expected.Name.LocalName, root.LocalName);
            Assert.Equal(expected.Name.NamespaceName, root.NamespaceUri);
        }
        Assert.True(XmlDocument.TryParse(source, out var document, out var diagnostics, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(diagnostics);
        Check(document!);
        var bytes = Encoding.UTF8.GetBytes(source);
        Assert.True(XmlDocument.TryParse(bytes.AsSpan(), out document, out diagnostics, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(diagnostics);
        Check(document!);
        for (var size = 1; size <= bytes.Length + 1; size++)
        {
            using var stream = new DomTests.ChunkStream(bytes, size);
            Check(XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(stream.WasDisposed);
        }
    }

    [Fact]
    public void ImplicitXmlUriPersistenceQuotaRejectsStructurallyAndOwnsDiagnostics()
    {
        const string source = "<r xml:lang='en'/>";
        var options = new XmlParseOptions { Limits = new() { InternedNames = 3 } };
        Assert.False(XmlDocument.TryParse(source, out var document, out var diagnostics, options, TestContext.Current.CancellationToken));
        Assert.Null(document);
        Assert.Equal("XML.ResourceLimit", Assert.Single(diagnostics).Code);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)diagnostics)[0] = new("test", XmlDiagnosticSeverity.Error, "test", default));
        var exception = Assert.Throws<XmlParseException>(() => XmlDocument.Parse(source, options, TestContext.Current.CancellationToken));
        Assert.Equal(diagnostics[0], Assert.Single(exception.Diagnostics));
        document = XmlDocument.Parse(source, options with
        {
            Limits = options.Limits with
            {
                InternedNames = 4
            }
        }, TestContext.Current.CancellationToken);
        Assert.Equal("en", document.RootElement.GetAttribute("lang", "http://www.w3.org/XML/1998/namespace"));
    }
    private static (string Source, string Value) Fixture(string shape)
    {
        string value = new('x', 32768);
        var source = shape switch
        {
            "Text" => "<r>" + value + "</r>",
            "CData" => "<r><![CDATA[" + value + "]]></r>",
            "Comment" => "<r><!--" + value + "--></r>",
            "PI" => "<r><?p " + value + "?></r>",
            "Name" => "<" + value + "/>",
            "Attribute" => "<r a='" + value + "'/>",
            _ => "<r xmlns:p='" + value + "'/>"
        };
        return (source, value);
    }
    private static void Check(XmlDocument document, string shape, string value)
    {
        var root = document.RootElement;
        if (shape == "Name")
        {
            Assert.Equal(value, root.LocalName);
            return;
        }
        Assert.Equal("r", root.LocalName);
        if (shape == "Attribute")
        {
            Assert.Equal(value, root.GetAttribute("a"));
            Assert.Equal(1, root.Attributes.Count);
            return;
        }
        if (shape == "Namespace")
        {
            Assert.Equal(value, root.NamespaceDeclarations[0].Value);
            Assert.Equal("p", root.NamespaceDeclarations[0].Prefix);
            return;
        }
        Assert.Equal(1, root.Children.Count);
        Assert.Equal(value, root.FirstChild.Value);
        Assert.Equal(shape switch
        {
            "Text" => XmlNodeKind.Text,
            "CData" => XmlNodeKind.CData,
            "Comment" => XmlNodeKind.Comment,
            _ => XmlNodeKind.ProcessingInstruction
        }, root.FirstChild.Kind);
        if (shape == "PI")
        {
            Assert.Equal("p", root.FirstChild.LocalName);
        }
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("CData")]
    [InlineData("Comment")]
    [InlineData("PI")]
    [InlineData("Name")]
    [InlineData("Attribute")]
    [InlineData("Namespace")]
    public void LargeValidatedValuesPersistExactlyAndOwnTheirLifetime(string shape)
    {
        var (source, value) = Fixture(shape);
        var input = source.ToCharArray();
        var document = XmlDocument.Parse(input.AsSpan(), cancellationToken: TestContext.Current.CancellationToken);
        input.AsSpan().Fill('!');
        Check(document, shape, value);
        var bytes = Encoding.UTF8.GetBytes(source);
        document = XmlDocument.Parse(bytes.AsSpan(), cancellationToken: TestContext.Current.CancellationToken);
        bytes.AsSpan().Fill(0xFF);
        Check(document, shape, value);
        using var stream = new DomTests.ChunkStream(Encoding.UTF8.GetBytes(source), 31);
        Check(XmlDocument.Load(stream, cancellationToken: TestContext.Current.CancellationToken), shape, value);
        Assert.False(stream.WasDisposed);
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("CData")]
    [InlineData("Comment")]
    [InlineData("PI")]
    [InlineData("Name")]
    [InlineData("Attribute")]
    [InlineData("Namespace")]
    public void ActualSinkChecksOperationTokenAtValidatedPersistenceBoundaries(string shape)
    {
        var (source, _) = Fixture(shape);
        using var operation = new CancellationTokenSource();
        var dom = new DomSink(new(), cancellationToken: operation.Token);
        // The core validates every supplied callback view. A distinct core token
        // allows this deterministic wrapper to isolate the sink's retained token:
        // cancel immediately before the exact valid persistence callback.
        using var core = new XmlParserCore(new());
        var sink = new BoundarySink(dom, operation, shape);
        var exception = Assert.Throws<OperationCanceledException>(() => core.Append(source, [], true, sink, TestContext.Current.CancellationToken));
        Assert.Equal(operation.Token, exception.CancellationToken);
        Assert.Equal(1, sink.Cancellations);
        if (shape == "Name")
        {
            Assert.True(dom.Document.RootElement.IsDefault);
        }
        else
        {
            Assert.False(dom.Document.RootElement.IsDefault);
            Assert.Equal(0, dom.Document.RootElement.Children.Count);
            Assert.Equal(0, dom.Document.RootElement.Attributes.Count);
            Assert.Equal(0, dom.Document.RootElement.NamespaceDeclarations.Count);
        }
        Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, TestContext.Current.CancellationToken));
    }

    private sealed class BoundarySink(DomSink dom, CancellationTokenSource operation, string shape) : IXmlEventSink
    {
        internal int Cancellations;
        private void Cancel(string boundary)
        {
            if (boundary == shape)
            {
                Cancellations++;
                operation.Cancel();
            }
        }
        public bool StartDocument(XmlLocation location) => dom.StartDocument(location);
        public bool Declaration(XmlDeclarationView declaration) => dom.Declaration(declaration);
        public bool StartElement(XmlNameView name, bool empty, XmlLocation location)
        {
            Cancel("Name");
            return dom.StartElement(name, empty, location);
        }
        public bool NamespaceDeclaration(XmlNamespaceView declaration)
        {
            Cancel("Namespace");
            return dom.NamespaceDeclaration(declaration);
        }
        public bool Attribute(XmlAttributeView attribute)
        {
            Cancel("Attribute");
            return dom.Attribute(attribute);
        }
        public bool StartElementContent() => dom.StartElementContent();
        public bool EndElement(XmlNameView name, XmlLocation location) => dom.EndElement(name, location);
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => dom.StartText(kind, target, location);
        public bool TextFragment(ReadOnlySpan<char> fragment, bool ended, XmlLocation location)
        {
            if (ended && shape is "Text" or "CData" or "Comment" or "PI")
            {
                Cancel(shape);
            }

            return dom.TextFragment(fragment, ended, location);
        }
        public bool EndDocument(XmlLocation location) => dom.EndDocument(location);
    }
}
