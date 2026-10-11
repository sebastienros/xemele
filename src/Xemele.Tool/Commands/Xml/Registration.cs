namespace Xemele.Tool;
internal sealed partial class CommandRegistry
{
    partial void RegisterXmlCommands()
    {
        Register("parse", "Parse XML and emit compact XML", args => XmlCommands.Run("parse", args));
        Register("format", "Format XML", args => XmlCommands.Run("format", args));
        Register("xpath", "Evaluate XPath 1.0", args => XmlCommands.Run("xpath", args));
        Register("validate-dtd", "Validate XML against a DTD", args => XmlCommands.Run("validate-dtd", args));
        Register("canonicalize", "Canonicalize XML", args => XmlCommands.Run("canonicalize", args));
        Register("catalog", "Resolve a catalog identifier", args => XmlCommands.Run("catalog", args));
        Register("xinclude", "Expand XInclude", args => XmlCommands.Run("xinclude", args));
    }
}
