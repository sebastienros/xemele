using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;

namespace Xemele.Resolution.XInclude;

// Source snapshots, working output, names, diagnostic lists and selected paths
// are operation-owned persistence, not allocation-free borrowed primitives.
internal sealed partial class IncludeOperation
{
    internal const string Xi = "http://www.w3.org/2001/XInclude";
    internal const string Xml = "http://www.w3.org/XML/1998/namespace";
    private readonly XmlDocument input;
    private readonly XIncludeOptions options;
    private readonly CancellationToken token;
    private readonly bool asynchronous;
    private readonly OperationBudget budget;
    private readonly List<XmlDiagnostic> diagnostics = [];
    private readonly List<(string Identity, string Path)> active = [];
    private readonly XmlDocument output;
    private int included;
    private sealed record Source(XmlDocument Document, string Identity, Uri? BaseUri, Dictionary<int, HashSet<string>> Ids);
    private sealed class Rejection(string code, bool eligible = false, Uri? uri = null) : Exception
    {
        internal readonly string Code = code;
        internal readonly bool Eligible = eligible;
        internal readonly Uri? Uri = uri;
    }
    internal IncludeOperation(XmlDocument input, XIncludeOptions? options, CancellationToken token, bool asynchronous)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        ArgumentNullException.ThrowIfNull(options.ParseOptions);
        ArgumentNullException.ThrowIfNull(options.ParseOptions.Limits);
        options.Limits.Validate(); options.ParseOptions.Limits.Validate();
        if (options.BaseUri is { IsAbsoluteUri: false }) throw new ArgumentException("BaseUri must be absolute.", nameof(options));
        if (options.ParseOptions.Recover) throw new ArgumentException("XInclude requires strict XML parsing.", nameof(options));
        if (!Enum.IsDefined(options.ParseOptions.DtdProcessing)) throw new ArgumentOutOfRangeException(nameof(options));
        var limits = Minimum(options.Limits, options.ParseOptions.Limits);
        this.options = options with { Limits = limits, ParseOptions = options.ParseOptions with { Limits = limits } };
        this.input = input; this.token = token; this.asynchronous = asynchronous;
        budget = new(this.options.ParseOptions, token);
        output = new(limits);
        // Transformation cannot attest the completeness/provenance of an original source.
        output.MarkSourceIncomplete();
    }
    internal async ValueTask<XIncludeResult> Run()
    {
        token.ThrowIfCancellationRequested();
        try
        {
            Complete(input);
            var captured = new XmlDocument(options.Limits);
            var sourceIds = CaptureIds(input);
            var ids = new Dictionary<int, HashSet<string>>();
            foreach (var item in input.DocumentNode.Children)
            {
                XmlNode copy;
                try { copy = CaptureTree(item, captured); }
                catch (InvalidOperationException) { throw new Rejection("Limit"); }
                captured.DocumentNode.AppendChild(copy);
                CopyIds(item, copy, sourceIds, ids);
            }
            var source = new Source(captured, options.BaseUri?.AbsoluteUri ?? "urn:xemele:include:input", options.BaseUri, ids);
            foreach (var child in captured.DocumentNode.Children)
                await Expand(child, output.DocumentNode, source, options.BaseUri, "", 0, 0).ConfigureAwait(false);
            if (output.RootElement.IsDefault) throw new Rejection("Structure");
            return new(output, diagnostics, included);
        }
        catch (Rejection failure)
        {
            AddDiagnostic(failure.Code, XmlDiagnosticSeverity.Error, failure.Uri);
            return new(null, diagnostics, included);
        }
    }
    private XmlNode CaptureTree(XmlNode source, XmlDocument destination)
    {
        XmlNode root = destination.ImportNode(source, false);
        var pending = new Stack<(XmlNode Source, XmlNode Target)>();
        pending.Push((source, root));
        while (pending.Count != 0)
        {
            token.ThrowIfCancellationRequested();
            var next = pending.Pop();
            foreach (var child in next.Source.Children)
            {
                token.ThrowIfCancellationRequested();
                var copy = destination.ImportNode(child, false);
                next.Target.AppendChild(copy);
                pending.Push((child, copy));
            }
        }
        return root;
    }
    private static void Complete(XmlDocument document)
    {
        if (!document.SourceInputComplete || document.HasIncompleteAttributes || document.UnresolvedReferences.Count != 0)
            throw new Rejection("Incomplete");
    }
    private void Charge(Quota quota, long count)
    {
        token.ThrowIfCancellationRequested();
        if (!budget.TryCharge(quota, count)) throw new Rejection("Limit");
    }
    private void AddDiagnostic(string code, XmlDiagnosticSeverity severity, Uri? uri)
    {
        int max = options.Limits.Diagnostics ?? int.MaxValue;
        var entry = new XmlDiagnostic("XINCLUDE." + code, severity, "XInclude " + code + ".", new(uri, 0, 0, null));
        if (diagnostics.Count == max)
        {
            diagnostics[^1] = new("XINCLUDE.Limit", XmlDiagnosticSeverity.Error, "XInclude diagnostic quota exceeded.", new(uri, 0, 0, null));
            if (severity == XmlDiagnosticSeverity.Warning) throw new Rejection("Limit", uri: uri);
            return;
        }
        diagnostics.Add(entry);
    }
    private async ValueTask Expand(XmlNode node, XmlNode parent, Source source, Uri? inheritedBase, string language, int inclusionDepth, int depth)
    {
        var pending = new Stack<(XmlNode Node, XmlNode Parent, Uri? Base, string Language, int Depth)>();
        pending.Push((node, parent, inheritedBase, language, depth));
        while (pending.Count != 0)
        {
            token.ThrowIfCancellationRequested();
            var next = pending.Pop();
            if (next.Node.Kind != XmlNodeKind.Element)
            {
                Append(next.Parent, CopyShallow(next.Node));
                continue;
            }
            if (options.Limits.Depth is int max && next.Depth >= max) throw new Rejection("Limit");
            var baseUri = Base(next.Node, next.Base);
            string ownLanguage = next.Node.GetAttribute("lang", Xml) ?? next.Language;
            if (next.Node.NamespaceUri == Xi)
            {
                if (next.Node.LocalName == "fallback") throw new Rejection("Syntax");
                if (next.Node.LocalName == "include")
                {
                    await Include(next.Node, next.Parent, source, baseUri, ownLanguage, inclusionDepth, next.Depth).ConfigureAwait(false);
                    continue;
                }
            }
            var copy = CopyShallow(next.Node);
            Append(next.Parent, copy);
            for (var child = next.Node.LastChild; !child.IsDefault; child = child.PreviousSibling)
                pending.Push((child, copy, baseUri, ownLanguage, checked(next.Depth + 1)));
        }
    }
    private XmlNode CopyShallow(XmlNode source)
    {
        if (source.Kind == XmlNodeKind.Element)
        {
            if (options.Limits.AttributesPerElement is int max && source.Attributes.Count > max) throw new Rejection("Limit");
            CheckToken(source.LocalName.Length);
            foreach (var attribute in source.Attributes) { CheckToken(attribute.Value.Length); Charge(Quota.ExpandedCharacters, attribute.Value.Length); }
        }
        else { CheckToken(source.Value.Length); Charge(Quota.ExpandedCharacters, source.Value.Length); }
        try { return output.ImportNode(source, false); }
        catch (InvalidOperationException) { throw new Rejection("Limit"); }
    }
    private void CheckToken(int count) { if (options.Limits.TokenCharacters is int max && count > max) throw new Rejection("Limit"); }
    private static void Append(XmlNode parent, XmlNode node)
    {
        try { parent.AppendChild(node); }
        catch (ArgumentException) { throw new Rejection("Structure"); }
    }
    private static Uri? Base(XmlNode node, Uri? inherited)
    {
        string? lexical = node.GetAttribute("base", Xml);
        if (lexical is null) return inherited;
        if (Uri.TryCreate(lexical, UriKind.Absolute, out var absolute)) return absolute;
        if (inherited is not null && Uri.TryCreate(inherited, lexical, out var resolved)) return resolved;
        throw new Rejection("Syntax");
    }
    private static XmlResourceLimits Minimum(XmlResourceLimits a, XmlResourceLimits b) => new()
    {
        DocumentCharacters = Min(a.DocumentCharacters, b.DocumentCharacters), Depth = MinInt(a.Depth, b.Depth),
        AttributesPerElement = MinInt(a.AttributesPerElement, b.AttributesPerElement), TokenCharacters = MinInt(a.TokenCharacters, b.TokenCharacters),
        InternedNames = MinInt(a.InternedNames, b.InternedNames), EntityDepth = MinInt(a.EntityDepth, b.EntityDepth),
        ExpandedCharacters = Min(a.ExpandedCharacters, b.ExpandedCharacters), ExternalBytes = Min(a.ExternalBytes, b.ExternalBytes),
        ExternalResources = MinInt(a.ExternalResources, b.ExternalResources), InclusionDepth = MinInt(a.InclusionDepth, b.InclusionDepth),
        Diagnostics = MinInt(a.Diagnostics, b.Diagnostics), EvaluationWork = Min(a.EvaluationWork, b.EvaluationWork)
    };
    private static long? Min(long? a, long? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
    private static int? MinInt(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
}
