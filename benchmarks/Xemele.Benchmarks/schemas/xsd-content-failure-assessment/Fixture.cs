using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdContentFailureAssessment;

internal sealed class ContinuationFixture
{
    internal const string Source = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='n' type='xs:int'/><xs:element name='r'><xs:complexType><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType></xs:element></xs:schema>";
    internal static readonly string[] Routes = ["valid-local-dom", "rejected-one-dom", "rejected-1025-dom", "rejected-2048-dom", "later-datatype-dom", "rejected-1025-string", "independent-diagnostic-refusal"];
    private readonly XmlSchema schema;
    private readonly XmlDocument[] documents;
    private readonly string longInstance;
    private readonly ValidationOptions options = new() { Limits = new() { Diagnostics = 1024 } };
    private int consumed;
    internal ContinuationFixture()
    {
        schema = XmlSchema.Compile(Source);
        longInstance = "<r>" + string.Concat(Enumerable.Repeat("<u/>", 1025)) + "</r>";
        documents = [XmlDocument.Parse("<r><a/></r>"), XmlDocument.Parse("<r><u/></r>"), XmlDocument.Parse(longInstance), XmlDocument.Parse("<r>" + string.Concat(Enumerable.Repeat("<u/>", 2048)) + "</r>"), XmlDocument.Parse("<r><u/><n>bad</n></r>"), XmlDocument.Parse(longInstance), XmlDocument.Parse("<r><u/>" + string.Concat(Enumerable.Repeat("<n>bad</n>", 1024)) + "</r>")];
    }
    internal object RunOwned(int route)
    {
        var result = route == 5 ? schema.Validate(longInstance, options) : schema.Validate(documents[route], options);
        var errors = route switch
        {
            0 => 0,
            4 => 2,
            6 => 1024,
            _ => 1
        };
        if (result.IsComplete != (route != 6) || result.IsValid != (route == 0) || result.Diagnostics.Count != errors || route == 6 && result.Diagnostics[^1].Code != "VALIDATION.Limit")
        {
            throw new InvalidOperationException("Route oracle failed: " + Routes[route]);
        }

        consumed += result.Diagnostics.Count + 1;
        return result;
    }
    internal int Consume() => consumed;
}
