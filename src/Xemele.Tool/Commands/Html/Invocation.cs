using System.Globalization;
using Xemele.Html;
namespace Xemele.Tool;
internal sealed class HtmlInvocationException(string message) : Exception(message);
internal sealed class HtmlRejectionException(string message) : Exception(message);
internal sealed class HtmlInvocation
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, string> Namespaces = new(StringComparer.Ordinal);
    internal string? Get(string key) => values.GetValueOrDefault(key);
    internal string Profile = "html5";
    internal bool Scripting;
    internal HtmlSerializationMode Mode;
    internal XmlResourceLimits Limits = new();
    internal HtmlQuirksMode Quirks;
    internal static HtmlInvocation Parse(string command, string[] args)
    {
        var result = new HtmlInvocation();
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "--input", "--output", "--mode", "--max-characters", "--max-depth", "--max-token-characters" };
        if (command != "html-legacy") allowed.Add("--scripting");
        if (command == "html-select") allowed.UnionWith(["--profile", "--selector", "--result", "--css-namespace"]);
        if (command == "html-fragment") allowed.UnionWith(["--context", "--context-namespace", "--quirks", "--context-file", "--context-selector", "--css-namespace"]);
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (!allowed.Contains(key) || key != "--css-namespace" && result.values.ContainsKey(key)) throw new HtmlInvocationException("Unknown, duplicate or inapplicable option: " + key);
            if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal)) throw new HtmlInvocationException("Missing value: " + key);
            if (key == "--css-namespace")
            {
                int separator = args[i].IndexOf('=');
                if (separator < 0 || !result.Namespaces.TryAdd(args[i][..separator], args[i][(separator + 1)..])) throw new HtmlInvocationException("--css-namespace requires a unique PREFIX=URI (empty prefix sets the default type namespace).");
            }
            else result.values.Add(key, args[i]);
        }
        result.Profile = command == "html-legacy" ? "legacy" : result.Get("--profile") ?? "html5";
        if (result.Profile is not ("legacy" or "html5")) throw new HtmlInvocationException("--profile must be legacy or html5.");
        if (result.Profile == "legacy" && result.Get("--scripting") is not null) throw new HtmlInvocationException("--scripting applies only to HTML5.");
        result.Scripting = result.Get("--scripting") switch { null or "false" => false, "true" => true, _ => throw new HtmlInvocationException("--scripting must be true or false (noscript parsing only).") };
        result.Mode = (result.Get("--mode") ?? result.Profile) switch { "legacy" => HtmlSerializationMode.Legacy, "html5" => HtmlSerializationMode.Html5, _ => throw new HtmlInvocationException("--mode must be legacy or html5.") };
        if (result.Get("--max-characters") is { } characters)
        {
            if (!long.TryParse(characters, NumberStyles.None, CultureInfo.InvariantCulture, out long max) || max <= 0) throw new HtmlInvocationException("--max-characters requires a positive integer.");
            result.Limits = result.Limits with { DocumentCharacters = max };
        }
        result.Limits = result.Limits with { Depth = Positive("--max-depth", result.Limits.Depth!.Value), TokenCharacters = Positive("--max-token-characters", result.Limits.TokenCharacters!.Value) };
        if (command == "html-select")
        {
            Require("--selector");
            if (result.Get("--result") is { } kind && kind is not ("markup" or "text")) throw new HtmlInvocationException("--result must be markup or text.");
        }
        if (command == "html-fragment")
        {
            if (result.Get("--context-file") is not null)
            {
                Require("--context-selector");
                if (result.Get("--context") is not null || result.Get("--context-namespace") is not null || result.Get("--quirks") is not null) throw new HtmlInvocationException("Context files preserve their selected element's namespace and document quirks; detached context options are inapplicable.");
            }
            else
            {
                Require("--context");
                if (result.Get("--context-selector") is not null || result.Namespaces.Count != 0) throw new HtmlInvocationException("--context-selector/--css-namespace require --context-file.");
                result.Quirks = result.Get("--quirks") switch { null or "no-quirks" => HtmlQuirksMode.NoQuirks, "quirks" => HtmlQuirksMode.Quirks, "limited-quirks" => HtmlQuirksMode.LimitedQuirks, _ => throw new HtmlInvocationException("--quirks must be no-quirks, limited-quirks or quirks.") };
            }
        }
        return result;
        void Require(string key) { if (string.IsNullOrEmpty(result.Get(key))) throw new HtmlInvocationException("Required option: " + key); }
        int Positive(string key, int fallback)
        {
            if (result.Get(key) is not { } text) return fallback;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0) throw new HtmlInvocationException(key + " requires a positive 32-bit integer.");
            return value;
        }
    }
}
