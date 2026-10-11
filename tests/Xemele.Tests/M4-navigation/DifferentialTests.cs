using System.Xml.XPath;
using XPathItem = Xemele.XPath.XPathItem;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;

namespace Xemele.Tests.M4Navigation;
public sealed class DifferentialTests
{
    [Theory]
    [InlineData("<r xmlns:p='urn:p'><a k='1'>A<![CDATA[B]]><x/></a><b p:k='2'><y/><z/></b><!--c--><?go data?><c/></r>")]
    [InlineData("<r xml:lang='en'><a><a><a>😀</a></a></a><a/><b>α<c/>β</b></r>")]
    public void AllPhysicalContextsAndThirteenAxesMatchPinnedReferenceMembership(string xml)
    {
        var token = TestContext.Current.CancellationToken;
        var document = XmlDocument.Parse(xml, cancellationToken: token);
        var budget = new XPathBudget(new() { MaxOperations = null }, default, token);
        var navigator = new Xemele.XPath.Internal.XPathNavigator(document, budget);
        var reference = new XPathDocument(new StringReader(xml)).CreateNavigator();
        var positions = new Dictionary<XPathItem, System.Xml.XPath.XPathNavigator>();
        Map(document.DocumentNode, reference);
        foreach (var (item, position) in positions)
        {
            foreach (var axis in Enum.GetValues<XPathAxis>())
            {
                string name = axis switch
                {
                    XPathAxis.FollowingSibling => "following-sibling", XPathAxis.PrecedingSibling => "preceding-sibling",
                    XPathAxis.DescendantOrSelf => "descendant-or-self", XPathAxis.AncestorOrSelf => "ancestor-or-self",
                    _ => axis.ToString().ToLowerInvariant()
                };
                var expected = position.Select(name + "::node()");
                var actual = navigator.SelectAxis(item, axis, budget);
                var referenceItems = new List<System.Xml.XPath.XPathNavigator>();
                while (expected.MoveNext()) referenceItems.Add(expected.Current!.Clone());
                Assert.Equal(referenceItems.Count, actual.Count);
                // Reference Select sorts the final node-set; our seam preserves
                // reverse proximity. Namespace/attribute relative order is a
                // deliberately frozen implementation choice, compare identity.
                var ordered = axis is XPathAxis.Ancestor or XPathAxis.AncestorOrSelf or XPathAxis.Preceding or XPathAxis.PrecedingSibling ? actual.Reverse() : actual;
                if (axis is XPathAxis.Namespace or XPathAxis.Attribute)
                    Assert.All(actual, value => Assert.Contains(referenceItems, r => r.IsSamePosition(positions[value])));
                else
                    foreach (var pair in ordered.Zip(referenceItems)) Assert.True(positions[pair.First].IsSamePosition(pair.Second), name);
            }
        }
        void Map(XmlNode node, System.Xml.XPath.XPathNavigator position)
        {
            if (XPathItem.IsText(node) && XPathItem.IsText(node.PreviousSibling)) return;
            var item = XPathItem.FromNode(node); positions.Add(item, position.Clone());
            if (node.Kind == XmlNodeKind.Element)
            {
                foreach (var attr in node.Attributes)
                {
                    var p = position.Clone(); Assert.True(p.MoveToAttribute(attr.LocalName, attr.NamespaceUri)); positions.Add(XPathItem.FromAttribute(attr), p);
                }
                foreach (var ns in navigator.SelectAxis(item, XPathAxis.Namespace, budget))
                { var p = position.Clone(); Assert.True(p.MoveToNamespace(ns.LocalName)); positions.Add(ns, p); }
            }
            var child = position.Clone(); if (!child.MoveToFirstChild()) return;
            for (var live = node.FirstChild; !live.IsDefault; live = live.NextSibling)
            {
                if (XPathItem.IsText(live) && XPathItem.IsText(live.PreviousSibling)) continue;
                Map(live, child); child.MoveToNext();
            }
        }
    }
}
