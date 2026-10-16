namespace Xemele.Validation;

internal sealed class XmlDatatypeRegistry : IXmlDatatypeRegistry
{
    private readonly XsdDatatypeLibrary xsd;
    private readonly RngDatatypeLibrary rng, empty;
    // Assigned once after ingestion; no reference to this mutable backing map
    // escapes and no method ever mutates it after construction.
    private readonly Dictionary<SchemaName, XmlDatatypeDefinition> types;
    internal XmlDatatypeRegistry() : this([], new SchemaCompileOperation()) { }
    internal XmlDatatypeRegistry(IEnumerable<XmlDatatypeDefinition> definitions, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(operation);
        DatatypeValues.Charge(operation);
        if (!operation.ChargeComponents(6, default))
        {
            throw SchemaSnapshots.Failure(operation);
        }

        xsd = new();
        rng = new(xsd, false);
        empty = new(xsd, true);
        var snapshot = new Dictionary<SchemaName, XmlDatatypeDefinition>();
        var context = new SchemaNamespaceContext(new Dictionary<string, string>(), "", null);
        foreach (var metadata in DatatypeTypes.All.Values)
        {
            DatatypeValues.Charge(operation);
            if (!operation.ChargeComponents(8, default))
            {
                throw SchemaSnapshots.Failure(operation);
            }

            var name = DatatypeTypes.Name(metadata.LocalName);
            var facets = new List<XmlFacet>
            {
                new(XmlFacetKind.WhiteSpace, metadata.Whitespace switch
                { XmlWhitespaceRule.Preserve => "preserve", XmlWhitespaceRule.Replace => "replace", _ => "collapse" }, metadata.WhitespaceFixed, context, default)
            };
            if (metadata.Variety == XmlDatatypeVariety.List)
            {
                facets.Add(new(XmlFacetKind.MinLength, "1", false, context, default));
            }

            if (metadata.LocalName == "integer")
            {
                facets.Add(new(XmlFacetKind.FractionDigits, "0", true, context, default));
            }

            var (minimum, maximum) = Bounds(metadata.LocalName);
            if (minimum is not null)
            {
                facets.Add(new(XmlFacetKind.MinInclusive, minimum, false, context, default));
            }

            if (maximum is not null)
            {
                facets.Add(new(XmlFacetKind.MaxInclusive, maximum, false, context, default));
            }

            snapshot.Add(name, new(name, metadata.Variety ?? XmlDatatypeVariety.Atomic,
                metadata.BaseType is null ? null : DatatypeTypes.Name(metadata.BaseType),
                metadata.ItemType is null ? null : DatatypeTypes.Name(metadata.ItemType), [], facets, [], false, default));
        }
        foreach (var definition in definitions)
        {
            DatatypeValues.Charge(operation);
            ArgumentNullException.ThrowIfNull(definition);
            DatatypeValues.RequireName(definition.Name);
            if (!operation.ChargeComponents(1, default))
            {
                throw SchemaSnapshots.Failure(operation);
            }
            // Definitions already own immutable member/facet/final snapshots (M8-model).
            // Own this name map, and preserve exact definition/context identity.
            if (definition.Name.NamespaceUri is DatatypeTypes.XsdNamespace or DatatypeTypes.RngNamespace ||
                !snapshot.TryAdd(definition.Name, definition))
            {
                throw new ArgumentException("Duplicate or reserved datatype definition name.", nameof(definitions));
            }
        }
        DatatypeValues.Charge(operation);
        types = snapshot;
        DatatypeValues.Charge(operation, 0);
    }
    public IXmlDatatypeLibrary? Find(string namespaceUri)
    {
        ArgumentNullException.ThrowIfNull(namespaceUri);
        return namespaceUri switch
        {
            DatatypeTypes.XsdNamespace => xsd,
            DatatypeTypes.RngNamespace => rng,
            "" => empty,
            _ => null
        };
    }
    public XmlDatatypeDefinition? FindType(SchemaName name)
    {
        DatatypeValues.RequireName(name);
        if (types.TryGetValue(name, out var exact))
        {
            return exact;
        }

        if (name.NamespaceUri == DatatypeTypes.RngNamespace || name.NamespaceUri == "" && name.LocalName is "string" or "token")
        {
            name = DatatypeTypes.Name(name.LocalName);
        }

        return types.GetValueOrDefault(name);
    }
    private static (string? Minimum, string? Maximum) Bounds(string name) => name switch
    {
        "nonPositiveInteger" => (null, "0"),
        "negativeInteger" => (null, "-1"),
        "nonNegativeInteger" => ("0", null),
        "positiveInteger" => ("1", null),
        "long" => ("-9223372036854775808", "9223372036854775807"),
        "int" => ("-2147483648", "2147483647"),
        "short" => ("-32768", "32767"),
        "byte" => ("-128", "127"),
        "unsignedLong" => ("0", "18446744073709551615"),
        "unsignedInt" => ("0", "4294967295"),
        "unsignedShort" => ("0", "65535"),
        "unsignedByte" => ("0", "255"),
        _ => (null, null)
    };
}
