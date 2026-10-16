using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdInstanceAssessment;

internal sealed class AssessmentFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly string[] Routes = ["wildcard-lax-local", "wildcard-absent", "wildcard-strict-missing-global",
        "wildcard-strict-global-fixed-equal", "wildcard-strict-global-fixed-unequal", "wildcard-lax-global-invalid-datatype",
        "wildcard-skip-known-global", "permitted-local-before-global", "prohibited-missing-default",
        "nil-string-unique-exemption", "nil-anyType-identity-rejection", "nil-complex-simple-content",
        "nil-element-only-identity-rejection", "nil-effective-xsi-string", "nil-long-multilingual-simple-lookup",
        "nil-long-graph-element-only-rejection"];
    private readonly XmlSchema[] schemas = new XmlSchema[16];
    private readonly XmlDocument[] documents = new XmlDocument[16];
    private readonly ValidationOptions options = new() { MaxOperations = null };
    private int consumed;
    internal AssessmentFixture()
    {
        var global = "<xs:attribute name='a' type='xs:int' fixed='01'/>";
        string[] source = [Attribute("lax"), Attribute(null), Attribute("strict"), Attribute("strict", global),
            Attribute("strict", global), Attribute("lax", global), Attribute("skip", global),
            Attribute("strict", global, "<xs:attribute name='a' type='xs:string'/>") ,
            Attribute("lax", "<xs:attribute name='a' type='xs:int' default='1'/>", "<xs:attribute ref='a' use='prohibited'/>",
                "<xs:key name='K'><xs:selector xpath='.'/><xs:field xpath='@a'/></xs:key>"),
            Nil("xs:string"), Nil("xs:anyType"),
            Nil("S", "<xs:complexType name='S'><xs:simpleContent><xs:extension base='xs:string'/></xs:simpleContent></xs:complexType>"),
            Nil("E", "<xs:complexType name='E'><xs:sequence><xs:element name='child' minOccurs='0'/></xs:sequence></xs:complexType>"),
            Nil("xs:anyType"), Nil("東京" + new string('a', 256), $"<xs:simpleType name='東京{new string('a', 256)}'><xs:restriction base='xs:string'/></xs:simpleType>"),
            Nil("E", string.Concat(Enumerable.Range(0, 64).Select(i => $"<xs:complexType name='T{i}'/>")) + "<xs:complexType name='E'><xs:sequence><xs:element name='child' minOccurs='0'/></xs:sequence></xs:complexType>")];
        string[] xml = ["<root a='123'/>", "<root a='123'/>", "<root a='123'/>", "<root a='1'/>", "<root a='2'/>", "<root a='bad'/>", "<root a='bad'/>", "<root a='text'/>", "<root/>",
            NilInstance(2), NilInstance(1), NilInstance(1), NilInstance(1), NilInstance(1, "xsi:type='xs:string'"), NilInstance(16), NilInstance(1)];
        for (var i = 0; i < source.Length; i++)
        {
            schemas[i] = XmlSchema.Compile(source[i]);
            documents[i] = XmlDocument.Parse(xml[i]);
        }
    }
    private static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    private static string Attribute(string? policy, string global = "", string local = "<xs:attribute name='a' use='prohibited'/>", string identity = "") =>
        Wrap(global + $"<xs:element name='root'><xs:complexType>{local}{(policy is null ? "" : $"<xs:anyAttribute processContents='{policy}'/>")}</xs:complexType>{identity}</xs:element>");
    private static string Nil(string type, string extra = "") => Wrap(extra + $"<xs:element name='root'><xs:complexType><xs:sequence><xs:element ref='v' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:unique name='U'><xs:selector xpath='v'/><xs:field xpath='.'/></xs:unique></xs:element><xs:element name='v' type='{type}' nillable='true'/>");
    private static string NilInstance(int count, string type = "") => $"<root xmlns:xsi='{Xs}-instance' xmlns:xs='{Xs}'>" + string.Concat(Enumerable.Repeat($"<v xsi:nil='true' {type}/>", count)) + "</root>";
    internal object RunOwned(int route)
    {
        var result = schemas[route].Validate(documents[route], options);
        var valid = route is 0 or 3 or 6 or 7 or 9 or 11 or 13 or 14;
        var diagnostic = route switch
        {
            1 or 2 => "XSD.Attribute",
            4 => "XSD.Fixed",
            8 or 10 or 12 or 15 => "XSD.Identity",
            _ => null
        };
        if (!result.IsComplete || result.IsValid != valid ||
            diagnostic is not null && !result.Diagnostics.Any(d => d.Code == diagnostic) ||
            !valid && (result.Diagnostics.Count == 0 || result.Diagnostics.Any(d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("Independent validation route expectation failed: " + Routes[route]);
        }

        consumed += result.Diagnostics.Count + 1;
        return result;
    }
    internal int Consume() => consumed;
}
