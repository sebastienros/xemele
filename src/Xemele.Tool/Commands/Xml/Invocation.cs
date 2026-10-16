namespace Xemele.Tool;

internal sealed class InvocationException(string message) : Exception(message);
internal sealed class RejectionException(string message) : Exception(message);
internal sealed class XmlInvocation
{
    internal readonly Dictionary<string, string> Values = new(StringComparer.Ordinal);
    internal readonly HashSet<string> Flags = new(StringComparer.Ordinal);
    internal string? Get(string key) => Values.GetValueOrDefault(key);
    internal bool Has(string key) => Flags.Contains(key);
    internal static XmlInvocation Parse(string command, string[] args)
    {
        var result = new XmlInvocation();
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "--input", "--output", "--base-uri", "--allow-root", "--catalog" };
        if (command != "catalog")
        {
            allowed.Add("--allow-dtd");
        }

        if (command == "xpath")
        {
            allowed.Add("--expression");
        }

        if (command == "validate-dtd")
        {
            allowed.Add("--dtd");
        }

        if (command == "canonicalize")
        {
            allowed.Add("--exclusive");
            allowed.Add("--with-comments");
        }
        if (command == "catalog")
        {
            allowed.Add("--uri");
            allowed.Add("--system-id");
            allowed.Add("--public-id");
        }
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!allowed.Contains(key) || result.Values.ContainsKey(key) || result.Flags.Contains(key))
            {
                throw new InvocationException("Unknown, duplicate or inapplicable option: " + key);
            }

            if (key is "--allow-dtd" or "--exclusive" or "--with-comments")
            {
                result.Flags.Add(key);
            }
            else
            {
                if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new InvocationException("Missing value: " + key);
                }

                result.Values.Add(key, args[i]);
            }
        }
        Require(command == "xpath", "--expression");
        Require(command == "validate-dtd", "--dtd");
        Require(command == "catalog", "--catalog");
        Require(command == "catalog", "--uri");
        if (result.Get("--base-uri") is { } baseText && !Uri.TryCreate(baseText, UriKind.Absolute, out _))
        {
            throw new InvocationException("--base-uri requires an absolute URI.");
        }

        if (result.Get("--uri") is { } uri && !Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            throw new InvocationException("--uri requires an absolute URI.");
        }

        return result;
        void Require(bool required, string key)
        {
            if (required && string.IsNullOrEmpty(result.Get(key)))
            {
                throw new InvocationException("Required option: " + key);
            }
        }
    }
}
