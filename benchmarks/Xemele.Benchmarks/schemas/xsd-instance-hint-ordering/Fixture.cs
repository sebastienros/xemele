using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdInstanceHintOrdering;

internal sealed class HintFixture
{
    internal static readonly string[] BorrowedRoutes = ["namespace-token-short", "location-token-long", "whitespace-long", "ordinal-long-hit", "ordinal-long-miss", "retained-table-hit"];
    internal static readonly string[] OwnedRoutes = ["no-hint-dom", "timely-hint-dom", "late-hint-dom", "skipped-late-hint-dom", "distinct32-dom", "late-hint-string", "diagnostic-refusal-dom"];
    private const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private readonly XmlSchema schema, skipSchema;
    private readonly XmlDocument[] documents;
    private readonly string late, token = new('a', 8192), whitespace = new(' ', 8192);
    private readonly string comparison = "urn:" + new string('a', 8192), miss = "urn:" + new string('a', 8191) + "b";
    private readonly XsdHintNamespaces table = new();
    private readonly Ledger operation = new();
    private readonly ValidationOptions options = new(), refused = new() { Limits = new() { Diagnostics = 1 } };
    private int consumed;
    internal HintFixture()
    {
        const string xs = "http://www.w3.org/2001/XMLSchema";
        schema = XmlSchema.Compile($"<xs:schema xmlns:xs='{xs}'><xs:element name='r'/></xs:schema>");
        skipSchema = XmlSchema.Compile($"<xs:schema xmlns:xs='{xs}'><xs:element name='r'><xs:complexType><xs:sequence><xs:any processContents='skip' maxOccurs='unbounded'/></xs:sequence></xs:complexType></xs:element></xs:schema>");
        var prefix = $"<r xmlns:xsi='{Xsi}' xmlns:p='urn:p'>";
        late = prefix + "<p:a/><b xsi:schemaLocation='urn:p one'/></r>";
        var many = "<r>" + string.Concat(Enumerable.Range(0, 32).Select(i => $"<n xmlns='urn:n{i}'/>")) + "</r>";
        documents = [XmlDocument.Parse("<r><a/></r>"), XmlDocument.Parse(prefix + "<a xsi:schemaLocation='urn:p one'/><p:a/></r>"), XmlDocument.Parse(late), XmlDocument.Parse(late), XmlDocument.Parse(many), XmlDocument.Parse(late), XmlDocument.Parse(prefix + "<p:a/><b xsi:schemaLocation='urn:p one'/><c xsi:schemaLocation='urn:p two'/></r>")];
        table.Add(comparison, operation, default);
    }
    internal int Borrowed(int route)
    {
        var remaining = (route switch
        {
            0 => "urn:p one",
            1 => token,
            _ => whitespace
        }).AsSpan();
        var answer = route switch
        {
            0 or 1 or 2 => XsdHintNamespaces.Token(ref remaining, operation, default, out var item) ? item.Length : 0,
            3 => XsdHintNamespaces.Equal(comparison, comparison, operation, default) ? 1 : 0,
            4 => XsdHintNamespaces.Equal(comparison, miss, operation, default) ? 1 : 0,
            _ => table.Contains(comparison, operation, default) ? 1 : 0,
        };
        if (answer != (route switch
        {
            0 => 5,
            1 => 8192,
            2 or 4 => 0,
            _ => 1
        }))
        {
            throw new InvalidOperationException("Borrowed route oracle failed.");
        }

        consumed += answer + 1;
        return answer;
    }
    internal ValidationResult Owned(int route)
    {
        var result = route == 5 ? schema.Validate(late, options) : (route == 3 ? skipSchema : schema).Validate(documents[route], route == 6 ? refused : options);
        var valid = route is 0 or 1 or 4;
        if (result.IsComplete != (route != 6) || result.IsValid != valid || result.Diagnostics.Count != (valid ? 0 : 1) || route == 6 && result.Diagnostics[^1].Code != "VALIDATION.Limit")
        {
            throw new InvalidOperationException("Owned route oracle failed: " + OwnedRoutes[route]);
        }

        if (!valid && route != 6 && result.Diagnostics[0].Code != "XSD.HintOrder")
        {
            throw new InvalidOperationException("Ordering oracle failed.");
        }

        consumed += result.Diagnostics.Count + 1;
        return result;
    }
    internal int Consume() => consumed;
    private sealed class Ledger : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new() { EvaluationWork = null, Diagnostics = null, DocumentCharacters = null, TokenCharacters = null };
        public CancellationToken CancellationToken => CancellationToken.None;
        public bool IsComplete => true;
        public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) => true;
        public bool ChargeComponents(int count, XmlLocation location) => true;
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException("No primitive diagnostic expected.");
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException(message);
    }
}
