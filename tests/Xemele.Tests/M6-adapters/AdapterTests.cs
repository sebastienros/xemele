using System.Text;
using System.Xml;
using System.Xml.XPath;
using Xemele.Interop;
using Xunit;
using Reader = Xemele.Streaming.XmlReader;

namespace Xemele.Tests.M6Adapters;

public sealed class AdapterTests
{
    private static Reader Create(string xml, XmlParseOptions? options = null) => Reader.Create(new MemoryStream(Encoding.UTF8.GetBytes(xml)), options, TestContext.Current.CancellationToken);
    [Fact]
    public void ReaderAttributesPositioningNamespaceOrderAndOwnership()
    {
        using var reader = Create("<r xmlns='urn:r' xmlns:p='urn:p' a='v' p:b='w'><p:c/></r>");
        using var adapter = new XemeleXmlReaderAdapter(reader);
        Assert.Equal(ReadState.Initial, adapter.ReadState);
        Assert.Equal(XmlNodeType.None, adapter.NodeType);
        Assert.False(adapter.EOF);
        Assert.True(adapter.Read());
        Assert.Equal("r", adapter.Name);
        Assert.Equal(0, adapter.Depth);
        Assert.Equal(4, adapter.AttributeCount);
        Assert.Equal("v", adapter.GetAttribute(0));
        Assert.Equal("w", adapter.GetAttribute("b", "urn:p"));
        Assert.Equal("urn:r", adapter.GetAttribute("xmlns"));
        Assert.Equal("urn:p", adapter.LookupNamespace("p"));
        Assert.Equal("http://www.w3.org/XML/1998/namespace", adapter.LookupNamespace("xml"));
        Assert.True(adapter.MoveToFirstAttribute());
        Assert.Equal("a", adapter.Name);
        Assert.Equal(1, adapter.Depth);
        Assert.True(adapter.HasValue);
        Assert.False(adapter.MoveToAttribute("missing"));
        Assert.Null(adapter.GetAttribute("missing"));
        Assert.Equal("a", adapter.Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.GetAttribute(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.MoveToAttribute(4));
        Assert.Equal("a", adapter.Name);
        Assert.True(adapter.ReadAttributeValue());
        Assert.Equal(XmlNodeType.Text, adapter.NodeType);
        Assert.Equal(2, adapter.Depth);
        Assert.Equal("v", adapter.Value);
        Assert.Equal("", adapter.Name);
        Assert.False(adapter.ReadAttributeValue());
        Assert.Equal(4, adapter.AttributeCount);
        Assert.True(adapter.MoveToNextAttribute());
        Assert.Equal("p:b", adapter.Name);
        Assert.True(adapter.MoveToElement());
        Assert.False(adapter.MoveToElement());
        Assert.True(adapter.MoveToAttribute("xmlns:p"));
        Assert.Equal("http://www.w3.org/2000/xmlns/", adapter.NamespaceURI);
        Assert.Equal("xmlns", adapter.Prefix);
        string ownedName = adapter.Name, ownedValue = adapter.Value;
        Assert.True(adapter.Read());
        Assert.Equal("p:c", adapter.Name);
        Assert.Equal(1, adapter.Depth);
        Assert.True(adapter.IsEmptyElement);
        Assert.Equal("xmlns:p", ownedName);
        Assert.Equal("urn:p", ownedValue);
        Assert.Same(adapter.Name, adapter.NameTable.Get("p:c"));
        Assert.True(adapter.Read());
        Assert.Equal(XmlNodeType.EndElement, adapter.NodeType);
        Assert.Equal("r", adapter.Name);
        Assert.Equal(0, adapter.Depth);
        Assert.False(adapter.Read());
        Assert.True(adapter.EOF);
        Assert.Equal(ReadState.EndOfFile, adapter.ReadState);
        Assert.Equal(0, adapter.AttributeCount);
        Assert.Equal("", adapter.Value);
        adapter.Close();
        adapter.Close();
        Assert.Equal(ReadState.Closed, adapter.ReadState);
        Assert.Equal(XmlNodeType.None, adapter.NodeType);
        Assert.False(adapter.Read());
        Assert.False(adapter.EOF);
        Assert.False(adapter.CanResolveEntity);
        Assert.Throws<InvalidOperationException>(() => adapter.ResolveEntity());
    }
    [Fact]
    public void ReaderAllLexicalKindsAndDtdInteriorAreHandled()
    {
        using var reader = Create("<?xml version='1.0' encoding='UTF-8' standalone='yes'?><!DOCTYPE r [<!ELEMENT r ANY><!ENTITY e 'ok'><!--hidden--><?hidden d?>]><r> <![CDATA[x]]><![CDATA[]]>&e;<!--c--><?pi d?><s xml:space='preserve'> </s></r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        });
        using var adapter = new XemeleXmlReaderAdapter(reader);
        var events = new List<(XmlNodeType Kind, string Name, string Value, int Depth)>();
        while (adapter.Read())
        {
            events.Add((adapter.NodeType, adapter.Name, adapter.Value, adapter.Depth));
        }

        Assert.Equal((XmlNodeType.XmlDeclaration, "xml", "version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"", 0), events[0]);
        Assert.Equal((XmlNodeType.DocumentType, "r", "", 0), events[1]);
        Assert.Contains((XmlNodeType.Whitespace, "", " ", 1), events);
        Assert.Contains((XmlNodeType.CDATA, "", "x", 1), events);
        Assert.Contains((XmlNodeType.CDATA, "", "", 1), events);
        Assert.Equal("ok", string.Concat(events.Where(e => e.Kind == XmlNodeType.Text).Select(e => e.Value)));
        Assert.Contains((XmlNodeType.Comment, "", "c", 1), events);
        Assert.Contains((XmlNodeType.ProcessingInstruction, "pi", "d", 1), events);
        Assert.Contains((XmlNodeType.SignificantWhitespace, "", " ", 2), events);
        Assert.DoesNotContain(events, e => e.Name == "hidden" || e.Value == "hidden");
    }
    [Fact]
    public void ReaderNamespaceScopeAndBaseAreRetainedUntilEndEvent()
    {
        using var reader = Create("<r xmlns:p='urn:a' xml:base='https://example.test/a/'><s xmlns:p='urn:b' xml:base='b/'><e xmlns:p='urn:c'/></s><t/></r>");
        using var adapter = new XemeleXmlReaderAdapter(reader);
        Assert.True(adapter.Read());
        Assert.Equal("https://example.test/a/", adapter.BaseURI);
        Assert.True(adapter.Read());
        Assert.Equal("urn:b", adapter.LookupNamespace("p"));
        Assert.Equal("https://example.test/a/b/", adapter.BaseURI);
        Assert.True(adapter.Read());
        Assert.Equal("urn:c", adapter.LookupNamespace("p"));
        Assert.True(adapter.Read());
        Assert.Equal("s", adapter.Name);
        Assert.Equal("urn:b", adapter.LookupNamespace("p"));
        Assert.True(adapter.Read());
        Assert.Equal("t", adapter.Name);
        Assert.Equal("urn:a", adapter.LookupNamespace("p"));
        Assert.Equal("https://example.test/a/", adapter.BaseURI);
    }
    [Fact]
    public void ReaderExternalSourceBaseTransitionsKeepAncestorXmlBase()
    {
        using var reader = Create("<!DOCTYPE r [<!ENTITY e SYSTEM 'https://example.test/requested.xml'>]><r xml:base='https://primary.test/'>&e;<s/></r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = new RedirectResolver()
        });
        using var adapter = new XemeleXmlReaderAdapter(reader);
        var elements = new List<(XmlNodeType Kind, string Name, string Base)>();
        while (adapter.Read())
        {
            if (adapter.NodeType is XmlNodeType.Element or XmlNodeType.EndElement)
            {
                elements.Add((adapter.NodeType, adapter.Name, adapter.BaseURI));
            }
        }

        Assert.Equal(new[] {
            (XmlNodeType.Element, "r", "https://primary.test/"),
            (XmlNodeType.Element, "a", "https://final.test/entities/sub/"),
            (XmlNodeType.Element, "b", "https://final.test/entities/sub/"),
            (XmlNodeType.EndElement, "a", "https://final.test/entities/sub/"),
            (XmlNodeType.Element, "s", "https://primary.test/"),
            (XmlNodeType.EndElement, "r", "https://primary.test/") }, elements);
    }
    private sealed class RedirectResolver : XmlResourceResolver
    {
        public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(new MemoryStream(Encoding.UTF8.GetBytes("<a xml:base='sub/'><b/></a>")), new Uri("https://final.test/entities/content.xml"));
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReaderLeaveOpenAlwaysPreservesCallerStream(bool leaveOpen)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<r/>"));
        using var reader = Reader.Create(stream, cancellationToken: TestContext.Current.CancellationToken);
        var adapter = new XemeleXmlReaderAdapter(reader, leaveOpen);
        adapter.Close();
        Assert.True(stream.CanRead);
        if (leaveOpen)
        {
            Assert.True(reader.Read());
        }
        else
        {
            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }
    }
    [Fact]
    public void ReaderErrorsPreserveOriginalFailureAndClearPosition()
    {
        using var reader = Create("<r><bad></r>");
        using var adapter = new XemeleXmlReaderAdapter(reader);
        Assert.True(adapter.Read());
        Assert.True(adapter.Read());
        Assert.Throws<XmlParseException>(() => { while (adapter.Read()) { } });
        Assert.Equal(ReadState.Error, adapter.ReadState);
        Assert.Equal(XmlNodeType.None, adapter.NodeType);
        Assert.Equal("", adapter.Name);
        Assert.Equal(0, adapter.AttributeCount);
        Assert.Throws<InvalidOperationException>(() => adapter.Read());
    }
    [Fact]
    public async Task ReaderAsyncUsesRealAsyncAndRejectsOverlapWithoutFaulting()
    {
        using var stream = new BlockedStream("<r/>");
        using var reader = Reader.Create(stream, cancellationToken: TestContext.Current.CancellationToken);
        using var adapter = new XemeleXmlReaderAdapter(reader);
        var pending = adapter.ReadAsync();
        await stream.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => adapter.Read());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ReadAsync());
        Assert.Throws<InvalidOperationException>(() => adapter.Close());
        stream.Release.TrySetResult();
        Assert.True(await pending);
        Assert.Equal(XmlNodeType.Element, adapter.NodeType);
        Assert.False(await adapter.ReadAsync());
        Assert.Equal(ReadState.EndOfFile, adapter.ReadState);
    }
    [Fact]
    public void NavigatorBclEvaluationSnapshotCloneIdentityAndCoalescedText()
    {
        var document = XmlDocument.Parse("<r xmlns='urn:r' xmlns:p='urn:p' a='v'>a<![CDATA[b]]><p:c xml:id='chosen'>c</p:c>d<!--e--><?pi f?></r>", cancellationToken: TestContext.Current.CancellationToken);
        var navigator = new XemeleXPathNavigatorAdapter(document);
        Assert.Equal(XPathNodeType.Root, navigator.NodeType);
        Assert.Equal("abcd", navigator.Value);
        Assert.False(navigator.CanEdit);
        var clone = navigator.Clone();
        Assert.True(navigator.IsSamePosition(clone));
        Assert.True(navigator.MoveToFirstChild());
        Assert.False(navigator.IsSamePosition(clone));
        Assert.Equal("r", navigator.Name);
        Assert.Equal("urn:r", navigator.NamespaceURI);
        Assert.True(clone.MoveTo(navigator));
        Assert.True(clone.IsSamePosition(navigator));
        Assert.True(navigator.MoveToFirstChild());
        Assert.Equal("ab", navigator.Value);
        Assert.Equal(XPathNodeType.Text, navigator.NodeType);
        Assert.False(navigator.MoveToFirstChild());
        Assert.True(navigator.MoveToNext());
        Assert.Equal("p:c", navigator.Name);
        Assert.Same(navigator.Name, navigator.NameTable.Get("p:c"));
        Assert.True(navigator.MoveToPrevious());
        Assert.Equal("ab", navigator.Value);
        var namespaces = new XmlNamespaceManager(navigator.NameTable);
        namespaces.AddNamespace("r", "urn:r");
        namespaces.AddNamespace("p", "urn:p");
        Assert.Equal(1d, navigator.Evaluate("count(/r:r/p:c)", namespaces));
        Assert.Equal("c", navigator.Evaluate("string(/r:r/p:c)", namespaces));
        Assert.True(navigator.MoveToId("chosen"));
        Assert.Equal("p:c", navigator.Name);
        Assert.False(navigator.MoveToId("no"));
        Assert.Equal("p:c", navigator.Name);
        document.RootElement.SetTextContent("changed");
        document.RootElement.SetAttribute("a", "changed");
        navigator.MoveToRoot();
        Assert.Equal("abcd", navigator.Value);
        Assert.True(navigator.MoveToFirstChild());
        Assert.True(navigator.MoveToFirstAttribute());
        Assert.Equal("v", navigator.Value);
        var unrelated = new XemeleXPathNavigatorAdapter(document);
        Assert.False(navigator.MoveTo(unrelated));
        Assert.False(navigator.IsSamePosition(unrelated));
        Assert.Throws<NotSupportedException>(() => navigator.SetValue("no"));
        Assert.Throws<NotSupportedException>(() => navigator.DeleteSelf());
        Assert.Throws<NotSupportedException>(() => navigator.AppendChild("<x/>"));
        Assert.True(navigator.MoveToParent());
        Assert.False(navigator.MoveToNextAttribute());
        Assert.True(navigator.MoveToParent());
        Assert.False(navigator.MoveToParent());
    }
    [Fact]
    public void NavigatorNamespaceScopesUndeclarationsAndBaseSnapshot()
    {
        var document = XmlDocument.Parse("<r xmlns='urn:r' xmlns:p='urn:p' xml:base='https://example.test/a/'><s xmlns='' xmlns:q='urn:q' xml:base='b/'/></r>", cancellationToken: TestContext.Current.CancellationToken);
        var navigator = new XemeleXPathNavigatorAdapter(document);
        navigator.MoveToFirstChild();
        navigator.MoveToFirstChild();
        Assert.Equal("https://example.test/a/b/", navigator.BaseURI);
        Assert.True(navigator.IsEmptyElement);
        var saved = navigator.Clone();
        Assert.True(navigator.MoveToFirstNamespace(XPathNamespaceScope.All));
        var all = new List<string>();
        do
        {
            all.Add(navigator.LocalName);
            Assert.Equal(XPathNodeType.Namespace, navigator.NodeType);
            Assert.False(navigator.MoveToNext());
        } while (navigator.MoveToNextNamespace(XPathNamespaceScope.All));
        Assert.Equal(new[] { "p", "q", "xml" }, all);
        Assert.True(navigator.MoveToParent());
        Assert.True(navigator.IsSamePosition(saved));
        Assert.True(navigator.MoveToFirstNamespace(XPathNamespaceScope.Local));
        Assert.Equal("q", navigator.Name);
        Assert.False(navigator.MoveToNextNamespace(XPathNamespaceScope.Local));
        navigator.MoveTo(saved);
        Assert.True(navigator.MoveToFirstNamespace(XPathNamespaceScope.ExcludeXml));
        Assert.Equal("p", navigator.Name);
        Assert.True(navigator.MoveToNextNamespace(XPathNamespaceScope.ExcludeXml));
        Assert.Equal("q", navigator.Name);
        Assert.False(navigator.MoveToNextNamespace(XPathNamespaceScope.ExcludeXml));
        Assert.Throws<ArgumentOutOfRangeException>(() => navigator.MoveToNextNamespace((XPathNamespaceScope)99));
    }
    [Fact]
    public void NavigatorUsesTypedIdsNotUntypedIdAndRejectsIncompleteInput()
    {
        var document = XmlDocument.Parse("<!DOCTYPE r [<!ELEMENT r (c)><!ELEMENT c EMPTY><!ATTLIST c key ID #REQUIRED>]><r><c key='typed' id='untyped'/></r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, TestContext.Current.CancellationToken);
        var navigator = new XemeleXPathNavigatorAdapter(document);
        Assert.True(navigator.MoveToId("typed"));
        Assert.False(navigator.MoveToId("untyped"));
        var incomplete = XmlDocument.ParseWithDiagnostics("<r>&unknown;</r>", new()
        {
            Recover = true
        }, TestContext.Current.CancellationToken);
        Assert.NotNull(incomplete.Value);
        Assert.Throws<ArgumentException>(() => new XemeleXPathNavigatorAdapter(incomplete.Value));
        Assert.Throws<ArgumentNullException>(() => new XemeleXPathNavigatorAdapter(null!));
        Assert.Throws<ArgumentNullException>(() => new XemeleXmlReaderAdapter(null!));
    }
    [Fact]
    public void FrozenCloneVectorKeepsOriginalAtRoot()
    {
        var navigator = new XemeleXPathNavigatorAdapter(XmlDocument.Parse("<r/>", cancellationToken: TestContext.Current.CancellationToken));
        var clone = navigator.Clone();
        Assert.True(clone.MoveToFirstChild());
        Assert.Equal(XPathNodeType.Root, navigator.NodeType);
        Assert.Equal(XPathNodeType.Element, clone.NodeType);
        Assert.False(clone.CanEdit);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4096)]
    public void ReaderChunkBoundariesPreserveUnicodeAndOwnedText(int chunk)
    {
        using var stream = new ChunkStream("<r>α😀β<![CDATA[根😀]]></r>", chunk);
        using var reader = Reader.Create(stream, cancellationToken: TestContext.Current.CancellationToken);
        using var adapter = new XemeleXmlReaderAdapter(reader);
        var text = new StringBuilder();
        var cdata = new StringBuilder();
        while (adapter.Read())
        {
            if (adapter.NodeType == XmlNodeType.Text)
            {
                text.Append(adapter.Value);
            }

            if (adapter.NodeType == XmlNodeType.CDATA)
            {
                cdata.Append(adapter.Value);
            }

            if (adapter.Value.Length != 0)
            {
                Assert.False(char.IsLowSurrogate(adapter.Value[0]));
                Assert.False(char.IsHighSurrogate(adapter.Value[^1]));
            }
        }
        Assert.Equal("α😀β", text.ToString());
        Assert.Equal("根😀", cdata.ToString());
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(4096, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4096, true)]
    public async Task LogicalTextClassificationMatchesBclAcrossChunks(int chunk, bool asynchronous)
    {
        string[] inputs = ["<r>a b</r>", "<r> a b </r>", "<r>   </r>", "<r xml:space='preserve'>   </r>", "<r xml:space='preserve'> a b </r>", "<r> a <![CDATA[ b ]]> c </r>", "<r> <s/> a </r>"];
        foreach (var xml in inputs)
        {
            var expected = new List<(XmlNodeType Kind, string Value, int Depth)>();
            using (var bcl = System.Xml.XmlReader.Create(new StringReader(xml)))
            {
                while (bcl.Read())
                {
                    expected.Add((bcl.NodeType, bcl.Value, bcl.Depth));
                }
            }

            using var stream = new ChunkStream(xml, chunk);
            using var reader = Reader.Create(stream, cancellationToken: TestContext.Current.CancellationToken);
            using var adapter = new XemeleXmlReaderAdapter(reader);
            var actual = new List<(XmlNodeType Kind, string Value, int Depth)>();
            while (asynchronous ? await adapter.ReadAsync() : adapter.Read())
            {
                actual.Add((adapter.NodeType, adapter.Value, adapter.Depth));
            }

            Assert.Equal(expected, actual);
        }
    }
    [Fact]
    public async Task ReaderCallerIoAndCancellationPropagateWithErrorState()
    {
        var failure = new IOException("caller failure");
        using var stream = new FailingStream(failure);
        using var reader = Reader.Create(stream, cancellationToken: TestContext.Current.CancellationToken);
        using var adapter = new XemeleXmlReaderAdapter(reader);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => adapter.ReadAsync()));
        Assert.Equal(ReadState.Error, adapter.ReadState);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        using var input = new MemoryStream("<r/>"u8.ToArray());
        using var cancelledReader = Reader.Create(input, cancellationToken: cancellation.Token);
        using var cancelled = new XemeleXmlReaderAdapter(cancelledReader);
        var error = Assert.Throws<OperationCanceledException>(() => cancelled.Read());
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(ReadState.Error, cancelled.ReadState);
    }
    private sealed class ChunkStream(string xml, int chunk) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(chunk, buffer.Length)]);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span));
        }
    }
    private sealed class FailingStream(IOException failure) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
    }
    private sealed class BlockedStream(string xml) : MemoryStream(Encoding.UTF8.GetBytes(xml))
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override int Read(Span<byte> buffer) => throw new InvalidOperationException("Synchronous input forbidden.");
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return base.Read(buffer.Span);
        }
    }
}
