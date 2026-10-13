namespace Xemele.Tool;

internal sealed partial class CommandRegistry
{
    partial void RegisterValidationCommands()
    {
        Register("validate-rng", "Validate XML against a RELAX NG XML schema", args => SchemaCommands.Run("validate-rng", args));
        Register("validate-xsd", "Validate XML against an XML Schema 1.0 schema", args => SchemaCommands.Run("validate-xsd", args));
        Register("validate-schematron", "Validate XML using ISO 2016 Schematron", args => SchemaCommands.Run("validate-schematron", args));
    }
}
