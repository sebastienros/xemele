using System.Globalization;

namespace Xemele.Tool;

internal sealed class SchemaInvocation
{
    private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
    internal string? Get(string key) => values.GetValueOrDefault(key);
    internal long Number(string key, long fallback) => Get(key) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : fallback;

    internal static SchemaInvocation Parse(string command, string[] args)
    {
        var invocation = new SchemaInvocation();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "--schema", "--input", "--schema-base-uri", "--allow-root", "--catalog", "--diagnostics",
            "--compile-max-operations", "--validation-max-operations", "--compile-max-components",
            "--compile-max-resources", "--compile-max-resource-bytes"
        };
        if (command == "validate-schematron") { allowed.Add("--phase"); allowed.Add("--svrl"); }
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (!allowed.Contains(key) || invocation.values.ContainsKey(key))
                throw new InvocationException("Unknown, duplicate or inapplicable option: " + key);
            if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || args[i].Length == 0)
                throw new InvocationException("Missing value: " + key);
            invocation.values.Add(key, args[i]);
        }
        if (invocation.Get("--schema") is null) throw new InvocationException("Required option: --schema");
        if (invocation.Get("--schema-base-uri") is { } uri && !Uri.TryCreate(uri, UriKind.Absolute, out _))
            throw new InvocationException("--schema-base-uri requires an absolute URI.");
        if (invocation.Get("--diagnostics") is { } format && format is not ("text" or "json"))
            throw new InvocationException("--diagnostics requires text or json.");
        foreach (var (key, value) in invocation.values)
        {
            if (!key.Contains("-max-", StringComparison.Ordinal)) continue;
            long maximum = key is "--compile-max-components" or "--compile-max-resources" ? int.MaxValue : long.MaxValue;
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) || number <= 0 || number > maximum)
                throw new InvocationException(key + " requires a positive finite integer.");
        }
        return invocation;
    }
}
