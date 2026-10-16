using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdIdentity;

public sealed class IdentityTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string> { ["p"] = "urn:p", ["q"] = "urn:p" }, "", null);
    internal static XsdCompiledSchema Schema(string body)
    {
        var context = new SchemaCompileContext();
        return new XmlSchemaCompiler().Compile(context.Parse($"<xs:schema xmlns:xs='{Xs}' xmlns:p='urn:p' xmlns:q='urn:p'>{body}</xs:schema>").Document, context.Options, context.Operation);
    }
    internal static string Constraint(string kind = "key", string selector = "row", string field = "@id", string name = "K", string? refer = null) =>
        $"<xs:{kind} name='{name}'{(refer is null ? "" : $" refer='{refer}'")}><xs:selector xpath='{selector}'/><xs:field xpath='{field}'/></xs:{kind}>";
    internal static XsdCompiledSchema Simple(string constraint) => Schema($"<xs:element name='root'>{constraint}</xs:element>");
    internal static XmlDatatypeValue Typed(XmlDatatypeRegistry registry, string type, string lexical, SchemaNamespaceContext? context = null)
    {
        var name = type.StartsWith("{", StringComparison.Ordinal) ? new SchemaName("urn:test", type[1..]) : new SchemaName(Xs, type);
        var definition = registry.FindType(name)!;
        var facets = new XmlFacetEvaluator();
        var op = new SchemaCompileOperation();
        Assert.True(facets.TryValidate(definition, lexical, context ?? Context, registry, op, out var value, out var error), error?.Message);
        return value!;
    }
    internal static XsdValidatedValue Valid(XmlDatatypeValue value) => new(true, value.TypeName, value);
    internal static void Drive(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument doc, ISchemaOperation op,
        Func<XmlNode, SchemaName?, XsdValidatedValue>? values = null, Func<XmlNode, XsdElementDeclaration?>? declarations = null,
        Action<IXsdIdentitySession, XmlNode, ISchemaOperation>? extraAttributes = null)
    {
        var registry = new XmlDatatypeRegistry(schema.SimpleTypes.Where(d => d.Name.NamespaceUri != "http://www.w3.org/2001/XMLSchema"), new SchemaCompileOperation());
        XsdValidatedValue Value(XmlNode node, SchemaName? attribute)
        {
            if (values is not null)
            {
                return values(node, attribute);
            }

            if (attribute is { } name)
            {
                return Valid(Typed(registry, "string", node.GetAttribute(name.LocalName, name.NamespaceUri)!));
            }

            return new(true, new(Xs, "anyType"), null);
        }
        var session = new XsdIdentityFactory().Create(schema, program, doc);
        var events = new Stack<(XmlNode Node, bool Leave)>();
        events.Push((doc.RootElement, false));
        while (events.Count != 0)
        {
            var (node, leave) = events.Pop();
            if (leave)
            {
                session.LeaveElement(node, Value(node, null), op);
                continue;
            }
            var decl = declarations is not null ? declarations(node) : schema.Elements.FirstOrDefault(e => e.Name == new SchemaName(node.NamespaceUri, node.LocalName));
            session.EnterElement(node, decl, op);
            foreach (var a in node.Attributes)
            {
                var name = new SchemaName(a.NamespaceUri, a.LocalName);
                session.Attribute(node, name, Value(node, name), op);
            }
            extraAttributes?.Invoke(session, node, op);
            events.Push((node, true));
            for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling)
            {
                if (child.Kind == XmlNodeKind.Element)
                {
                    events.Push((child, false));
                }
            }
        }
        session.Complete(op);
    }
    internal static SchemaValidationOperation Run(XsdCompiledSchema schema, string xml, Func<XmlNode, SchemaName?, XsdValidatedValue>? values = null)
    {
        var program = new XsdIdentityCompiler().Compile(schema, new SchemaCompileOperation());
        var op = new SchemaValidationOperation();
        Drive(schema, program, XmlDocument.Parse(xml), op, values);
        return op;
    }
    private static void Result(string constraint, string xml, bool valid)
    {
        var result = Run(Simple(constraint), xml).Complete();
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("key", "<row id='a'/><row id='b'/>", true)]
    [InlineData("key", "<row id='a'/><row id='a'/>", false)]
    [InlineData("unique", "<row id='a'/><row id='a'/>", false)]
    [InlineData("unique", "<row/><row/>", true)]
    [InlineData("key", "<row/><row/>", false)]
    [InlineData("key", "", true)]
    public void KeysAndUnique(string kind, string content, bool valid) => Result(Constraint(kind), $"<root>{content}</root>", valid);

    [Theory]
    [InlineData("ref", true)]
    [InlineData("wrong", false)]
    public void ForwardReferencesResolveAtOwningScope(string value, bool valid)
    {
        Result(Constraint("keyref", "use", "@id", "R", "K") + Constraint("key", "row"), $"<root><use id='{value}'/><row id='ref'/></root>", valid);
    }
    [Fact]
    public void MissingKeyrefFieldsDoNotQualify()
    {
        Result(Constraint() + Constraint("keyref", "use", "@id", "R", "K"), "<root><use/><use/></root>", true);
    }
    [Theory]
    [InlineData("@id|@id", "<row id='x'/>", true)]
    [InlineData("a|a", "<row><a>x</a></row>", true)]
    [InlineData("a|b", "<row><a>x</a><b>x</b></row>", false)]
    [InlineData("a", "<row><a>x</a><a>x</a></row>", false)]
    [InlineData("@*", "<row a='x' b='x'/>", false)]
    public void FieldCardinalityAndUnionDeduplication(string field, string row, bool valid)
    {
        var schema = Simple(Constraint("unique", field: field));
        var registry = new XmlDatatypeRegistry();
        var result = Run(schema, $"<root>{row}</root>", (node, attr) => Valid(Typed(registry, "string", attr is { } n ? node.GetAttribute(n.LocalName, n.NamespaceUri)! : node.GetTextContent()))).Complete();
        Assert.Equal(valid, result.IsValid);
    }
    [Theory]
    [InlineData("key")]
    [InlineData("unique")]
    public void InvalidTypedValuesAreNeverAbsent(string kind)
    {
        var result = Run(Simple(Constraint(kind)), "<root><row id='bad'/></root>", (_, _) => new(false, new(Xs, "integer"), null)).Complete();
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("invalid value", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("key")]
    [InlineData("unique")]
    [InlineData("keyref")]
    public void UntypedOrComplexFieldIsAnError(string kind)
    {
        var constraint = kind == "keyref" ? Constraint() + Constraint(kind, "row", ".", "R", "K") : Constraint(kind, field: ".");
        var result = Run(Simple(constraint), "<root><row/></root>").Complete();
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("simple typed", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("key", "false", false)]
    [InlineData("key", "true", false)]
    [InlineData("unique", "true", true)]
    [InlineData("unique", "false", true)]
    [InlineData("unique", " 1 ", true)]
    public void NillableElementAndNilRules(string kind, string nil, bool valid)
    {
        var schema = Schema($"<xs:element name='root'>{Constraint(kind, field: "v")}</xs:element><xs:element name='v' type='xs:string' nillable='true'/>");
        var registry = new XmlDatatypeRegistry();
        var result = Run(schema, $"<root xmlns:xsi='{Xs}-instance'><row><v xsi:nil='{nil}'>x</v></row></root>", (node, attr) =>
            attr is null && node.LocalName == "v" && nil.Trim() != "false" ? new(true, new(Xs, "string"), null) : Valid(Typed(registry, "string", "x"))).Complete();
        Assert.Equal(valid, result.IsValid);
    }
    [Fact]
    public void DefaultsAreTypedEffectiveHookEventsWithoutDocumentMutation()
    {
        var schema = Simple(Constraint());
        var program = new XsdIdentityCompiler().Compile(schema, new SchemaCompileOperation());
        var doc = XmlDocument.Parse("<root><row/><row/></root>", cancellationToken: TestContext.Current.CancellationToken);
        var before = doc.ContentRevision;
        var registry = new XmlDatatypeRegistry();
        var value = Valid(Typed(registry, "integer", "1"));
        var op = new SchemaValidationOperation();
        Drive(schema, program, doc, op, extraAttributes: (session, node, operation) => { if (node.LocalName == "row") { session.Attribute(node, new("", "id"), value, operation); } });
        Assert.False(op.Complete().IsValid);
        Assert.Equal(before, doc.ContentRevision);
        Assert.Null(doc.RootElement.FirstChild.GetAttribute("id"));
    }
    [Theory]
    [InlineData("integer", "01", "decimal", "1.0", false)]
    [InlineData("positiveInteger", "1", "int", "1", false)]
    [InlineData("double", "NaN", "double", "NaN", false)]
    [InlineData("float", "-0", "float", "0", false)]
    [InlineData("float", "1", "double", "1", true)]
    [InlineData("QName", "p:a", "QName", "q:a", false)]
    [InlineData("NMTOKENS", "a  b", "NMTOKENS", "a b", false)]
    [InlineData("string", "1", "integer", "1", true)]
    [InlineData("dateTime", "2000-01-01T00:00:00Z", "dateTime", "1999-12-31T19:00:00-05:00", false)]
    public void EqualityUsesVerifiedValueSpaces(string leftType, string left, string rightType, string right, bool valid)
    {
        var registry = new XmlDatatypeRegistry();
        var a = Valid(Typed(registry, leftType, left));
        var b = Valid(Typed(registry, rightType, right));
        var result = Run(Simple(Constraint()), "<root><row id='a'/><row id='b'/></root>", (node, _) => node.GetAttribute("id") == "a" ? a : b).Complete();
        Assert.Equal(valid, result.IsValid);
    }
    [Fact]
    public void UnionSelectedValueAndDerivedListCompareThroughTheCore()
    {
        var list = new XmlDatatypeDefinition(new("urn:test", "List"), XmlDatatypeVariety.List, null, new(Xs, "integer"), [], [], [], false, default);
        var union = new XmlDatatypeDefinition(new("urn:test", "Union"), XmlDatatypeVariety.Union, null, null, [new(Xs, "decimal"), new(Xs, "string")], [], [], false, default);
        var registry = new XmlDatatypeRegistry([list, union], new SchemaCompileOperation());
        var a = Valid(Typed(registry, "{Union", "1.00"));
        var b = Valid(Typed(registry, "integer", "1"));
        Assert.False(Run(Simple(Constraint()), "<root><row id='a'/><row id='b'/></root>", (node, _) => node.GetAttribute("id") == "a" ? a : b).Complete().IsValid);
        a = Valid(Typed(registry, "{List", "01 2"));
        b = Valid(Typed(registry, "{List", "1 02"));
        Assert.False(Run(Simple(Constraint()), "<root><row id='a'/><row id='b'/></root>", (node, _) => node.GetAttribute("id") == "a" ? a : b).Complete().IsValid);
    }
    [Fact]
    public void KeyrefToUniqueUsesTypedCrossDerivedEquality()
    {
        var registry = new XmlDatatypeRegistry();
        var key = Valid(Typed(registry, "positiveInteger", "01"));
        var reference = Valid(Typed(registry, "decimal", "1.0"));
        var schema = Simple(Constraint("unique") + Constraint("keyref", "use", "@id", "R", "K"));
        Assert.True(Run(schema, "<root><use id='r'/><row id='k'/></root>", (node, _) => node.LocalName == "use" ? reference : key).Complete().IsValid);
    }
    [Fact]
    public void EmptyIdentityProgramHasARealFreshSession()
    {
        Assert.True(Run(Simple(""), "<root><a><b/></a></root>").Complete().IsValid);
    }
    [Fact]
    public void InvalidKeyrefValueIsAnErrorRatherThanAnUnqualifiedAbsentTuple()
    {
        var registry = new XmlDatatypeRegistry();
        var key = Valid(Typed(registry, "integer", "1"));
        var schema = Simple(Constraint() + Constraint("keyref", "use", "@id", "R", "K"));
        var result = Run(schema, "<root><row id='1'/><use id='bad'/></root>", (node, _) => node.LocalName == "use" ? new(false, new(Xs, "integer"), null) : key).Complete();
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("invalid value", StringComparison.Ordinal));
    }
    [Fact]
    public void FieldsCanTraverseUntypedWildcardIntermediateElements()
    {
        var registry = new XmlDatatypeRegistry();
        var value = Valid(Typed(registry, "string", "x"));
        var result = Run(Simple(Constraint(field: "box/value")), "<root><row><box><value>x</value></box></row><row><box><value>x</value></box></row></root>", (_, _) => value).Complete();
        Assert.False(result.IsValid);
    }
}
