namespace Xemele.Validation;

internal sealed record XsdValidatedValue(bool IsValid, SchemaName Type, XmlDatatypeValue? Value);
internal interface IXsdIdentitySession
{
    void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation operation);
    void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation operation);
    void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation operation);
    void Complete(ISchemaOperation operation);
}
internal abstract class XsdIdentityProgram
{
}
internal interface IXsdIdentityCompiler
{
    XsdIdentityProgram Compile(XsdCompiledSchema schema, ISchemaOperation operation);
}
internal interface IXsdIdentityFactory
{
    IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document);
}

internal static class IdentityWork
{
    internal static void Tick(ISchemaOperation op, XmlLocation at, long count = 1)
    {
        op.CancellationToken.ThrowIfCancellationRequested();
        if (!op.Charge(count, at))
        {
            throw SchemaSnapshots.Failure(op);
        }
    }
    internal static void Retain(ISchemaOperation op, XmlLocation at, int count = 1)
    {
        Tick(op, at, count);
        if (!op.ChargeComponents(count, at))
        {
            throw SchemaSnapshots.Failure(op);
        }
    }
    internal static void Error(ISchemaOperation op, XmlLocation at, string message)
    {
        Tick(op, at);
        var diagnostic = new XmlDiagnostic("XSD.Identity", XmlDiagnosticSeverity.Error, message, at);
        if (!op.AddDiagnostic(diagnostic))
        {
            throw SchemaSnapshots.Failure(op);
        }
    }
    internal static void Invalid(ISchemaOperation op, XmlLocation at, string message)
    {
        Error(op, at, message);
        throw SchemaSnapshots.Failure(op);
    }
}
