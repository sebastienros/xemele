namespace Xemele.Tool;

internal sealed class LintInvocation
{
    internal List<string> Inputs { get; } = [];
    internal string Type { get; private set; } = "auto";
    internal bool AllowDtd { get; private set; }

    internal static LintInvocation Parse(string[] args)
    {
        var result = new LintInvocation();
        var positional = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (!positional && argument == "--")
            {
                positional = true;
            }
            else if (!positional && argument.StartsWith('-') && argument != "-")
            {
                if (argument is not ("--type" or "--allow-dtd") || !seen.Add(argument))
                {
                    throw new InvocationException("Unknown or duplicate option: " + argument);
                }

                if (argument == "--allow-dtd")
                {
                    result.AllowDtd = true;
                    continue;
                }

                if (++i == args.Length || args[i] is not ("auto" or "xml" or "html"))
                {
                    throw new InvocationException("--type requires auto, xml, or html.");
                }

                result.Type = args[i];
            }
            else
            {
                if (string.IsNullOrWhiteSpace(argument))
                {
                    throw new InvocationException("Input paths must not be empty.");
                }

                result.Inputs.Add(argument);
            }
        }

        if (result.Inputs.Count == 0)
        {
            result.Inputs.Add("-");
        }

        if (result.Inputs.Count(input => input == "-") > 1)
        {
            throw new InvocationException("Standard input may only be specified once.");
        }

        if (result.Type == "html" && result.AllowDtd)
        {
            throw new InvocationException("--allow-dtd applies only to XML.");
        }

        return result;
    }
}
