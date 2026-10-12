using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdRecursiveComponents;

internal sealed class RecursiveFixture
{
    internal static readonly string[] Routes = [
        "compiler-original-addB077", "compiler-original-particlesZ010", "compiler-original-particlesEb041",
        "facade-original-addB077", "facade-original-particlesZ010", "facade-original-particlesEb041",
        "compiler-nested-anonymous-24", "compiler-long-multilingual-name-8192",
        "diagnostic-forbidden-model-group", "diagnostic-forbidden-base-type", "diagnostic-deferred-publication",
        "validation-recursive-96-valid", "validation-recursive-96-invalid"
    ];
    private readonly XmlDocument[] documents;
    private readonly XmlSchema traversal;
    private readonly XmlDocument valid, invalid, originalInstance;
    private int consumed;
    internal RecursiveFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-recursive-components"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Pinned fixture directory missing.");
        string fixtures = Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-recursive-components");
        string[] files = ["addB077.xsd", "particlesZ010.xsd", "particlesEb041.xsd"];
        documents = new XmlDocument[8];
        for (int i = 0; i < files.Length; i++) documents[i] = XmlDocument.Parse(File.ReadAllText(Path.Combine(fixtures, files[i])));
        originalInstance = XmlDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "particlesEb041.xml")));
        var nested = new StringBuilder();
        for (int level = 0; level < 24; level++) nested.Append("<xs:element name='n").Append(level).Append("'><xs:complexType><xs:sequence>");
        nested.Append("<xs:element name='leaf' type='xs:string'/>");
        for (int level = 0; level < 24; level++) nested.Append("</xs:sequence></xs:complexType></xs:element>");
        documents[3] = XmlDocument.Parse(Wrap(nested.ToString()));
        string longName = new('名', 8192);
        documents[4] = XmlDocument.Parse(Wrap("<xs:element name='" + longName + "'><xs:complexType><xs:attribute name='a'/></xs:complexType></xs:element>"));
        documents[5] = XmlDocument.Parse(Wrap("<xs:group name='g'><xs:sequence><xs:group ref='h'/></xs:sequence></xs:group><xs:group name='h'><xs:sequence><xs:group ref='g'/></xs:sequence></xs:group>"));
        documents[6] = XmlDocument.Parse(Wrap("<xs:complexType name='T'><xs:complexContent><xs:extension base='U'/></xs:complexContent></xs:complexType><xs:complexType name='U'><xs:complexContent><xs:extension base='T'/></xs:complexContent></xs:complexType>"));
        documents[7] = XmlDocument.Parse(Wrap("<xs:element name='unused'><xs:complexType><xs:attribute name='a'><xs:simpleType><xs:restriction base='xs:int'><xs:minInclusive value='bad'/></xs:restriction></xs:simpleType></xs:attribute></xs:complexType></xs:element>"));
        traversal = XmlSchema.Compile(Wrap("<xs:group name='g'><xs:sequence><xs:element name='node'><xs:complexType><xs:group ref='g' minOccurs='0'/></xs:complexType></xs:element></xs:sequence></xs:group><xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element>"));
        var instance = new StringBuilder("<root>");
        for (int level = 0; level < 96; level++) instance.Append("<node>");
        string prefix = instance.ToString();
        for (int level = 0; level < 96; level++) instance.Append("</node>");
        instance.Append("</root>"); valid = XmlDocument.Parse(instance.ToString());
        invalid = XmlDocument.Parse(prefix + "<wrong/>" + instance.ToString().AsSpan(prefix.Length).ToString());
    }
    private static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + body + "</xs:schema>";
    internal object Run(int route)
    {
        if (route is 8 or 9 or 10)
        {
            try
            {
                if (route == 10) XmlSchema.Compile(documents[7]);
                else new XmlSchemaCompiler().Compile(documents[route - 3], new(), new SchemaCompileOperation());
            }
            catch (SchemaCompileException failure)
            {
                if (failure.Diagnostics.Count == 0 || failure.Diagnostics.Any(d => d.Code is "SCHEMA.Limit" or "SCHEMA.Syntax")) throw new InvalidOperationException("Refusal is not semantic rejection.");
                if (route < 10 && !failure.Diagnostics.Any(d => d.Code == "XSD.SchemaRestriction" && d.Message.StartsWith("Circular", StringComparison.Ordinal))) throw new InvalidOperationException("Wrong forbidden-cycle diagnostic.");
                consumed += failure.Diagnostics.Count; return failure;
            }
            throw new InvalidOperationException("Forbidden schema published.");
        }
        if (route >= 11)
        {
            var result = traversal.Validate(route == 11 ? valid : invalid);
            if (!result.IsComplete || result.IsValid != (route == 11)) throw new InvalidOperationException("Finite recursive instance outcome mismatch.");
            if (route == 12 && !result.Diagnostics.Any(d => d.Code == "XSD.Content")) throw new InvalidOperationException("Wrong recursive invalid-instance diagnostic.");
            consumed += result.Diagnostics.Count + 1; return result;
        }
        if (route is 3 or 4 or 5)
        {
            var schema = XmlSchema.Compile(documents[route - 3]);
            if (route == 5)
            {
                var result = schema.Validate(originalInstance);
                if (!result.IsComplete || !result.IsValid) throw new InvalidOperationException("Unchanged original instance failed.");
                consumed += result.Diagnostics.Count + 1;
            }
            consumed++; return schema;
        }
        int document = route < 3 ? route : route - 3;
        var graph = new XmlSchemaCompiler().Compile(documents[document], new(), new SchemaCompileOperation());
        if (graph.Elements.Count == 0 || graph.Types.Count < 40) throw new InvalidOperationException("Incomplete owned graph.");
        if (route == 6 && graph.Elements.Count != 25) throw new InvalidOperationException("Nested pending bodies not fully drained.");
        if (route == 7 && graph.Elements[0].Name.LocalName.Length != 8192) throw new InvalidOperationException("Long name not preserved.");
        consumed += graph.Types.Count + graph.Elements.Count; return graph;
    }
    internal int Consume() => consumed;
}
