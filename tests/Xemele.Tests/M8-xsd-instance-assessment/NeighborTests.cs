using System.Text;
using Xemele.Validation;
using Xunit;
using Identity = Xemele.Tests.M8XsdIdentity.IdentityTests;
using Ledger = Xemele.Tests.M8XsdIdentity.OperationTests.Ledger;
using Evaluation = Xemele.Tests.M8XsdValidate.EvaluatorTests;
namespace Xemele.Tests.M8XsdInstanceAssessment;

public sealed class NeighborTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    private static ValidationResult Oracle(string source, string xml, bool valid)
    {
        Assert.Equal(valid, OriginalWitnessTests.BclValid(Encoding.UTF8.GetBytes(source), Encoding.UTF8.GetBytes(xml)));
        var result = XmlSchema.Compile(source).Validate(xml);
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
        return result;
    }
    [Theory]
    [InlineData("absent", "", false)]
    [InlineData("namespace-mismatch", "<xs:anyAttribute namespace='urn:other' processContents='lax'/>", false)]
    [InlineData("lax", "<xs:anyAttribute namespace='##local' processContents='lax'/>", true)]
    [InlineData("skip", "<xs:anyAttribute namespace='##local' processContents='skip'/>", true)]
    [InlineData("strict", "<xs:anyAttribute namespace='##local' processContents='strict'/>", false)]
    public void ProhibitionRequiresMatchingWildcardAndStrictDeclaration(string policy, string wildcard, bool valid)
    {
        Assert.NotEmpty(policy);
        var result = Oracle(Wrap($"<xs:element name='root'><xs:complexType><xs:attribute name='a' use='prohibited'/>{wildcard}</xs:complexType></xs:element>"), "<root a='123'/>", valid);
        if (!valid)
        {
            Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Attribute");
        }
    }
    [Theory]
    [InlineData("lax", "1", true)]
    [InlineData("strict", "01", true)]
    [InlineData("lax", "bad", false)]
    [InlineData("strict", "bad", false)]
    [InlineData("lax", "2", false)]
    [InlineData("strict", "2", false)]
    [InlineData("skip", "bad", true)]
    [InlineData("skip", "2", true)]
    public void WildcardUsesGlobalDatatypeAndTypedFixed(string policy, string lexical, bool valid)
    {
        var result = Oracle(Wrap($"<xs:attribute name='a' type='xs:int' fixed='01'/><xs:element name='root'><xs:complexType><xs:attribute name='a' use='prohibited'/><xs:anyAttribute processContents='{policy}'/></xs:complexType></xs:element>"), $"<root a='{lexical}'/>", valid);
        if (!valid && lexical == "2")
        {
            Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Fixed");
        }
    }
    [Theory]
    [InlineData("optional", "text", true)]
    [InlineData("required", "text", true)]
    [InlineData("optional", null, true)]
    [InlineData("required", null, false)]
    public void PermittedLocalUsePrecedesMatchingGlobal(string use, string? lexical, bool valid)
    {
        Oracle(Wrap($"<xs:attribute name='a' type='xs:int' fixed='1'/><xs:element name='root'><xs:complexType><xs:attribute name='a' type='xs:string' use='{use}'/><xs:anyAttribute processContents='strict'/></xs:complexType></xs:element>"), lexical is null ? "<root/>" : $"<root a='{lexical}'/>", valid);
    }
    [Fact]
    public void ProhibitedReferenceDoesNotInjectGlobalDefault()
    {
        var source = Wrap("<xs:attribute name='a' type='xs:int' default='1'/><xs:element name='root'><xs:complexType><xs:attribute ref='a' use='prohibited'/><xs:anyAttribute processContents='lax'/></xs:complexType><xs:key name='K'><xs:selector xpath='.'/><xs:field xpath='@a'/></xs:key></xs:element>");
        var result = Oracle(source, "<root/>", false);
        Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Identity");
        var prepared = Evaluation.Prepare(source[($"<xs:schema xmlns:xs='{Xs}'>").Length..^12]);
        var hook = new Evaluation.RecordingFactory();
        var doc = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(prepared.Run(doc, factory: hook).IsValid);
        Assert.Empty(hook.Attributes);
        Assert.Equal(0, doc.RootElement.Attributes.Count);
    }
    internal static string NilSchema(string type = "xs:anyType", string kind = "unique", bool nillable = true, string extra = "") => Wrap(
        extra + $"<xs:element name='root'><xs:complexType><xs:sequence><xs:element ref='v' maxOccurs='unbounded'/></xs:sequence></xs:complexType><xs:{kind} name='K'><xs:selector xpath='v'/><xs:field xpath='.'/></xs:{kind}></xs:element><xs:element name='v' type='{type}' nillable='{nillable.ToString().ToLowerInvariant()}'/>");
    internal static string NilXml(string type = "", string nil = "true", string content = "") => $"<root xmlns:xsi='{Xs}-instance' xmlns:xs='{Xs}'>\n<v xsi:nil='{nil}' {type}>{content}</v>\n</root>";
    internal const string SimpleContent = "<xs:complexType name='S'><xs:simpleContent><xs:extension base='xs:string'/></xs:simpleContent></xs:complexType>";
    internal const string ElementOnly = "<xs:complexType name='E'><xs:sequence><xs:element name='child' minOccurs='0'/></xs:sequence></xs:complexType>";
    [Theory]
    [InlineData("xs:string", "", true)]
    [InlineData("xs:int", "", true)]
    [InlineData("xs:anySimpleType", "", true)]
    [InlineData("xs:anyType", "", false)]
    [InlineData("S", SimpleContent, true)]
    [InlineData("E", ElementOnly, false)]
    [InlineData("M", "<xs:complexType name='M' mixed='true'/>", false)]
    public void NilledUniqueFieldStillRequiresSimpleEffectiveType(string type, string extra, bool valid)
    {
        var result = Oracle(NilSchema(type, extra: extra), NilXml(), valid);
        if (!valid)
        {
            Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Identity" && d.Location.LineNumber == 2);
        }
    }
    [Theory]
    [InlineData("xs:string", "", true)]
    [InlineData("S", SimpleContent, true)]
    [InlineData("E", ElementOnly, false)]
    public void NilledFieldUsesEffectiveXsiType(string type, string extra, bool valid)
    {
        Oracle(NilSchema(extra: extra), NilXml($"xsi:type='{type}'"), valid);
    }
    [Fact]
    public void TwoNilledStringFieldsDoNotQualifyDuplicateTuples()
    {
        Oracle(NilSchema("xs:string"), $"<root xmlns:xsi='{Xs}-instance'><v xsi:nil='true'/><v xsi:nil='1'/></root>", true);
    }
    [Theory]
    [InlineData("key", true, "true", false)]
    [InlineData("unique", false, "true", false)]
    [InlineData("unique", true, "false", true)]
    [InlineData("unique", true, " 1 ", true)]
    [InlineData("unique", true, "true", true)]
    public void ExistingKeyNillableAndNilLexicalRulesRemain(string kind, bool nillable, string nil, bool valid)
    {
        Oracle(NilSchema("xs:string", kind, nillable), NilXml(nil: nil), valid);
    }
    [Fact]
    public void SecondEditionKeyRejectsNillableDeclarationEvenWithoutNil()
    {
        string source = NilSchema("xs:string", "key"), xml = NilXml(nil: "false");
        // Structures Second Edition 3.11.4 clause 4.2.3 excludes nillable
        // declarations. The BCL accepts this non-nil instance; retain that
        // oracle difference explicitly rather than changing the profile.
        Assert.True(OriginalWitnessTests.BclValid(Encoding.UTF8.GetBytes(source), Encoding.UTF8.GetBytes(xml)));
        var result = XmlSchema.Compile(source).Validate(xml);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Identity");
    }
    [Theory]
    [InlineData("xs:string", "", true)]
    [InlineData("xs:anyType", "", false)]
    [InlineData("S", SimpleContent, true)]
    [InlineData("E", ElementOnly, false)]
    public void NilAssessmentSharesExactPrechargedWorkAndMemory(string type, string extra, bool valid)
    {
        var schema = Identity.Schema(NilSchema(type, extra: extra)[($"<xs:schema xmlns:xs='{Xs}'>").Length..^12]);
        var compiler = new XsdIdentityCompiler();
        var observed = new Ledger();
        var program = compiler.Compile(schema, observed);
        var compiledWork = observed.Work;
        var doc = XmlDocument.Parse(NilXml(), cancellationToken: TestContext.Current.CancellationToken);
        XsdValidatedValue Values(XmlNode node, SchemaName? attribute) => new(true, new(node.LocalName == "v" ? (type.StartsWith("xs:", StringComparison.Ordinal) ? Xs : "") : Xs, node.LocalName == "v" ? type.Replace("xs:", "", StringComparison.Ordinal) : "anyType"), null);
        Identity.Drive(schema, program, doc, observed, Values);
        Assert.Equal(valid, observed.Diagnostics.Count == 0);
        var exact = new Ledger(observed.Work, observed.Components);
        program = compiler.Compile(schema, exact);
        Identity.Drive(schema, program, doc, exact, Values);
        var shortWork = new Ledger(observed.Work - 1);
        program = compiler.Compile(schema, shortWork);
        Assert.Throws<SchemaCompileException>(() => Identity.Drive(schema, program, doc, shortWork, Values));
        Assert.False(shortWork.IsComplete);
        Assert.True(shortWork.Work >= compiledWork);
        var shortMemory = new Ledger(components: observed.Components - 1);
        program = compiler.Compile(schema, shortMemory);
        Assert.Throws<SchemaCompileException>(() => Identity.Drive(schema, program, doc, shortMemory, Values));
        Assert.False(shortMemory.IsComplete);
    }
    [Fact]
    public void ComplexNilTypeLookupPollsInsideChargedGraphTraversal()
    {
        var fillers = string.Concat(Enumerable.Range(0, 64).Select(i => $"<xs:complexType name='T{i}'/>"));
        var schema = Identity.Schema(NilSchema("S", extra: fillers + SimpleContent)[($"<xs:schema xmlns:xs='{Xs}'>").Length..^12]);
        var program = new XsdIdentityCompiler().Compile(schema, new Ledger());
        var doc = XmlDocument.Parse(NilXml(), cancellationToken: TestContext.Current.CancellationToken);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var op = new Ledger(token: cts.Token);
        var session = new XsdIdentityFactory().Create(schema, program, doc);
        session.EnterElement(doc.RootElement, schema.Elements.Single(e => e.Name.LocalName == "root"), op);
        var child = doc.RootElement.FirstChild;
        while (child.Kind != XmlNodeKind.Element)
        {
            child = child.NextSibling;
        }

        session.EnterElement(child, schema.Elements.Single(e => e.Name.LocalName == "v"), op);
        var stop = op.Calls + 20;
        op.OnCharge = n => { if (n == stop) { cts.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => session.LeaveElement(child, new(true, new("", "S"), null), op));
        Assert.Equal(stop, op.Calls);
        Assert.Empty(op.Diagnostics);
    }
}
