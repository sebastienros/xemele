using Xemele.XPath;
using Xemele.XPath.Internal;

namespace Xemele.Validation;

internal static class SchemaXPath
{
    internal static XPathValue Evaluate(XPathExpression expression, XmlNode context, XPathContext? options, ISchemaOperation operation, XmlLocation location = default)
    {
        var capped = Cap(options, operation, location);
        long used = 0;
        Exception? primary = null;
        try
        {
            return XPathEvaluationAccounting.EvaluateWithAccounting(expression, context, capped, out used);
        }
        catch (Exception error) { primary = error; throw; }
        finally { Debit(operation, used, location, primary); }
    }
    internal static XPathValue Evaluate(XPathExpression expression, XPathItem context, XPathContext? options, ISchemaOperation operation, XmlLocation location = default)
    {
        var capped = Cap(options, operation, location);
        long used = 0;
        Exception? primary = null;
        try
        {
            return XPathEvaluationAccounting.EvaluateWithAccounting(expression, context, capped, out used);
        }
        catch (Exception error) { primary = error; throw; }
        finally { Debit(operation, used, location, primary); }
    }
    private static XPathContext Cap(XPathContext? options, ISchemaOperation operation, XmlLocation location)
    {
        ArgumentNullException.ThrowIfNull(operation);
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        if (!operation.Charge(0, location))
        {
            throw new InvalidOperationException("Schema operation stopped.");
        }

        long? remaining = operation.RemainingWork, perExpression = options.Limits.MaxOperations;
        var cap = remaining is long r && perExpression is long p ? Math.Min(r, p) : remaining ?? perExpression;
        if (cap is <= 0)
        {
            operation.Refuse(operation is SchemaCompileOperation ? "SCHEMA.Limit" : "VALIDATION.Limit", "XPath work exhausted.", location);
            throw new InvalidOperationException("Schema operation stopped.");
        }
        return options with
        {
            Limits = options.Limits with
            {
                MaxOperations = cap
            },
            CancellationToken = operation.CancellationToken
        };
    }
    private static void Debit(ISchemaOperation operation, long used, XmlLocation location, Exception? primary)
    {
        try
        {
            if (!operation.Charge(used, location) && primary is null)
            {
                throw new InvalidOperationException("Schema operation stopped.");
            }
        }
        catch when (primary is not null) { } // Preserve callback/engine failure if final polling throws.
    }
}
