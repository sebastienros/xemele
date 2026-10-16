namespace Xemele.Tool;

internal sealed partial class CommandRegistry
{
    partial void RegisterHtmlCommands()
    {
        Register("html-legacy", "Parse with legacy HTML recovery", args => HtmlCommands.Run("html-legacy", args));
        Register("html5", "Parse with the HTML5 algorithm", args => HtmlCommands.Run("html5", args));
        Register("html-fragment", "Parse an HTML5 fragment in a real context", args => HtmlCommands.Run("html-fragment", args));
        Register("html-select", "Select HTML elements using CSS", args => HtmlCommands.Run("html-select", args));
    }
}
