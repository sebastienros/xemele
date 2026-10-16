using Xemele.Validation;
namespace Xemele.Benchmarks.M8SchemaResourceOutcomes;

// Fixtures allocate outside primitive measurement; each owned route has a fresh
// composition and returns only consumed, owned results. No timers/counters here.
internal sealed class OutcomeFixture : IDisposable
{
    internal static readonly string[] OwnedRoutes = ["optional-unavailable", "optional-resolved", "optional-legacy-denied", "optional-malformed", "principal-external-shared"];
    private static readonly Uri Source = new("urn:outcome:fixture");
    private static readonly SchemaResourceReference Reference = new("urn:outcome:child", Source, SchemaResourcePurpose.XsdInclude, default);
    private readonly LegacyDenied legacy = new();
    private readonly Unavailable unavailable = new();
    private readonly Available available = new("<r/>");
    private readonly Available malformed = new("<r>");
    private readonly XmlResourceRequest request = new(Source, null, XmlResourceKind.Schema);
    private readonly XmlResolvedResource resource = new(new MemoryStream(), Source);
    private long consumed;

    internal object RunOwned(int route)
    {
        var context = new SchemaCompileContext(new()
        {
            Resolver = route switch
            {
                0 => unavailable,
                2 => legacy,
                3 => malformed,
                _ => available
            }
        });
        if (route == 4)
        {
            context.Parse("<p/>");
        }

        try
        {
            var present = context.Resources.TryLoadOptional(Reference, null, context.Operation, out var document);
            if (route == 0)
            {
                if (present || document is not null || !context.Operation.IsComplete)
                {
                    throw new Exception("Unavailable failed.");
                }

                consumed++;
                return context;
            }
            if (route is 2 or 3 || !present || document is null || document.RootElement.LocalName != "r")
            {
                throw new Exception("Owned result failed.");
            }

            consumed += document.RootElement.LocalName.Length;
            return document;
        }
        catch (SchemaCompileException error) when (route is 2 or 3)
        {
            if (error.Diagnostics.Single().Code != (route == 2 ? "SCHEMA.ResourceDenied" : "SCHEMA.Syntax"))
            {
                throw;
            }

            consumed += error.Diagnostics.Count;
            return error;
        }
    }

    internal void RunPrimitive(int route)
    {
        if (route == 0)
        {
            if (legacy.Resolve(request, default) is not null)
            {
                throw new Exception();
            }

            consumed++;
        }
        else
        {
            var result = route == 1 ? legacy.ResolveResult(request, default) : XmlResourceResolution.FromResource(resource);
            if (route == 1 && result.Status != XmlResourceResolutionStatus.Denied || route == 2 && !ReferenceEquals(resource, result.Resource))
            {
                throw new Exception();
            }

            consumed += (int)result.Status + 1;
        }
    }
    internal long Consume() => consumed;
    public void Dispose() => resource.Dispose();
    private sealed class LegacyDenied : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => null;
    }
    private sealed class Unavailable : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => null;
        public override XmlResourceResolution ResolveResult(XmlResourceRequest request, CancellationToken cancellationToken) => XmlResourceResolution.Unavailable;
    }
    private sealed class Available(string xml) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)), request.Uri);
    }
}
