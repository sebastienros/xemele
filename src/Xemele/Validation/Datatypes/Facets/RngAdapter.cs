namespace Xemele.Validation;

// Internal join protocol, not an extension of the frozen
// interface. A null definition on success means the existing empty library is
// used directly, after an explicit no-parameter check at this adapter boundary.
internal sealed class RngDatatypeFacetAdapter(IXmlFacetEvaluator evaluator)
{
    internal bool ValidateParameters(SchemaName datatype, IReadOnlyList<XmlFacet> parameters, XmlLocation location,
        IXmlDatatypeRegistry registry, ISchemaOperation operation, out XmlDatatypeDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(operation);
        definition = null;
        if (!operation.Charge(1, location))
        {
            return false;
        }

        if (!DatatypeNormalization.TryMeasure(datatype.NamespaceUri, XmlWhitespaceRule.Preserve, operation, out _, out _, out _) ||
            !DatatypeNormalization.TryMeasure(datatype.LocalName, XmlWhitespaceRule.Preserve, operation, out _, out _, out _))
        {
            return operation.IsComplete && Reject(operation, location, "Invalid Unicode in datatype name.");
        }

        var library = datatype.NamespaceUri is "" or DatatypeTypes.RngNamespace ? registry.Find(datatype.NamespaceUri) : null;
        if (library is null || !library.IsDefined(datatype))
        {
            return Reject(operation, location, "Unknown RELAX NG datatype library or datatype.");
        }

        if (datatype.NamespaceUri == "")
        {
            if (parameters.Count != 0)
            {
                return Reject(operation, location, "The empty datatype library accepts no parameters.");
            }

            return operation.Charge(0, location);
        }
        var canonical = registry.FindType(DatatypeTypes.Name(datatype.LocalName));
        if (canonical is null)
        {
            return Reject(operation, location, "Missing canonical datatype definition.");
        }

        if (parameters.Count == int.MaxValue)
        {
            operation.Refuse("SCHEMA.Limit", "Datatype parameter representation exceeded.", location);
            return false;
        }
        if (!operation.ChargeComponents(parameters.Count + 1, location))
        {
            return false;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            if (!operation.Charge(1, parameters[i].Location))
            {
                return false;
            }

            if (parameters[i].Fixed)
            {
                return Reject(operation, parameters[i].Location, "RELAX NG parameters do not have a fixed property.");
            }
        }
        var candidate = new XmlDatatypeDefinition(datatype, canonical.Variety, canonical.Name, null, [], parameters, [], false, location);
        if (!evaluator.ValidateDefinition(candidate, registry, operation))
        {
            return false;
        }

        if (!operation.Charge(0, location))
        {
            return false;
        }

        definition = candidate;
        return true;
    }
    private static bool Reject(ISchemaOperation operation, XmlLocation at, string message)
    {
        operation.AddDiagnostic(new("RNG.Datatype", XmlDiagnosticSeverity.Error, message, at));
        return false;
    }
}
