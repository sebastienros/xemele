using System.Text;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.IncompleteAttributes;

public sealed class ReviewCorrectionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public void RedirectedExternalGeneralAttributeRetainsCrLfAtEveryResourceBoundaryAndPause()
    {
        const string body = "<n a='A\r\n&gap;B'/>";
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>";
        for (int boundary = 1; boundary <= Encoding.UTF8.GetByteCount(body); boundary++)
        foreach (bool pause in new[] { false, true })
        {
            var resolver = new TransportAndGuardsTests.Resolver("urn:entity", body, "urn:final", boundary);
            using var core = new XmlParserCore(RuntimeTests.Options with { Resolver = resolver }); var sink = new RuntimeTests.Sink(pause);
            Assert.Equal(ScanStep.Completed, RuntimeTests.Feed(core, xml, [], true, sink).Step);
            var value = Assert.Single(sink.Dom.Document.IncompleteAttributeValues);
            Assert.Equal("A\r\n&gap;B", value.LexicalValue); Assert.Null(value.Default); Assert.False(value.IsTokenized);
            Assert.Equal(new XmlLocation(new Uri("urn:final"), 1, 7, 6), value.ValueLocation);
            Assert.Equal(3, value.Parts.Count);
            var a = Assert.IsType<XmlKnownAttributeText>(value.Parts[0]); Assert.Equal("A ", a.Text); Assert.Equal(value.ValueLocation, a.Location);
            var gap = Assert.IsType<XmlUnknownAttributeReference>(value.Parts[1]); Assert.Equal("gap", gap.Name); Assert.Equal("&gap;", gap.LexicalReference);
            Assert.Equal(new XmlLocation(new Uri("urn:final"), 2, 1, 9), gap.Location);
            var b = Assert.IsType<XmlKnownAttributeText>(value.Parts[2]); Assert.Equal("B", b.Text); Assert.Equal(new XmlLocation(new Uri("urn:final"), 2, 6, 14), b.Location);
            Assert.Equal(1, resolver.Opens); Assert.Equal(1, resolver.Disposals);
        }
    }
    [Fact]
    public void MarkupInInternalEntityRetainsAuthoredNumericSpellingAndDeclarationLocation()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY e \"<n a='A&#38;gap;B'/>\">]><r>&e;</r>";
        for (int split = 0; split <= xml.Length; split++)
        foreach (bool pause in new[] { false, true })
        {
            using var core = new XmlParserCore(RuntimeTests.Options); var sink = new RuntimeTests.Sink(pause);
            Assert.Equal(ScanStep.NeedInput, RuntimeTests.Feed(core, xml.AsSpan(0, split), [], false, sink).Step);
            Assert.Equal(ScanStep.Completed, RuntimeTests.Feed(core, xml.AsSpan(split), [], true, sink).Step);
            var value = Assert.Single(sink.Dom.Document.IncompleteAttributeValues);
            Assert.Equal("A&#38;gap;B", value.LexicalValue);
            var gap = Assert.Single(value.Parts.OfType<XmlUnknownAttributeReference>());
            Assert.Equal("&gap;", gap.LexicalReference); Assert.Equal("gap", gap.Name);
            Assert.Equal(new XmlLocation(null, 1, xml.IndexOf("&#38;", StringComparison.Ordinal) + 1, null), gap.Location);
        }
    }
    [Fact]
    public void KnownRunsSeparateRedirectedPeOriginsAtEveryResourceBoundaryAndPause()
    {
        const string body = "<!ENTITY % p SYSTEM 'urn:pe'><!ENTITY e 'A%p;B&gap;'><!ATTLIST r a CDATA '&e;'>";
        const string xml = "<!DOCTYPE r SYSTEM 'urn:subset'><r/>";
        for (int boundary = 1; boundary <= Encoding.UTF8.GetByteCount(body); boundary++)
        foreach (bool pause in new[] { false, true })
        {
            var resolver = new SplitResolver(body, boundary);
            using var core = new XmlParserCore(RuntimeTests.Options with { Resolver = resolver }); var sink = new RuntimeTests.Sink(pause);
            Assert.Equal(ScanStep.Completed, RuntimeTests.Feed(core, xml, [], true, sink).Step);
            var value = Assert.Single(sink.Dom.Document.IncompleteAttributeValues);
            Assert.Equal("&e;", value.LexicalValue); Assert.Equal(4, value.Parts.Count);
            string[] text = ["A", "K", "B"];
            XmlLocation[] at = [new(new Uri("urn:subset-final"), 1, body.IndexOf("A%p;", StringComparison.Ordinal) + 1, body.IndexOf("A%p;", StringComparison.Ordinal)),
                new(new Uri("urn:pe-final"), 1, 1, 0),
                new(new Uri("urn:subset-final"), 1, body.IndexOf("B&gap;", StringComparison.Ordinal) + 1, body.IndexOf("B&gap;", StringComparison.Ordinal))];
            for (int i = 0; i < 3; i++) { var known = Assert.IsType<XmlKnownAttributeText>(value.Parts[i]); Assert.Equal(text[i], known.Text); Assert.Equal(at[i], known.Location); }
            var gap = Assert.IsType<XmlUnknownAttributeReference>(value.Parts[3]);
            Assert.Equal("gap", gap.Name); Assert.Equal("&gap;", gap.LexicalReference);
            Assert.Equal(new XmlLocation(new Uri("urn:subset-final"), 1, body.IndexOf("&gap;", StringComparison.Ordinal) + 1, body.IndexOf("&gap;", StringComparison.Ordinal)), gap.Location);
            Assert.Equal(2, resolver.Opens); Assert.Equal(2, resolver.Disposals);
        }
    }
    [Fact]
    public void MultipleUnusedDefaultsShareInternedElementAtExactDistinctNameQuota()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST unused a CDATA '&x;' b CDATA '&x;' c CDATA '&x;' d CDATA '&x;'>]><r/>";
        var exact = RuntimeTests.Options with { Limits = new() { InternedNames = 8 } };
        var doc = XmlDocument.Parse(xml, exact, Ct);
        Assert.Equal(4, doc.IncompleteAttributeDefaults.Count); Assert.Empty(doc.IncompleteAttributeValues);
        Assert.Same(doc.IncompleteAttributeDefaults[0].ElementName, doc.IncompleteAttributeDefaults[1].ElementName);
        Assert.Same(doc.FindInternedName("unused"), doc.IncompleteAttributeDefaults[0].ElementName);
        Assert.True(XmlDocument.TryParse(xml, out _, out _, exact, Ct));
        Assert.False(XmlDocument.TryParse(xml, out _, out var errors, exact with { Limits = new() { InternedNames = 7 } }, Ct));
        Assert.Equal("XML.ResourceLimit", Assert.Single(errors).Code);
    }
    [Fact]
    public void ExternalEntityPhysicalIdentitiesAreDistinctOpaqueTokens()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY a SYSTEM 'urn:entity'><!ENTITY b SYSTEM 'urn:entity'>]><r>&a;&b;</r>";
        var resolver = new TransportAndGuardsTests.Resolver("urn:entity", "<n a='&gap;'/>", "urn:final", 1);
        using var core = new XmlParserCore(RuntimeTests.Options with { Resolver = resolver }); var sink = new RuntimeTests.Sink(true);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var sourcesField = typeof(XmlParserCore).GetField("entitySources", flags)!;
        var identityField = typeof(XmlParserCore).GetField("attributePhysicalIdentity", flags)!;
        var identitiesField = typeof(XmlParserCore).GetField("attributePhysicalIdentities", flags)!;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance); int offset = 0; ScanResult result;
        do
        {
            result = core.Append(xml.AsSpan(offset), [], true, sink, Ct); offset += result.Consumed;
            if (sourcesField.GetValue(core) is System.Collections.IList sources)
                foreach (var source in sources)
                {
                    var sourceType = source!.GetType();
                    if (sourceType.GetField("External", flags)!.GetValue(source) is not { } owner) continue;
                    var token = sourceType.GetField("PhysicalIdentity", flags)!.GetValue(source)!;
                    Assert.IsType<object>(token); Assert.NotSame(owner, token); seen.Add(token);
                }
            if (identityField.GetValue(core) is { } current) Assert.IsType<object>(current);
            foreach (var token in (object?[])identitiesField.GetValue(core)!)
                if (token is not null) { Assert.IsType<object>(token); Assert.Contains(token, seen); }
        } while (result.Step == ScanStep.Paused);
        Assert.Equal(ScanStep.Completed, result.Step); Assert.Equal(xml.Length, offset); Assert.Equal(2, seen.Count);
        Assert.Equal(2, resolver.Opens); Assert.Equal(2, resolver.Disposals);
        Assert.Equal(2, sink.Dom.Document.IncompleteAttributeValues.Count);
    }
    private sealed class SplitResolver(string body, int boundary) : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Opens++;
            bool subset = request.Uri.AbsoluteUri == "urn:subset";
            Assert.True(subset || request.Uri.AbsoluteUri == "urn:pe");
            return new(new ResourceStream(this, Encoding.UTF8.GetBytes(subset ? body : "K"), subset ? boundary : 1), new Uri(subset ? "urn:subset-final" : "urn:pe-final"));
        }
        private sealed class ResourceStream(SplitResolver owner, byte[] bytes, int boundary) : MemoryStream(bytes, false)
        {
            public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, Position < boundary ? boundary - (int)Position : 1)]);
            public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, Position < boundary ? boundary - (int)Position : 1));
            protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); }
        }
    }
}
