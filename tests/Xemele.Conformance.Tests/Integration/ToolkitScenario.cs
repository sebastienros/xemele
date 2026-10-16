using System.Text;
using Xemele.Interop;
using Xemele.Serialization;
using Xemele.Validation;
using Xemele.XPath;
namespace Xemele.IntegrationSamples;

public static class ToolkitScenario
{
    public static async Task<string> RunAsync(string corpus, CancellationToken token = default, int maxResources = 2)
    {
        var transport = new OfflineTransport(corpus);
        var catalog = XmlCatalog.Parse(File.ReadAllText(Path.Combine(corpus, "catalogs/toolkit.xml")));
        var resolver = new CatalogResolver(catalog, transport);
        using var primary = File.OpenRead(Path.Combine(corpus, "xinclude/root.xml"));
        var source = XmlDocument.Load(primary, null, new Uri("https://origin.invalid/root.xml"), token);
        var options = new XIncludeOptions
        {
            BaseUri = new("https://origin.invalid/root.xml"),
            Resolver = resolver,
            FixupBaseUris = false,
            FixupLanguage = false,
            Limits = new() { ExternalResources = maxResources, InclusionDepth = 2, ExternalBytes = 4096 }
        };
        var included = await XIncludeProcessor.ProcessAsync(source, options, token);
        if (transport.Opened != transport.Closed)
        {
            throw new InvalidOperationException("Transferred resource leaked.");
        }

        if (!included.Success || included.IncludedResources != 2 || included.Document is not { } expanded)
        {
            throw new InvalidOperationException("Offline include failed: " + string.Join("; ", included.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        }

        if (!primary.CanRead || transport.Opened != 2 || transport.Closed != 2 || source.RootElement.FirstChild.LocalName != "include")
        {
            throw new InvalidOperationException("Ownership or source mutation mismatch.");
        }

        var schema = DtdSchema.Compile("<!ELEMENT r (item)><!ATTLIST r xmlns:xi CDATA #IMPLIED><!ELEMENT item (#PCDATA)><!ATTLIST item xmlns:xi CDATA #IMPLIED>");
        var syntheticValidation = schema.Validate(expanded);
        if (syntheticValidation.IsValid || !syntheticValidation.Diagnostics.Any(d => d.Code == "DTD.IncompleteInput"))
        {
            throw new InvalidOperationException("Synthetic provenance must remain incomplete.");
        }
        // Materialize complete serialized input before DTD provenance validation.
        var document = XmlDocument.Parse(expanded.ToXmlString());
        var validation = schema.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("DTD validation failed: " + string.Join("; ", validation.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        }

        if (XPathExpression.Compile("string(/r/item)").Evaluate(document.DocumentNode).AsString() != "offline & safe")
        {
            throw new InvalidOperationException("XPath mismatch.");
        }

        var navigator = new XemeleXPathNavigatorAdapter(document);
        if ((string)navigator.Evaluate("string(/r/item)") != "offline & safe")
        {
            throw new InvalidOperationException("BCL navigator mismatch.");
        }

        using var serialized = new MemoryStream(Encoding.UTF8.GetBytes(document.ToXmlString()));
        using var reader = new XemeleXmlReaderAdapter(Xemele.Streaming.XmlReader.Create(serialized));
        var elements = 0;
        while (await reader.ReadAsync())
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element)
            {
                elements++;
            }
        }

        if (elements != 2 || !serialized.CanRead)
        {
            throw new InvalidOperationException("BCL reader mismatch.");
        }

        using var canonical = new MemoryStream();
        XmlCanonicalizer.Canonicalize(document, canonical);
        var result = Encoding.UTF8.GetString(canonical.ToArray());
        if (result != File.ReadAllText(Path.Combine(corpus, "xinclude/expected.txt")))
        {
            throw new InvalidOperationException("Frozen canonical output mismatch: " + result);
        }

        return result;
    }
    private sealed class OfflineTransport(string corpus) : XmlResourceResolver
    {
        public int Opened
        {
            get; private set;
        }
        public int Closed
        {
            get; private set;
        }
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var (name, final) = request.Uri.AbsoluteUri switch
            {
                "https://store.invalid/item.xml" => ("item.xml", "https://final.invalid/chapter/item.xml"),
                "https://store.invalid/value.txt" => ("value.txt", "https://final.invalid/chapter/value.txt"),
                _ => throw new InvalidOperationException("Offline allowlist rejected " + request.Uri)
            };
            Opened++;
            return new(new Tracked(File.ReadAllBytes(Path.Combine(corpus, "xinclude", name)), () => Closed++), new Uri(final));
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken token)
        {
            await Task.Yield();
            return Resolve(request, token);
        }
    }
    private sealed class Tracked(byte[] content, Action closed) : MemoryStream(content, false)
    {
        protected override void Dispose(bool disposing)
        {
            if (CanRead)
            {
                closed();
            }

            base.Dispose(disposing);
        }
    }
}
