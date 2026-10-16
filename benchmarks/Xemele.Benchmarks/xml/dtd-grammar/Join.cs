using Xemele.Parsing.Dtd;
namespace Xemele.DtdGrammarJoin;

// Demonstration composition, not a production DOCTYPE/entity context.
internal static class Join
{
    internal const string Internal = "<!ELEMENT r (a,b?)><!ATTLIST r local NMTOKENS ' x  y '><!ENTITY e '中😀'><!ENTITY e 'ignored'>";
    internal const string External = "<?xml encoding='UTF-8'?><!ELEMENT r ANY><!ELEMENT a EMPTY><!ELEMENT b (#PCDATA)><!ATTLIST r inherited CDATA '&e;&#9;' local CDATA 'ignored'><!ENTITY e SYSTEM 'forbidden'>";
    internal static (DtdModel Model, string[] Defaults) Run()
    {
        using var i = new DtdParser(DtdSubsetKind.Internal, new());
        using var e = new DtdParser(DtdSubsetKind.External, new());
        if (i.Append(Internal, [], true, default).Step != DtdParseStep.Completed || e.Append(External, [], true, default).Step != DtdParseStep.Completed)
        {
            throw new InvalidOperationException("Subset rejected.");
        }

        return Compose(i.Model, e.Model);
    }
    internal static (DtdModel Model, string[] Defaults) Compose(DtdModel first, DtdModel second)
    {
        var elements = first.Elements.Concat(second.Elements).DistinctBy(d => d.Name, StringComparer.Ordinal);
        var attributes = first.Attributes.Concat(second.Attributes).DistinctBy(d => (d.ElementName, d.Name));
        var general = first.GeneralEntities.Concat(second.GeneralEntities).DistinctBy(d => d.Name, StringComparer.Ordinal);
        var parameter = first.ParameterEntities.Concat(second.ParameterEntities).DistinctBy(d => d.Name, StringComparer.Ordinal);
        var notations = first.Notations.Concat(second.Notations).DistinctBy(d => d.Name, StringComparer.Ordinal);
        var model = new DtdModel(elements, attributes, general, parameter, notations);
        var table = model.GeneralEntities.ToDictionary(d => d.Name, StringComparer.Ordinal);
        var defaults = model.Attributes.Select(d => DtdValueProcessor.NormalizeAttributeDefault(d.DefaultValue!, d.Type, table, new()).Value
            ?? throw new InvalidOperationException("Effective default rejected.")).ToArray();
        return (model, defaults);
    }
}
