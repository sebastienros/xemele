using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.DtdCorpusRepair;

public sealed class RepairTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri Primary = new("https://fixture.invalid/corpus/020.xml"), External = new("https://fixture.invalid/corpus/020.ent");
    private static string Root
    {
        get
        {
            for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
            {
                if (File.Exists(Path.Combine(d.FullName, "global.json")))
                {
                    return d.FullName;
                }
            }

            throw new Exception("root");
        }
    }
    public static IEnumerable<object[]> Originals()
    {
        using var file = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/fixtures/M2-dtd-corpus-repair/originals.json")));
        foreach (var row in file.RootElement.EnumerateArray())
        {
            yield return [row.Clone()];
        }
    }
    [Theory, MemberData(nameof(Originals))]
    public void SevenOriginalBytesProfileAndEveryPrimarySplit(JsonElement row)
    {
        using var inventory = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/corpora/xml/inventory.json")));
        using var profile = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "tests/corpora/xml/m2-applicability.json")));
        var original = Assert.Single(inventory.RootElement.GetProperty("cases").EnumerateArray(), c => c.GetProperty("id").GetString() == row.GetProperty("id").GetString());
        var expectation = Assert.Single(profile.RootElement.GetProperty("cases").EnumerateArray(), c => c.GetProperty("id").GetString() == row.GetProperty("id").GetString());
        var valid = row.GetProperty("accepted").GetBoolean();
        Assert.Equal(valid ? "accepted" : "rejected", Assert.Single(expectation.GetProperty("expectedOutcomes").EnumerateArray()).GetString());
        var files = row.GetProperty("files").EnumerateArray().ToArray();
        foreach (var f in files)
        {
            var registered = Assert.Single(inventory.RootElement.GetProperty("files").EnumerateArray(), c => c.GetProperty("path").GetString() == f.GetProperty("path").GetString());
            Assert.True(JsonElement.DeepEquals(registered, f));
            Read(f);
        }
        var bytes = Read(files[0]);
        Assert.Equal(original.GetProperty("inputSha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var source = new UTF8Encoding(false, true).GetString(bytes);
        if (!valid)
        {
            Assert.False(XmlDocument.TryParse(bytes, out _, out var diagnostics, new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, Ct));
            Assert.Single(diagnostics);
        }
        for (var split = 0; split <= source.Length; split++)
        {
            var resolver = valid ? new Resolver(Read(files.Single(f => f.GetProperty("path").GetString()!.EndsWith("020.ent", StringComparison.Ordinal))), 1) : null;
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
            using var core = new XmlParserCore(options, valid ? Primary : null);
            var sink = new DomSink(options);
            var first = core.Append(source.AsSpan(0, split), [], false, sink, Ct);
            Assert.InRange(first.Consumed, 0, split);
            var last = first.Step == ScanStep.Rejected ? first : core.Append(source.AsSpan(split), [], true, sink, Ct);
            Assert.Equal(valid ? ScanStep.Completed : ScanStep.Rejected, last.Step);
            if (valid)
            {
                Model(sink.Document);
                Assert.Equal(1, resolver!.Disposals);
            }
            else
            {
                Assert.NotNull(last.Diagnostic);
                Assert.Equal(0, sink.Document.RootElement.IsDefault ? 0 : 1);
            }
        }
        if (valid)
        {
            // Real byte composition, every short external decoder/read boundary.
            var subset = Read(files.Single(f => f.GetProperty("path").GetString()!.EndsWith("020.ent", StringComparison.Ordinal)));
            for (var chunk = 1; chunk <= subset.Length; chunk++)
            {
                var resolver = new Resolver(subset, chunk);
                var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
                using var core = new XmlParserCore(options, Primary);
                var sink = new DomSink(options);
                core.CompleteByteSource(bytes.Length);
                long[] maps = [.. Enumerable.Range(0, bytes.Length).Select(i => (long)i)];
                Assert.Equal(ScanStep.Completed, core.Append(source, maps, true, sink, Ct).Step);
                Model(sink.Document);
                Assert.Equal(1, resolver.Disposals);
                Assert.Single(resolver.Requests);
            }
        }
    }
    private static byte[] Read(JsonElement f)
    {
        var path = f.GetProperty("path").GetString()!;
        var bytes = File.ReadAllBytes(Path.Combine(Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources", path));
        Assert.Equal(f.GetProperty("bytes").GetInt32(), bytes.Length);
        Assert.Equal(f.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return bytes;
    }
    private static void Model(XmlDocument doc)
    {
        Assert.Equal("doc", doc.RootElement.LocalName);
        Assert.Equal("", doc.RootElement.NamespaceUri);
        Assert.Equal("v1", doc.RootElement.GetAttribute("a1"));
        Assert.Equal(1, doc.RootElement.Attributes.Count);
        Assert.True(doc.RootElement.FirstChild.IsDefault);
        Assert.Equal("", doc.RootElement.GetTextContent());
        Assert.Equal("doc", doc.DocumentType!.Name);
        Assert.Equal("020.ent", doc.DocumentType.SystemId);
        Assert.Null(doc.DocumentType.PublicId);
        Assert.Empty(doc.Dtd!.GeneralEntities);
        Assert.Empty(doc.Dtd.Notations);
        var e = Assert.Single(doc.Dtd.Elements);
        Assert.Equal("doc", e.Name);
        Assert.True(e.IsExternal);
        Assert.Equal(DtdParticleKind.Mixed, e.Content.Kind);
        Assert.Equal(DtdOccurrence.Once, e.Content.Occurrence);
        Assert.Null(e.Content.Name);
        Assert.Empty(e.Content.Children);
        Assert.Equal(new XmlLocation(External, 2, 1, 21), e.Location);
        var a = Assert.Single(doc.Dtd.Attributes);
        Assert.Equal("doc", a.ElementName);
        Assert.Equal("a1", a.Name);
        Assert.Equal(DtdAttributeType.CData, a.Type);
        Assert.Equal(DtdDefaultKind.Value, a.DefaultKind);
        Assert.Equal("v1", a.DefaultValue);
        Assert.Empty(a.Tokens);
        Assert.True(a.IsExternal);
        Assert.Equal(External, a.Location.SourceUri);
        Assert.Equal(3, a.Location.LineNumber);
        Assert.Equal(13, a.Location.LinePosition);
        Assert.Equal(59, a.Location.ByteOffset);
        Assert.Equal(External, doc.Dtd.ParameterEntities[0].Location.SourceUri);
        var p = Assert.Single(doc.Dtd.ParameterEntities);
        Assert.Equal("e", p.Name);
        Assert.True(p.IsParameter);
        Assert.True(p.IsExternal);
        Assert.Equal("doc", p.ReplacementText);
        Assert.Null(p.SystemId);
        Assert.Null(p.PublicId);
        Assert.Null(p.NotationName);
        Assert.Equal(new XmlLocation(External, 1, 1, 0), p.Location);
    }
    [Theory]
    [InlineData("<!ENTITY e 'v'><!ATTLIST r a CDATA '&e;'>", true)]
    [InlineData("<!ATTLIST r a CDATA '&e;'><!ENTITY e 'v'>", false)]
    [InlineData("<!ENTITY e '&f;'><!ATTLIST r a CDATA '&e;'><!ENTITY f 'v'>", false)]
    [InlineData("<!ENTITY e '&f;'><!ENTITY f 'v'><!ATTLIST r a CDATA '&e;'>", true)]
    [InlineData("<!ATTLIST r a CDATA '&amp;&#9;'>", true)]
    [InlineData("<!ENTITY % p \"<!ENTITY e 'v'>\">%p;<!ATTLIST r a CDATA '&e;'>", false)]
    public void DeclarationTimeLiveTableAndOriginEverySplit(string text, bool valid)
    {
        for (var split = 0; split <= text.Length; split++)
        {
            using var p = new DtdParser(DtdSubsetKind.Internal, new());
            p.ConfigureDeclarationReading(true);
            var a = p.Append(text.AsSpan(0, split), [], false, Ct);
            var b = a.Step == DtdParseStep.Rejected ? a : p.Append(text.AsSpan(split), [], true, Ct);
            Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, b.Step);
        }
    }
    [Fact]
    public void DefaultDeclarationApplicability()
    {
        const string forward = "<!ATTLIST r a CDATA '&later;'>";
        using var internalOnly = new DtdParser(DtdSubsetKind.Internal, new());
        internalOnly.ConfigureDeclarationReading(false);
        Assert.Equal(DtdParseStep.NeedInput, internalOnly.Append(forward, [], false, Ct).Step);
        Assert.Equal(DtdParseStep.Rejected, internalOnly.Append("<!ENTITY later 'v'>", [], true, Ct).Step);
        using var laterPe = new DtdParser(DtdSubsetKind.Internal, new());
        laterPe.ConfigureDeclarationReading(false);
        var laterPeResult = laterPe.Append(forward + "<!ENTITY later 'v'><!ENTITY % p ' '>%p;", [], true, Ct);
        Assert.Equal(DtdParseStep.Rejected, laterPeResult.Step);
        Assert.Equal("DTD.InvalidDefault", laterPeResult.Diagnostic!.Code);
        using var external = new DtdParser(DtdSubsetKind.Internal, new());
        external.ConfigureDeclarationReading(false, hasExternalSubset: true);
        Assert.Equal(DtdParseStep.NeedInput, external.Append(forward, [], false, Ct).Step);
        using var yes = new DtdParser(DtdSubsetKind.Internal, new());
        yes.ConfigureDeclarationReading(true, hasExternalSubset: true);
        Assert.Equal(DtdParseStep.Rejected, yes.Append(forward, [], false, Ct).Step);
        using var unread = new DtdParser(DtdSubsetKind.Internal, new());
        unread.ConfigureDeclarationReading(false, new XmlLocation(null, 1, 1, null));
        Assert.Equal(DtdParseStep.Completed, unread.Append(forward, [], true, Ct).Step);
        Assert.Empty(unread.Model.Attributes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoreForwardsExternalSubsetApplicability(bool standalone)
    {
        var resolver = new Resolver(" "u8.ToArray(), 1);
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
        var xml = (standalone ? "<?xml version='1.0' standalone='yes'?>" : "") +
            "<!DOCTYPE doc SYSTEM '020.ent' [<!ATTLIST doc a1 CDATA '&e;'><!ENTITY e 'v1'>]><doc/>";
        using var core = new XmlParserCore(options, Primary);
        var sink = new DomSink(options);
        var result = core.Append(xml, [], true, sink, Ct);
        Assert.Equal(ScanStep.Rejected, result.Step);
        Assert.Equal(standalone ? "DTD.UndeclaredEntity" : "DTD.InvalidDefault", result.Diagnostic!.Code);
        Assert.Empty(resolver.Requests);
        Assert.Equal(0, resolver.Disposals);
        Assert.True(sink.Document.RootElement.IsDefault);
    }
    [Theory]
    [InlineData("<!ENTITY a:b 'unused'>", false)]
    [InlineData("<!ENTITY a:b 'x'><!ENTITY a:b 'y'>", false)]
    [InlineData("<!ENTITY % a:b 'unused'>", false)]
    [InlineData("<!NOTATION a:b SYSTEM 'unused'>", false)]
    [InlineData("<!NOTATION a:b SYSTEM 'x'><!NOTATION a:b SYSTEM 'y'>", false)]
    [InlineData("<!ENTITY e SYSTEM 'x' NDATA a:b>", false)]
    [InlineData("<!ATTLIST r a NOTATION (a:b) #IMPLIED>", false)]
    [InlineData("<!ELEMENT p:r (p:c)><!ATTLIST p:r p:a (a:b|2|中) 'a:b'>", true)]
    [InlineData("<!ENTITY ÀͰ‌😀 'ok'><!NOTATION 中 SYSTEM 'ok'>", true)]
    [InlineData("<!ENTITY % p 'r'><!ELEMENT%p;EMPTY>", false)]
    public void NamespacesNamesAndInternalPlacementEverySplit(string text, bool valid)
    {
        for (var split = 0; split <= text.Length; split++)
        {
            using var p = new DtdParser(DtdSubsetKind.Internal, new());
            var a = p.Append(text.AsSpan(0, split), [], false, Ct);
            var b = a.Step == DtdParseStep.Rejected ? a : p.Append(text.AsSpan(split), [], true, Ct);
            Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, b.Step);
        }
    }
    [Theory]
    [InlineData("ATTLIST", "r", "a CDATA 'v'", true)]
    [InlineData("ELEMENT", "r", "EMPTY", true)]
    [InlineData("ENTITY", "e", "'v'", true)]
    [InlineData("NOTATION", "n", "SYSTEM 'v'", true)]
    [InlineData("AT", "TLIST r", "a CDATA 'v'", false)]
    public void KeywordAdjacentExternalParameterEverySplit(string keyword, string child, string suffix, bool valid)
    {
        var text = "<!ENTITY % p SYSTEM 'child'><!" + keyword + "%p;" + suffix + ">";
        for (var split = 0; split <= text.Length; split++)
        {
            for (var boundary = 0; boundary <= child.Length; boundary++)
            {
                var ledger = new DtdParserSourceTests.Ledger();
                using var p = new DtdParser(DtdSubsetKind.External, new(), ledger, new(Primary, 1, 1, null));
                var consumed = 0;
                var r = p.Append(text.AsSpan(0, split), [], false, Ct);
                consumed += r.Consumed;
                if (r.Step == DtdParseStep.NeedInput)
                {
                    r = p.Append(text.AsSpan(consumed), [], true, Ct);
                    consumed += r.Consumed;
                }
                if (r.Step == DtdParseStep.NeedEntity)
                {
                    p.PushEntity(External);
                    r = p.Append(child.AsSpan(0, boundary), [], false, Ct);
                    if (r.Step == DtdParseStep.NeedInput)
                    {
                        r = p.Append(child.AsSpan(boundary), [], true, Ct);
                    }

                    if (r.Step == DtdParseStep.NeedInput)
                    {
                        r = p.Append(text.AsSpan(consumed), [], true, Ct);
                    }
                }
                Assert.Equal(valid ? DtdParseStep.Completed : DtdParseStep.Rejected, r.Step);
                Assert.Equal(ledger.Enters, ledger.Exits);
                if (valid)
                {
                    Assert.Single(p.Model.ParameterEntities);
                    if (keyword == "ATTLIST")
                    {
                        Assert.Equal("v", Assert.Single(p.Model.Attributes).DefaultValue);
                    }
                }
            }
        }
    }
    private sealed class Resolver(byte[] body, int chunk) : XmlResourceResolver
    {
        internal int Disposals; internal readonly List<XmlResourceRequest> Requests = [];
        public override XmlResolvedResource? Resolve(XmlResourceRequest r, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(r);
            Assert.Equal(External, r.Uri);
            Assert.Equal(Primary, r.Referrer);
            Assert.Equal(XmlResourceKind.ExternalSubset, r.Kind);
            return new(new Owned(this, body, chunk), External);
        }
        private sealed class Owned(Resolver owner, byte[] bytes, int maximum) : MemoryStream(bytes, false)
        {
            public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(maximum, buffer.Length)]); protected override void Dispose(bool disposing)
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
