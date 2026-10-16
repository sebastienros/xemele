using Xemele.Validation;
using Xemele.XPath;
using Xunit;

namespace Xemele.Tests.M8SchCompile;

public sealed class CompilerTests
{
    internal const string Ns = "http://purl.oclc.org/dsdl/schematron";
    internal static string Schema(string content, string attributes = "") => "<schema xmlns='" + Ns + "' " + attributes + ">" + content + "</schema>";
    internal static string Pattern(string content, string attributes = "") => "<pattern " + attributes + ">" + content + "</pattern>";
    internal static string Rule(string context = "root", string content = "<assert test='true()'>ok</assert>") => "<rule context='" + context.Replace("'", "&apos;", StringComparison.Ordinal) + "'>" + content + "</rule>";
    internal static SchematronCompiledModel Compile(string source, SchematronCompileOptions? options = null)
    {
        options ??= new();
        var context = new SchemaCompileContext(options.Schema);
        var document = context.Parse(source).Document;
        return new SchematronCompiler().Compile(document, options, context.Operation);
    }
    private static SchemaCompileException Reject(string source) => Assert.Throws<SchemaCompileException>(() => Compile(source));

    [Fact]
    public void FullCompilerPublishesOwnedKeysPhasesScopesSubjectsAndDynamicContent()
    {
        var document = XmlDocument.Parse(Schema("<title>A<dir> title</dir></title><ns prefix='p' uri='urn:p'/>" +
            "<let name='top' value='1'/><phase id='one'><let name='limit' value='1'/><active pattern='named'/></phase>" +
            "<phase id='two'><let name='limit' value='2'/><active pattern='named'/></phase>" +
            Pattern("<title>Named</title><let name='x' value='$top + $limit'/><rule context='p:item | @code' subject='child'>" +
                "<let name='x' value='3'/><assert id='a' role='' flag='f' test='$x = 3' subject='@code' diagnostics='d' properties='prop'>" +
                "before <emph>bold</emph><span class='a'> span</span><dir value='rtl'> dir</dir><name/><value-of select='$x'/> after</assert>" +
                "<report test='false()'>report</report></rule>", "id='named'") + Pattern(Rule("/")) +
            "<diagnostics><diagnostic id='d'>D<value-of select='$x'/></diagnostic></diagnostics>" +
            "<properties><property id='prop'>P<name path='@code'/></property></properties>", "defaultPhase='one'"), cancellationToken: TestContext.Current.CancellationToken);
        var graph = new SchematronCompiler().Compile(document, new(), new SchemaCompileOperation());
        Assert.Equal("A title", graph.Title);
        Assert.Equal("one", graph.DefaultPhase);
        Assert.Equal([0, 1], graph.Patterns.Select(p => p.Key));
        Assert.Null(graph.Patterns[1].Id);
        var pattern = graph.Patterns[0];
        Assert.Equal("named", pattern.Id);
        Assert.Equal("Named", pattern.Name);
        Assert.Equal([0], graph.Phases["one"].ActivePatternKeys);
        Assert.Equal(1d, graph.Phases["one"].Lets[0].Value.Evaluate(document.RootElement).AsNumber());
        Assert.Equal(2d, graph.Phases["two"].Lets[0].Value.Evaluate(document.RootElement).AsNumber());
        var rule = Assert.Single(pattern.Rules);
        Assert.Equal(0, rule.Key);
        Assert.Equal("child", rule.SubjectSource);
        var check = rule.Checks[0];
        Assert.Equal("@code", check.SubjectSource);
        Assert.Equal("", check.Role);
        Assert.Equal("f", check.Flag);
        Assert.Equal("a", check.Id);
        Assert.Equal("d", Assert.Single(check.Diagnostics));
        Assert.Equal("prop", Assert.Single(check.Properties));
        Assert.Equal(7, check.Text.Count);
        Assert.IsType<SchematronNameOf>(check.Text[4]);
        Assert.IsType<SchematronValueOf>(check.Text[5]);
        Assert.True(check.Location.LinePosition > 0);
        Assert.Null(check.Location.ByteOffset);
        document.RootElement.SetTextContent("changed");
        Assert.Equal("urn:p", graph.Namespaces["p"]);
        Assert.Equal("A title", graph.Title);
        Assert.IsAssignableFrom<System.Collections.ObjectModel.ReadOnlyCollection<SchematronCompiledPattern>>(graph.Patterns);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)graph.Namespaces).Add("q", "urn:q"));
    }

    [Fact]
    public void AbstractParametersAreTokenAndStringAwareAndExtendsPreservesOrder()
    {
        var graph = Compile(Schema(Pattern("<rule abstract='true' id='base'><assert test='$threshold = 3'>base</assert></rule>" +
                "<rule context='$context'><let name='threshold-long' value='4'/><extends rule='base'/>" +
                "<report test=\"$threshold-long = 4 and '$threshold' = '$threshold'\">more</report></rule>", "abstract='true' id='template'") +
            Pattern("<param name='context' value='item'/><param name='threshold' value='3'/>", "is-a='template' id='concrete'")));
        var rule = Assert.Single(Assert.Single(graph.Patterns).Rules);
        Assert.Equal("item", rule.ContextSource);
        Assert.Equal(2, rule.Checks.Count);
        Assert.Equal("3 = 3", rule.Checks[0].TestSource);
        Assert.Equal("$threshold-long = 4 and '$threshold' = '$threshold'", rule.Checks[1].TestSource);
        var doc = XmlDocument.Parse("<root><item/><x><item/></x></root>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, rule.ContextSelector.Evaluate(doc.DocumentNode).AsNodeSet().Count);
    }

    [Fact]
    public void LetDependenciesOrderAndShadowWithoutCrossPhaseState()
    {
        var graph = Compile(Schema("<let name='b' value='$a + 1'/><let name='a' value='1'/>" +
            "<phase id='first'><let name='v' value='1'/><active pattern='p'/></phase>" +
            "<phase id='second'><let name='v' value='2'/><active pattern='p'/></phase>" +
            Pattern("<let name='a' value='3'/><rule context='root'><let name='a' value='4'/><assert test='$a + $v = 5'>x</assert></rule>", "id='p'"), "defaultPhase='first'"));
        Assert.Equal(["a", "b"], graph.Lets.Select(l => l.Name.LocalName));
        Assert.Equal(3d, graph.Patterns[0].Lets[0].Value.Evaluate(XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken).RootElement).AsNumber());
        Assert.Equal(4d, graph.Patterns[0].Rules[0].Lets[0].Value.Evaluate(XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken).RootElement).AsNumber());
        Parallel.For(0, 20, i => Assert.Equal(i % 2 + 1d, graph.Phases[i % 2 == 0 ? "first" : "second"].Lets[0].Value.Evaluate(XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken).RootElement).AsNumber()));
    }

    [Theory]
    [InlineData("item", 3)]
    [InlineData("item[1]", 2)]
    [InlineData("item[last()]", 2)]
    [InlineData("group/item", 2)]
    [InlineData("group//item", 2)]
    [InlineData("/root/item", 1)]
    [InlineData("/", 1)]
    [InlineData("@code", 2)]
    [InlineData("attribute::code", 2)]
    [InlineData("child::item", 3)]
    [InlineData("item | @code", 5)]
    [InlineData("node()", 8)]
    [InlineData("text()", 1)]
    [InlineData("comment()", 1)]
    [InlineData("processing-instruction('x')", 1)]
    [InlineData("*[position()=1 and self::item]", 2)]
    public void MatchPatternsSelectEquivalentRootContextNodeSets(string context, int expected)
    {
        var graph = Compile(Schema(Pattern(Rule(context))));
        var doc = XmlDocument.Parse("<root code='a'><item/><group><item code='b'/><item/></group>text<!--c--><?x p?></root>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(expected, graph.Patterns[0].Rules[0].ContextSelector.Evaluate(doc.DocumentNode).AsNodeSet().Count);
    }

    [Fact]
    public void NamespaceBindingsAreSchNsOnlyAndCompiledExpressionsSurviveSchemaMutation()
    {
        var doc = XmlDocument.Parse(Schema("<ns prefix='p' uri='urn:p'/>" + Pattern(Rule("p:item")), "xmlns:p='urn:other'"), cancellationToken: TestContext.Current.CancellationToken);
        var graph = new SchematronCompiler().Compile(doc, new(), new SchemaCompileOperation());
        doc.RootElement.SetNamespaceDeclaration("p", "urn:changed");
        var instance = XmlDocument.Parse("<item xmlns='urn:p'/>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(graph.Patterns[0].Rules[0].ContextSelector.Evaluate(instance.DocumentNode).AsNodeSet());
        Assert.Empty(Compile(Schema(Pattern(Rule("item")))).Patterns[0].Rules[0].ContextSelector.Evaluate(instance.DocumentNode).AsNodeSet());
        Assert.Equal("SCH.XPath", Reject(Schema(Pattern(Rule("p:item")), "xmlns:p='urn:p'")).Diagnostics[0].Code);
    }

    [Theory]
    [InlineData("<pattern><rule context='root'><assert test='true()'/></rule></pattern>", "queryBinding='xpath2'", "SCH.QueryBinding")]
    [InlineData("<pattern><rule context='root'><assert test='true()'/></rule></pattern>", "queryBinding='XPath1'", "SCH.QueryBinding")]
    [InlineData("<pattern documents='doc.xml'><rule context='root'><assert test='true()'/></rule></pattern>", "", "SCH.QueryBinding")]
    [InlineData("<pattern><rule context='root'><assert test='document(\"x\")'/></rule></pattern>", "", "SCH.XPath")]
    [InlineData("<pattern><rule context='root'><assert test='bad:fn()'/></rule></pattern>", "", "SCH.XPath")]
    [InlineData("<pattern><rule context='root'><assert test='$missing'/></rule></pattern>", "", "SCH.XPath")]
    [InlineData("<let name='a' value='$b'/><let name='b' value='$a'/><pattern><rule context='root'><assert test='true()'/></rule></pattern>", "", "SCH.XPath")]
    [InlineData("<let name='a' value='1'/><let name='a' value='2'/><pattern><rule context='root'><assert test='true()'/></rule></pattern>", "", "SCH.SchemaRestriction")]
    [InlineData("<pattern id='same'/><pattern id='same'/>", "", "SCH.SchemaRestriction")]
    [InlineData("<pattern is-a='missing'/>", "", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='root'><extends rule='absent'/></rule></pattern>", "", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule id='a' abstract='true'><extends rule='b'/></rule><rule id='b' abstract='true'><extends rule='a'/></rule><rule context='root'><extends rule='a'/></rule></pattern>", "", "SCH.SchemaRestriction")]
    [InlineData("<pattern><rule context='root'><assert test='true()' diagnostics='missing'/></rule></pattern>", "", "SCH.SchemaRestriction")]
    [InlineData("<phase id='p'><active pattern='absent'/></phase><pattern/>", "", "SCH.Phase")]
    [InlineData("<phase id='p'><active pattern='a'/><active pattern='a'/></phase><pattern id='a'/>", "", "SCH.Phase")]
    [InlineData("<pattern/>", "defaultPhase='missing'", "SCH.Phase")]
    [InlineData("<pattern/>", "defaultPhase='#DEFAULT'", "SCH.Phase")]
    [InlineData("<phase id='p'/><pattern><rule context='root'><assert test='true()'/></rule></pattern>", "defaultPhase='#ALL'", null)]
    public void SchemaBindingScopeAndReferenceErrorsAreDistinct(string content, string attributes, string? code)
    {
        if (code is null)
        {
            Assert.Equal("#ALL", Compile(Schema(content, attributes)).DefaultPhase);
            return;
        }
        Assert.Equal(code, Reject(Schema(content, attributes)).Diagnostics[0].Code);
    }

    [Theory]
    [InlineData("self::item")]
    [InlineData("ancestor::item")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("$v")]
    [InlineData("1+2")]
    [InlineData("item/")]
    [InlineData("item| ")]
    [InlineData("(item)")]
    [InlineData("id(@code)")]
    [InlineData("key('k','v')")]
    [InlineData("namespace::*")]
    public void NonMatchExpressionsRejectInsteadOfSelectingIncorrectNodes(string context) => Assert.Equal("SCH.XPath", Reject(Schema(Pattern(Rule(context)))).Diagnostics[0].Code);

    [Theory]
    [InlineData("<pattern bad='x'/>")]
    [InlineData("<unknown/>")]
    [InlineData("<pattern><assert test='true()'/></pattern>")]
    [InlineData("<pattern><rule context='root'/></pattern>")]
    [InlineData("<pattern><rule context='root'><assert/></rule></pattern>")]
    [InlineData("<pattern abstract='maybe'/>")]
    [InlineData("<pattern><rule abstract='true' id='a' context='root'><assert test='true()'/></rule></pattern>")]
    [InlineData("<pattern><rule context='root'><assert test='true()'/><let name='a' value='1'/></rule></pattern>")]
    [InlineData("<pattern/><ns prefix='p' uri='urn:p'/>")]
    [InlineData("<ns prefix='p' uri='urn:p'/><ns prefix='p' uri='urn:p'/><pattern/>")]
    [InlineData("<pattern><rule context='root'><assert test='true()'><span>bad</span></assert></rule></pattern>")]
    [InlineData("<pattern><rule context='root'><assert test='true()'><value-of select='.'/><name><p/></name></assert></rule></pattern>")]
    [InlineData("<pattern>unexpected</pattern>")]
    public void GrammarRestrictionsFailAtOriginalLocation(string content)
    {
        var error = Reject(Schema(content));
        Assert.Equal("SCH.SchemaRestriction", error.Diagnostics[0].Code);
        Assert.True(error.Diagnostics[0].Location.LinePosition > 0);
    }

    [Fact]
    public void AnnotationAndPresentationTextArePreservedWithoutExecutableForeignSyntax()
    {
        Assert.Equal("hello", Assert.IsType<SchematronLiteral>(Compile(Schema("<a:note xmlns:a='urn:note'><a:item/></a:note>" + Pattern(Rule(content: "<assert test='true()'>hello</assert>")) + "<p>trailing note</p>")).Patterns[0].Rules[0].Checks[0].Text[0]).Text);
        Assert.Equal("SCH.QueryBinding", Reject(Schema("<xsl:template xmlns:xsl='http://www.w3.org/1999/XSL/Transform'/>" + Pattern(Rule()))).Diagnostics[0].Code);
    }

    [Fact]
    public void FrozenRecordCollectionsSnapshotBothConstructionAndWithUpdates()
    {
        var lets = new List<SchematronLet>();
        var rules = new List<SchematronCompiledRule>();
        var pattern = new SchematronCompiledPattern(0, null, null, lets, rules);
        lets.Add(new(new("", "x"), XPathExpression.Compile("1", cancellationToken: TestContext.Current.CancellationToken)));
        rules.Clear();
        Assert.Empty(pattern.Lets);
        var changed = pattern with
        {
            Lets = lets
        };
        lets.Clear();
        Assert.Single(changed.Lets);
        var texts = new List<SchematronTextPart> { new SchematronLiteral("owned") };
        var map = new Dictionary<string, IReadOnlyList<SchematronTextPart>> { ["d"] = texts };
        var graph = new SchematronCompiledModel(null, "#ALL", new Dictionary<string, string>(), [], [pattern], new Dictionary<string, SchematronCompiledPhase>(), map, map);
        texts.Clear();
        map.Clear();
        Assert.Single(graph.Diagnostics["d"]);
        Assert.Single(graph.Properties["d"]);
    }
    [Fact]
    public void BareIdIsAnOrdinaryNameAndUnusedAbstractReferencesMustResolve()
    {
        var graph = Compile(Schema(Pattern(Rule("id"))));
        var instance = XmlDocument.Parse("<root><id/><group><id/></group></root>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, graph.Patterns[0].Rules[0].ContextSelector.Evaluate(instance.DocumentNode).AsNodeSet().Count);
        Assert.Equal("SCH.SchemaRestriction", Reject(Schema(Pattern("<rule abstract='true' id='unused'><assert test='true()' diagnostics='missing'/></rule>" + Rule()))).Diagnostics[0].Code);
    }

    [Fact]
    public void UnrelatedPhaseNamesCannotResolveAnotherPatternsVariables()
    {
        Assert.Equal("SCH.XPath", Reject(Schema("<phase id='first'><let name='onlyFirst' value='1'/><active pattern='firstPattern'/></phase>" +
            "<phase id='second'><let name='onlySecond' value='1'/><active pattern='secondPattern'/></phase>" +
            Pattern(Rule(content: "<assert test='$onlySecond=1'/>"), "id='firstPattern'") + Pattern(Rule(), "id='secondPattern'"))).Diagnostics[0].Code);
        Assert.Equal("SCH.XPath", Reject(Schema("<phase id='first'><let name='v' value='1'/><active pattern='shared'/></phase>" +
            "<phase id='second'><active pattern='shared'/></phase>" + Pattern(Rule(content: "<assert test='$v=1'/>"), "id='shared'"))).Diagnostics[0].Code);
    }

    [Fact]
    public void ContentValuedLetsAreAQueryBindingBoundaryAndAnnotationsRemainAnnotations()
    {
        Assert.Equal("SCH.QueryBinding", Reject(Schema("<let name='content'><a:node xmlns:a='urn:a'>text</a:node></let>" + Pattern(Rule()))).Diagnostics[0].Code);
        Assert.Empty(Compile(Schema(Pattern("<rule abstract='true' id='r'><assert test='true()'/></rule>", "abstract='true' id='p'"))).Patterns);
    }

    [Fact]
    public void ShadowingLetCanReferToTheOuterBindingInItsOwnValue()
    {
        var graph = Compile(Schema("<let name='x' value='1'/>" +
            Pattern("<let name='x' value='$x+1'/><rule context='root'><let name='x' value='$x+1'/><assert test='$x=3'/></rule>")));
        var document = XmlDocument.Parse("<root/>", cancellationToken: TestContext.Current.CancellationToken);
        var variables = new Dictionary<XPathExpandedName, XPathValue>();
        var name = new XPathExpandedName("", "x");
        variables[name] = graph.Lets[0].Value.Evaluate(document.RootElement);
        variables[name] = graph.Patterns[0].Lets[0].Value.Evaluate(document.RootElement, new()
        {
            Variables = variables
        });
        variables[name] = graph.Patterns[0].Rules[0].Lets[0].Value.Evaluate(document.RootElement, new()
        {
            Variables = variables
        });
        Assert.Equal(3d, variables[name].AsNumber());
        Assert.True(graph.Patterns[0].Rules[0].Checks[0].Test.Evaluate(document.RootElement, new()
        {
            Variables = variables
        }).AsBoolean());
    }

    [Fact]
    public void UnnamespacedAnnotationsAreIgnoredButNestedExecutableSyntaxIsRejected()
    {
        Assert.Single(Compile(Schema("<note xmlns=''><nested>annotation</nested></note>" + Pattern(Rule()))).Patterns);
        Assert.Equal("SCH.QueryBinding", Reject(Schema("<note xmlns=''><xsl:template xmlns:xsl='http://www.w3.org/1999/XSL/Transform'/></note>" + Pattern(Rule()))).Diagnostics[0].Code);
        Assert.Equal("SCH.QueryBinding", Reject(Schema("<let name='content'><note xmlns=''>template value</note></let>" + Pattern(Rule()))).Diagnostics[0].Code);
    }

    [Fact]
    public void SelfWithoutOuterAndOtherLocalEdgesCannotEscapeCyclesThroughOuterNames()
    {
        Assert.Equal("SCH.XPath", Reject(Schema("<let name='x' value='$x'/>" + Pattern(Rule()))).Diagnostics[0].Code);
        Assert.Equal("SCH.XPath", Reject(Schema(Pattern("<let name='x' value='$x'/>" + Rule()))).Diagnostics[0].Code);
        Assert.Equal("SCH.XPath", Reject(Schema("<let name='x' value='1'/>" + Pattern("<let name='x' value='$y'/><let name='y' value='$x'/>" + Rule()))).Diagnostics[0].Code);
        Assert.Equal("SCH.SchemaRestriction", Reject(Schema("<let name='x' value='1'/>" + Pattern("<let name='x' value='2'/><let name='x' value='3'/>" + Rule()))).Diagnostics[0].Code);
    }

}
