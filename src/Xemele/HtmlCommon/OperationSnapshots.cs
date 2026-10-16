using System.Collections.ObjectModel;
namespace Xemele.Html;
// Per-operation immutable copies: options contain no global mutable state.
internal static class HtmlOperationSnapshots
{
    internal static readonly XmlResourceLimits DefaultLimits = new();
    internal static readonly HtmlParseOptions DefaultLegacyOptions = new() { Limits = DefaultLimits };
    internal static readonly Html5ParseOptions DefaultHtml5Options = new() { Limits = DefaultLimits };
    internal static readonly HtmlTokenizerOptions DefaultTokenizerOptions = new() { Limits = DefaultLimits };
    private static readonly ReadOnlyCollection<HtmlAttribute> EmptyAttributes = Array.AsReadOnly(Array.Empty<HtmlAttribute>());
    static HtmlOperationSnapshots()
    {
        DefaultLimits.Validate();
    }
    internal static HtmlParseOptions Snapshot(HtmlParseOptions? options)
    {
        if (options is null)
        {
            return DefaultLegacyOptions;
        }
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        return options with
        {
            Limits = options.Limits with
            {
            }
        };
    }
    internal static Html5ParseOptions Snapshot(Html5ParseOptions? options)
    {
        if (options is null)
        {
            return DefaultHtml5Options;
        }
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        return options with
        {
            Limits = options.Limits with
            {
            }
        };
    }
    internal static HtmlTokenizerOptions Snapshot(HtmlTokenizerOptions? options)
    {
        if (options is null)
        {
            return DefaultTokenizerOptions;
        }
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        ArgumentNullException.ThrowIfNull(options.LastStartTagName);
        if (!Enum.IsDefined(options.InitialState))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.InitialState is HtmlTokenizerState.RcData or HtmlTokenizerState.RawText or HtmlTokenizerState.ScriptData && options.LastStartTagName.Length == 0)
        {
            throw new ArgumentException("Initial text state requires last start tag name.", nameof(options));
        }

        return options with
        {
            Limits = options.Limits with
            {
            },
            LastStartTagName = HtmlNames.Fold(options.LastStartTagName)
        };
    }
    internal static HtmlFragmentSnapshot Snapshot(HtmlFragmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.ContextElement);
        if (context.ContextElement.Kind != HtmlNodeKind.Element)
        {
            throw new ArgumentException("Fragment context must be an element.", nameof(context));
        }

        if (!Enum.IsDefined(context.QuirksMode))
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }

        var ancestry = new List<HtmlElementSnapshot>();
        for (var node = context.ContextElement; node != null; node = node.Parent)
        {
            if (node.Kind == HtmlNodeKind.Element)
            {
                var rawAttrs = node.RawAttributes;
                IReadOnlyList<HtmlAttribute> attrs = rawAttrs.Count == 0 ? EmptyAttributes : Array.AsReadOnly(rawAttrs.ToArray());
                ancestry.Add(new(node.LocalName, node.NamespaceUri, node.Prefix, attrs));
            }
        }

        return new(context.QuirksMode, ancestry.AsReadOnly());
    }
}
// Ancestors are ordered context first, then nearest through outermost element.
internal sealed record HtmlElementSnapshot(string LocalName, string NamespaceUri, string Prefix, IReadOnlyList<HtmlAttribute> Attributes);
internal sealed record HtmlFragmentSnapshot(HtmlQuirksMode QuirksMode, IReadOnlyList<HtmlElementSnapshot> Ancestry);
