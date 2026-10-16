using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.UndeclaredReferences;

public sealed class ReadStateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public void FirstUnreadInternalPeKeepsEarlierDeclarationsAndSuppressesLaterEntitiesAndDefaults()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY before 'B'>%missing;<!ENTITY after 'A'><!ATTLIST r v CDATA 'value'><!ELEMENT r ANY>]><r>&before;&after;</r>";
        var document = XmlDocument.Parse(xml, ContentTests.Options, Ct);
        Assert.Equal("B", document.RootElement.FirstChild.Value);
        Assert.Equal("after", Assert.Single(document.UnresolvedReferences).Name);
        Assert.Null(document.RootElement.GetAttribute("v"));
        Assert.Equal("before", Assert.Single(document.Dtd!.GeneralEntities).Name);
        Assert.Empty(document.Dtd.Attributes);
        Assert.Empty(document.Dtd.ParameterEntities);
        Assert.Equal("r", Assert.Single(document.Dtd.Elements).Name);
        var standalone = "<?xml version='1.0' standalone='yes'?>" + xml;
        Assert.False(XmlDocument.TryParse(standalone, out _, out var diagnostics, ContentTests.Options, Ct));
        Assert.Equal("DTD.UndeclaredEntity", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void FullyReadExternalSubsetStillMakesUnknownContentValidityOnly()
    {
        var resolver = new Resolver("urn:subset", "<!ENTITY known 'K'><!ELEMENT r ANY>");
        var options = ContentTests.Options with
        {
            Resolver = resolver
        };
        var document = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'urn:subset'><r>&known;&unknown;</r>", options, Ct);
        Assert.Equal("K", document.RootElement.FirstChild.Value);
        Assert.Equal("unknown", Assert.Single(document.UnresolvedReferences).Name);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
        Assert.False(XmlDocument.TryParse("<?xml version='1.0' standalone='yes'?><!DOCTYPE r SYSTEM 'urn:subset'><r>&unknown;</r>", out _, out var errors, options, Ct));
        Assert.Equal("XML.UndeclaredEntity", Assert.Single(errors).Code);
    }

    [Fact]
    public void StandaloneMissingPeClassificationUsesActiveSourceOrigin()
    {
        var resolver = new Resolver("urn:subset", "%missing;<!ENTITY known 'K'><!ATTLIST r a CDATA #IMPLIED><!ELEMENT r ANY>");
        var options = ContentTests.Options with
        {
            Resolver = resolver
        };
        const string declaration = "<?xml version='1.0' standalone='yes'?>";
        // Missing PE inside the acquired external subset is outside the WFC
        // origin clause. Standalone still processes declarations after the gap.
        var document = XmlDocument.Parse(declaration + "<!DOCTYPE r SYSTEM 'urn:subset'><r/>", options, Ct);
        Assert.Empty(document.UnresolvedReferences);
        Assert.Equal("known", Assert.Single(document.Dtd!.GeneralEntities).Name);
        Assert.Equal("K", document.Dtd.GeneralEntities[0].ReplacementText);
        Assert.True(document.Dtd.GeneralEntities[0].IsExternal);
        Assert.Single(document.Dtd.Attributes);
        Assert.Single(document.Dtd.Elements);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
        Assert.False(XmlDocument.TryParse(declaration + "<!DOCTYPE r [%missing;<!ENTITY known 'K'>]><r/>", out _, out var errors, ContentTests.Options, Ct));
        Assert.Equal("DTD.UndeclaredEntity", Assert.Single(errors).Code);
        var peResolver = new Resolver("urn:pe", "%missing;<!ENTITY known 'K'>");
        var peDocument = XmlDocument.Parse(declaration + "<!DOCTYPE r [<!ENTITY % pe SYSTEM 'urn:pe'>%pe;]><r/>",
            ContentTests.Options with
            {
                Resolver = peResolver
            }, Ct);
        Assert.Equal("known", Assert.Single(peDocument.Dtd!.GeneralEntities).Name);
        Assert.Equal(1, peResolver.Opens);
        Assert.Equal(1, peResolver.Disposals);
    }

    [Fact]
    public void FirstUnreadExternalPeSuppressesLaterDeclarationsAndRetainsPriorInternalPrecedence()
    {
        var resolver = new Resolver("urn:subset", "<!ENTITY before 'wrong'>%missing;<!ENTITY after 'A'><!ATTLIST r a CDATA 'wrong'>");
        var document = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'urn:subset' [<!ENTITY before 'B'><!ATTLIST r a CDATA 'right'>]><r>&before;&after;</r>", ContentTests.Options with
        {
            Resolver = resolver
        }, Ct);
        Assert.Equal("B", document.RootElement.FirstChild.Value);
        Assert.Equal("right", document.RootElement.GetAttribute("a"));
        Assert.Equal("after", Assert.Single(document.UnresolvedReferences).Name);
        Assert.Equal("before", Assert.Single(document.Dtd!.GeneralEntities).Name);
        Assert.Single(document.Dtd.Attributes);
    }

    [Fact]
    public void ExternalGeneralReferenceUsesActiveRedirectedUriScalarLocationAndOriginalByteMap()
    {
        var resolver = new Resolver("urn:entity", "🙂a&unknown;b", "urn:redirected");
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>";
        var options = ContentTests.Options with
        {
            Resolver = resolver
        };
        var document = XmlDocument.Parse(xml, options, Ct);
        var occurrence = Assert.Single(document.UnresolvedReferences);
        Assert.Equal(new XmlLocation(new Uri("urn:redirected"), 1, 3, 5), occurrence.Location);
        Assert.Equal("unknown", occurrence.Name);
        Assert.Equal("&unknown;", occurrence.LexicalReference);
        Assert.Equal("🙂a", document.RootElement.FirstChild.Value);
        Assert.Equal("b", document.RootElement.LastChild.Value);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
    }

    [Fact]
    public void DeniedDeclaredResourcesRemainDeniedAndKnownRecursionUnparsedAndAttributesKeepErrors()
    {
        string[] cases = [
            "<!DOCTYPE r SYSTEM 'urn:denied'><r>&x;</r>",
            "<!DOCTYPE r [<!ENTITY % p SYSTEM 'urn:denied'>%p;]><r>&x;</r>",
            ContentTests.Prefix[..^2] + "<!ENTITY e SYSTEM 'urn:denied'>]><r>&e;</r>"
        ];
        foreach (var xml in cases)
        {
            Assert.False(XmlDocument.TryParse(xml, out _, out var errors, ContentTests.Options, Ct));
            Assert.Equal("XML.ResourceDenied", Assert.Single(errors).Code);
        }
        var controls = new[] {
            ("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e '&e;'>]><r>&e;</r>", "XML.EntityCycle"),
            ("<!DOCTYPE r [<!ENTITY % p ''>%p;<!NOTATION n SYSTEM 'urn:n'><!ENTITY e SYSTEM 'urn:e' NDATA n>]><r>&e;</r>", "XML.Syntax"),
            ("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e SYSTEM 'urn:e'>]><r a='&e;'/>", "XML.Syntax")
        };
        foreach (var (xml, code) in controls)
        {
            Assert.False(XmlDocument.TryParse(xml, out _, out var errors, ContentTests.Options, Ct));
            Assert.Equal(code, Assert.Single(errors).Code);
        }
    }

    [Fact]
    public void ExactQuotasAndNoUnknownReplacementCharge()
    {
        const string head = "<!DOCTYPE r [<!ENTITY % p ''>%p;]>";
        var xml = head + "<r>" + new string('a', 40) + "&" + new string('x', 38) + ";</r>";
        var exact = ContentTests.Options with
        {
            Limits = new()
            {
                TokenCharacters = 40,
                InternedNames = 3,
                DocumentCharacters = xml.Length,
                ExpandedCharacters = 1
            }
        };
        var document = XmlDocument.Parse(xml, exact, Ct);
        Assert.Single(document.UnresolvedReferences);
        using var core = new XmlParserCore(exact);
        var sink = new DomSink(exact);
        Assert.Equal(ScanStep.Completed, core.Append(xml, [], true, sink, Ct).Step);
        var field = typeof(XmlParserCore).GetField("budget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var ledger = (Xemele.Internal.Operations.OperationBudget)field.GetValue(core)!;
        Assert.Equal(0, ledger.Count(Xemele.Internal.Operations.Quota.ExpandedCharacters));
        foreach (var limits in new[] { exact.Limits with { TokenCharacters = 39 }, exact.Limits with { InternedNames = 2 }, exact.Limits with { DocumentCharacters = xml.Length - 1 } })
        {
            Assert.False(XmlDocument.TryParse(xml, out _, out var errors, exact with
            {
                Limits = limits
            }, Ct));
            Assert.Equal("XML.ResourceLimit", Assert.Single(errors).Code);
        }
    }

    [Fact]
    public void SuppressedDeclarationsValidateSyntaxWithoutRetentionOrReplacementExpansion()
    {
        const string baseline = "<!DOCTYPE r [<!ENTITY before 'B'>%missing;]><r>&before;&gap;</r>";
        const string discarded = "<!DOCTYPE r [<!ENTITY before 'B'>%missing;<!ENTITY discarded '&#65;&#x42;'><!ENTITY % ignored 'value'><!ATTLIST r unused (a|b) '&#67;'>]><r>&before;&gap;</r>";
        var options = ContentTests.Options with
        {
            Limits = new()
            {
                InternedNames = 3,
                ExpandedCharacters = 1,
                TokenCharacters = 64
            }
        };
        long[] Counts(string xml)
        {
            using var core = new XmlParserCore(options);
            var sink = new DomSink(options);
            Assert.Equal(ScanStep.Completed, core.Append(xml, [], true, sink, Ct).Step);
            Assert.Null(sink.Diagnostic);
            Assert.Single(sink.Document.Dtd!.GeneralEntities);
            Assert.Empty(sink.Document.Dtd.Attributes);
            Assert.Empty(sink.Document.Dtd.ParameterEntities);
            Assert.Equal("B", sink.Document.RootElement.FirstChild.Value);
            Assert.Equal("gap", Assert.Single(sink.Document.UnresolvedReferences).Name);
            var field = typeof(XmlParserCore).GetField("budget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var ledger = (Xemele.Internal.Operations.OperationBudget)field.GetValue(core)!;
            return [ledger.Count(Xemele.Internal.Operations.Quota.InternedNames), ledger.Count(Xemele.Internal.Operations.Quota.ExpandedCharacters)];
        }
        Assert.Equal(new long[] { 3, 1 }, Counts(baseline));
        Assert.Equal(Counts(baseline), Counts(discarded));
        Assert.False(XmlDocument.TryParse(discarded, out _, out var namesError, options with
        {
            Limits = options.Limits with
            {
                InternedNames = 2
            }
        }, Ct));
        Assert.Equal("XML.ResourceLimit", Assert.Single(namesError).Code);
        Assert.False(XmlDocument.TryParse(discarded.Replace("&before;&gap;", "&before;&before;&gap;", StringComparison.Ordinal), out _, out var expansionError, options, Ct));
        Assert.Equal("XML.ResourceLimit", Assert.Single(expansionError).Code);
        foreach (var invalid in new[] { "<!ENTITY discarded '&#0;'>", "<!ENTITY discarded '&#x;'>", "<!ATTLIST r unused BAD 'x'>", "<!ENTITY discarded >" })
        {
            var xml = "<!DOCTYPE r [%missing;" + invalid + "]><r/>";
            Assert.False(XmlDocument.TryParse(xml, out _, out var errors, ContentTests.Options, Ct));
            Assert.NotEqual("XML.UnresolvedReferenceSinkRequired", Assert.Single(errors).Code);
        }
        const string predefined = "<!DOCTYPE r [%missing;<!ENTITY lt '&#38;#60;'><!ENTITY amp '&#38;#38;'>]><r/>";
        Assert.Empty(XmlDocument.Parse(predefined, ContentTests.Options, Ct).Dtd!.GeneralEntities);
        var resolver = new Resolver("urn:subset", "%missing;<!ENTITY discarded '%side;'>");
        var external = XmlDocument.Parse("<!DOCTYPE r SYSTEM 'urn:subset' [<!ENTITY % side SYSTEM 'urn:side'>]><r/>",
            ContentTests.Options with
            {
                Resolver = resolver
            }, Ct);
        Assert.Empty(external.Dtd!.GeneralEntities);
        Assert.Equal("side", Assert.Single(external.Dtd.ParameterEntities).Name);
        Assert.Equal(1, resolver.Opens);
        Assert.Equal(1, resolver.Disposals);
    }

    [Fact]
    public void PendingReferenceIsOwnedAcrossCallerMutationAndFalseFinalTextPause()
    {
        var xml = ContentTests.Prefix + "<r>a&unknown;b</r>";
        var input = xml.ToCharArray();
        using var core = new XmlParserCore(ContentTests.Options);
        var sink = new ContentTests.Sink(new DomSink(ContentTests.Options), true);
        var consumed = 0;
        ScanResult result;
        do
        {
            result = core.Append(input.AsSpan(consumed), [], true, sink, Ct);
            consumed += result.Consumed;
        }
        while (result.Step == ScanStep.Paused && !sink.Events.Contains("fragment::True"));
        Assert.Equal(ScanStep.Paused, result.Step);
        Assert.Empty(sink.Dom.Document.UnresolvedReferences);
        // The whole reference has been accepted but its callback awaits text end.
        Assert.Equal(xml.IndexOf(';', xml.IndexOf("&unknown;", StringComparison.Ordinal)) + 1, consumed);
        input.AsSpan(0, consumed).Fill('!');
        Assert.Equal(ScanStep.Completed, ContentTests.Feed(core, input.AsSpan(consumed), [], true, sink).Step);
        var occurrence = Assert.Single(sink.Dom.Document.UnresolvedReferences);
        Assert.Equal("unknown", occurrence.Name);
        Assert.Equal("&unknown;", occurrence.LexicalReference);
    }

    private sealed class Resolver(string requested, string body, string? redirected = null) : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(requested, request.Uri.AbsoluteUri);
            Opens++;
            return new(new ResourceStream(this, Encoding.UTF8.GetBytes(body)), new Uri(redirected ?? requested));
        }
        private sealed class ResourceStream(Resolver owner, byte[] bytes) : MemoryStream(bytes, false)
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                }

                base.Dispose(disposing);
            }
        }
    }
}
