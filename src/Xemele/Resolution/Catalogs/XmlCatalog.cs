using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xemele.Internal.Operations;

namespace Xemele;

public sealed record XmlCatalogOptions
{
    public Uri? BaseUri { get; init; }
    public XmlResourceResolver? Resolver { get; init; }
    public XmlResourceLimits Limits { get; init; } = new();
    public int MaxCatalogDepth { get; init; } = 32;
    public int MaxCatalogDocuments { get; init; } = 128;
}

/// <summary>An eagerly loaded immutable OASIS XML Catalogs 1.0 graph.</summary>
public sealed class XmlCatalog
{
    private const string Namespace = "urn:oasis:names:tc:entity:xmlns:xml:catalog";
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    private sealed record Entry(string Kind, string Key, Uri Target, bool PreferPublic, XmlCatalog? Child);
    private readonly Entry[] entries;
    private XmlCatalog(Entry[] entries) => this.entries = entries;

    public static XmlCatalog Parse(string xml, XmlCatalogOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        if (options.BaseUri is { IsAbsoluteUri: false } || options.MaxCatalogDepth <= 0 || options.MaxCatalogDocuments <= 0)
            throw new ArgumentException("Absolute base and positive catalog quotas required.", nameof(options));
        var snapshot = options with { Limits = options.Limits with { } };
        return new Loader(snapshot, cancellationToken).ParseDocument(xml, snapshot.BaseUri, 1);
    }

