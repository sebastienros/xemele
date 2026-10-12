namespace Xemele.Validation;
internal abstract class XsdContentProgram { }
internal interface IXsdContentCompiler
{
    XsdContentProgram Compile(XsdCompiledSchema schema, XsdTypeDefinition type, ISchemaOperation operation);
}
internal interface IXsdContentCursor
{
    bool Accept(SchemaName childName, ISchemaOperation operation, out int? declarationId, out XsdProcessContents processContents);
    bool Complete(ISchemaOperation operation);
}
internal interface IXsdContentMatcher
{
    IXsdContentCursor CreateCursor(XsdContentProgram program);
}
