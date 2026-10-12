using Xemele.Validation;
namespace Xemele.Benchmarks.M8Facets;

internal sealed class FacetFixture
{
    internal static readonly string[] Names = ["borrowed scalar length", "borrowed decimal bounds", "borrowed digits", "borrowed decimal enum", "borrowed QName enum", "borrowed pattern match", "borrowed pattern miss", "borrowed pooled pattern", "borrowed malformed Unicode", "borrowed list length", "borrowed union enumeration", "owned short value", "owned pooled long value", "owned list", "owned union", "owned definition preparation", "owned stopped diagnostic"];
    internal static readonly string Long = new('a', 512);
    internal static readonly string[] Inputs = ["根😀", "1.00", ".001", "1.00", "q:a", "alpha", "Alpha", Long, "\ud800", "1 2", "1.0", "alpha", Long, "1 2", "1.0", "alpha", "alpha"];
    private readonly SchemaNamespaceContext context = new(new Dictionary<string,string> { ["p"]="urn:enum",["q"]="urn:enum" }, "", null);
    private readonly XmlFacetEvaluator evaluator = new();
    private readonly XmlDatatypeRegistry registry;
    private readonly ISchemaOperation operation = new SchemaCompileOperation(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } });
    private readonly XmlDatatypeDefinition[] definitions;
    private readonly XmlDatatypeValue[] values;
    internal FacetFixture()
    {
        XmlFacet F(XmlFacetKind kind, string text) => new(kind,text,false,context,default);
        XmlDatatypeDefinition T(string name,string type,params XmlFacet[] facets) => new(new("urn:bench",name),XmlDatatypeVariety.Atomic,DatatypeTypes.Name(type),null,[],facets,[],false,default);
        var scalar = T("scalar","string",F(XmlFacetKind.Length,"2"));
        var bounds = T("bounds","decimal",F(XmlFacetKind.MinInclusive,"1"),F(XmlFacetKind.MaxExclusive,"2"));
        var digits = T("digits","decimal",F(XmlFacetKind.TotalDigits,"3"),F(XmlFacetKind.FractionDigits,"3"));
        var enumeration = T("enum","decimal",F(XmlFacetKind.Enumeration,"1"));
        var qname = T("qname","QName",F(XmlFacetKind.Enumeration,"p:a"));
        var pattern = T("pattern","string",F(XmlFacetKind.Pattern,"[a-z]+"),F(XmlFacetKind.MinLength,"1"));
        var pooled = T("pooled","string",F(XmlFacetKind.Pattern,"a{512}"));
        var list = new XmlDatatypeDefinition(new("urn:bench","list"),XmlDatatypeVariety.List,null,DatatypeTypes.Name("integer"),[],[F(XmlFacetKind.Length,"2")],[],false,default);
        var union = new XmlDatatypeDefinition(new("urn:bench","union"),XmlDatatypeVariety.Union,null,null,[DatatypeTypes.Name("decimal"),DatatypeTypes.Name("string")],[F(XmlFacetKind.Enumeration,"1")],[],false,default);
        definitions = [scalar,bounds,digits,enumeration,qname,pattern,pattern,pooled,pattern,list,union,pattern,pooled,list,union,pattern,pattern];
        registry = new([scalar,bounds,digits,enumeration,qname,pattern,pooled,list,union],operation);
        foreach (var definition in definitions) if (!evaluator.ValidateDefinition(definition,registry,operation)) throw new InvalidOperationException();
        values = new XmlDatatypeValue[11];
        for (int i=0;i<values.Length;i++)
        {
            int source = i is 6 or 8 ? 5 : i;
            if (!evaluator.TryValidate(definitions[i],Inputs[source],context,registry,operation,out var value,out _)) throw new InvalidOperationException();
            values[i]=value!;
        }
    }
    internal void Run(int route)
    {
        bool expected = route is not (6 or 8 or 16); bool actual;
        if (route < 11) actual = evaluator.CheckPreparedValue(definitions[route], Inputs[route], values[route], registry, operation);
        else if (route == 15) actual = evaluator.ValidateDefinition(definitions[route], registry, operation);
        else if (route == 16)
        {
            var stopped = new SchemaValidationOperation(new() { MaxOperations = 1 });
            actual = evaluator.TryValidate(definitions[route], Inputs[route],context,registry,stopped,out var value,out var error);
            if (stopped.IsComplete || value is not null || error != DatatypeErrors.Stopped) throw new InvalidOperationException();
        }
        else
        {
            actual = evaluator.TryValidate(definitions[route], Inputs[route],context,registry,operation,out var value,out _);
            if (value is null) throw new InvalidOperationException(); GC.KeepAlive(value);
        }
        if (actual != expected) throw new InvalidOperationException("Route result changed: "+route);
    }
    internal object Retain() => registry;
}
