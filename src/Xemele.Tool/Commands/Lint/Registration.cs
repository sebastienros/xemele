namespace Xemele.Tool;

internal sealed partial class CommandRegistry
{
    partial void RegisterLintCommands()
    {
        Register("lint", "Lint XML/HTML files, directories, or stdin", args => LintCommands.Run(args));
    }
}
