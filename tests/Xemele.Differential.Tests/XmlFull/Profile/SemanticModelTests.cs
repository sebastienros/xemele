using System.Text;
using System.Text.Json;
using Xemele.TestUtilities.References;
using Xunit;

namespace Xemele.Differential.Tests.XmlFull.Profile;

public sealed class SemanticModelTests
{
    private static string Root
    {
        get
        {
            var path = new DirectoryInfo(AppContext.BaseDirectory);
            while (path is not null && !File.Exists(Path.Combine(path.FullName, "global.json")))
            {
                path = path.Parent;
            }

            return path?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
        }
    }
    [Theory]
    [InlineData("<!DOCTYPE r [<!ENTITY e 'a&#xD;b'><!ATTLIST r a NMTOKENS '  owned   tail  '>]><r>&e;<![CDATA[c]]></r>", "owned tail", "a\rbc")]
    [InlineData("<!DOCTYPE r [<!ATTLIST r a CDATA 'a&#x9;b'>]><r/>", "a\tb", "")]
    public async Task IndependentlySpecifiedDtdAttributeAndTextModelsMatch(string xml, string attribute, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        var request = JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            caseId = "m2-model",
            operation = "xml.events",
            inputBase64 = Convert.ToBase64String(bytes),
            profile = "xml-1.0",
            options = new
            {
                sourceUri = "https://offline.invalid/main.xml"
            }
        });
        var runner = Environment.GetEnvironmentVariable("XEMELE_BCL_RUNNER") ?? Path.Combine(Root, "tools/ReferenceRunners/Bcl/bin", ReferenceProcess.BuildConfiguration(AppContext.BaseDirectory), "net10.0/BclReference.dll");
        using var response = JsonDocument.Parse(await ReferenceProcess.ExecuteAsync(Environment.GetEnvironmentVariable("XEMELE_DOTNET") ?? "dotnet", [runner], request, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal("success", response.RootElement.GetProperty("outcome").GetString());
        var events = response.RootElement.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal("start", events[0].GetProperty("kind").GetString());
        Assert.Equal("r", events[0].GetProperty("localName").GetString());
        Assert.Equal("", events[0].GetProperty("namespaceUri").GetString());
        Assert.Equal(attribute, Assert.Single(events[0].GetProperty("attributes").EnumerateArray()).GetProperty("value").GetString());
        Assert.Equal(text.Length == 0 ? 2 : 3, events.Length);
        if (text.Length != 0)
        {
            Assert.Equal(text, events[1].GetProperty("value").GetString());
        }

        var product = XmlDocument.Parse(bytes, new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, TestContext.Current.CancellationToken);
        Assert.Equal(attribute, product.RootElement.GetAttribute("a"));
        Assert.Equal(text, product.RootElement.GetTextContent());
    }
    [Fact]
    public void OptionalValidityErrorsDoNotBecomeGeneralDtdValidation()
    {
        var product = XmlDocument.Parse("<!DOCTYPE r [<!ELEMENT r EMPTY><!ATTLIST r id ID #IMPLIED>]><r id='duplicate'>content<r id='duplicate'/></r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse
        }, TestContext.Current.CancellationToken);
        Assert.Equal("content", product.RootElement.GetTextContent());
        Assert.Equal("duplicate", product.RootElement.GetAttribute("id"));
    }
}
