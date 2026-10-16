using System.Globalization;
using System.Reflection;
using System.Text;
using Xemele.Serialization;
using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M5C14n;

public sealed class CanonicalizationTests
{
    private static XmlDocument Parse(string xml, XmlParseOptions? options = null) => XmlDocument.Parse(xml, options, TestContext.Current.CancellationToken);
    private static IReadOnlyList<XPathItem> Select(XmlDocument d, string expression) => XPathExpression.Compile(expression, cancellationToken: TestContext.Current.CancellationToken).Evaluate(d.DocumentNode, new() { CancellationToken = TestContext.Current.CancellationToken }).AsNodeSet();
    private static XmlCanonicalizationOptions Options(int mode, bool comments = false) => new() { Mode = (XmlCanonicalizationMode)mode, IncludeComments = comments, CancellationToken = TestContext.Current.CancellationToken };
    private static string Canonical(XmlDocument d, XmlCanonicalizationOptions? options = null)
    {
        using var stream = new MemoryStream();
        XmlCanonicalizer.Canonicalize(d, stream, options);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static string Canonical(IReadOnlyList<XPathItem> nodes, XmlCanonicalizationOptions? options = null)
    {
        using var stream = new MemoryStream();
        XmlCanonicalizer.Canonicalize(nodes, stream, options);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static IEnumerable<object[]> Modes => from mode in Enumerable.Range(0, 3) from comments in new[] { false, true } select new object[] { mode, comments };
    [Theory, MemberData(nameof(Modes))]
    public void WholeDocumentHasExactEscapingUtf8AndOuterRootLineFeeds(int mode, bool comments)
    {
        var d = Parse("<?pre p?><!--before--><r z='2' a='&quot;&amp;&lt;&#x9;&#xA;&#xD;'>x&lt;&amp;&gt;&#xD;根😀<![CDATA[<>]]><!--inside--><?go?></r><?post?><!--after-->");
        var expected = "<?pre p?>\n" + (comments ? "<!--before-->\n" : "") + "<r a=\"&quot;&amp;&lt;&#x9;&#xA;&#xD;\" z=\"2\">x&lt;&amp;&gt;&#xD;根😀&lt;&gt;" + (comments ? "<!--inside-->" : "") + "<?go?></r>\n<?post?>" + (comments ? "\n<!--after-->" : "");
        using var stream = new MemoryStream();
        XmlCanonicalizer.Canonicalize(d, stream, Options(mode, comments));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), stream.ToArray());
        Assert.Equal(expected, Canonical(Select(d, "//node() | //@* | //namespace::*"), Options(mode, comments)));
    }
    [Theory, MemberData(nameof(Modes))]
    public void NamespaceAndExpandedNameSortingSuppressesRedundantBindings(int mode, bool comments)
    {
        var d = Parse("<r xmlns='urn:d' xmlns:z='urn:z' xmlns:a='urn:a' z:b='Z' a:b='A' b='B'><x xmlns:z='urn:z'/><n xmlns=''/></r>");
        var expected = mode == 2
            ? "<r xmlns=\"urn:d\" xmlns:a=\"urn:a\" xmlns:z=\"urn:z\" b=\"B\" a:b=\"A\" z:b=\"Z\"><x></x><n xmlns=\"\"></n></r>"
            : "<r xmlns=\"urn:d\" xmlns:a=\"urn:a\" xmlns:z=\"urn:z\" b=\"B\" a:b=\"A\" z:b=\"Z\"><x></x><n xmlns=\"\"></n></r>";
        Assert.Equal(expected, Canonical(d, Options(mode, comments)));
    }
    [Theory, MemberData(nameof(Modes))]
    public void SelectionIsExactMembershipAndTraversalCrossesExcludedAncestors(int mode, bool comments)
    {
        var d = Parse("<r xmlns:p='urn:p' xml:lang='en' xml:id='parent'><skip><p:x a='A'>text<y/></p:x></skip></r>");
        var x = d.RootElement.FirstChild.FirstChild;
        var inherited = mode == 0 ? " xml:id=\"parent\" xml:lang=\"en\"" : mode == 1 ? " xml:lang=\"en\"" : "";
        Assert.Equal("<p:x" + inherited + "></p:x>", Canonical([XPathItem.FromNode(x)], Options(mode, comments)));
        Assert.Equal("<p:x xmlns:p=\"urn:p\" a=\"A\"" + inherited + ">text<y></y></p:x>", Canonical(Select(d, "(//node() | //@* | //namespace::*)[ancestor-or-self::*[local-name()='x']]"), Options(mode, comments)));
        Assert.Equal("text", Canonical([XPathItem.FromNode(x.FirstChild)], Options(mode, comments)));
        Assert.Equal("", Canonical([XPathItem.FromAttribute(x.Attributes[0]), XPathItem.FromNamespace(x, "p")], Options(mode, comments)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DuplicateUnorderedSelectionKeepsIdentityAndDocumentOrder(int mode)
    {
        var d = Parse("<r><a same='x'/><b same='x'/></r>");
        var a = d.RootElement.FirstChild;
        var b = a.NextSibling;
        var items = new[] { XPathItem.FromNode(b), XPathItem.FromAttribute(b.Attributes[0]), XPathItem.FromNode(a), XPathItem.FromAttribute(a.Attributes[0]), XPathItem.FromNode(b), XPathItem.FromAttribute(a.Attributes[0]) };
        Assert.Equal("<a same=\"x\"></a><b same=\"x\"></b>", Canonical(items, Options(mode)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AdjacentPhysicalTextIsOneExactXPathIdentity(int mode)
    {
        var d = Parse("<r>A<![CDATA[B]]>C<x/>D</r>");
        var text = XPathItem.FromNode(d.RootElement.FirstChild.NextSibling);
        Assert.Equal("ABC", Canonical([text], Options(mode)));
        Assert.Equal("ABC", Canonical([text, XPathItem.FromNode(d.RootElement.FirstChild.NextSibling.NextSibling)], Options(mode)));
    }
    [Theory]
    [InlineData(0, " xml:base=\"near/\" xml:id=\"ID\" xml:lang=\"far\" xml:other=\"O\" xml:space=\"preserve\"")]
    [InlineData(1, " xml:base=\"top/near/\" xml:lang=\"far\" xml:space=\"preserve\"")]
    [InlineData(2, "")]
    public void InclusiveXmlContextAndBaseIdDifferences(int mode, string expected)
    {
        var d = Parse("<a xml:lang='far' xml:base='top/' xml:id='ID' xml:other='O'><b xml:space='preserve' xml:base='near/'><c/></b></a>");
        Assert.Equal("<c" + expected + "></c>", Canonical([XPathItem.FromNode(d.RootElement.FirstChild.FirstChild)], Options(mode)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OwnExcludedXmlAttributeStillSuppressesInheritance(int mode)
    {
        var d = Parse("<a xml:lang='parent'><b xml:lang='own'/></a>");
        Assert.Equal("<b></b>", Canonical([XPathItem.FromNode(d.RootElement.FirstChild)], Options(mode)));
        // An omitted direct parent does not stop inheritance from selected ancestors.
        var other = Parse("<a xml:lang='en'><b><c/></b></a>");
        Assert.Equal("<a><c xml:lang=\"en\"></c></a>", Canonical([XPathItem.FromNode(other.RootElement), XPathItem.FromNode(other.RootElement.FirstChild.FirstChild)], Options(mode)));
    }
    [Fact]
    public void PublishedC14n11SubsetBaseVectorWithE11_01Erratum()
    {
        var d = Parse("<doc xmlns='http://www.ietf.org' xmlns:w3c='http://www.w3.org' xml:base='something/else'><e1><e2 xmlns='' xml:space='preserve' xml:id='abc' xml:base='bar/'><e3 id='E3' xml:base='foo'/></e2></e1></doc>");
        var items = Select(d, "(//node() | //@* | //namespace::*)[local-name()='e1' or (parent::*[local-name()='e1'] and not(self::text() or local-name()='e2')) or ancestor-or-self::*[local-name()='e3']]");
        Assert.Equal("<e1 xmlns=\"http://www.ietf.org\" xmlns:w3c=\"http://www.w3.org\" xml:base=\"something/else\"><e3 xmlns=\"\" id=\"E3\" xml:base=\"bar/foo\" xml:space=\"preserve\"></e3></e1>", Canonical(items, Options(1)));
    }
    [Theory]
    [InlineData("..", "", "../")]
    [InlineData("..", "?q", "../?q")]
    [InlineData("a/..", "", "a/../")]
    [InlineData("abc/", "../", "")]
    [InlineData("../", "../", "../../")]
    [InlineData("..", "..", "../../")]
    [InlineData("x/y/", "../../z", "z")]
    [InlineData("http://a/b/c", "../d?x#frag", "http://a/d?x")]
    [InlineData("http://a/b", "//other//a///b", "http://other/a/b")]
    [InlineData("a/b", "/x/./y/..", "/x/")]
    public void C14n11RelativeBaseJoinUsesModifiedRfcRules(string outer, string inner, string expected)
    {
        var d = new XmlDocument();
        var a = d.CreateElement("a");
        var b = d.CreateElement("b");
        d.DocumentNode.AppendChild(a);
        a.AppendChild(b);
        a.SetAttribute("base", outer, "http://www.w3.org/XML/1998/namespace", "xml");
        b.SetAttribute("base", inner, "http://www.w3.org/XML/1998/namespace", "xml");
        Assert.Equal("<b" + (expected.Length == 0 ? "" : " xml:base=\"" + expected + "\"") + "></b>", Canonical([XPathItem.FromNode(b), XPathItem.FromAttribute(b.Attributes[0])], Options(1)));
    }
    [Fact]
    public void SelectedBaseAncestorStopsFixupAndExcludedOwnBaseDoesNotCauseFixup()
    {
        var d = Parse("<a xml:base='foo/bar'><b xml:base='..'><c xml:base='..'><d xml:base='x'/></c></b></a>");
        var leaf = d.RootElement.FirstChild.FirstChild.FirstChild;
        Assert.Equal("<a xml:base=\"foo/bar\"><d xml:base=\"../../x\"></d></a>", Canonical([XPathItem.FromNode(d.RootElement), XPathItem.FromAttribute(d.RootElement.Attributes[0]), XPathItem.FromNode(leaf), XPathItem.FromAttribute(leaf.Attributes[0])], Options(1)));
        Assert.Equal("<a><d xml:base=\"../../x\"></d></a>", Canonical([XPathItem.FromNode(d.RootElement), XPathItem.FromNode(leaf), XPathItem.FromAttribute(leaf.Attributes[0])], Options(1)));
    }
    [Fact]
    public void IncludedAncestorWithoutPhysicalBaseStopsOmittedBaseChain()
    {
        var d = Parse("<a xml:base='top/' xml:lang='en'><b><c><d/></c></b></a>");
        var b = d.RootElement.FirstChild;
        var leaf = b.FirstChild.FirstChild;
        Assert.Equal("<b xml:base=\"top/\" xml:lang=\"en\"><d xml:lang=\"en\"></d></b>", Canonical([XPathItem.FromNode(b), XPathItem.FromNode(leaf)], Options(1)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ProgrammaticNamespaceBindingsMustAgreeBeforeOutput(int mode)
    {
        var d = new XmlDocument();
        var r = d.CreateElement("r", "urn:p", "p");
        d.DocumentNode.AppendChild(r);
        void Reject()
        {
            using var output = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(d, output, Options(mode)));
            Assert.Equal(0, output.Length);
        }
        Reject();
        r.SetNamespaceDeclaration("p", "urn:other");
        Reject();
        r.SetNamespaceDeclaration("p", "urn:p");
        Assert.Equal("<p:r xmlns:p=\"urn:p\"></p:r>", Canonical(d, Options(mode)));
        r.SetAttribute("a", "v", "urn:attr", "q");
        Reject();
        r.SetNamespaceDeclaration("q", "urn:other");
        Reject();
        r.SetNamespaceDeclaration("q", "urn:attr");
        Assert.Equal("<p:r xmlns:p=\"urn:p\" xmlns:q=\"urn:attr\" q:a=\"v\"></p:r>", Canonical(d, Options(mode)));
        r.SetAttribute("unprefixed", "v", "urn:attr");
        Reject();
        var plain = new XmlDocument();
        var root = plain.CreateElement("r");
        plain.DocumentNode.AppendChild(root);
        root.SetAttribute("lang", "en", "http://www.w3.org/XML/1998/namespace", "xml");
        Assert.Equal("<r xml:lang=\"en\"></r>", Canonical(plain, Options(mode)));
        root.SetNamespaceDeclaration("", "urn:default");
        using var invalidDefault = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(plain, invalidDefault, Options(mode)));
        Assert.Equal(0, invalidDefault.Length);
    }
    [Fact]
    public void ExclusivePrefixesAreExactSelectedNamespaceNodes()
    {
        var d = Parse("<r xmlns='urn:d' xmlns:p='urn:p' xmlns:q='urn:q'><p:x q:a='v'/></r>");
        var x = d.RootElement.FirstChild;
        var items = Select(d, "(//node() | //@* | //namespace::*)[ancestor-or-self::*[local-name()='x']]");
        Assert.Equal("<p:x xmlns:p=\"urn:p\" xmlns:q=\"urn:q\" q:a=\"v\"></p:x>", Canonical(items, Options(2)));
        Assert.Equal("<p:x xmlns=\"urn:d\" xmlns:p=\"urn:p\" xmlns:q=\"urn:q\" q:a=\"v\"></p:x>", Canonical(items, Options(2) with
        {
            InclusivePrefixes = ["#default", "q", "q"]
        }));
        Assert.Equal("<p:x></p:x>", Canonical([XPathItem.FromNode(x)], Options(2) with
        {
            InclusivePrefixes = ["p", "q"]
        }));
    }
    [Fact]
    public void ScalarOrderingDiffersFromUtf16OrdinalAndCultureDoesNotMatter()
    {
        var d = new XmlDocument();
        var r = d.CreateElement("r");
        d.DocumentNode.AppendChild(r);
        r.SetAttribute("\U00010000", "supp");
        r.SetAttribute("\uF900", "bmp");
        r.SetNamespaceDeclaration("\U00010000", "urn:s");
        r.SetNamespaceDeclaration("\uF900", "urn:b");
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new("tr-TR");
            Assert.Equal("<r xmlns:\uF900=\"urn:b\" xmlns:\U00010000=\"urn:s\" \uF900=\"bmp\" \U00010000=\"supp\"></r>", Canonical(d, Options(0)));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
    [Fact]
    public void ProgrammaticCommentPiCarriageReturnsAndCallerStreamLifetime()
    {
        var d = new XmlDocument();
        var comment = d.CreateComment("a\rb");
        var pi = d.CreateProcessingInstruction("p", "a\rb");
        d.DocumentNode.AppendChild(comment);
        d.DocumentNode.AppendChild(pi);
        var r = d.CreateElement("r");
        d.DocumentNode.AppendChild(r);
        using var stream = new ObserveStream();
        stream.Write("prefix"u8);
        var oldWrites = stream.Writes;
        XmlCanonicalizer.Canonicalize(d, stream, Options(1, true));
        Assert.Equal("prefix<!--a&#xD;b-->\n<?p a&#xD;b?>\n<r></r>", Encoding.UTF8.GetString(stream.ToArray()));
        Assert.False(stream.Flushed);
        Assert.False(stream.Closed);
        Assert.True(stream.Writes > oldWrites);
    }
    [Fact]
    public void InvalidOptionsRejectBeforeWriting()
    {
        var d = Parse("<r/>");
        var invalid = new[] { new XmlCanonicalizationOptions { Mode = (XmlCanonicalizationMode)8 }, new() { Limits = null! }, new() { Limits = new() { Depth = 0 } }, new() { MaxOutputBytes = 0 }, new() { InclusivePrefixes = null! }, new() { InclusivePrefixes = ["p"] }, new() { Mode = XmlCanonicalizationMode.Exclusive10, InclusivePrefixes = [null!] }, new() { Mode = XmlCanonicalizationMode.Exclusive10, InclusivePrefixes = ["p:q"] } };
        foreach (var options in invalid)
        {
            using var stream = new MemoryStream();
            Assert.ThrowsAny<ArgumentException>(() => XmlCanonicalizer.Canonicalize(d, stream, options));
            Assert.Equal(0, stream.Length);
        }
        using var readonlyStream = new MemoryStream([], false);
        Assert.Throws<ArgumentException>(() => XmlCanonicalizer.Canonicalize(d, readonlyStream));
    }
    [Fact]
    public void InvalidStaleDetachedMixedAndIncompleteInputRejectsBeforeOutput()
    {
        var d = Parse("<r xmlns:p='urn:p' a='A'>A</r>");
        var r = d.RootElement;
        var stale = XPathItem.FromNode(r);
        var text = r.FirstChild;
        text.Value = "B";
        text.Value = "A";
        var detached = XPathItem.FromNode(text);
        text.Remove();
        var a = XPathItem.FromAttribute(r.Attributes[0]);
        r.Attributes[0].Remove();
        var ns = XPathItem.FromNamespace(r, "p");
        r.SetNamespaceDeclaration("p", "urn:new");
        var second = Parse("<s/>");
        foreach (var nodes in new IReadOnlyList<XPathItem>[] { [default], [stale], [detached], [a], [ns], [XPathItem.FromNode(r), XPathItem.FromNode(second.RootElement)] })
        {
            using var stream = new MemoryStream();
            Assert.Throws<ArgumentException>(() => XmlCanonicalizer.Canonicalize(nodes, stream));
            Assert.Equal(0, stream.Length);
        }
        var incomplete = XmlDocument.ParseWithDiagnostics("<r>&#0;</r>", new()
        {
            Recover = true
        }, TestContext.Current.CancellationToken).Value!;
        using var target = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(incomplete, target));
        Assert.Equal(0, target.Length);
        var unresolved = Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>&unknown;</r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        });
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(unresolved, target));
        Assert.Equal(0, target.Length);
        typeof(XmlDocument).GetField("<ContentRevision>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(second, -1L);
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(second, target));
        Assert.Equal(0, target.Length);
    }
    [Fact]
    public void RelativeNamespaceAnywhereRejectsDuringPreflightEvenIfExcluded()
    {
        var d = Parse("<r><x xmlns:p='relative'/><y/></r>");
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => XmlCanonicalizer.Canonicalize(d, stream));
        Assert.Equal(0, stream.Length);
        Assert.Throws<ArgumentException>(() => XmlCanonicalizer.Canonicalize([XPathItem.FromNode(d.RootElement.LastChild)], stream));
        Assert.Equal(0, stream.Length);
    }
    [Fact]
    public void EmptySetWritesNothingAndQuotasUseUtf8BytesAndScalarUnits()
    {
        using var target = new MemoryStream();
        XmlCanonicalizer.Canonicalize([], target);
        Assert.Equal(0, target.Length);
        var d = Parse("<r>😀</r>");
        var expected = Encoding.UTF8.GetBytes("<r>😀</r>");
        Assert.Equal("<r>😀</r>", Canonical(d, Options(0) with
        {
            MaxOutputBytes = expected.Length,
            Limits = new()
            {
                Depth = 1,
                TokenCharacters = 1
            }
        }));
        Assert.Throws<InvalidOperationException>(() => Canonical(d, Options(0) with { MaxOutputBytes = expected.Length - 1 }));
        Assert.Throws<InvalidOperationException>(() => Canonical(d, Options(0) with { Limits = new() { EvaluationWork = 1 } }));
        Assert.Throws<InvalidOperationException>(() => Canonical(Parse("<r><x/></r>"), Options(0) with { Limits = new() { Depth = 1 } }));
        Assert.Throws<InvalidOperationException>(() => Canonical(Parse("<r a='1' b='2'/>"), Options(0) with { Limits = new() { AttributesPerElement = 1 } }));
        Assert.Throws<InvalidOperationException>(() => Canonical(Parse("<r>AB</r>"), Options(0) with { Limits = new() { TokenCharacters = 1 } }));
    }
    [Fact]
    public void CancellationMutationAndIoExceptionsPropagateWithCallerOwnedStream()
    {
        var d = Parse("<r>" + new string('x', 10000) + "</r>");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var output = new ObserveStream();
        var ex = Assert.Throws<OperationCanceledException>(() => XmlCanonicalizer.Canonicalize(d, output, new() { CancellationToken = cancelled.Token }));
        Assert.Equal(cancelled.Token, ex.CancellationToken);
        Assert.Equal(0, output.Length);
        using var cancel = new CancellationTokenSource();
        using var cancelling = new ObserveStream { OnWrite = () => cancel.Cancel() };
        ex = Assert.Throws<OperationCanceledException>(() => XmlCanonicalizer.Canonicalize(d, cancelling, new() { CancellationToken = cancel.Token }));
        Assert.Equal(cancel.Token, ex.CancellationToken);
        Assert.True(cancelling.Length > 0);
        using var mutate = new ObserveStream { OnWrite = () => d.RootElement.SetAttribute("changed", "yes") };
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(d, mutate));
        Assert.True(mutate.Length > 0);
        Assert.False(mutate.Closed);
        var error = new IOException("caller");
        using var throwing = new ObserveStream { OnWrite = () => throw error };
        Assert.Same(error, Assert.Throws<IOException>(() => XmlCanonicalizer.Canonicalize(d, throwing)));
        Assert.False(throwing.Closed);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExactNamespaceMembershipMayProduceIntentionallyUnboundSubsets(int mode)
    {
        var d = Parse("<r xmlns='urn:d' xmlns:p='urn:p'><p:x><y/></p:x></r>");
        var r = d.RootElement;
        var x = r.FirstChild;
        var y = x.FirstChild;
        var items = new[] { XPathItem.FromNode(r), XPathItem.FromNamespace(r, ""), XPathItem.FromNode(x), XPathItem.FromNamespace(x, "p"), XPathItem.FromNode(y), XPathItem.FromNamespace(y, "") };
        var expected = mode == 2 ? "<r xmlns=\"urn:d\"><p:x xmlns:p=\"urn:p\"></p:x><y></y></r>" : "<r xmlns=\"urn:d\"><p:x xmlns=\"\" xmlns:p=\"urn:p\"><y xmlns=\"urn:d\"></y></p:x></r>";
        if (mode == 2)
        {
            expected = "<r xmlns=\"urn:d\"><p:x xmlns:p=\"urn:p\"><y></y></p:x></r>";
        }

        Assert.Equal(expected, Canonical(items, Options(mode)));
    }
    [Fact]
    public void ScalarAggregateAndAdjacentTextTokenBudgetsAreEnforcedBeforeOutput()
    {
        var d = Parse("<r>😀</r>");
        Assert.Equal("<r>😀</r>", Canonical(d, Options(0) with
        {
            Limits = new()
            {
                DocumentCharacters = 2
            }
        }));
        Assert.Throws<InvalidOperationException>(() => Canonical(d, Options(0) with { Limits = new() { DocumentCharacters = 1 } }));
        var split = Parse("<r>A<![CDATA[B]]>C</r>");
        using var stream = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(split, stream, Options(0) with { Limits = new() { TokenCharacters = 2 } }));
        Assert.Equal(0, stream.Length);
    }
    [Fact]
    public void IncompleteAttributeSidecarsAndStaleNamespaceRoundTripsRejectBeforeOutput()
    {
        var d = Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'x&unknown;y'>]><r/>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        });
        using var stream = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => XmlCanonicalizer.Canonicalize(d, stream));
        Assert.Equal(0, stream.Length);
        var other = Parse("<r xmlns:p='urn:p'/>");
        var ns = XPathItem.FromNamespace(other.RootElement, "p");
        other.RootElement.SetNamespaceDeclaration("p", "urn:b");
        other.RootElement.SetNamespaceDeclaration("p", "urn:p");
        Assert.Throws<ArgumentException>(() => XmlCanonicalizer.Canonicalize([ns], stream));
        Assert.Equal(0, stream.Length);
    }
    [Fact]
    public void OwnedOfflineVectorsAreByteExact()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests/fixtures/M5-c14n/vectors.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "tests/fixtures/M5-c14n/vectors.json")));
        foreach (var vector in json.RootElement.EnumerateArray())
        {
            var document = Parse(vector.GetProperty("xml").GetString()!);
            var options = Options(vector.GetProperty("mode").GetInt32(), vector.TryGetProperty("comments", out var comments) && comments.GetBoolean());
            if (vector.TryGetProperty("prefixes", out var prefixes))
            {
                options = options with
                {
                    InclusivePrefixes = [.. prefixes.EnumerateArray().Select(p => p.GetString()!)]
                };
            }

            var actual = vector.TryGetProperty("expression", out var expr) ? Canonical(Select(document, expr.GetString()!), options) : Canonical(document, options);
            Assert.Equal(Encoding.UTF8.GetBytes(vector.GetProperty("expected").GetString()!), Encoding.UTF8.GetBytes(actual));
        }
    }
    [Theory]
    [InlineData(0, "&lt;&amp;&gt;&#xD;😀")]
    [InlineData(1, "&lt;&amp;>&#xD;😀")]
    [InlineData(2, "<&>&#xD;😀")]
    [InlineData(3, "<&>\r😀")]
    public void BorrowedEncodingMatchesExactBytesAndNeverSplitsScalar(int mode, string expected)
    {
        var input = "<&>\r😀";
        var output = new byte[6];
        using var result = new MemoryStream();
        var offset = 0;
        while (offset < input.Length)
        {
            var written = CanonicalEscapes.Encode(input.AsSpan(offset), output, mode, out var consumed);
            Assert.True(consumed > 0);
            result.Write(output.AsSpan(0, written));
            offset += consumed;
        }
        Assert.Equal(Encoding.UTF8.GetBytes(expected), result.ToArray());
        Assert.Equal(0, CanonicalEscapes.Encode("😀", new byte[3], mode, out var tooSmall));
        Assert.Equal(0, tooSmall);
        Assert.Throws<ArgumentException>(() => CanonicalEscapes.Encode("\uD800", new byte[6], mode, out _));
    }
    private sealed class ObserveStream : MemoryStream
    {
        internal Action? OnWrite; internal int Writes; internal bool Flushed, Closed;
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Writes++;
            base.Write(buffer);
            OnWrite?.Invoke();
        }
        public override void Flush()
        {
            Flushed = true;
            base.Flush();
        }
        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}
