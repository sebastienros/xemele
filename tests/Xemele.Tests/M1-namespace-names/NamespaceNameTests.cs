using System.Xml.Linq;
using Xunit;

namespace Xemele.Tests.NamespaceNames;

public class NamespaceNameTests
{
    private static XmlSaveOptions Options(XmlNamespaceHandling handling) => new() { OmitXmlDeclaration = true, NamespaceHandling = handling };

    [Fact]
    public void WarmContextValidationUsesBorrowedNamesWithoutAllocations()
    {
        static void Validate()
        {
            for (var i = 0; i < 1000; i++)
            {
                TreeValidation.Expanded("xmlns", "", "");
                TreeValidation.Expanded("xmlns", "urn:a", "p", attribute: true);
                TreeValidation.Expanded("xmlns", "urn:a", "", attribute: true);
            }
        }
        for (var i = 0; i < 20; i++)
        {
            Validate();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        Validate();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    // These models are independently accepted by BCL before trusted replay.
    [Theory]
    [InlineData("", "")]
    [InlineData("urn:e", "")]
    [InlineData("urn:e", "p")]
    [InlineData("http://www.w3.org/XML/1998/namespace", "xml")]
    public void LegalLocalNamesReplayThroughPublicCheckedAndTrustedPaths(string uri, string prefix)
    {
        var declarations = prefix == "xml" || uri.Length == 0 ? "" : prefix.Length == 0 ? $" xmlns='{uri}'" : $" xmlns:{prefix}='{uri}'";
        var qname = prefix.Length == 0 ? "xmlns" : prefix + ":xmlns";
        var input = $"<{qname}{declarations} xmlns:a='urn:a' a:xmlns='v'/>";
        var oracle = XDocument.Parse(input).Root!;
        Assert.Equal(XName.Get("xmlns", uri), oracle.Name);
        Assert.Equal("v", oracle.Attribute(XName.Get("xmlns", "urn:a"))!.Value);
        foreach (var path in new[] { 0, 1, 2 })
        {
            var d = new XmlDocument();
            var b = path == 0 ? null : d.BeginDomConstruction();
            var e = path == 2 ? b!.CreateSyntaxValidatedNode(XmlNodeKind.Element, "xmlns", uri, prefix, "", TestContext.Current.CancellationToken) : d.CreateElement("xmlns", uri, prefix);
            if (path == 0)
            {
                d.DocumentNode.AppendChild(e);
            }
            else if (path == 1)
            {
                b!.AppendChild(d.DocumentNode, e);
            }
            else
            {
                b!.AppendSyntaxValidatedChild(d.DocumentNode, e, TestContext.Current.CancellationToken);
            }

            foreach (var n in oracle.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                var p = n.Name.LocalName == "xmlns" ? "" : n.Name.LocalName;
                if (path == 0)
                {
                    e.SetNamespaceDeclaration(p, n.Value);
                }
                else if (path == 1)
                {
                    b!.AppendNamespaceDeclaration(e, p, n.Value);
                }
                else
                {
                    b!.AppendSyntaxValidatedNamespaceDeclaration(e, p, n.Value, TestContext.Current.CancellationToken);
                }
            }
            if (path == 0)
            {
                e.SetAttribute("xmlns", "v", "urn:a", "a");
            }
            else if (path == 1)
            {
                b!.AppendAttribute(e, "xmlns", "v", "urn:a", "a");
            }
            else
            {
                b!.AppendSyntaxValidatedAttribute(e, "xmlns", "v", "urn:a", "a", TestContext.Current.CancellationToken);
            }

            var version = d.MutationVersion;
            var attribute = e.Attributes[0];
            foreach (var handling in new[] { XmlNamespaceHandling.Strict, XmlNamespaceHandling.Repair })
            {
                var output = d.ToXmlString(Options(handling), cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(XNode.DeepEquals(oracle, XDocument.Parse(output).Root));
                using var stream = new MemoryStream();
                d.Save(stream, Options(handling), cancellationToken: TestContext.Current.CancellationToken);
                stream.Position = 0;
                Assert.True(XNode.DeepEquals(oracle, XDocument.Load(stream).Root));
            }
            Assert.Equal(version, d.MutationVersion);
            Assert.Equal(attribute, e.Attributes[0]);
            Assert.Equal("xmlns", d.ImportNode(e).LocalName);
            Assert.Equal("v", e.Clone().GetAttribute("xmlns", "urn:a"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyPreferredAttributePrefixIsRepairedWithoutChangingModel(bool checkedBuilder)
    {
        var d = new XmlDocument();
        var b = checkedBuilder ? d.BeginDomConstruction() : null;
        var e = d.CreateElement("xmlns");
        if (b is null)
        {
            d.DocumentNode.AppendChild(e);
        }
        else
        {
            b.AppendChild(d.DocumentNode, e);
        }

        var a = b is null ? e.SetAttribute("xmlns", "v", "urn:a") : b.AppendAttribute(e, "xmlns", "v", "urn:a");
        var version = d.MutationVersion;
        Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Options(XmlNamespaceHandling.Strict), cancellationToken: TestContext.Current.CancellationToken));
        var output = d.ToXmlString(Options(XmlNamespaceHandling.Repair), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("<xmlns xmlns:ns1=\"urn:a\" ns1:xmlns=\"v\"/>", output);
        Assert.Equal("v", XDocument.Parse(output).Root!.Attribute(XName.Get("xmlns", "urn:a"))!.Value);
        Assert.Equal("", a.Prefix);
        Assert.Equal(version, d.MutationVersion);
        Assert.Equal(0, e.NamespaceDeclarations.Count);
    }

    [Theory]
    [InlineData("", "xmlns")]
    [InlineData("urn:x", "xmlns")]
    [InlineData("http://www.w3.org/2000/xmlns/", "p")]
    [InlineData("http://www.w3.org/2000/xmlns/", "")]
    [InlineData("urn:wrong", "xml")]
    [InlineData("http://www.w3.org/XML/1998/namespace", "")]
    [InlineData("http://www.w3.org/XML/1998/namespace", "p")]
    [InlineData("", "p")]
    public void ReservedBindingsRejectBeforeAnyObservableMutation(string uri, string prefix)
    {
        var d = new XmlDocument();
        var e = d.CreateElement("root");
        d.DocumentNode.AppendChild(e);
        var existing = e.SetAttribute("xmlns", "old", "urn:a", "a");
        var version = d.MutationVersion;
        var nodes = d.Nodes.Count;
        Assert.Throws<ArgumentException>(() => d.CreateElement("xmlns", uri, prefix));
        Assert.Throws<ArgumentException>(() => e.SetAttribute("xmlns", "new", uri, prefix));
        Assert.Throws<ArgumentException>(() => e.SetNamespaceDeclaration(prefix, uri));
        Assert.Equal(version, d.MutationVersion);
        Assert.Equal(nodes, d.Nodes.Count);
        Assert.Equal(existing, e.Attributes[0]);
        Assert.Equal("old", existing.Value);
        Assert.Equal("a", existing.Prefix);
        Assert.Equal(0, e.NamespaceDeclarations.Count);
        var cd = new XmlDocument();
        var b = cd.BeginDomConstruction();
        var ce = cd.CreateElement("root");
        b.AppendChild(cd.DocumentNode, ce);
        var cv = cd.MutationVersion;
        Assert.Throws<ArgumentException>(() => b.AppendAttribute(ce, "xmlns", "v", uri, prefix));
        Assert.Equal(cv, cd.MutationVersion);
        Assert.Equal(0, b.MemberChecks);
        Assert.Equal(0, ce.Attributes.Count);
    }

    [Fact]
    public void DeclarationShapeUsesDedicatedRepresentationAndRejectsCorruption()
    {
        var declarationOracle = XDocument.Parse("<r xmlns='urn:default'/>").Root!;
        Assert.True(declarationOracle.Attributes().Single().IsNamespaceDeclaration);
        var d = new XmlDocument();
        var b = d.BeginDomConstruction();
        var e = d.CreateElement("r", "urn:default");
        b.AppendChild(d.DocumentNode, e);
        var version = d.MutationVersion;
        Assert.Throws<ArgumentException>(() => e.SetAttribute("xmlns", "v"));
        Assert.Throws<ArgumentException>(() => b.AppendAttribute(e, "xmlns", "v"));
        Assert.Equal(version, d.MutationVersion);
        Assert.Equal(0, b.MemberChecks);
        Assert.Equal(0, d.AttributeSlots.Count);
        b.AppendNamespaceDeclaration(e, "", "urn:default");
        Assert.Equal(0, e.Attributes.Count);
        Assert.Equal(1, e.NamespaceDeclarations.Count);
        Assert.True(XNode.DeepEquals(declarationOracle, XDocument.Parse(d.ToXmlString(Options(XmlNamespaceHandling.Strict), cancellationToken: TestContext.Current.CancellationToken)).Root));
        var a = e.SetAttribute("a", "v");
        d.AttributeSlots[a.Slot] = new XmlDocument.MemberData(e.Slot, "xmlns", "", "", "v");
        foreach (var mode in new[] { XmlNamespaceHandling.Strict, XmlNamespaceHandling.Repair })
        {
            Assert.Throws<InvalidOperationException>(() => d.ToXmlString(Options(mode), cancellationToken: TestContext.Current.CancellationToken));
        }

        d.AttributeSlots[a.Slot] = new XmlDocument.MemberData(e.Slot, "xmlns", "urn:a", "a", "v");
        e.SetNamespaceDeclaration("a", "urn:a");
        foreach (var mode in new[] { XmlNamespaceHandling.Strict, XmlNamespaceHandling.Repair })
        {
            Assert.Equal("v", XDocument.Parse(d.ToXmlString(Options(mode), cancellationToken: TestContext.Current.CancellationToken)).Root!.Attribute(XName.Get("xmlns", "urn:a"))!.Value);
        }
    }

    [Theory]
    [InlineData("<xmlns:r xmlns:xmlns='urn:x'/>")]
    [InlineData("<r xmlns:p='http://www.w3.org/2000/xmlns/'/>")]
    [InlineData("<r xmlns='http://www.w3.org/2000/xmlns/'/>")]
    [InlineData("<r xmlns:xml='urn:wrong'/>")]
    [InlineData("<r xmlns:p='http://www.w3.org/XML/1998/namespace'/>")]
    public void BclIndependentlyRejectsReservedBindingCases(string input) => Assert.Throws<System.Xml.XmlException>(() => XDocument.Parse(input));
}
