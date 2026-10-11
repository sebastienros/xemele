using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;

namespace Xemele.Tests.M4Navigation;
public sealed class NavigationTests
{
    private static XPathBudget Budget(XPathEvaluationLimits? limits = null) => new(limits ?? new() { MaxOperations = null }, new(3, 7), TestContext.Current.CancellationToken);
    private static XPathBudget TokenBudget(CancellationToken token) => new(new() { MaxOperations = null }, new(3, 7), token);
    private static XmlDocument Parse(string xml, bool dtd = false) => XmlDocument.Parse(xml, new() { DtdProcessing = dtd ? XmlDtdProcessing.Parse : XmlDtdProcessing.Prohibit });
    private static string Label(XPathItem item) => item.Kind switch
    {
        XPathItemKind.Root => "root", XPathItemKind.Text => "text:" + item.StringValue,
        XPathItemKind.Comment => "comment:" + item.StringValue, XPathItemKind.Namespace => "ns:" + item.Name,
        XPathItemKind.Attribute => "@" + item.Name, _ => item.Name
    };
    private static string[] Axis(XPathNavigator nav, XPathItem item, XPathAxis axis, IXPathBudget? budget = null) => nav.SelectAxis(item, axis, budget ?? Budget()).Select(Label).ToArray();

    [Fact]
    public void AllThirteenAxesHaveExactProximityAndMembership()
    {
        var d = Parse("<r><a><x/>A</a><b k='v'><y/><z/></b><c/>tail<!--note--><?go data?></r>");
        var nav = new XPathNavigator(d, Budget()); var b = XPathItem.FromNode(d.RootElement.FirstChild.NextSibling);
        var expected = new Dictionary<XPathAxis, string[]>
        {
            [XPathAxis.Child] = ["y", "z"], [XPathAxis.Descendant] = ["y", "z"], [XPathAxis.Parent] = ["r"],
            [XPathAxis.Ancestor] = ["r", "root"], [XPathAxis.FollowingSibling] = ["c", "text:tail", "comment:note", "go"],
            [XPathAxis.PrecedingSibling] = ["a"], [XPathAxis.Following] = ["c", "text:tail", "comment:note", "go"],
            [XPathAxis.Preceding] = ["text:A", "x", "a"], [XPathAxis.Attribute] = ["@k"], [XPathAxis.Namespace] = ["ns:xml"],
            [XPathAxis.Self] = ["b"], [XPathAxis.DescendantOrSelf] = ["b", "y", "z"], [XPathAxis.AncestorOrSelf] = ["b", "r", "root"]
        };
        Assert.Equal(13, expected.Count);
        foreach (var (axis, labels) in expected) Assert.Equal(labels, Axis(nav, b, axis));
        Assert.Empty(Axis(nav, nav.Root, XPathAxis.Parent)); Assert.Empty(Axis(nav, nav.Root, XPathAxis.PrecedingSibling));
        Assert.Equal(new[] { "r" }, Axis(nav, nav.Root, XPathAxis.Child));
    }
    [Fact]
    public void NamespaceAndAttributeOrderScopeAndOwnerRules()
    {
        var d = Parse("<r xmlns='urn:r' xmlns:z='urn:z' xmlns:a='urn:a'><n xmlns='' xmlns:a='urn:near' z:q='2' b='1' a:q='3'><x/></n><after/></r>");
        var nav = new XPathNavigator(d, Budget()); var n = XPathItem.FromNode(d.RootElement.FirstChild);
        Assert.Equal(new[] { "ns:a", "ns:xml", "ns:z" }, Axis(nav, n, XPathAxis.Namespace));
        var ns = nav.SelectAxis(n, XPathAxis.Namespace, Budget()); Assert.Equal("urn:near", ns[0].StringValue);
        Assert.All(ns, item => { Assert.Null(item.Node); Assert.Null(item.Attribute); Assert.Equal(n.Node, item.OwnerElement); Assert.Equal("", item.NamespaceUri); Assert.Equal("", item.Prefix); });
        Assert.Equal(new[] { "@b", "@a:q", "@z:q" }, Axis(nav, n, XPathAxis.Attribute));
        var attr = nav.SelectAxis(n, XPathAxis.Attribute, Budget())[0];
        foreach (var member in new[] { attr, ns[0] })
        {
            Assert.Equal(new[] { "n" }, Axis(nav, member, XPathAxis.Parent));
            Assert.Equal(new[] { "n", "r", "root" }, Axis(nav, member, XPathAxis.Ancestor));
            foreach (var axis in new[] { XPathAxis.Child, XPathAxis.Descendant, XPathAxis.Attribute, XPathAxis.Namespace, XPathAxis.FollowingSibling, XPathAxis.PrecedingSibling }) Assert.Empty(Axis(nav, member, axis));
            Assert.Equal(new[] { "x", "after" }, Axis(nav, member, XPathAxis.Following)); Assert.Empty(Axis(nav, member, XPathAxis.Preceding));
            Assert.Equal(new[] { Label(member) }, Axis(nav, member, XPathAxis.DescendantOrSelf));
        }
        var normalized = nav.Normalize([attr, ns[2], n, ns[0], n, ns[1]], Budget());
        Assert.Equal(new[] { "n", "ns:a", "ns:xml", "ns:z", "@b" }, normalized.Select(Label));
        for (int i = 0; i < normalized.Count; i++) for (int j = 0; j < normalized.Count; j++)
            Assert.Equal(Math.Sign(i - j), Math.Sign(nav.CompareDocumentOrder(normalized[i], normalized[j], Budget())));
    }
    [Fact]
    public void LogicalTextIncludesEmptyFirstAndOmitsEmptyAggregate()
    {
        var d = new XmlDocument(); var r = d.CreateElement("r"); d.DocumentNode.AppendChild(r);
        var empty = d.CreateText(""); var cd = d.CreateCData("根😀"); var text = d.CreateText("B");
        r.AppendChild(empty); r.AppendChild(cd); r.AppendChild(text); r.AppendChild(d.CreateComment("stop"));
        r.AppendChild(d.CreateText("")); r.AppendChild(d.CreateCData("")); r.AppendChild(d.CreateProcessingInstruction("go", "data"));
        var n = d.CreateElement("n"); r.AppendChild(n); n.AppendChild(d.CreateText("C"));
        var nav = new XPathNavigator(d, Budget()); var root = nav.Rebind(XPathItem.FromNode(r), Budget());
        Assert.Equal("根😀BC", root.StringValue); Assert.Equal(root.StringValue, nav.Root.StringValue);
        var children = nav.SelectAxis(root, XPathAxis.Child, Budget()); Assert.Equal(new[] { "text:根😀B", "comment:stop", "go", "n" }, children.Select(Label));
        Assert.Equal(empty, children[0].Node); Assert.Equal(children[0], nav.Rebind(XPathItem.FromNode(cd), Budget())); Assert.Equal(children[0], nav.Rebind(XPathItem.FromNode(text), Budget()));
        Assert.All(new[] { children[0], children[1], nav.Root }, item => Assert.Equal("", item.Name));
        Assert.Equal("go", children[2].LocalName); Assert.Equal("data", children[2].StringValue);
        var emptyNav = new XPathNavigator(new XmlDocument(), Budget()); Assert.Empty(emptyNav.SelectAxis(emptyNav.Root, XPathAxis.Descendant, Budget()));
    }
    [Fact]
    public void SnapshotSurvivesAllLiveMutationsAndRebindsOldMetadata()
    {
        var d = Parse("<r xmlns:p='urn:old' xml:lang='en'><a k='old'>old</a><b/></r>"); var a = d.RootElement.FirstChild;
        var nav = new XPathNavigator(d, Budget()); var old = XPathItem.FromNode(a); var attr = XPathItem.FromAttribute(a.Attributes[0]); var ns = XPathItem.FromNamespace(a, "p");
        var items = nav.SelectAxis(nav.Root, XPathAxis.Descendant, Budget()); var before = items.Select(item => (Label(item), item.StringValue, item.LocalName, item.NamespaceUri, item.Prefix, item.Name)).ToArray();
        var liveAttribute = a.Attributes[0]; liveAttribute.Value = "new"; d.RootElement.SetNamespaceDeclaration("p", "urn:new"); d.RootElement.SetAttribute("lang", "fr", "http://www.w3.org/XML/1998/namespace", "xml");
        var liveText = a.FirstChild; liveText.Value = "changed"; a.Remove(); d.RootElement.AppendChild(a); a.RemoveAttribute("k"); a.SetAttribute("k", "replacement");
        Assert.Equal(before, nav.SelectAxis(nav.Root, XPathAxis.Descendant, Budget()).Select(item => (Label(item), item.StringValue, item.LocalName, item.NamespaceUri, item.Prefix, item.Name)));
        Assert.Equal("old", nav.Rebind(old, Budget()).StringValue); Assert.Equal("old", nav.Rebind(attr, Budget()).StringValue); Assert.Equal("urn:old", nav.Rebind(ns, Budget()).StringValue);
        Assert.Equal("en", nav.FindLanguage(attr, Budget())); Assert.True(nav.CompareDocumentOrder(old, items[^1], Budget()) < 0);
        Assert.Equal(new[] { "a", "b" }, Axis(nav, XPathItem.FromNode(d.RootElement), XPathAxis.Child));
        var next = new XPathNavigator(d, Budget()); Assert.Equal(new[] { "b", "a" }, Axis(next, XPathItem.FromNode(d.RootElement), XPathAxis.Child));
        Assert.Equal("changed", next.Rebind(old, Budget()).StringValue); Assert.Equal("urn:new", next.Rebind(ns, Budget()).StringValue);
        Assert.Equal("XPATH-NODESET", Assert.Throws<XPathEvaluationException>(() => next.Rebind(attr, Budget())).Diagnostic.Code);
        a.Remove(); Assert.Equal("old", nav.Rebind(old, Budget()).StringValue);
        Assert.Equal("XPATH-CONTEXT", Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(d, Budget()).Rebind(old, Budget(), true)).Diagnostic.Code);
    }
    [Fact]
    public void DeclaredIdsOnlyIncludingDefaultsDuplicatesAndLexicalQNames()
    {
        var d = Parse("<!DOCTYPE r [<!ATTLIST a key ID #IMPLIED><!ATTLIST b key ID 'default'><!ATTLIST p:c p:key ID #IMPLIED>]><r xmlns:p='urn:p'><a key='same'/><a key='same'/><b/><p:c p:key='q'/><a id='guess' xml:id='xmlguess'/></r>", true);
        var nav = new XPathNavigator(d, Budget());
        Assert.Equal(new[] { "a", "a", "b", "p:c" }, nav.SelectId("q \t same same\ndefault\r", Budget()).Select(Label));
        Assert.Empty(nav.SelectId("guess xmlguess", Budget()));
        var liveId = d.RootElement.FirstChild.Attributes[0]; liveId.Value = "changed"; d.RootElement.FirstChild.Remove();
        Assert.Equal(2, nav.SelectId("same", Budget()).Count); Assert.Empty(nav.SelectId("changed", Budget()));
        Assert.Empty(new XPathNavigator(d, Budget()).SelectId("changed", Budget()));
    }
    [Fact]
    public void LanguageEmptyDeclarationStopsAndMembersUseOwner()
    {
        var d = Parse("<r xml:lang='EN-us'><a xml:lang=''><b k='v'>text</b></a><c><!--x--></c></r>"); var nav = new XPathNavigator(d, Budget());
        var b = d.RootElement.FirstChild.FirstChild;
        foreach (var item in new[] { XPathItem.FromNode(b), XPathItem.FromAttribute(b.Attributes[0]), XPathItem.FromNamespace(b, "xml"), XPathItem.FromNode(b.FirstChild) }) Assert.Equal("", nav.FindLanguage(item, Budget()));
        Assert.Equal("EN-us", nav.FindLanguage(XPathItem.FromNode(d.RootElement.LastChild.FirstChild), Budget())); Assert.Null(nav.FindLanguage(nav.Root, Budget()));
    }
    [Fact]
    public void InvalidDetachedAndMixedDocumentIdentitiesReject()
    {
        var d = Parse("<r/>"); var other = Parse("<r/>"); var nav = new XPathNavigator(d, Budget());
        Assert.Throws<ArgumentException>(() => nav.Rebind(default, Budget())); Assert.Throws<ArgumentOutOfRangeException>(() => nav.SelectAxis(nav.Root, (XPathAxis)99, Budget()));
        Assert.Equal("XPATH-NODESET", Assert.Throws<XPathEvaluationException>(() => nav.CompareDocumentOrder(nav.Root, XPathItem.FromNode(other.RootElement), Budget())).Diagnostic.Code);
        Assert.Throws<XPathEvaluationException>(() => nav.Normalize([nav.Root, XPathItem.FromNode(other.DocumentNode)], Budget()));
        var old = XPathItem.FromNode(d.RootElement); d.RootElement.Remove(); var next = new XPathNavigator(d, Budget()); Assert.Throws<XPathEvaluationException>(() => next.Rebind(old, Budget()));
        Assert.Throws<ArgumentException>(() => XPathItem.FromNode(d.CreateFragment())); Assert.Throws<ArgumentException>(() => XPathItem.FromNode(d.CreateElement("detached")));
    }
    [Fact]
    public void UnresolvedAndIncompleteAttributeMetadataRejectsAtEntry()
    {
        var unresolved = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>&missing;</r>", new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        Assert.Equal("XPATH-INCOMPLETE", Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(unresolved, Budget(), new(3, 7))).Diagnostic.Code);
        var attribute = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;]><r a='&missing;'/>", new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        Assert.Equal("XPATH-INCOMPLETE", Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(attribute, Budget())).Diagnostic.Code);
        var defaults = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA '&missing;'>]><r/>", new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        Assert.Equal("XPATH-INCOMPLETE", Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(defaults, Budget())).Diagnostic.Code);
    }
    [Fact]
    public void ExactInputCharacterItemOperationAndStringLimits()
    {
        var d = Parse("<r xmlns:p='urn:p' a='v'>AB<c>CD</c></r>"); var budget = Budget(); var nav = new XPathNavigator(d, budget);
        // Count every emitted item's five fields independently; inherited scopes
        // and root/element descendant strings count once for each owner.
        var all = new List<XPathItem> { nav.Root };
        foreach (var item in nav.SelectAxis(nav.Root, XPathAxis.Descendant, Budget()))
        {
            all.Add(item); if (item.Kind != XPathItemKind.Element) continue;
            all.AddRange(nav.SelectAxis(item, XPathAxis.Namespace, Budget())); all.AddRange(nav.SelectAxis(item, XPathAxis.Attribute, Budget()));
        }
        long chars = all.Sum(i => (long)i.StringValue.Length + i.LocalName.Length + i.NamespaceUri.Length + i.Prefix.Length + i.Name.Length);
        Assert.Equal(all.Count, nav.InputItems); Assert.Equal(chars, nav.InputCharacters);
        var exact = new XPathEvaluationLimits { MaxInputItems = all.Count, MaxInputCharacters = chars, MaxOperations = budget.Operations, MaxStringCharacters = all.Max(i => Math.Max(i.StringValue.Length, i.Name.Length)) };
        _ = new XPathNavigator(d, Budget(exact));
        foreach (var limits in new[] { exact with { MaxInputItems = all.Count - 1 }, exact with { MaxInputCharacters = chars - 1 }, exact with { MaxOperations = budget.Operations - 1 }, exact with { MaxStringCharacters = exact.MaxStringCharacters - 1 } })
            Assert.Equal("XPATH-EVALUATION-LIMIT", Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(d, Budget(limits))).Diagnostic.Code);
        var e = new XmlDocument(); var r = e.CreateElement("r"); e.DocumentNode.AppendChild(r); r.AppendChild(e.CreateText(new string('x', 10000)));
        Assert.Throws<XPathEvaluationException>(() => new XPathNavigator(e, Budget(new() { MaxInputCharacters = 1 })));
    }
    [Fact]
    public void NodeSetLimitsCoverAxisScratchNormalizationAndIds()
    {
        var d = Parse("<!DOCTYPE r [<!ATTLIST a k ID #IMPLIED>]><r><a k='x'/><a k='x'/></r>", true); var nav = new XPathNavigator(d, Budget());
        Assert.Equal(2, nav.SelectId("x", Budget(new() { MaxNodeSetItems = 2 })).Count);
        Assert.Throws<XPathEvaluationException>(() => nav.SelectId("x", Budget(new() { MaxNodeSetItems = 1 })));
        var r = XPathItem.FromNode(d.RootElement); Assert.Equal(2, nav.SelectAxis(r, XPathAxis.Child, Budget(new() { MaxNodeSetItems = 2 })).Count);
        Assert.Throws<XPathEvaluationException>(() => nav.SelectAxis(r, XPathAxis.Child, Budget(new() { MaxNodeSetItems = 1 })));
        var a = XPathItem.FromNode(d.RootElement.FirstChild); Assert.Single(nav.Normalize([a, a], Budget(new() { MaxNodeSetItems = 2 })));
        Assert.Throws<XPathEvaluationException>(() => nav.Normalize([a, a], Budget(new() { MaxNodeSetItems = 1 })));
        Assert.Throws<XPathEvaluationException>(() => nav.SelectId("long", Budget(new() { MaxStringCharacters = 3 })));
    }
    [Fact]
    public void CancellationAtEntryAndBoundedDuringAxisNamesAndCopy()
    {
        var d = Parse("<r/>"); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new XPathNavigator(d, TokenBudget(canceled.Token)));
        var nav = new XPathNavigator(d, Budget()); using var source = new CancellationTokenSource();
        var observed = new CancelBudget(source, 2); Assert.Throws<OperationCanceledException>(() => nav.SelectAxis(nav.Root, XPathAxis.Descendant, observed)); Assert.InRange(observed.Operations, 2, 3);
        using var names = new CancellationTokenSource(); var longPrefix = new string('p', 1000); d.RootElement.SetNamespaceDeclaration(longPrefix, "urn:p"); nav = new XPathNavigator(d, Budget());
        var item = XPathItem.FromNamespace(d.RootElement, longPrefix); var nameBudget = new CancelBudget(names, 256);
        Assert.Throws<OperationCanceledException>(() => nav.Rebind(item, nameBudget)); Assert.InRange(nameBudget.Operations, 256, 257);
        Assert.Throws<OperationCanceledException>(() => nav.FindLanguage(nav.Root, TokenBudget(canceled.Token)));
    }
    [Fact]
    public void SnapshotCancellationInterruptsAggregateCopyAndNamespaceSorting()
    {
        foreach (int stop in new[] { 1, 256, 1024, 4096 })
        {
            var d = new XmlDocument(); var root = d.CreateElement("r"); d.DocumentNode.AppendChild(root);
            root.AppendChild(d.CreateText(new string('x', 10000))); root.SetNamespaceDeclaration(new string('p', 1000), "urn:long");
            using var source = new CancellationTokenSource(); var work = new CancelBudget(source, stop);
            var ex = Assert.Throws<OperationCanceledException>(() => new XPathNavigator(d, Budget(), work));
            Assert.Equal(source.Token, ex.CancellationToken); Assert.InRange(work.Operations, stop, stop + 255);
        }
        var small = Parse("<r z='v' a='v' xmlns:z='urn:z' xmlns:a='urn:a'/>"); var full = Budget(); _ = new XPathNavigator(small, full);
        // Every charge boundary must stop without returning a partial snapshot.
        for (int stop = 1; stop <= full.Operations; stop++)
        {
            using var source = new CancellationTokenSource(); var work = new CancelBudget(source, stop);
            Assert.Throws<OperationCanceledException>(() => new XPathNavigator(small, Budget(), work));
        }
    }
    private sealed class CancelBudget(CancellationTokenSource source, long stop) : IXPathBudget
    {
        internal long Operations; public CancellationToken CancellationToken => source.Token;
        public void ChargeOperations(long amount) { source.Token.ThrowIfCancellationRequested(); Operations += amount; if (Operations >= stop) source.Cancel(); }
        public void CheckNodeSetSize(int count) => source.Token.ThrowIfCancellationRequested(); public void CheckStringSize(int count) => source.Token.ThrowIfCancellationRequested();
    }
}
