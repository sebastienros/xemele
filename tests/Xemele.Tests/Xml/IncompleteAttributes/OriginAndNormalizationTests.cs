using System.Text;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.IncompleteAttributes;

public sealed class OriginAndNormalizationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public void LiteralNumericAndWholeTokenizedNormalizationStayDistinct()
    {
        const string xml = "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ENTITY k ' K&#x9;Z '>" +
            "<!ATTLIST r a NMTOKENS #IMPLIED b NMTOKENS #IMPLIED c CDATA #IMPLIED>]><r a='  A &x; B&#9;' b='  A &k; B&#9; ' c='\t&k;&#13;'/>";
        var doc = XmlDocument.Parse(xml, RuntimeTests.Options, Ct);
        var value = Assert.Single(doc.IncompleteAttributeValues);
        Assert.True(value.IsTokenized);
        Assert.Equal("  A ", Assert.IsType<XmlKnownAttributeText>(value.Parts[0]).Text);
        Assert.Equal(" B\t", Assert.IsType<XmlKnownAttributeText>(value.Parts[2]).Text);
        Assert.Equal("A K Z B\t", doc.RootElement.GetAttribute("b"));
        Assert.Equal("  K Z \r", doc.RootElement.GetAttribute("c"));
    }
    [Fact]
    public void ExternalDefaultPhysicalLiteralAndNestedNumericAmpMapAtEveryResourceBoundary()
    {
        const string body = "<!ENTITY e 'K&#38;x;Z'>\r\n<!ATTLIST r a CDATA ' A\r\n&e; B&#9;'>";
        const string xml = "<!DOCTYPE r SYSTEM 'urn:subset'><r/>";
        for (var boundary = 1; boundary < Encoding.UTF8.GetByteCount(body); boundary++)
        {
            var resolver = new TransportAndGuardsTests.Resolver("urn:subset", body, "urn:final", boundary);
            var doc = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), RuntimeTests.Options with
            {
                Resolver = resolver
            }, Ct);
            var declaration = Assert.Single(doc.IncompleteAttributeDefaults);
            var applied = Assert.Single(doc.IncompleteAttributeValues);
            Assert.Equal(" A\r\n&e; B&#9;", declaration.LexicalValue);
            Assert.Equal(declaration.LexicalValue, applied.LexicalValue);
            Assert.Same(declaration, applied.Default);
            Assert.Same(declaration.Parts, applied.Parts);
            var gap = Assert.Single(declaration.Parts.OfType<XmlUnknownAttributeReference>());
            Assert.Equal(new XmlLocation(new Uri("urn:final"), 1, body.IndexOf("&#38;", StringComparison.Ordinal) + 1, body.IndexOf("&#38;", StringComparison.Ordinal)), gap.Location);
            Assert.Equal("&x;", gap.LexicalReference);
            Assert.Equal("x", gap.Name);
            var start = body.IndexOf(" A\r", StringComparison.Ordinal);
            Assert.Equal(new XmlLocation(new Uri("urn:final"), 2, 22, start), declaration.ValueLocation);
            Assert.Equal(1, resolver.Opens);
            Assert.Equal(1, resolver.Disposals);
        }
    }
    [Fact]
    public void InternalParameterPhysicalMappingsPreserveFrozenModelLocations()
    {
        const string xml = "<?xml version='1.0' standalone='yes'?><!DOCTYPE r [<!ENTITY % p \"<!ATTLIST unused a CDATA '&x;'>\">%p;]><r/>";
        var doc = XmlDocument.Parse(Encoding.UTF8.GetBytes(xml), RuntimeTests.Options, Ct);
        var d = Assert.Single(doc.IncompleteAttributeDefaults);
        var gap = Assert.Single(d.Parts.OfType<XmlUnknownAttributeReference>());
        var offset = xml.IndexOf("&x;", StringComparison.Ordinal);
        Assert.Equal(new XmlLocation(null, 1, offset + 1, offset), gap.Location);
        Assert.False(d.IsExternal);
        Assert.Empty(doc.IncompleteAttributeValues);
        // The established DTD model cursor still uses containing-source line 1;
        // physical sidecars alone retain the declaration's authored position.
        Assert.Equal(new XmlLocation(null, 1, 18, null), Assert.Single(doc.Dtd!.Attributes).Location);
    }
    [Fact]
    public void EveryCharacterAndMappedSplitIncludingCrLfSurrogatesAndAdjacentGaps()
    {
        const string xml = RuntimeTests.Prefix + "<r a='🙂\r\n&𐀀;&x;Z'/>";
        var expected = XmlDocument.Parse(xml, RuntimeTests.Options, Ct).IncompleteAttributeValues[0];
        var offsets = new long[xml.Length];
        long byteOffset = 0;
        for (var i = 0; i < xml.Length; i++)
        {
            offsets[i] = byteOffset;
            var width = char.IsHighSurrogate(xml[i]) ? 2 : 1;
            if (width == 2)
            {
                offsets[i + 1] = byteOffset;
            }

            byteOffset += Encoding.UTF8.GetByteCount(xml.AsSpan(i, width));
            i += width - 1;
        }
        for (var split = 0; split <= xml.Length; split++)
        {
            foreach (var mapped in new[] { false, true })
            {
                foreach (var pause in new[] { false, true })
                {
                    using var core = new XmlParserCore(RuntimeTests.Options);
                    var sink = new RuntimeTests.Sink(pause);
                    if (mapped)
                    {
                        core.CompleteByteSource(byteOffset);
                    }

                    Assert.Equal(ScanStep.NeedInput, RuntimeTests.Feed(core, xml.AsSpan(0, split), mapped ? offsets.AsSpan(0, split) : [], false, sink).Step);
                    Assert.Equal(ScanStep.Completed, RuntimeTests.Feed(core, xml.AsSpan(split), mapped ? offsets.AsSpan(split) : [], true, sink).Step);
                    var value = Assert.Single(sink.Dom.Document.IncompleteAttributeValues);
                    Assert.Equal(expected.LexicalValue, value.LexicalValue);
                    Assert.Equal(expected.Parts.Count, value.Parts.Count);
                    for (var i = 0; i < value.Parts.Count; i++)
                    {
                        Assert.Equal(expected.Parts[i].GetType(), value.Parts[i].GetType());
                        Assert.Equal(expected.Parts[i].Location.LineNumber, value.Parts[i].Location.LineNumber);
                        Assert.Equal(expected.Parts[i].Location.LinePosition, value.Parts[i].Location.LinePosition);
                        if (value.Parts[i] is XmlUnknownAttributeReference gap)
                        {
                            var original = (XmlUnknownAttributeReference)expected.Parts[i];
                            Assert.Equal(original.Name, gap.Name);
                            Assert.Equal(original.LexicalReference, gap.LexicalReference);
                            var index = xml.IndexOf(gap.LexicalReference, StringComparison.Ordinal);
                            Assert.Equal(mapped ? offsets[index] : (long?)null, gap.Location.ByteOffset);
                        }
                    }
                }
            }
        }
    }
}
