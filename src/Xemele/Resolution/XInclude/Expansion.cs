using Xemele.Internal.Operations;
namespace Xemele.Resolution.XInclude;

internal sealed partial class IncludeOperation
{
    private async ValueTask Include(XmlNode node, XmlNode parent, Source origin, Uri? baseUri, string language, int inclusionDepth, int depth, Stack<Work> pending)
    {
        if (options.Limits.InclusionDepth is int maximum && inclusionDepth >= maximum) throw new Rejection("Limit", uri: baseUri);
        string parse = node.GetAttribute("parse") ?? "xml";
        if (parse is not ("xml" or "text")) throw new Rejection("Syntax", uri: baseUri);
        if (node.GetAttribute("accept") is not null || node.GetAttribute("accept-language") is not null) throw new Rejection("Unsupported", uri: baseUri);
        string href = node.GetAttribute("href") ?? "";
        if (href.Contains('#')) throw new Rejection("Syntax", uri: baseUri);
        string? expression = node.GetAttribute("xpointer");
        Pointer? pointer = expression is null ? null : ParsePointer(expression);
        if (parse == "text" && (href.Length == 0 || pointer is not null)) throw new Rejection("Syntax", uri: baseUri);
        if (href.Length == 0 && pointer is null) throw new Rejection("Syntax", uri: baseUri);
        XmlNode fallback = default;
        foreach (var child in node.Children)
        {
            token.ThrowIfCancellationRequested();
            if (child.Kind == XmlNodeKind.Element && child.NamespaceUri == Xi && child.LocalName == "fallback")
            {
                if (!fallback.IsDefault) throw new Rejection("Syntax", uri: baseUri);
                fallback = child;
            }
            else if (child.Kind == XmlNodeKind.Element && child.NamespaceUri == Xi) throw new Rejection("Syntax", uri: baseUri);
        }
        // Only load/decode/selection errors select fallback. A later nested fatal
        // rejection never retroactively selects this include's fallback.
        Source target = origin;
        List<XmlNode>? selected = null;
        string? text = null;
        try
        {
            if (href.Length == 0) selected = Select(origin, pointer);
            else
            {
                Uri requestUri;
                if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)) requestUri = absolute;
                else if (baseUri is not null && Uri.TryCreate(baseUri, href, out var resolved)) requestUri = resolved;
                else throw new Rejection("ResourceDenied", true, baseUri);
                Charge(Quota.ExternalResources, 1);
                var request = new XmlResourceRequest(requestUri, baseUri, parse == "xml" ? XmlResourceKind.XIncludeXml : XmlResourceKind.XIncludeText);
                var resource = options.Resolver is null ? null : asynchronous
                    ? await options.Resolver.ResolveAsync(request, token).ConfigureAwait(false)
                    : options.Resolver.Resolve(request, token);
                if (resource is null) throw new Rejection("ResourceDenied", true, requestUri);
                bool failed = false;
                try
                {
                    if (parse == "text") { text = await ReadText(resource, node.GetAttribute("encoding")).ConfigureAwait(false); included = checked(included + 1); }
                    else
                    {
                        var loaded = await ReadXml(resource).ConfigureAwait(false);
                        included = checked(included + 1);
                        Complete(loaded);
                        target = new(loaded, resource.SourceUri.AbsoluteUri, resource.SourceUri, CaptureIds(loaded));
                        selected = Select(target, pointer);
                    }
                }
                catch { failed = true; throw; }
                finally
                {
                    if (failed)
                    {
                        try { if (asynchronous) await resource.DisposeAsync().ConfigureAwait(false); else resource.Dispose(); } catch { }
                    }
                    else if (asynchronous) await resource.DisposeAsync().ConfigureAwait(false);
                    else resource.Dispose();
                }
            }
        }
        catch (Rejection error) when (error.Eligible && !fallback.IsDefault)
        {
            AddDiagnostic("Fallback", XmlDiagnosticSeverity.Warning, error.Uri);
            var fallbackBase = Base(fallback, baseUri);
            string fallbackLanguage = fallback.GetAttribute("lang", Xml) ?? language;
            for (var child = fallback.LastChild; !child.IsDefault; child = child.PreviousSibling)
            {
                token.ThrowIfCancellationRequested();
                pending.Push(new(WorkKind.Insert, child, parent, origin, fallbackBase, fallbackLanguage, checked(inclusionDepth + 1), depth));
            }
            return;
        }
        if (text is not null)
        {
            CheckToken(Scalars(text)); Charge(Quota.ExpandedCharacters, Scalars(text));
            Append(parent, output.CreateText(text)); return;
        }
        for (int index = selected!.Count - 1; index >= 0; index--)
        {
            token.ThrowIfCancellationRequested();
            var item = selected[index];
            var context = Context(item, target.BaseUri);
            pending.Push(new(WorkKind.SelectedInsert, item, parent, target, context.Base, context.Language,
                checked(inclusionDepth + 1), depth, origin, node));
        }
    }
    private (Uri? Base, string Language) Context(XmlNode node, Uri? initial)
    {
        var ancestors = new List<XmlNode>();
        for (var p = node.Parent; !p.IsDefault && p.Kind != XmlNodeKind.Document; p = p.Parent) { token.ThrowIfCancellationRequested(); ancestors.Add(p); }
        string language = "";
        for (int i = ancestors.Count - 1; i >= 0; i--)
        {
            token.ThrowIfCancellationRequested();
            initial = Base(ancestors[i], initial);
            language = ancestors[i].GetAttribute("lang", Xml) ?? language;
        }
        return (initial, language);
    }
    private void FixupInserted(XmlNode item, XmlNode parent, XmlNode previous, Uri? baseUri, string language)
    {
        // Includes may themselves emit several roots. Apply fixups to each new
        // element; their own recursive fixups take precedence over this boundary.
        var inserted = previous.IsDefault ? parent.FirstChild : previous.NextSibling;
        while (!inserted.IsDefault)
        {
            token.ThrowIfCancellationRequested();
            if (inserted.Kind == XmlNodeKind.Element)
            {
                if (item.Kind == XmlNodeKind.Element && !(item.NamespaceUri == Xi && item.LocalName == "include"))
                {
                    var actualBase = Base(item, baseUri);
                    if (options.FixupBaseUris && actualBase is not null) SetFixAttribute(inserted, "base", actualBase.AbsoluteUri);
                    string actualLanguage = item.GetAttribute("lang", Xml) ?? language;
                    if (options.FixupLanguage)
                    {
                        string parentLanguage = "";
                        for (var p = parent; !p.IsDefault && p.Kind != XmlNodeKind.Document; p = p.Parent)
                            if (p.GetAttribute("lang", Xml) is { } lang) { parentLanguage = lang; break; }
                        if (actualLanguage != parentLanguage) SetFixAttribute(inserted, "lang", actualLanguage);
                    }
                    // Selected roots retain the bindings supplied by source ancestors.
                    var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (var p = item; !p.IsDefault && p.Kind != XmlNodeKind.Document; p = p.Parent)
                        foreach (var declaration in p.NamespaceDeclarations) bindings.TryAdd(declaration.Prefix, declaration.Value);
                    foreach (var declaration in bindings) SetFixNamespace(inserted, declaration.Key, declaration.Value);
                    if (inserted.Prefix.Length == 0 && !bindings.ContainsKey("") && DefaultNamespace(parent) != inserted.NamespaceUri) SetFixNamespace(inserted, "", inserted.NamespaceUri);
                }
                if (options.Limits.AttributesPerElement is int max && (long)inserted.Attributes.Count + inserted.NamespaceDeclarations.Count > max) throw new Rejection("Limit");
            }
            inserted = inserted.NextSibling;
        }
    }
    private static string DefaultNamespace(XmlNode node)
    {
        for (var p = node; !p.IsDefault && p.Kind != XmlNodeKind.Document; p = p.Parent)
            foreach (var binding in p.NamespaceDeclarations)
                if (binding.Prefix.Length == 0) return binding.Value;
        return "";
    }
    private void SetFixAttribute(XmlNode node, string name, string value)
    {
        CheckToken(Scalars(value));
        Charge(Quota.ExpandedCharacters, Scalars(value));
        try { node.SetAttribute(name, value, Xml, "xml"); }
        catch (InvalidOperationException) { throw new Rejection("Limit"); }
    }
    private static void SetFixNamespace(XmlNode node, string prefix, string uri)
    {
        try { node.SetNamespaceDeclaration(prefix, uri); }
        catch (InvalidOperationException) { throw new Rejection("Limit"); }
    }
}
