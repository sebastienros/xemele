using Xemele.Internal.Operations;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Dom;
using System.Reflection;
using Xunit;
namespace Xemele.Tests.IncompleteAttributes;

public sealed class PolicyAndLifecycleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("", false)]
    [InlineData("<?xml version='1.0' standalone='no'?>", false)]
    [InlineData("<?xml version='1.0' standalone='yes'?>", true)]
    public void PhysicalExternalOriginRemainsVcWhileDocumentOriginHasItsOwnWfc(string declaration, bool standalone)
    {
        const string body = "<!ATTLIST unused a CDATA '&gap;'>";
        var resolver = new TransportAndGuardsTests.Resolver("urn:subset", body);
        var options = RuntimeTests.Options with
        {
            Resolver = resolver
        };
        var doc = XmlDocument.Parse(declaration + "<!DOCTYPE r SYSTEM 'urn:subset'><r/>", options, Ct);
        Assert.Empty(doc.IncompleteAttributeValues);
        var d = Assert.Single(doc.IncompleteAttributeDefaults);
        Assert.True(d.IsExternal);
        Assert.Equal(new Uri("urn:subset"), Assert.Single(d.Parts).Location.SourceUri);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
        resolver = new("urn:subset", body);
        options = options with
        {
            Resolver = resolver
        };
        var success = XmlDocument.TryParse(declaration + "<!DOCTYPE r SYSTEM 'urn:subset'><r b='&gap;'/>", out doc, out var errors, options, Ct);
        Assert.Equal(!standalone, success);
        if (standalone)
        {
            Assert.Equal("XML.UndeclaredEntity", Assert.Single(errors).Code);
        }
        else
        {
            Assert.Single(doc!.IncompleteAttributeValues);
        }
    }
    [Fact]
    public void StandaloneDependenciesAreCheckedOnlyWhenIncompleteDefaultIsApplied()
    {
        const string body = "<!ENTITY e 'K&gap;'><!ATTLIST unused a CDATA '&e;'>";
        var resolver = new TransportAndGuardsTests.Resolver("urn:subset", body);
        const string xml = "<?xml version='1.0' standalone='yes'?><!DOCTYPE r SYSTEM 'urn:subset'><r/>";
        var doc = XmlDocument.Parse(xml, RuntimeTests.Options with
        {
            Resolver = resolver
        }, Ct);
        Assert.Single(doc.IncompleteAttributeDefaults);
        Assert.Empty(doc.IncompleteAttributeValues);
        resolver = new("urn:subset", body.Replace("unused", "r", StringComparison.Ordinal));
        Assert.False(XmlDocument.TryParse(xml, out _, out var errors, RuntimeTests.Options with
        {
            Resolver = resolver
        }, Ct));
        Assert.Equal("XML.Standalone", Assert.Single(errors).Code);
    }
    [Fact]
    public void IndirectLaterDeclarationStaysUnknownAndFirstUnreadSuppressesProvenance()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e '&later;'>" +
            "<!ATTLIST r a CDATA '&e;'><!ENTITY later 'known'>]><r/>";
        var doc = XmlDocument.Parse(xml, RuntimeTests.Options, Ct);
        Assert.Equal("later", Assert.Single(Assert.Single(doc.IncompleteAttributeValues).Parts.OfType<XmlUnknownAttributeReference>()).Name);
        const string suppressed = "<!DOCTYPE r [<!ATTLIST r before CDATA 'ok'>%unread;<!ATTLIST r after CDATA '&gap;'><!ENTITY gap 'known'>]><r/>";
        doc = XmlDocument.Parse(suppressed, RuntimeTests.Options, Ct);
        Assert.Empty(doc.IncompleteAttributeDefaults);
        Assert.Empty(doc.IncompleteAttributeValues);
        Assert.Equal("ok", doc.RootElement.GetAttribute("before"));
        Assert.Null(doc.RootElement.GetAttribute("after"));
        Assert.Single(doc.Dtd!.Attributes);
        Assert.Empty(doc.Dtd.GeneralEntities);
    }
    [Fact]
    public void CompleteNamespaceDefaultsResolveAndUnusedIncompleteNamespaceDoesNotBind()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST unused xmlns CDATA '&gap;'>" +
            "<!ATTLIST r xmlns CDATA 'urn:root'>]><r/>";
        var doc = XmlDocument.Parse(xml, RuntimeTests.Options, Ct);
        Assert.Equal("urn:root", doc.RootElement.NamespaceUri);
        Assert.Single(doc.IncompleteAttributeDefaults);
        Assert.Equal(1, doc.RootElement.NamespaceDeclarations.Count);
        const string applied = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r xmlns CDATA '&gap;'>]><r/>";
        Assert.False(XmlDocument.TryParse(applied, out _, out var errors, RuntimeTests.Options, Ct));
        Assert.Equal("XML.UnresolvedNamespaceValue", Assert.Single(errors).Code);
        doc = XmlDocument.Parse(applied.Replace("<r/>", "<r xmlns='urn:explicit'/>", StringComparison.Ordinal), RuntimeTests.Options, Ct);
        Assert.Equal("urn:explicit", doc.RootElement.NamespaceUri);
        Assert.Single(doc.IncompleteAttributeDefaults);
        Assert.Empty(doc.IncompleteAttributeValues);
    }
    [Fact]
    public void OldSinksRefuseBeforeAffectedTagOrDefaultBatch()
    {
        foreach (var xml in new[] { RuntimeTests.Prefix + "<r a='&x;'/>", "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST unused a CDATA '&x;'>]><r/>" })
        {
            using var core = new XmlParserCore(RuntimeTests.Options);
            var sink = new OldSink();
            var result = core.Append(xml, [], true, sink, Ct);
            Assert.Equal(ScanStep.Rejected, result.Step);
            Assert.Equal("XML.IncompleteAttributeSinkRequired", result.Diagnostic!.Code);
            Assert.Equal(xml.IndexOf("&x;", StringComparison.Ordinal) + 1, result.Diagnostic.Location.LinePosition);
            Assert.Equal(0, sink.Elements);
            Assert.Equal(0, sink.Attributes);
        }
        using var clean = new XmlParserCore(RuntimeTests.Options);
        Assert.Equal(ScanStep.Completed, clean.Append(RuntimeTests.Prefix + "<r a='ok'/>", [], true, new OldSink(), Ct).Step);
    }
    [Theory]
    [InlineData("default:0")]
    [InlineData("incomplete:a")]
    public void OptionalCallbackFaultAndCancellationNeverReplay(string callback)
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA '&x;'>]><r/>";
        var fault = new InvalidOperationException("callback sentinel");
        using (var core = new XmlParserCore(RuntimeTests.Options))
        {
            var sink = new RuntimeTests.Sink(false) { ThrowOn = callback, Fault = fault };
            Assert.Same(fault, Assert.Throws<InvalidOperationException>(() => core.Append(xml, [], true, sink, Ct)));
            Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
            Assert.Equal(1, sink.Events.Count(e => e == callback));
        }
        using var cancellation = new CancellationTokenSource();
        using (var core = new XmlParserCore(RuntimeTests.Options))
        {
            var sink = new RuntimeTests.Sink(false) { CancelOn = callback, Cancellation = cancellation };
            Assert.Throws<OperationCanceledException>(() => core.Append(xml, [], true, sink, cancellation.Token));
            Assert.Throws<InvalidOperationException>(() => core.Append([], [], true, sink, Ct));
            Assert.Equal(1, sink.Events.Count(e => e == callback));
        }
    }
    [Fact]
    public void ExactAndOneOverQuotasAndSingleDefaultInterpretationCharge()
    {
        const string head = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e 'KK&x;'>" +
            "<!ATTLIST item a CDATA '&e;'>]>";
        const string xml = head + "<r><item/><item/></r>";
        using var core = new XmlParserCore(RuntimeTests.Options);
        var sink = new DomSink(RuntimeTests.Options);
        Assert.Equal(ScanStep.Completed, core.Append(xml, [], true, sink, Ct).Step);
        var budget = (OperationBudget)typeof(XmlParserCore).GetField("budget", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(core)!;
        // Each replacement scalar is charged once at interpretation. Applying
        // the immutable default twice adds zero replacement work.
        Assert.Equal(5, budget.Count(Quota.ExpandedCharacters));
        Assert.Equal(2, sink.Document.IncompleteAttributeValues.Count);
        var exact = RuntimeTests.Options with
        {
            Limits = new()
            {
                ExpandedCharacters = 5,
                AttributesPerElement = 1,
                DocumentCharacters = xml.Length
            }
        };
        Assert.Equal(2, XmlDocument.Parse(xml, exact, Ct).IncompleteAttributeValues.Count);
        foreach (var limits in new[] { exact.Limits with { ExpandedCharacters = 4 }, exact.Limits with { DocumentCharacters = xml.Length - 1 } })
        {
            Assert.False(XmlDocument.TryParse(xml, out _, out var errors, exact with
            {
                Limits = limits
            }, Ct));
            Assert.Equal("XML.ResourceLimit", Assert.Single(errors).Code);
        }
        var repeated = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e 'AAAAAAAA&x;'>]><r a='" + string.Concat(Enumerable.Repeat("&e;", 16)) + "'/>";
        Assert.Single(XmlDocument.Parse(repeated, RuntimeTests.Options with
        {
            Limits = new()
            {
                TokenCharacters = 176
            }
        }, Ct).IncompleteAttributeValues);
        Assert.False(XmlDocument.TryParse(repeated, out _, out var tokenError, RuntimeTests.Options with
        {
            Limits = new()
            {
                TokenCharacters = 175
            }
        }, Ct));
        Assert.Equal("XML.ResourceLimit", Assert.Single(tokenError).Code);
        const string attr = RuntimeTests.Prefix + "<r a='&x;' b='ok'/>";
        Assert.True(XmlDocument.TryParse(attr, out _, out _, RuntimeTests.Options with
        {
            Limits = new()
            {
                AttributesPerElement = 2
            }
        }, Ct));
        Assert.False(XmlDocument.TryParse(attr, out _, out var attrsError, RuntimeTests.Options with
        {
            Limits = new()
            {
                AttributesPerElement = 1
            }
        }, Ct));
        Assert.Equal("XML.ResourceLimit", Assert.Single(attrsError).Code);
    }
    private sealed class OldSink : IXmlEventSink
    {
        internal int Elements, Attributes;
        public bool StartDocument(XmlLocation l) => true;
        public bool Declaration(XmlDeclarationView v) => true;
        public bool StartElement(XmlNameView v, bool e, XmlLocation l)
        {
            Elements++;
            return true;
        }
        public bool NamespaceDeclaration(XmlNamespaceView v) => true;
        public bool Attribute(XmlAttributeView v)
        {
            Attributes++;
            return true;
        }
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView v, XmlLocation l) => true;
        public bool StartText(XmlTextKind k, ReadOnlySpan<char> t, XmlLocation l) => true;
        public bool TextFragment(ReadOnlySpan<char> t, bool e, XmlLocation l) => true;
        public bool EndDocument(XmlLocation l) => true;
    }
}
