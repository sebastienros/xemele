using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdParticleNormalization;

internal sealed class ParticlesFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly string[] Routes = [
        "legacy-content-original-elemZ027_e", "legacy-content-original-elemZ027_f", "legacy-content-original-particlesZ008",
        "legacy-content-original-unused-addB194", "legacy-complete-nonunit-implicit-choice",
        "batch-original-elemZ027_e", "batch-original-elemZ027_f", "batch-original-particlesZ008", "batch-original-addB194", "batch-substitution-chain-16",
        "facade-original-elemZ027_e", "facade-original-elemZ027_f", "facade-original-particlesZ008", "facade-original-addB194", "facade-substitution-chain-16",
        "diagnostic-authored-choice-order", "diagnostic-singleton-incompatible", "diagnostic-singleton-bounds", "diagnostic-original-group-used",
        "diagnostic-unused-edc", "diagnostic-unused-redefine-fixed", "validation-members-64-valid", "validation-members-64-invalid", "legacy-complete-other-type-fixed-mismatch"
    ];
    private readonly XmlDocument[] originals;
    private readonly XmlDocument[] originalInstances;
    private readonly XsdCompiledSchema[] graphs;
    private readonly XsdTypeDefinition[] requested;
    private readonly XmlDocument nonunit, scaled, authoredOrder, incompatible, bounds, usedGroup, edc, redefine;
    private readonly XsdCompiledSchema scaledGraph, invalidFixedGraph;
    private readonly XsdTypeDefinition unusedFixed;
    private readonly XmlSchema validationSchema;
    private readonly XmlDocument valid, invalid;
    private readonly SchemaName scaledMember;
    private readonly SchemaCompileOptions redefineOptions;
    private readonly XmlFacetEvaluator facets = new();
    private int consumed;

    internal ParticlesFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-particle-normalization")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Pinned fixture directory missing.");
        }

        var fixtures = Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-particle-normalization");
        string Read(string path) => File.ReadAllText(Path.Combine(fixtures, path));
        var groupSource = Read("msData/additional/addB194.xsd");
        originals = [XmlDocument.Parse(Read("msData/element/elemZ027e.xsd")), XmlDocument.Parse(Read("msData/element/elemZ027f.xsd")), XmlDocument.Parse(Read("msData/particles/particlesZ008.xsd")), XmlDocument.Parse(groupSource)];
        originalInstances = [XmlDocument.Parse("<doc><a>1</a></doc>"), XmlDocument.Parse("<doc><b>2</b></doc>"), XmlDocument.Parse(Read("msData/particles/particlesZ008.xml"))];
        nonunit = XmlDocument.Parse(Restriction(Sequence(Element("d", "minOccurs='2' maxOccurs='4'")), Sequence(Element("c", "minOccurs='2' maxOccurs='4'")), "<xs:element name='d'/><xs:element name='c' substitutionGroup='d'/><xs:element name='b' type='xs:int' substitutionGroup='c'/><xs:element name='a' type='xs:int' substitutionGroup='b'/>"));
        graphs = [Compile(originals[0]), Compile(originals[1]), Compile(originals[2]), Compile(originals[3]), Compile(nonunit)];
        requested = [Find(graphs[0], "derived"), Find(graphs[1], "derived"), Find(graphs[2], "ContainMember2Type"), Find(graphs[3], "#model-group:THDMVV"), Find(graphs[4], "D")];
        // Sixteen linked global declarations, an eight-member restricted set,
        // and a 256-scalar multilingual name exercise bounded mapping/storage.
        var names = new string[16];
        names[0] = new string('名', 256);
        for (var i = 1; i < names.Length; i++)
        {
            names[i] = "項目" + i;
        }

        var declarations = new StringBuilder();
        for (var i = 15; i >= 0; i--)
        {
            declarations.Append("<xs:element name='").Append(names[i]).Append("' type='xs:string'");
            if (i < 15)
            {
                declarations.Append(" substitutionGroup='").Append(names[i + 1]).Append('\'');
            }

            declarations.Append("/>");
        }
        scaled = XmlDocument.Parse(Restriction(Sequence(Element(names[15], "minOccurs='0' maxOccurs='unbounded'")), Sequence(Element(names[7], "minOccurs='0' maxOccurs='unbounded'")), declarations.ToString()));
        scaledGraph = Compile(scaled);
        scaledMember = new("", names[0]);
        valid = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<" + names[0] + ">x</" + names[0] + ">", 64)) + "</root>");
        invalid = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<" + names[0] + ">x</" + names[0] + ">", 32)) + "<" + names[15] + ">x</" + names[15] + ">" + string.Concat(Enumerable.Repeat("<" + names[0] + ">x</" + names[0] + ">", 32)) + "</root>");
        validationSchema = XmlSchema.Compile(scaled);
        string Choice(string order) => "<xs:choice>" + string.Concat(order.Select(c => "<xs:element name='" + c + "' type='xs:string'/>")) + "</xs:choice>";
        authoredOrder = XmlDocument.Parse(Restriction(Choice("abc"), Choice("cba")));
        incompatible = XmlDocument.Parse(Singleton("other"));
        bounds = XmlDocument.Parse(Singleton("m", "minOccurs='2' maxOccurs='3'", "minOccurs='1' maxOccurs='3'"));
        usedGroup = XmlDocument.Parse(groupSource.Replace("</xs:schema>", "<xs:complexType name='GroupUser'><xs:group ref='THDMVV'/></xs:complexType></xs:schema>", StringComparison.Ordinal));
        edc = XmlDocument.Parse(Wrap("<xs:group name='g'><xs:choice><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:string'/></xs:choice></xs:group>"));
        var child = Wrap("<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:decimal' fixed='1'/></xs:sequence></xs:group>");
        redefine = XmlDocument.Parse(Wrap("<xs:redefine schemaLocation='child.xsd'><xs:group name='g'><xs:sequence><xs:element name='x' type='xs:decimal' fixed='2'/></xs:sequence></xs:group></xs:redefine><xs:complexType name='Unused'/>"));
        redefineOptions = new()
        {
            BaseUri = new Uri("https://particles.test/main.xsd"),
            Resolver = new Resolver(Encoding.UTF8.GetBytes(child))
        };
        invalidFixedGraph = Compile(XmlDocument.Parse(Restriction(Sequence("<xs:element name='x' type='xs:decimal' fixed='1'/>"), Sequence("<xs:element name='x' type='xs:decimal' fixed='2'/>")).Replace("</xs:schema>", "<xs:complexType name='Unused'/></xs:schema>", StringComparison.Ordinal)));
        unusedFixed = Find(invalidFixedGraph, "Unused");
    }
    private static XsdTypeDefinition Find(XsdCompiledSchema graph, string name) => graph.Types.Single(t => t.Name.LocalName == name);
    private static XsdCompiledSchema Compile(XmlDocument document)
    {
        var operation = new SchemaCompileOperation();
        var graph = new XmlSchemaCompiler().Compile(document, new(), operation);
        return graph with
        {
            Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), operation)
        };
    }
    private static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + body + "</xs:schema>";
    private static string Element(string name, string occurs = "") => "<xs:element ref='" + name + "' " + occurs + "/>";
    private static string Sequence(string body) => "<xs:sequence>" + body + "</xs:sequence>";
    private static string Restriction(string basis, string derived, string declarations = "") => Wrap(declarations + "<xs:complexType name='B'>" + basis + "</xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'>" + derived + "</xs:restriction></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
    private static string Singleton(string member, string basis = "", string derived = "") => Restriction("<xs:sequence " + basis + ">" + Element("h") + "</xs:sequence>", "<xs:sequence " + derived + ">" + Element(member) + "</xs:sequence>", "<xs:element name='h' type='xs:string' abstract='true'/><xs:element name='m' type='xs:string' substitutionGroup='h'/><xs:element name='other' type='xs:string'/>");

    internal object Run(int route)
    {
        if (route <= 4)
        {
            var operation = new SchemaCompileOperation();
            var graph = graphs[route];
            var compiler = new XsdContentCompiler();
            var program = route == 4 ? compiler.Compile(graph, requested[route], operation, facets) : compiler.Compile(graph, requested[route], operation);
            if (!operation.IsComplete || program is not CompiledContent content || !ReferenceEquals(content.Root?.Particle, requested[route].Particle))
            {
                throw new InvalidOperationException("Wrong requested content program.");
            }

            if (route != 3)
            {
                var member = route == 2 ? new SchemaName("urn:my-namespace", "Member2") : new SchemaName("", "a");
                var cursor = new XsdContentMatcher().CreateCursor(program);
                if (!cursor.Accept(member, operation, out var declaration, out _) || declaration != graph.GlobalElements[member])
                {
                    throw new InvalidOperationException("Wrong implicit member declaration.");
                }

                if (route == 4 && !cursor.Accept(new("", "c"), operation, out _, out _))
                {
                    throw new InvalidOperationException("Nonunit member occurrence lost.");
                }

                if (!cursor.Complete(operation))
                {
                    throw new InvalidOperationException("Member cursor did not complete.");
                }

                var rejected = new XsdContentMatcher().CreateCursor(program);
                if (rejected.Accept(route == 2 ? new("urn:my-namespace", "other") : new("", "d"), operation, out _, out _))
                {
                    throw new InvalidOperationException("Incompatible member accepted.");
                }

                if (!operation.IsComplete)
                {
                    throw new InvalidOperationException("Refusal is not member rejection.");
                }
            }
            consumed++;
            return program;
        }
        if (route is >= 5 and <= 9)
        {
            var graph = route == 9 ? scaledGraph : graphs[route - 5];
            var operation = new SchemaCompileOperation();
            var programs = new XsdContentCompiler().CompileAll(graph, operation, facets);
            if (!operation.IsComplete || programs.Count != graph.Types.Count)
            {
                throw new InvalidOperationException("Batch keys missing.");
            }

            for (var i = 0; i < graph.Types.Count; i++)
            {
                var type = graph.Types[i];
                if (!programs.TryGetValue(type.Name, out var ready) || ready is not CompiledContent content || content.Location != type.Location || type.Particle is not null && !ReferenceEquals(content.Root?.Particle, type.Particle) || type.Particle is null && content.Root is not null)
                {
                    throw new InvalidOperationException("Batch program/type identity mismatch.");
                }

                consumed += content.Nodes.Length + 1;
            }
            if (route == 9)
            {
                var cursor = new XsdContentMatcher().CreateCursor(programs[new("", "D")]);
                if (!cursor.Accept(scaledMember, operation, out var id, out _) || id != scaledGraph.GlobalElements[scaledMember] || !cursor.Complete(operation))
                {
                    throw new InvalidOperationException("Scaled substitution match failed.");
                }
            }
            return programs;
        }
        if (route is >= 10 and <= 14)
        {
            var schema = XmlSchema.Compile(route == 14 ? scaled : originals[route - 10]);
            if (route != 13)
            {
                var result = schema.Validate(route == 14 ? valid : originalInstances[route - 10]);
                if (!result.IsComplete || !result.IsValid)
                {
                    throw new InvalidOperationException("Independent original/member instance failed.");
                }

                consumed += result.Diagnostics.Count + 1;
                GC.KeepAlive(result);
            }
            consumed++;
            return schema;
        }
        if (route is >= 15 and <= 20 || route == 23)
        {
            try
            {
                if (route == 23)
                {
                    new XsdContentCompiler().Compile(invalidFixedGraph, unusedFixed, new SchemaCompileOperation(), facets);
                }
                else if (route == 20)
                {
                    XmlSchema.Compile(redefine, redefineOptions);
                }
                else
                {
                    XmlSchema.Compile(route switch
                    {
                        15 => authoredOrder,
                        16 => incompatible,
                        17 => bounds,
                        18 => usedGroup,
                        _ => edc
                    });
                }
            }
            catch (SchemaCompileException error)
            {
                if (error.Diagnostics.Count == 0)
                {
                    throw new InvalidOperationException("Diagnostic missing.");
                }

                bool semantic = false, attribution = false;
                for (var i = 0; i < error.Diagnostics.Count; i++)
                {
                    var diagnostic = error.Diagnostics[i];
                    if (diagnostic.Code.StartsWith("SCHEMA.", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Refusal is not invalidity.");
                    }

                    semantic |= diagnostic.Code == "XSD.SchemaRestriction";
                    attribution |= diagnostic.Message.Contains("unique particle attribution", StringComparison.Ordinal);
                }
                if (!semantic || route == 18 && !attribution)
                {
                    throw new InvalidOperationException("Wrong semantic rejection.");
                }

                consumed += error.Diagnostics.Count;
                return error;
            }
            throw new InvalidOperationException("Invalid schema published.");
        }
        if (route is 21 or 22)
        {
            var result = validationSchema.Validate(route == 21 ? valid : invalid);
            if (!result.IsComplete || result.IsValid != (route == 21))
            {
                throw new InvalidOperationException("Member validation outcome mismatch.");
            }

            if (route == 22)
            {
                var content = false;
                for (var i = 0; i < result.Diagnostics.Count; i++)
                {
                    content |= result.Diagnostics[i].Code == "XSD.Content";
                }

                if (!content)
                {
                    throw new InvalidOperationException("Wrong invalid-instance diagnostic.");
                }
            }
            consumed += result.Diagnostics.Count + 1;
            return result;
        }
        throw new ArgumentOutOfRangeException(nameof(route));
    }
    internal int Consume() => consumed;
    private sealed class Resolver(byte[] child) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return request.Uri == new Uri("https://particles.test/child.xsd") ? new(new MemoryStream(child, writable: false), request.Uri) : null;
        }
    }
}
