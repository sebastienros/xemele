using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdComponentMapping;

internal sealed class MappingFixture
{
    internal const int BorrowedStart = 13;
    internal static readonly string[] Routes = ["compiler-mixed-zero", "compiler-name-short", "compiler-name-long-ascii", "compiler-name-long-multilingual", "compiler-invalid-name-short", "compiler-invalid-name-long", "compiler-invalid-supplementary", "compiler-name-whitespace", "compiler-qname-whitespace", "compiler-id-whitespace", "compiler-empty-namespace", "compiler-whitespace-empty-namespace", "facade-mixed-zero-composition", "borrowed-short-ascii", "borrowed-long-ascii", "borrowed-multilingual", "borrowed-invalid-short", "borrowed-invalid-long", "borrowed-invalid-supplementary"];
    private readonly XmlDocument[] documents;
    private readonly string[] names = ["Name", new string('a', 8192), new string('名', 8192), "ĳ", new string('a', 8192) + "ĳ", "𕀀"];
    private readonly SchemaCompileOperation borrowed = new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } });
    private readonly object borrowedResult = new();
    private int consumed;
    internal MappingFixture()
    {
        string whitespace = new(' ', 4096);
        string[] bodies = [
            "<xs:complexType name='T' mixed='true'><xs:sequence minOccurs='0' maxOccurs='0'><xs:element name='unused'/></xs:sequence></xs:complexType><xs:element name='r' type='T'/>",
            "<xs:element name='Name'/>",
            "<xs:element name='" + names[1] + "'/>",
            "<xs:element name='" + names[2] + "'/>",
            "<xs:element name='ĳ'/>",
            "<xs:element name='" + names[4] + "'/>",
            "<xs:element name='𕀀'/>",
            "<xs:element name='" + whitespace + "Name" + whitespace + "'/>",
            "<xs:element name='r' type='" + whitespace + "xs:string" + whitespace + "'/>",
            "<xs:element name='r' id='" + whitespace + "ID" + whitespace + "'/>",
            "<xs:element name='r'><xs:complexType><xs:sequence><xs:any namespace='' minOccurs='0'/></xs:sequence></xs:complexType></xs:element>",
            "<xs:element name='r'><xs:complexType><xs:anyAttribute namespace='" + whitespace + "'/></xs:complexType></xs:element>"
        ];
        documents = new XmlDocument[bodies.Length];
        for (var i = 0; i < bodies.Length; i++)
        {
            documents[i] = XmlDocument.Parse("<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + bodies[i] + "</xs:schema>");
        }
    }
    internal object Run(int route)
    {
        if (route >= BorrowedStart)
        {
            var expected = route < BorrowedStart + 3;
            var valid = XsdCompilation.ComponentNCName(names[route - BorrowedStart].AsSpan(), borrowed);
            if (valid != expected || !borrowed.IsComplete)
            {
                throw new InvalidOperationException("Borrowed name scan oracle mismatch.");
            }

            consumed += valid ? 2 : 1;
            return borrowedResult;
        }
        if (route == 12)
        {
            var schema = XmlSchema.Compile(documents[0]);
            var text = schema.Validate("<r>名 text</r>");
            var child = schema.Validate("<r><unused/></r>");
            if (!text.IsComplete || !text.IsValid || !child.IsComplete || child.IsValid || !child.Diagnostics.Any(d => d.Code == "XSD.Content"))
            {
                throw new InvalidOperationException("Facade mixed zero composition failed.");
            }

            consumed += text.Diagnostics.Count + child.Diagnostics.Count + 1;
            return schema;
        }
        var invalid = route is 4 or 5 or 6;
        try
        {
            var graph = new XmlSchemaCompiler().Compile(documents[route], new(), new SchemaCompileOperation());
            if (invalid)
            {
                throw new InvalidOperationException("Expected name rejection.");
            }

            if (route == 0)
            {
                var t = graph.Types.Single(t => t.Name == new SchemaName("", "T"));
                if (t.ContentKind != XsdContentKind.Mixed || t.Particle?.Term is not XsdGroupTerm { Compositor: XsdCompositor.Sequence, Particles.Count: 0 } || t.Particle.MinOccurs != 1 || t.Particle.MaxOccurs != 1)
                {
                    throw new InvalidOperationException("Mixed zero mapping failed.");
                }
            }
            if (route is 10 or 11)
            {
                XsdWildcard? wildcard = null;
                foreach (var t in graph.Types)
                {
                    if (route == 11 && t.AnyAttribute is not null)
                    {
                        wildcard = t.AnyAttribute;
                    }
                    else if (route == 10 && t.Particle?.Term is XsdGroupTerm { Particles.Count: 1 } group && group.Particles[0].Term is XsdWildcardTerm term)
                    {
                        wildcard = term.Wildcard;
                    }
                }

                if (wildcard is null || wildcard.Negated || wildcard.Namespaces.Count != 0)
                {
                    throw new InvalidOperationException("Empty namespace mapping failed.");
                }
            }
            consumed += graph.Types.Count + graph.Elements.Count;
            return graph;
        }
        catch (SchemaCompileException error) when (invalid)
        {
            if (error.Diagnostics.Count != 1 || error.Diagnostics[0].Code != "XSD.SchemaRestriction" || error.Diagnostics[0].Message != "Invalid NCName.")
            {
                throw new InvalidOperationException("Wrong name diagnostic.");
            }

            consumed += error.Diagnostics.Count;
            return error;
        }
    }
    internal int Consume() => consumed;
}
