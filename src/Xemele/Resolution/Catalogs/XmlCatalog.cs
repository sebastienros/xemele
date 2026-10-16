using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xemele.Internal.Operations;

namespace Xemele;

/// <summary>Configures eager catalog loading and its resource quotas.</summary>
public sealed record XmlCatalogOptions
{
    /// <summary>Gets the absolute base URI for resolving relative references in the initial catalog, or null when unavailable.</summary>
    public Uri? BaseUri
    {
        get; init;
    }
    /// <summary>Gets the resolver used to load referenced catalog documents.</summary>
    public XmlResourceResolver? Resolver
    {
        get; init;
    }
    /// <summary>Gets the resource limits applied while loading catalogs.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets the positive maximum catalog reference depth; the initial catalog has depth one. Defaults to 32.</summary>
    public int MaxCatalogDepth { get; init; } = 32;
    /// <summary>Gets the positive maximum number of catalog documents loaded. Defaults to 128.</summary>
    public int MaxCatalogDocuments { get; init; } = 128;
}

/// <summary>An eagerly loaded immutable OASIS XML Catalogs 1.0 graph.</summary>
public sealed class XmlCatalog
{
    private const string Namespace = "urn:oasis:names:tc:entity:xmlns:xml:catalog";
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    private sealed record Entry(string Kind, string Key, Uri Target, bool PreferPublic, int Child);
    private sealed record CatalogNode(Entry[] Entries);
    private readonly CatalogNode[] nodes;
    private readonly int maximumDepth;
    private XmlCatalog(CatalogNode[] nodes, int maximumDepth)
    {
        this.nodes = nodes;
        this.maximumDepth = maximumDepth;
    }

    /// <summary>Parses a catalog and eagerly loads its referenced catalogs.</summary>
    /// <param name="xml">The initial catalog's XML characters.</param>
    /// <param name="options">Loading options, or null for defaults.</param>
    /// <param name="cancellationToken">A token for canceling catalog loading.</param>
    /// <returns>An immutable catalog graph.</returns>
    /// <exception cref="ArgumentNullException">The XML or resource limits are null.</exception>
    /// <exception cref="ArgumentException">The base URI is relative or a catalog quota is not positive.</exception>
    public static XmlCatalog Parse(string xml, XmlCatalogOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        if (options.BaseUri is { IsAbsoluteUri: false } || options.MaxCatalogDepth <= 0 || options.MaxCatalogDocuments <= 0)
        {
            throw new ArgumentException("Absolute base and positive catalog quotas required.", nameof(options));
        }

        var snapshot = options with
        {
            Limits = options.Limits with
            {
            }
        };
        return new Loader(snapshot, cancellationToken).ParseDocument(xml, snapshot.BaseUri, 1);
    }

