using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;
using Xemele.StreamingAcceptance;
namespace Xemele.Conformance.Tests.Streaming;

public sealed class StreamingTests
{
    [Theory]
    [InlineData("<r/>")]
    [InlineData(" \n<!--before--><?pi data?><r xmlns='urn:d' xmlns:p='urn:p' p:a='β😀'>A&amp;B<![CDATA[]]><![CDATA[x]]><!--c--><p:e/>tail</r> \n")]
    [InlineData("<?xml version='1.0' standalone='yes'?><!DOCTYPE r [<!ELEMENT r (#PCDATA)><!ATTLIST r a CDATA 'v'><!ENTITY e 'expanded'>]><r>&e;</r>")]
    [InlineData("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a CDATA 'v'>]><r b='x&unknown;y'>x&unknown;y</r>")]
    [InlineData("<r a='1' a='2'/>")]
    [InlineData("<r>😀</wrong>")]
    public async Task AuthoredCasesPreserveAllSurfaces(string xml)
    {
        foreach (var seed in new[] { 0, 1, 1771, 65537 })
        {
            await Equivalence.Verify(Encoding.UTF8.GetBytes(xml), new()
            {
                DtdProcessing = XmlDtdProcessing.Parse
            }, seed, TestContext.Current.CancellationToken);
        }
    }
    [Fact]
    public async Task InvalidUtf8NameIsStructuredRejectionAcrossTransports()
    {
        await Equivalence.Verify(Convert.FromBase64String("PHKvPg=="), new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Limits = new()
            {
                DocumentCharacters = 1_048_576,
                Depth = 64,
                TokenCharacters = 65536
            }
        }, 2, TestContext.Current.CancellationToken);
    }
    [Fact]
    public void NormalizationOracleDetectsValueNamespaceAndCDataChanges()
    {
        var baseline = NormalizedEvents.Dom(XmlDocument.Parse("<r xmlns='urn:a'>x<![CDATA[y]]></r>", cancellationToken: TestContext.Current.CancellationToken));
        foreach (var changed in new[] { "<r xmlns='urn:b'>x<![CDATA[y]]></r>", "<r xmlns='urn:a'>z<![CDATA[y]]></r>", "<r xmlns='urn:a'>xy</r>" })
        {
            Assert.Throws<InvalidDataException>(() => Equivalence.Equal(baseline.Content, NormalizedEvents.Dom(XmlDocument.Parse(changed, cancellationToken: TestContext.Current.CancellationToken)).Content, "negative control"));
        }
    }
    [Fact]
    public async Task EveryFrozenCaseExecutesWithoutExclusion()
    {
        var root = FindRoot();
        using var ledger = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tests/corpora/xml/m2-applicability.json")));
        var resources = Xemele.TestUtilities.TestRepository.CorpusResources;
        var hashes = ledger.RootElement.GetProperty("files").EnumerateArray().ToDictionary(f => f.GetProperty("path").GetString()!, f => f.GetProperty("sha256").GetString()!);
        List<string> failures = [], contextDifferences = [], baseDenials = [];
        int count = 0, accepted = 0, rejected = 0;
        foreach (var c in ledger.RootElement.GetProperty("cases").EnumerateArray())
        {
            count++;
            var id = c.GetProperty("id").GetString()!;
            var bytes = XmlFull.Profile.ApplicabilityTests.Read(resources, c.GetProperty("inputPath").GetString()!, c.GetProperty("inputSha256").GetString()!);
            var resolver = new OfflineResolver(resources, hashes);
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
            try
            {
                var success = XmlDocument.TryParse(bytes, out _, out var diagnostics, options, TestContext.Current.CancellationToken);
                if (success)
                {
                    accepted++;
                }
                else
                {
                    rejected++;
                }

                if (!success && diagnostics.Any(d => d.Message == "External identifier has no absolute base URI."))
                {
                    Assert.Equal(0, resolver.Calls);
                    baseDenials.Add(id);
                }
                var outcome = success ? "accepted" : "rejected";
                if (!c.GetProperty("expectedOutcomes").EnumerateArray().Any(o => o.GetString() == outcome))
                {
                    contextDifferences.Add(id + ": no primary base: " + outcome + " " + string.Join(",", diagnostics.Select(d => d.Code + ":" + d.Message)));
                }

                await Equivalence.Verify(bytes, options, count, TestContext.Current.CancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException) { failures.Add(id + ": " + e.Message); }
        }
        var directory = Path.Combine(root, "artifacts/M3-corpus");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "corpus.json"), JsonSerializer.Serialize(new
        {
            count,
            accepted,
            rejected,
            contextDifferences,
            baseDenialsBeforeResolver = baseDenials,
            failures
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(2585, count);
        Assert.True(failures.Count == 0, $"{failures.Count} failures; report artifacts/M3-corpus/corpus.json; first: {failures.FirstOrDefault()}");
    }
    private sealed class OfflineResolver(string resources, Dictionary<string, string> hashes) : XmlResourceResolver
    {
        public int Calls
        {
            get; private set;
        }
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            const string prefix = "https://xemele.invalid/corpus/";
            if (!request.Uri.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var path = Uri.UnescapeDataString(request.Uri.AbsoluteUri[prefix.Length..]);
            return hashes.TryGetValue(path, out var hash) ? new(new MemoryStream(XmlFull.Profile.ApplicabilityTests.Read(resources, path, hash), false), request.Uri) : null;
        }
    }
    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "global.json")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("Root missing.");
    }
}
