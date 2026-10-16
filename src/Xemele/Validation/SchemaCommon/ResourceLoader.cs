namespace Xemele.Validation;

// One loader belongs to one composition; never share it or its counters across runs.
internal sealed class SchemaResourceLoader : ISchemaResourceLoader
{
    private readonly SchemaCompileOptions options;
    private readonly ISchemaOperation owner;
    private readonly SchemaParsing parsing;
    private readonly Dictionary<XmlDocument, Uri> sources = [];
    private readonly HashSet<Uri> reading = [];
    private long externalBytes;
    private int resources, depth;

    internal SchemaResourceLoader(SchemaCompileOptions? options, ISchemaOperation operation, SchemaParsing? parsing = null)
    {
        this.options = SchemaSnapshots.Compile(options);
        ArgumentNullException.ThrowIfNull(operation);
        owner = operation;
        this.parsing = parsing ?? new(operation);
    }

    internal Uri? SourceUri(XmlDocument document) => sources.TryGetValue(document, out var source) ? source : document.ParsedSourceUri ?? document.ValidationDeclarationLocation.SourceUri ?? options.BaseUri;

    // Compiler traversal holds this scope through nested Load calls. Language-specific
    // cycle/cache handling (including legal XSD import cycles) belongs to the compiler.
    internal IDisposable EnterComposition(XmlLocation location)
    {
        if (!owner.Charge(0, location))
        {
            throw SchemaSnapshots.Failure(owner);
        }

        var maximum = owner.Limits.InclusionDepth ?? int.MaxValue;
        if (depth == maximum)
        {
            Fail("SCHEMA.Limit", "Schema inclusion depth exceeded.", location);
        }

        depth++;
        return new DepthScope(this);
    }
    private sealed class DepthScope(SchemaResourceLoader loader) : IDisposable
    {
        private SchemaResourceLoader? owner = loader;
        public void Dispose()
        {
            if (owner is { } current)
            {
                owner = null;
                current.depth--;
            }
        }
    }

    public XmlDocument Load(SchemaResourceReference reference, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Load(reference, reference.Referrer ?? options.BaseUri, operation);
    }

    // Effective xml:base controls resolution; Referrer identifies the actual source
    // resource. Explicit null means no resolution base, even when options has one.
    internal XmlDocument Load(SchemaResourceReference reference, Uri? effectiveBaseUri, ISchemaOperation operation)
        => LoadCore(reference, effectiveBaseUri, operation, optional: false)!;

    internal bool TryLoadOptional(SchemaResourceReference reference, Uri? effectiveBaseUri, ISchemaOperation operation, out XmlDocument? document)
    {
        document = LoadCore(reference, effectiveBaseUri, operation, optional: true);
        return document is not null;
    }

    private XmlDocument? LoadCore(SchemaResourceReference reference, Uri? effectiveBaseUri, ISchemaOperation operation, bool optional)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(operation);
        if (!ReferenceEquals(owner, operation))
        {
            throw new ArgumentException("Loader belongs to a different operation.", nameof(operation));
        }

        ArgumentNullException.ThrowIfNull(reference.Href);
        if (!Enum.IsDefined(reference.Purpose) || reference.Referrer is { IsAbsoluteUri: false })
        {
            throw new ArgumentException("Invalid schema reference.", nameof(reference));
        }

        if (effectiveBaseUri is { IsAbsoluteUri: false })
        {
            throw new ArgumentException("Schema resolution base must be absolute.", nameof(effectiveBaseUri));
        }
        // Optional attempts must make progress through the shared work ledger, even
        // when unavailable. Keep the established mandatory parsing debit unchanged.
        if (!operation.Charge(optional ? 1 : 0, reference.Location))
        {
            throw SchemaSnapshots.Failure(operation);
        }

        if (!Uri.TryCreate(reference.Href, UriKind.Absolute, out var uri) && (effectiveBaseUri is null || !Uri.TryCreate(effectiveBaseUri, reference.Href, out uri)))
        {
            Fail("SCHEMA.ResourceDenied", "Schema reference has no absolute base URI.", reference.Location);
        }

        if (options.Resolver is null)
        {
            Fail("SCHEMA.ResourceDenied", "No schema resolver was supplied.", reference.Location);
        }

        if (resources == (operation.Limits.ExternalResources ?? int.MaxValue))
        {
            Fail("SCHEMA.Limit", "Schema resource count exceeded.", reference.Location);
        }

        using var scope = EnterComposition(reference.Location);
        operation.CancellationToken.ThrowIfCancellationRequested();
        var request = new XmlResourceRequest(uri!, reference.Referrer ?? options.BaseUri, XmlResourceKind.Schema) { SystemId = reference.Href };
        // Resolver exceptions, including XmlParseException, are never schema syntax errors.
        var resolution = options.Resolver!.ResolveResult(request, operation.CancellationToken);
        if (optional && resolution.Status == XmlResourceResolutionStatus.Unavailable)
        {
            if (!operation.Charge(0, reference.Location))
            {
                throw SchemaSnapshots.Failure(operation);
            }

            return null;
        }
        var resource = resolution.Resource;
        if (resolution.Status != XmlResourceResolutionStatus.Resolved || resource is null)
        {
            Fail("SCHEMA.ResourceDenied", "Schema resolver refused the request.", reference.Location);
        }

        resources++;
        bool registered = false, disposed = false;
        try
        {
            operation.CancellationToken.ThrowIfCancellationRequested();
            if (!resource!.Content.CanRead)
            {
                Fail("SCHEMA.ResourceDenied", "Schema resource is not readable.", reference.Location);
            }

            if (!reading.Add(resource.SourceUri))
            {
                Fail("SCHEMA.ResourceCycle", "Reentrant schema resource cycle.", reference.Location with
                {
                    SourceUri = resource.SourceUri
                });
            }

            registered = true;
            var document = parsing.Parse(resource.Content, resource.SourceUri, this);
            operation.CancellationToken.ThrowIfCancellationRequested();
            sources.Add(document, resource.SourceUri);
            return document;
        }
        catch
        {
            // Cleanup cannot replace a primary parser/I/O/cancellation/caller failure.
            disposed = true;
            try
            {
                resource!.Dispose();
            }
            catch { }
            throw;
        }
        finally
        {
            if (registered)
            {
                reading.Remove(resource!.SourceUri);
            }

            if (!disposed)
            {
                resource!.Dispose();
            }
        }
    }

    internal int Read(Stream input, Span<byte> buffer, Uri? sourceUri)
    {
        owner.CancellationToken.ThrowIfCancellationRequested();
        var remaining = (owner.Limits.ExternalBytes ?? long.MaxValue) - externalBytes;
        var allowance = remaining == 0 ? 1 : (int)Math.Min(remaining, buffer.Length);
        var count = input.Read(buffer[..allowance]);
        if ((uint)count > (uint)allowance)
        {
            throw new IOException("Invalid stream read count.");
        }

        if (count > remaining)
        {
            Fail("SCHEMA.Limit", "Schema external byte limit exceeded.", new(sourceUri, 0, 0, null));
        }

        externalBytes += count;
        owner.CancellationToken.ThrowIfCancellationRequested();
        return count;
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void Fail(string code, string message, XmlLocation location)
    {
        owner.Refuse(code, message, location);
        throw SchemaSnapshots.Failure(owner);
    }
}