    public Uri? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Uri);
        if (!request.Uri.IsAbsoluteUri || request.Referrer is { IsAbsoluteUri: false } || !Enum.IsDefined(request.Kind))
            throw new ArgumentException("Invalid resource request.", nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        bool entity = request.Kind is XmlResourceKind.ExternalEntity or XmlResourceKind.ExternalSubset;
        if (!entity) return Lookup(false, null, null, request.Uri.AbsoluteUri, cancellationToken);
        string? system = request.SystemId, pub = request.PublicId;
        if (pub?.StartsWith("urn:publicid:", StringComparison.OrdinalIgnoreCase) == true) pub = DecodePublicUrn(pub, cancellationToken);
        if (pub is not null) pub = NormalizePublic(pub, cancellationToken);
        if (system?.StartsWith("urn:publicid:", StringComparison.OrdinalIgnoreCase) == true)
        {
            string decoded = DecodePublicUrn(system, cancellationToken);
            if (pub is not null && pub != decoded) throw Error("Syntax", request.Uri);
            pub = decoded; system = null;
        }
        return Lookup(entity, system, pub, request.Uri.AbsoluteUri, cancellationToken);
    }

    private Uri? Lookup(bool entity, string? system, string? pub, string uri, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!entity) return Path("uri", "rewriteURI", "delegateURI", uri, false, null, token);
        if (system is not null)
        {
            Uri? result = Exact("system", system, false, token);
            if (result is not null) return result;
            result = Rewrite("rewriteSystem", system, token);
            if (result is not null) return result;
            if (HasDelegates("delegateSystem", system, false, token)) return Delegate("delegateSystem", system, false, null, token);
        }
        if (pub is not null)
        {
            bool requirePublic = system is not null;
            Uri? result = Exact("public", pub, requirePublic, token);
            if (result is not null) return result;
            if (HasDelegates("delegatePublic", pub, requirePublic, token)) return Delegate("delegatePublic", pub, requirePublic, pub, token);
        }
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Kind == "nextCatalog" && entry.Child!.Lookup(true, system, pub, uri, token) is { } result) return result;
        }
        return null;
    }
    private Uri? Path(string exact, string rewrite, string delegation, string key, bool prefer, string? pub, CancellationToken token)
    {
        Uri? result = Exact(exact, key, prefer, token) ?? Rewrite(rewrite, key, token);
        if (result is not null) return result;
        if (HasDelegates(delegation, key, prefer, token)) return Delegate(delegation, key, prefer, pub, token);
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Kind == "nextCatalog" && entry.Child!.Lookup(false, null, null, key, token) is { } mapped) return mapped;
        }
        return null;
    }
    private Uri? Exact(string kind, string key, bool prefer, CancellationToken token)
    {
        foreach (var e in entries) { token.ThrowIfCancellationRequested(); if (e.Kind == kind && (!prefer || e.PreferPublic) && key == e.Key) return e.Target; }
        return null;
    }
    private Uri? Rewrite(string kind, string key, CancellationToken token)
    {
        Entry? best = null;
        foreach (var e in entries) { token.ThrowIfCancellationRequested(); if (e.Kind == kind && key.AsSpan().StartsWith(e.Key, StringComparison.Ordinal) && (best is null || e.Key.Length > best.Key.Length)) best = e; }
        // Rewriting creates an owned URI result; exact hits return the stored immutable URI.
        return best is null ? null : new Uri(string.Concat(best.Target.AbsoluteUri, key.AsSpan(best.Key.Length)), UriKind.Absolute);
    }
    private bool HasDelegates(string kind, string key, bool prefer, CancellationToken token)
    {
        foreach (var e in entries) { token.ThrowIfCancellationRequested(); if (e.Kind == kind && (!prefer || e.PreferPublic) && key.AsSpan().StartsWith(e.Key, StringComparison.Ordinal)) return true; }
        return false;
    }
    private Uri? Delegate(string kind, string key, bool prefer, string? pub, CancellationToken token)
    {
        // Stable longest-first order is frozen at construction, never sorted during lookup.
        foreach (var e in entries)
        {
            token.ThrowIfCancellationRequested();
            if (e.Kind != kind || (prefer && !e.PreferPublic) || !key.AsSpan().StartsWith(e.Key, StringComparison.Ordinal)) continue;
            Uri? mapped = kind == "delegateURI" ? e.Child!.Lookup(false, null, null, key, token) :
                e.Child!.Lookup(true, kind == "delegateSystem" ? key : null, pub, key, token);
            if (mapped is not null) return mapped;
        }
        return null;
    }
    private static string NormalizePublic(string value, CancellationToken token)
    {
        // Canonical already-normalized lexical keys remain borrowed from their owner.
        bool changed = value.Length != 0 && (IsSpace(value[0]) || IsSpace(value[^1]));
        for (int i = 0; i < value.Length; i++) { if ((i & 4095) == 0) token.ThrowIfCancellationRequested(); if (value[i] is '\t' or '\r' or '\n' || (i > 0 && value[i] == ' ' && value[i - 1] == ' ')) changed = true; }
        if (!changed) return value;
        var result = new StringBuilder(value.Length); bool space = false;
        for (int i = 0; i < value.Length; i++) { if ((i & 4095) == 0) token.ThrowIfCancellationRequested(); char c = value[i]; if (IsSpace(c)) { space = result.Length != 0; continue; } if (space) result.Append(' '); result.Append(c); space = false; }
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
        private int anonymousDocuments;
        internal XmlCatalog ParseDocument(string xml, Uri? source, int depth) => ParseGraph(xml, null, source, depth);
        private XmlCatalog ParseGraph(string? xml, Stream? content, Uri? source, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > options.MaxCatalogDepth) throw Error("Limit", source);
            string? identity = source is null ? null : Identity(source);
            if (identity is not null && !active.Add(identity)) throw Error("Cycle", source);
            try
            {
                if (identity is null ? ++anonymousDocuments + documents.Count > options.MaxCatalogDocuments : documents.Add(identity) && documents.Count + anonymousDocuments > options.MaxCatalogDocuments) throw Error("Limit", source);
                XmlDocument document;
                if (xml is not null)
                {
                    using var core = new XmlParserCore(parser.Options, source, operationBudget: parser);
                    var sink = new DomSink(parser.Options, cancellationToken: token);
                    var result = core.Append(xml.AsSpan(), [], true, sink, token);
                    if ((sink.Diagnostic ?? result.Diagnostic) is { } diagnostic) throw ParserError(diagnostic, source);
                    document = sink.Document;
                }
                else
                {
                    using var adapter = new ByteDomAdapter(parser.Options, token, parser, source);
                    byte[] buffer = new byte[4096];
                    while (adapter.Diagnostic is null)
                    {
                        token.ThrowIfCancellationRequested();
                        long remaining = (options.Limits.ExternalBytes ?? long.MaxValue) - transport.Count(Quota.ExternalBytes);
                        int requested = (int)Math.Min(buffer.Length, Math.Max(1, remaining));
                        int count = content!.Read(buffer.AsSpan(0, requested));
                        if ((uint)count > (uint)requested) throw new IOException("Invalid stream read count.");
                        if (count == 0) { adapter.Complete(); break; }
                        ChargeTransport(Quota.ExternalBytes, count, source);
                        adapter.Append(buffer.AsSpan(0, count));
                    }
                    if (adapter.Diagnostic is { } diagnostic) throw ParserError(diagnostic, source);
                    document = adapter.Document;
                }
                var root = document.RootElement;
                if (root.NamespaceUri != Namespace || root.LocalName != "catalog") throw Error("Syntax", source);
                var entries = new List<Entry>();
                Walk(root, source, true, entries, depth);
                // Sort only delegation slots: nondelegation document order remains untouched.
                var delegates = entries.Where(e => e.Kind.StartsWith("delegate", StringComparison.Ordinal)).OrderByDescending(e => e.Key.Length).ToArray();
                int index = 0;
                for (int i = 0; i < entries.Count; i++) if (entries[i].Kind.StartsWith("delegate", StringComparison.Ordinal)) entries[i] = delegates[index++];
                return new(entries.ToArray());
            }
            finally { if (identity is not null) active.Remove(identity); }
        }
        private void Walk(XmlNode node, Uri? basis, bool prefer, List<Entry> entries, int depth)
        {
            var pending = new Stack<(XmlNode Node, Uri? Basis, bool Prefer)>();
            pending.Push((node, basis, prefer));
            while (pending.TryPop(out var work))
            {
                (node, basis, prefer) = work;
                token.ThrowIfCancellationRequested();
                if (node.Kind != XmlNodeKind.Element || node.NamespaceUri != Namespace) continue;
                if (node.GetAttribute("base", XmlNamespace) is { } b) basis = Absolute(b, basis);
                string kind = node.LocalName;
                if (kind is "catalog" or "group")
                {
                    if (node.GetAttribute("prefer") is { } p) prefer = p switch { "public" => true, "system" => false, _ => throw Error("Syntax", basis) };
                    for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling) pending.Push((child, basis, prefer));
                    continue;
                }
                string? keyName = kind switch { "system" => "systemId", "public" => "publicId", "uri" => "name", "rewriteSystem" or "delegateSystem" => "systemIdStartString", "rewriteURI" or "delegateURI" => "uriStartString", "delegatePublic" => "publicIdStartString", "nextCatalog" => null, _ => throw Error("Unsupported", basis) };
                string key = keyName is null ? "" : node.GetAttribute(keyName) ?? throw Error("Syntax", basis);
                if (kind is "public" or "delegatePublic") key = NormalizePublic(key, token);
                if (kind == "uri" && Uri.TryCreate(key, UriKind.Absolute, out var uriKey)) key = uriKey.AbsoluteUri;
                string targetName = kind.StartsWith("rewrite", StringComparison.Ordinal) ? "rewritePrefix" : kind.StartsWith("delegate", StringComparison.Ordinal) || kind == "nextCatalog" ? "catalog" : "uri";
                Uri target = Absolute(node.GetAttribute(targetName) ?? throw Error("Syntax", basis), basis);
                XmlCatalog? childGraph = null;
                if (targetName == "catalog")
                {
                    ChargeTransport(Quota.ExternalResources, 1, basis);
                    token.ThrowIfCancellationRequested();
                    var resource = options.Resolver?.Resolve(new(target, basis, XmlResourceKind.Catalog), token) ?? throw Error("ResourceDenied", basis);
                    try { childGraph = ParseGraph(null, resource.Content, resource.SourceUri, depth + 1); }
                    catch { try { resource.Dispose(); } catch { } throw; }
                    resource.Dispose();
                }
                entries.Add(new(kind, key, target, prefer, childGraph));
            }
        }
        private static XmlParseException ParserError(XmlDiagnostic diagnostic, Uri? source) =>
            new([diagnostic with { Code = "CATALOG." + (diagnostic.Code.Contains("Limit", StringComparison.OrdinalIgnoreCase) ? "Limit" : "Syntax"), Location = diagnostic.Location with { SourceUri = source } }]);
        private static string Identity(Uri source)
        {
            string absolute = source.AbsoluteUri; int fragment = absolute.IndexOf('#');
            return fragment < 0 ? absolute : absolute[..fragment];
        }
        private void ChargeTransport(Quota quota, long amount, Uri? source) { if (!transport.TryCharge(quota, amount)) throw Error("Limit", source); }
        private static Uri Absolute(string value, Uri? basis)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)) return absolute;
            if (basis is not null && Uri.TryCreate(basis, value, out var relative)) return relative;
            throw Error("Syntax", basis);
        }
    }

}

public sealed class CatalogResolver : XmlResourceResolver
{
    private readonly XmlCatalog catalog;
    private readonly XmlResourceResolver fallback;
    public CatalogResolver(XmlCatalog catalog, XmlResourceResolver fallback)
    { ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(fallback); this.catalog = catalog; this.fallback = fallback; }
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => fallback.Resolve(Map(request, cancellationToken), cancellationToken);
    public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken) => fallback.ResolveAsync(Map(request, cancellationToken), cancellationToken);
    private XmlResourceRequest Map(XmlResourceRequest request, CancellationToken token) => catalog.Resolve(request, token) is { } uri ? request with { Uri = uri } : request;
}
