namespace Xemele.Validation;

internal interface IXsdEvaluator
{
    ValidationResult Validate(XsdCompiledSchema schema, XmlDocument document, ValidationOptions options,
        IReadOnlyDictionary<SchemaName, XsdContentProgram> contentPrograms, IXsdContentMatcher matcher,
        IXmlFacetEvaluator facets, IXsdIdentityFactory? identityFactory, XsdIdentityProgram? identityProgram);
}