    /// <summary>Looks up a resource URI using the loaded catalog without opening the resource.</summary>
    /// <param name="request">The absolute resource request and any original public or system identifiers.</param>
    /// <param name="cancellationToken">A token for canceling lookup.</param>
    /// <returns>The mapped URI, or null when no catalog entry matches.</returns>
    /// <exception cref="ArgumentNullException">The request or its URI is null.</exception>
    /// <exception cref="ArgumentException">A URI is relative or the resource kind is invalid.</exception>
    public Uri? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Uri);
        if (!request.Uri.IsAbsoluteUri || request.Referrer is { IsAbsoluteUri: false } || !Enum.IsDefined(request.Kind))
        {
            throw new ArgumentException("Invalid resource request.", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entity = request.Kind is XmlResourceKind.ExternalEntity or XmlResourceKind.ExternalSubset;
        if (!entity)
        {
            return Lookup(false, null, null, request.Uri.AbsoluteUri, cancellationToken);
        }

        string? system = request.SystemId, pub = request.PublicId;
        if (pub?.StartsWith("urn:publicid:", StringComparison.OrdinalIgnoreCase) == true)
        {
            pub = DecodePublicUrn(pub, cancellationToken);
        }

        if (pub is not null)
        {
            pub = NormalizePublic(pub, cancellationToken);
        }

        if (system?.StartsWith("urn:publicid:", StringComparison.OrdinalIgnoreCase) == true)
        {
            var decoded = DecodePublicUrn(system, cancellationToken);
            if (pub is not null && pub != decoded)
            {
                throw Error("Syntax", request.Uri);
            }

            pub = decoded;
            system = null;
        }
        return Lookup(entity, system, pub, request.Uri.AbsoluteUri, cancellationToken);
    }

    private enum Route : byte
    {
        Next, System, Public, Uri
    }
    private struct LookupFrame(int node, bool entity, bool system, bool pub)
    {
        internal int Node = node, Index;
        internal bool Entity = entity, System = system, Public = pub;
        internal Route Route;
        internal bool Prefer;
    }
    private Uri? Lookup(bool entity, string? system, string? pub, string uri, CancellationToken token)
    {
        var root = new LookupFrame(0, entity, system is not null, pub is not null);
        if (Prepare(ref root, system, pub, uri, token) is { } immediate)
        {
            return immediate;
        }

        if (!Next(ref root, system, pub, uri, token, out var current))
        {
            return null;
        }

        LookupFrame[]? rented = null;
        Span<LookupFrame> stack = stackalloc LookupFrame[32];
        if (maximumDepth > stack.Length)
        {
            stack = rented = System.Buffers.ArrayPool<LookupFrame>.Shared.Rent(maximumDepth);
        }

        var count = 1;
        stack[0] = root;
        try
        {
            while (true)
            {
                if (Prepare(ref current, system, pub, uri, token) is { } mapped)
                {
                    return mapped;
                }

                if (Next(ref current, system, pub, uri, token, out var child))
                {
                    stack[count++] = current;
                    current = child;
                    continue;
                }
                var resume = false;
                while (count != 0)
                {
                    if (Next(ref stack[count - 1], system, pub, uri, token, out current))
                    {
                        resume = true;
                        break;
                    }
                    count--;
                }
                if (!resume)
                {
                    return null;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<LookupFrame>.Shared.Return(rented);
            }
        }
    }
    private Uri? Prepare(ref LookupFrame frame, string? system, string? pub, string uri, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var entries = nodes[frame.Node].Entries;
        frame.Route = Route.Next;
        if (!frame.Entity)
        {
            var mapped = Exact(entries, "uri", uri, false, token) ?? Rewrite(entries, "rewriteURI", uri, token);
            if (mapped is not null)
            {
                return mapped;
            }

            if (HasDelegates(entries, "delegateURI", uri, false, token))
            {
                frame.Route = Route.Uri;
            }

            return null;
        }
        if (frame.System)
        {
            var mapped = Exact(entries, "system", system!, false, token) ?? Rewrite(entries, "rewriteSystem", system!, token);
            if (mapped is not null)
            {
                return mapped;
            }

            if (HasDelegates(entries, "delegateSystem", system!, false, token))
            {
                frame.Route = Route.System;
                return null;
            }
        }
        if (frame.Public)
        {
            var mapped = Exact(entries, "public", pub!, frame.System, token);
            if (mapped is not null)
            {
                return mapped;
            }

            if (HasDelegates(entries, "delegatePublic", pub!, frame.System, token))
            {
                frame.Route = Route.Public;
                frame.Prefer = frame.System;
            }
        }
        return null;
    }
    private bool Next(ref LookupFrame frame, string? system, string? pub, string uri, CancellationToken token, out LookupFrame child)
    {
        var entries = nodes[frame.Node].Entries;
        while (frame.Index < entries.Length)
        {
            token.ThrowIfCancellationRequested();
            var e = entries[frame.Index++];
            var eligible = frame.Route switch
            {
                Route.Next => e.Kind == "nextCatalog",
                Route.System => e.Kind == "delegateSystem" && system!.AsSpan().StartsWith(e.Key, StringComparison.Ordinal),
                Route.Public => e.Kind == "delegatePublic" && (!frame.Prefer || e.PreferPublic) && pub!.AsSpan().StartsWith(e.Key, StringComparison.Ordinal),
                Route.Uri => e.Kind == "delegateURI" && uri.AsSpan().StartsWith(e.Key, StringComparison.Ordinal),
                _ => false
            };
            if (!eligible)
            {
                continue;
            }

            child = frame.Route switch
            {
                Route.System => new(e.Child, true, true, false),
                Route.Public => new(e.Child, true, false, true),
                Route.Uri => new(e.Child, false, false, false),
                _ => new(e.Child, frame.Entity, frame.System, frame.Public)
            };
            return true;
        }
        child = default;
        return false;
    }
    private static Uri? Exact(Entry[] entries, string kind, string key, bool prefer, CancellationToken token)
    {
        foreach (var e in entries)
        {
            token.ThrowIfCancellationRequested();
            if (e.Kind == kind && (!prefer || e.PreferPublic) && key == e.Key)
            {
                return e.Target;
            }
        }
        return null;
    }
    private static Uri? Rewrite(Entry[] entries, string kind, string key, CancellationToken token)
    {
        Entry? best = null;
        foreach (var e in entries)
        {
            token.ThrowIfCancellationRequested();
            if (e.Kind == kind && key.AsSpan().StartsWith(e.Key, StringComparison.Ordinal) && (best is null || e.Key.Length > best.Key.Length))
            {
                best = e;
            }
        }
        // Rewriting creates an owned URI result; exact hits return the stored immutable URI.
        return best is null ? null : new Uri(string.Concat(best.Target.AbsoluteUri, key.AsSpan(best.Key.Length)), UriKind.Absolute);
    }
    private static bool HasDelegates(Entry[] entries, string kind, string key, bool prefer, CancellationToken token)
    {
        foreach (var e in entries)
        {
            token.ThrowIfCancellationRequested();
            if (e.Kind == kind && (!prefer || e.PreferPublic) && key.AsSpan().StartsWith(e.Key, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
    private static string NormalizePublic(string value, CancellationToken token)
    {
        // Canonical already-normalized lexical keys remain borrowed from their owner.
        var changed = value.Length != 0 && (IsSpace(value[0]) || IsSpace(value[^1]));
        for (var i = 0; i < value.Length; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (value[i] is '\t' or '\r' or '\n' || (i > 0 && value[i] == ' ' && value[i - 1] == ' '))
            {
                changed = true;
            }
        }
        if (!changed)
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        var space = false;
        for (var i = 0; i < value.Length; i++)
        {
            if ((i & 4095) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var c = value[i];
            if (IsSpace(c))
            {
                space = result.Length != 0;
                continue;
            }
            if (space)
            {
                result.Append(' ');
            }

            result.Append(c);
            space = false;
        }
        return result.ToString();
    }
    private static bool IsSpace(char c) => c is ' ' or '\t' or '\r' or '\n';
    private static string DecodePublicUrn(string value, CancellationToken token) => NormalizePublic(Uri.UnescapeDataString(value[13..].Replace("+", " ", StringComparison.Ordinal).Replace(":", "//", StringComparison.Ordinal).Replace(";", "::", StringComparison.Ordinal)), token);
    private static XmlParseException Error(string code, Uri? source) => new([new("CATALOG." + code, XmlDiagnosticSeverity.Fatal, "Catalog " + code + ".", new(source, 0, 0, null))]);

    private sealed class Loader(XmlCatalogOptions options, CancellationToken token)
    {
        // Transport counts attempts/bytes once; parser owns exact scalar/expansion/token/depth accounting.
        private readonly OperationBudget transport = new(new() { Limits = options.Limits }, token);
        private readonly OperationBudget parser = new(new() { Limits = options.Limits with { ExternalBytes = null, ExternalResources = null } }, token);
        private readonly HashSet<string> active = new(StringComparer.Ordinal);
        private readonly HashSet<string> documents = new(StringComparer.Ordinal);
        private readonly List<CatalogNode> nodes = [];
        private int anonymousDocuments, maximumDepth;
        private sealed class BuildFrame(int index, int depth, string? identity, XmlNode root, Uri? source)
        {
            internal readonly int Index = index, Depth = depth;
            internal readonly string? Identity = identity;
            internal readonly Stack<(XmlNode Node, Uri? Basis, bool Prefer)> Pending = new([(root, source, true)]);
            internal readonly List<Entry> Entries = [];
        }
        internal XmlCatalog ParseDocument(string xml, Uri? source, int depth)
        {
            var pending = new Stack<BuildFrame>();
            try
            {
                pending.Push(CreateFrame(xml, null, source, depth));
                while (pending.TryPeek(out var frame))
                {
                    token.ThrowIfCancellationRequested();
                    if (!ReadEntry(frame, out var entry, out var basis))
                    {
                        nodes[frame.Index] = new(SortDelegates(frame.Entries));
                        if (frame.Identity is not null)
                        {
                            active.Remove(frame.Identity);
                        }

                        pending.Pop();
                        continue;
                    }
                    if (entry.Kind is "delegateSystem" or "delegatePublic" or "delegateURI" or "nextCatalog")
                    {
                        ChargeTransport(Quota.ExternalResources, 1, basis);
                        token.ThrowIfCancellationRequested();
                        var resource = options.Resolver?.Resolve(new(entry.Target, basis, XmlResourceKind.Catalog), token) ?? throw Error("ResourceDenied", basis);
                        BuildFrame child;
                        try
                        {
                            child = CreateFrame(null, resource.Content, resource.SourceUri, checked(frame.Depth + 1));
                        }
                        catch { try { resource.Dispose(); } catch { } throw; }
                        // Content is fully parsed and owned before nested acquisition; no live stream spans graph frames.
                        resource.Dispose();
                        frame.Entries.Add(entry with
                        {
                            Child = child.Index
                        });
                        pending.Push(child);
                    }
                    else
                    {
                        frame.Entries.Add(entry);
                    }
                }
                return new([.. nodes], maximumDepth);
            }
            finally { active.Clear(); }
        }
        private BuildFrame CreateFrame(string? xml, Stream? content, Uri? source, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > options.MaxCatalogDepth)
            {
                throw Error("Limit", source);
            }

            var identity = source is null ? null : Identity(source);
            if (identity is not null && !active.Add(identity))
            {
                throw Error("Cycle", source);
            }

            try
            {
                if (identity is null ? ++anonymousDocuments + documents.Count > options.MaxCatalogDocuments : documents.Add(identity) && documents.Count + anonymousDocuments > options.MaxCatalogDocuments)
                {
                    throw Error("Limit", source);
                }

                XmlDocument document;
                if (xml is not null)
                {
                    using var core = new XmlParserCore(parser.Options, source, operationBudget: parser);
                    var sink = new DomSink(parser.Options, cancellationToken: token);
                    var result = core.Append(xml.AsSpan(), [], true, sink, token);
                    if ((sink.Diagnostic ?? result.Diagnostic) is { } diagnostic)
                    {
                        throw ParserError(diagnostic, source);
                    }

                    document = sink.Document;
                }
                else
                {
                    using var adapter = new ByteDomAdapter(parser.Options, token, parser, source);
                    var buffer = new byte[4096];
                    while (adapter.Diagnostic is null)
                    {
                        token.ThrowIfCancellationRequested();
                        var remaining = (options.Limits.ExternalBytes ?? long.MaxValue) - transport.Count(Quota.ExternalBytes);
                        var requested = (int)Math.Min(buffer.Length, Math.Max(1, remaining));
                        var count = content!.Read(buffer.AsSpan(0, requested));
                        if ((uint)count > (uint)requested)
                        {
                            throw new IOException("Invalid stream read count.");
                        }

                        if (count == 0)
                        {
                            adapter.Complete();
                            break;
                        }
                        ChargeTransport(Quota.ExternalBytes, count, source);
                        adapter.Append(buffer.AsSpan(0, count));
                    }
                    if (adapter.Diagnostic is { } diagnostic)
                    {
                        throw ParserError(diagnostic, source);
                    }

                    document = adapter.Document;
                }
                var root = document.RootElement;
                if (root.NamespaceUri != Namespace || root.LocalName != "catalog")
                {
                    throw Error("Syntax", source);
                }

                maximumDepth = Math.Max(maximumDepth, depth);
                var index = nodes.Count;
                nodes.Add(null!);
                return new(index, depth, identity, root, source);
            }
            catch { if (identity is not null) { active.Remove(identity); } throw; }
        }
        private bool ReadEntry(BuildFrame frame, out Entry entry, out Uri? basis)
        {
            while (frame.Pending.TryPop(out var work))
            {
                var (node, inheritedBase, prefer) = work;
                basis = inheritedBase;
                token.ThrowIfCancellationRequested();
                if (node.Kind != XmlNodeKind.Element || node.NamespaceUri != Namespace)
                {
                    continue;
                }

                if (node.GetAttribute("base", XmlNamespace) is { } b)
                {
                    basis = Absolute(b, basis);
                }

                var kind = node.LocalName;
                if (kind is "catalog" or "group")
                {
                    if (node.GetAttribute("prefer") is { } p)
                    {
                        prefer = p switch
                        {
                            "public" => true,
                            "system" => false,
                            _ => throw Error("Syntax", basis)
                        };
                    }

                    for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling)
                    {
                        token.ThrowIfCancellationRequested();
                        frame.Pending.Push((child, basis, prefer));
                    }
                    continue;
                }
                var keyName = kind switch
                {
                    "system" => "systemId",
                    "public" => "publicId",
                    "uri" => "name",
                    "rewriteSystem" or "delegateSystem" => "systemIdStartString",
                    "rewriteURI" or "delegateURI" => "uriStartString",
                    "delegatePublic" => "publicIdStartString",
                    "nextCatalog" => null,
                    _ => throw Error("Unsupported", basis)
                };
                var key = keyName is null ? "" : node.GetAttribute(keyName) ?? throw Error("Syntax", basis);
                if (kind is "public" or "delegatePublic")
                {
                    key = NormalizePublic(key, token);
                }

                if (kind == "uri" && Uri.TryCreate(key, UriKind.Absolute, out var uriKey))
                {
                    key = uriKey.AbsoluteUri;
                }

                var targetName = kind.StartsWith("rewrite", StringComparison.Ordinal) ? "rewritePrefix" : kind.StartsWith("delegate", StringComparison.Ordinal) || kind == "nextCatalog" ? "catalog" : "uri";
                var target = Absolute(node.GetAttribute(targetName) ?? throw Error("Syntax", basis), basis);
                entry = new(kind, key, target, prefer, -1);
                return true;
            }
            entry = null!;
            basis = null;
            return false;
        }
        private Entry[] SortDelegates(List<Entry> entries)
        {
            var delegates = new List<Entry>();
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.Kind.StartsWith("delegate", StringComparison.Ordinal))
                {
                    delegates.Add(entry);
                }
            }
            var sorted = CatalogSort.StableSort(delegates.ToArray(), static (left, right) => right.Key.Length.CompareTo(left.Key.Length), token);
            var result = new Entry[entries.Count];
            var index = 0;
            for (var i = 0; i < result.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                result[i] = entries[i].Kind.StartsWith("delegate", StringComparison.Ordinal) ? sorted[index++] : entries[i];
            }
            return result;
        }
        private static XmlParseException ParserError(XmlDiagnostic diagnostic, Uri? source) =>
            new([diagnostic with { Code = "CATALOG." + (diagnostic.Code.Contains("Limit", StringComparison.OrdinalIgnoreCase) ? "Limit" : "Syntax"), Location = diagnostic.Location with { SourceUri = source } }]);
        private static string Identity(Uri source)
        {
            var absolute = source.AbsoluteUri;
            var fragment = absolute.IndexOf('#');
            return fragment < 0 ? absolute : absolute[..fragment];
        }
        private void ChargeTransport(Quota quota, long amount, Uri? source)
        {
            if (!transport.TryCharge(quota, amount))
            {
                throw Error("Limit", source);
            }
        }
        private static Uri Absolute(string value, Uri? basis)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            {
                return absolute;
            }

            if (basis is not null && Uri.TryCreate(basis, value, out var relative))
            {
                return relative;
            }

            throw Error("Syntax", basis);
        }
    }

}

/// <summary>Maps requests through an XML catalog before passing them to another resolver.</summary>
public sealed class CatalogResolver : XmlResourceResolver
{
    private readonly XmlCatalog catalog;
    private readonly XmlResourceResolver fallback;
    /// <summary>Creates a catalog-mapping resolver.</summary>
    /// <param name="catalog">The catalog used to map request URIs.</param>
    /// <param name="fallback">The resolver that opens mapped resources, or the original request when no entry matches.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public CatalogResolver(XmlCatalog catalog, XmlResourceResolver fallback)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(fallback);
        this.catalog = catalog;
        this.fallback = fallback;
    }
    /// <inheritdoc/>
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => fallback.Resolve(Map(request, cancellationToken), cancellationToken);
    /// <inheritdoc/>
    public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken) => fallback.ResolveAsync(Map(request, cancellationToken), cancellationToken);
    private XmlResourceRequest Map(XmlResourceRequest request, CancellationToken token) => catalog.Resolve(request, token) is { } uri ? request with { Uri = uri } : request;
}
