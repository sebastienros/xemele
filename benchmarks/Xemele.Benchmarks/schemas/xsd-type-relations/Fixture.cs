using System.Text;
using Xemele.Validation;
namespace Xemele.Benchmarks.M8XsdTypeRelations;

internal sealed class RelationsFixture
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly string[] Routes = [
        "relation-identity", "relation-union-member", "relation-member-derived", "relation-nested-union-24",
        "relation-unrelated-miss", "relation-head-blocked", "relation-whole-path-blocked", "relation-multilingual-name-8192",
        "compiler-original-particlesZ012", "compiler-original-particlesZ031", "compiler-original-stZ007",
        "compiler-inherited-32-attributes", "compiler-nested-union-24",
        "content-precomputed-union-members", "content-precomputed-blocked-members",
        "facade-original-particlesZ012", "facade-original-particlesZ031", "facade-original-stZ007", "facade-original-attZ015",
        "diagnostic-unrelated-substitution", "diagnostic-mixed-simple-extension", "diagnostic-anySimple-facet", "diagnostic-union-upa",
        "validation-members-64-valid", "validation-members-64-invalid"
    ];
    private readonly XmlDocument originalUnion, originalExtension, originalUr, originalAttributes;
    private readonly XmlDocument originalUnionInstance, originalUrInstance, originalAttributeInstance;
    private readonly XmlDocument attributes, nested, unrelated, mixed, urFacet, upa;
    private readonly XsdCompiledSchema relationGraph, nestedGraph, blockedGraph;
    private readonly Dictionary<SchemaName, XsdTypeDefinition> relationTypes, nestedTypes;
    private readonly XsdTypeDefinition relationContent, blockedContent;
    private readonly XmlSchema validationSchema;
    private readonly XmlDocument valid, invalid;
    private readonly SchemaName longName;
    private int consumed;

    internal RelationsFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-type-relations")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Pinned fixture directory missing.");
        }

        var fixtures = Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-type-relations");
        XmlDocument Read(string path) => XmlDocument.Parse(File.ReadAllText(Path.Combine(fixtures, path)));
        originalUnion = Read("msData/particles/particlesZ012.xsd");
        originalExtension = Read("msData/particles/particlesZ031.xsd");
        originalUr = Read("msData/simpleType/stZ007.xsd");
        originalAttributes = Read("msData/attribute/attZ015.xsd");
        originalUnionInstance = Read("msData/particles/particlesZ012.xml");
        originalUrInstance = Read("msData/simpleType/stZ007.xml");
        originalAttributeInstance = Read("msData/attribute/attZ015.xml");
        var attrs = new StringBuilder();
        for (var i = 0; i < 32; i++)
        {
            attrs.Append("<xs:attribute name='属性").Append(i).Append("' type='xs:int' fixed='2'/>");
        }

        attributes = XmlDocument.Parse(Wrap("<xs:complexType name='B'>" + attrs + "</xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'/></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>"));
        longName = new("", new string('名', 8192));
        var unions = new StringBuilder("<xs:simpleType name='U0'><xs:union memberTypes='xs:integer xs:boolean'/></xs:simpleType>");
        for (var i = 1; i < 24; i++)
        {
            unions.Append("<xs:simpleType name='U").Append(i).Append("'><xs:union memberTypes='U").Append(i - 1).Append(" xs:boolean'/></xs:simpleType>");
        }

        unions.Append("<xs:simpleType name='").Append(longName.LocalName).Append("'><xs:restriction base='xs:positiveInteger'/></xs:simpleType><xs:element name='root' type='U23'/>");
        nested = XmlDocument.Parse(Wrap(unions.ToString()));
        relationGraph = Compile(XmlDocument.Parse(Substitution()));
        nestedGraph = Compile(nested);
        relationTypes = relationGraph.Types.ToDictionary(t => t.Name);
        nestedTypes = nestedGraph.Types.ToDictionary(t => t.Name);
        relationContent = relationTypes[new("", "T")];
        blockedGraph = Compile(XmlDocument.Parse(Substitution("block='restriction'")));
        blockedContent = blockedGraph.Types.Single(t => t.Name == new SchemaName("", "T"));
        unrelated = XmlDocument.Parse(Substitution(member: "xs:date"));
        mixed = XmlDocument.Parse(Wrap("<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:int'/></xs:simpleContent></xs:complexType><xs:complexType name='D'><xs:complexContent mixed='true'><xs:extension base='B'/></xs:complexContent></xs:complexType>"));
        urFacet = XmlDocument.Parse(Wrap("<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:anySimpleType'/></xs:simpleContent></xs:complexType><xs:complexType name='D'><xs:simpleContent><xs:restriction base='B'><xs:minLength value='0'/></xs:restriction></xs:simpleContent></xs:complexType>"));
        upa = Read("union-upa.xsd");
        validationSchema = XmlSchema.Compile(Substitution());
        valid = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<m>2</m>", 64)) + "</root>");
        invalid = XmlDocument.Parse("<root>" + string.Concat(Enumerable.Repeat("<m>2</m>", 32)) + "<wrong/>" + string.Concat(Enumerable.Repeat("<m>2</m>", 32)) + "</root>");
        // Whole-path witness: the extension method appears above M, while M's
        // prohibition must constrain the entire D->B derivation.
        var whole = Compile(XmlDocument.Parse(Wrap("<xs:complexType name='B'/><xs:complexType name='M' block='extension'><xs:complexContent><xs:extension base='B'/></xs:complexContent></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='M'/></xs:complexContent></xs:complexType>")));
        wholeTypes = whole.Types.ToDictionary(t => t.Name);
        wholeRegistry = whole.Datatypes;
    }
    private readonly Dictionary<SchemaName, XsdTypeDefinition> wholeTypes;
    private readonly IXmlDatatypeRegistry wholeRegistry;
    private static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>" + body + "</xs:schema>";
    private static string Substitution(string block = "", string member = "xs:integer") => Wrap("<xs:simpleType name='U'><xs:union memberTypes='xs:integer xs:boolean'/></xs:simpleType><xs:element name='h' type='U' abstract='true' " + block + "/><xs:element name='m' type='" + member + "' substitutionGroup='h'/><xs:complexType name='T'><xs:sequence><xs:element ref='h' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:element name='root' type='T'/>");
    private static XsdCompiledSchema Compile(XmlDocument doc) => new XmlSchemaCompiler().Compile(doc, new(), new SchemaCompileOperation());
    internal object Run(int route)
    {
        var name = Routes[route];
        if (route < 8)
        {
            var op = new SchemaCompileOperation();
            bool hit;
            bool restriction, extension;
            if (route == 6)
            {
                hit = XsdSubstitutionGroups.TypeRelation(wholeTypes, null, wholeRegistry, new("", "D"), new("", "B"), op, default, true, false, false, out restriction, out extension);
            }
            else
            {
                var deep = route is 3 or 7;
                var d = route switch
                {
                    0 or 1 or 5 => new SchemaName(Xs, "integer"),
                    2 or 3 => new SchemaName(Xs, "positiveInteger"),
                    4 => new SchemaName(Xs, "date"),
                    _ => longName
                };
                var b = route == 0 ? d : new SchemaName("", deep ? "U23" : "U");
                hit = XsdSubstitutionGroups.TypeRelation(deep ? nestedTypes : relationTypes, null, deep ? nestedGraph.Datatypes : relationGraph.Datatypes, d, b, op, default, true, route == 5, false, out restriction, out extension);
            }
            var expected = route is not (4 or 5 or 6);
            if (hit != expected || !op.IsComplete || hit && (restriction != (route != 0) || extension))
            {
                throw new InvalidOperationException("Relation outcome/flags mismatch: " + name);
            }

            consumed++;
            return op; // operation is owned; queue/set scratch expires.
        }
        if (route is >= 8 and <= 12)
        {
            var doc = route switch
            {
                8 => originalUnion,
                9 => originalExtension,
                10 => originalUr,
                11 => attributes,
                _ => nested
            };
            var graph = Compile(doc);
            if (route == 11)
            {
                var b = graph.Types.Single(t => t.Name == new SchemaName("", "B"));
                var d = graph.Types.Single(t => t.Name == new SchemaName("", "D"));
                if (d.Attributes.Count != 32 || d.AnyAttribute is not null)
                {
                    throw new InvalidOperationException("Inherited attribute outcome mismatch.");
                }

                for (var i = 0; i < 32; i++)
                {
                    if (!ReferenceEquals(b.Attributes[i], d.Attributes[i]))
                    {
                        throw new InvalidOperationException("Inherited binding identity lost.");
                    }
                }
            }
            if (route == 10 && graph.Types.Single(t => t.Name.LocalName == "t2").SimpleType != new SchemaName(Xs, "anySimpleType"))
            {
                throw new InvalidOperationException("Inherited urtype lost.");
            }

            consumed += graph.Types.Count + graph.Elements.Count;
            return graph;
        }
        if (route is 13 or 14)
        {
            var op = new SchemaCompileOperation();
            var graph = route == 13 ? relationGraph : blockedGraph;
            var program = new XsdContentCompiler().Compile(graph, route == 13 ? relationContent : blockedContent, op);
            var cursor = new XsdContentMatcher().CreateCursor(program);
            var accepted = cursor.Accept(new("", "m"), op, out var id, out _);
            if (!op.IsComplete || accepted != (route == 13) || route == 13 && (id != graph.GlobalElements[new("", "m")] || !cursor.Complete(op)))
            {
                throw new InvalidOperationException("Precomputed membership mismatch.");
            }

            consumed++;
            return program;
        }
        if (route is >= 15 and <= 18)
        {
            var doc = route switch
            {
                15 => originalUnion,
                16 => originalExtension,
                17 => originalUr,
                _ => originalAttributes
            };
            var schema = XmlSchema.Compile(doc);
            if (route != 16)
            {
                var instance = route switch
                {
                    15 => originalUnionInstance,
                    17 => originalUrInstance,
                    _ => originalAttributeInstance
                };
                var result = schema.Validate(instance);
                if (!result.IsComplete || !result.IsValid)
                {
                    throw new InvalidOperationException("Unchanged original instance failed.");
                }

                consumed += result.Diagnostics.Count + 1;
            }
            consumed++;
            return schema;
        }
        if (route is >= 19 and <= 22)
        {
            var doc = route switch
            {
                19 => unrelated,
                20 => mixed,
                21 => urFacet,
                _ => upa
            };
            try
            {
                XmlSchema.Compile(doc);
            }
            catch (SchemaCompileException error)
            {
                if (error.Diagnostics.Count == 0 || error.Diagnostics.Any(d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("Refusal is not semantic rejection.");
                }

                if (!error.Diagnostics.Any(d => d.Code == "XSD.SchemaRestriction"))
                {
                    throw new InvalidOperationException("Wrong schema rejection.");
                }

                if (route == 22 && !error.Diagnostics.Any(d => d.Message.Contains("attribution", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Wrong UPA rejection.");
                }

                consumed += error.Diagnostics.Count;
                return error;
            }
            throw new InvalidOperationException("Invalid schema published.");
        }
        if (route is 23 or 24)
        {
            var result = validationSchema.Validate(route == 23 ? valid : invalid);
            if (!result.IsComplete || result.IsValid != (route == 23) || route == 24 && !result.Diagnostics.Any(d => d.Code == "XSD.Content"))
            {
                throw new InvalidOperationException("Member validation outcome mismatch.");
            }

            consumed += result.Diagnostics.Count + 1;
            return result;
        }
        throw new ArgumentOutOfRangeException(nameof(route));
    }
    internal int Consume() => consumed;
}
