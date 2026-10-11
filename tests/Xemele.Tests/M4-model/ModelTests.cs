using System.Globalization;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M4Model;
public sealed class ModelTests
{
    private static (XmlDocument Document, XmlNode Root) Tree()
    { var d = new XmlDocument(); var r = d.CreateElement("r"); d.DocumentNode.AppendChild(r); return (d, r); }
    [Fact]
    public void DefaultsAndScalarConversions()
    {
        XPathValue value = default;
        Assert.Equal(XPathValueKind.NodeSet, value.Kind); Assert.Empty(value.AsNodeSet()); Assert.False(value.AsBoolean()); Assert.Equal("", value.AsString()); Assert.True(double.IsNaN(value.AsNumber()));
        Assert.Equal(1, XPathValue.FromBoolean(true).AsNumber()); Assert.Equal("false", XPathValue.FromBoolean(false).AsString());
        Assert.True(XPathValue.FromString("false").AsBoolean()); Assert.False(XPathValue.FromString("").AsBoolean());
        foreach (double n in new[] { 0d, -0d, double.NaN }) Assert.False(XPathValue.FromNumber(n).AsBoolean());
        Assert.True(XPathValue.FromNumber(double.PositiveInfinity).AsBoolean());
        Assert.Throws<ArgumentNullException>(() => XPathValue.FromString(null!));
        Assert.Throws<InvalidOperationException>(() => XPathValue.FromNumber(1).AsNodeSet());
    }
    [Theory]
    [InlineData(" \t\n-12.50\r", -12.5)] [InlineData(".5", .5)] [InlineData("1.", 1)] [InlineData("-.25", -.25)]
    [InlineData("01", 1)] [InlineData("-0", 0)]
    public void NumberGrammar(string input, double expected) => Assert.Equal(expected, XPathValue.FromString(input).AsNumber());
    [Theory]
    [InlineData("")] [InlineData("-")] [InlineData(".")] [InlineData("-.")] [InlineData("+1")] [InlineData("1e2")] [InlineData("Infinity")]
    [InlineData("NaN")] [InlineData("1,2")] [InlineData("\u00a01")] [InlineData("1\u0085")] [InlineData("1 2")] [InlineData("١")]
    [InlineData("😀")] [InlineData("\uD800")]
    public void RejectNonXPathNumbers(string input) => Assert.True(double.IsNaN(XPathValue.FromString(input).AsNumber()));
    [Fact]
    public void NumberFormattingIsShortestInvariantAndNeverExponent()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "fr-FR", "tr-TR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = new(culture);
                Assert.Equal("0", XPathValue.FromNumber(-0d).AsString()); Assert.Equal("NaN", XPathValue.FromNumber(double.NaN).AsString());
                Assert.Equal("Infinity", XPathValue.FromNumber(double.PositiveInfinity).AsString()); Assert.Equal("-Infinity", XPathValue.FromNumber(double.NegativeInfinity).AsString());
                Assert.Equal("100000000000000000000", XPathValue.FromNumber(1e20).AsString()); Assert.Equal("0.0000001", XPathValue.FromNumber(1e-7).AsString());
                foreach (double n in new[] { 1d, -2.5, double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue, Math.PI, 1e100, 1e-100, 1000000000000000128d })
                {
                    string s = XPathValue.FromNumber(n).AsString(); Assert.DoesNotContain("E", s); Assert.DoesNotContain("e", s);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(n), BitConverter.DoubleToInt64Bits(XPathValue.FromString(s).AsNumber()));
                }
                Assert.Equal("根😀", XPathValue.FromString("根😀").AsString());
            }
        }
        finally { CultureInfo.CurrentCulture = prior; }
        Assert.Equal(double.PositiveInfinity, XPathValue.FromString(new string('9', 400)).AsNumber());
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(XPathValue.FromString("-0").AsNumber()));
    }
    [Fact]
    public void DefaultItemsRejectAllMetadataAndHandleProperties()
    {
        XPathItem item = default; Assert.True(item.IsDefault); Assert.Equal(default, item); Assert.Equal(0, item.GetHashCode());
        foreach (var property in typeof(XPathItem).GetProperties().Where(p => p.Name != nameof(XPathItem.IsDefault)))
            Assert.IsType<InvalidOperationException>(Assert.Throws<System.Reflection.TargetInvocationException>(() => property.GetValue(item)).InnerException);
        Assert.Throws<ArgumentException>(() => XPathItem.FromNode(default)); Assert.Throws<ArgumentException>(() => XPathItem.FromAttribute(default));
        Assert.Throws<ArgumentException>(() => XPathValue.FromNodeSet([default])); Assert.Throws<ArgumentNullException>(() => XPathValue.FromNodeSet(null!));
    }
    [Fact]
    public void TextRunsAndContainerStringsIgnoreCommentAndPi()
    {
        var (d, r) = Tree(); var empty = d.CreateText(""); var a = d.CreateCData("根😀"); var b = d.CreateText("B");
        r.AppendChild(empty); r.AppendChild(a); r.AppendChild(b);
        var comment = d.CreateComment("ignored"); r.AppendChild(comment); var pi = d.CreateProcessingInstruction("go", "data"); r.AppendChild(pi);
        var child = d.CreateElement("x"); r.AppendChild(child); child.AppendChild(d.CreateText("C"));
        var t = XPathItem.FromNode(b); Assert.Equal(empty, t.Node); Assert.Equal("根😀B", t.StringValue); Assert.Equal(t, XPathItem.FromNode(a)); Assert.Equal(t, XPathItem.FromNode(empty));
        Assert.Equal(XPathItemKind.Text, t.Kind); Assert.Equal("", t.Name); Assert.Null(t.OwnerElement); Assert.Null(t.Attribute);
        Assert.Equal("根😀BC", XPathItem.FromNode(r).StringValue); Assert.Equal("根😀BC", XPathItem.FromNode(d.DocumentNode).StringValue);
        Assert.Equal("ignored", XPathItem.FromNode(comment).StringValue); Assert.Equal("go", XPathItem.FromNode(pi).Name);
        Assert.Equal("", XPathItem.FromNode(pi).Prefix); Assert.Equal("", XPathItem.FromNode(pi).NamespaceUri);
        a.Value = "changed"; Assert.Equal("根😀B", t.StringValue); Assert.Equal(t, XPathItem.FromNode(a));
        var onlyEmpty = d.CreateText(""); child.AppendChild(d.CreateComment("boundary")); child.AppendChild(onlyEmpty);
        Assert.Throws<ArgumentException>(() => XPathItem.FromNode(onlyEmpty));
        b.Remove(); Assert.Throws<ArgumentException>(() => XPathItem.FromNode(b));
        var detached = d.CreateElement("detached"); detached.AppendChild(d.CreateText("x"));
        Assert.Throws<ArgumentException>(() => XPathItem.FromNode(detached.FirstChild)); Assert.Throws<ArgumentException>(() => XPathItem.FromNode(d.CreateFragment()));
        Assert.Equal("", XPathItem.FromNode(new XmlDocument().DocumentNode).StringValue);
    }
    [Fact]
    public void AttributeAndVirtualNamespaceIdentitiesAreDistinctAndSnapshotsStayStable()
    {
        var (d, r) = Tree(); var c = d.CreateElement("x", "urn:p", "p"); r.AppendChild(c); r.SetNamespaceDeclaration("p", "urn:p");
        var attribute = c.SetAttribute("a", "before", "urn:p", "p"); var a = XPathItem.FromAttribute(attribute);
        Assert.Equal("p:a", a.Name); Assert.Equal("urn:p", a.NamespaceUri); Assert.Null(a.Node); Assert.Equal(attribute, a.Attribute); Assert.Equal(c, a.OwnerElement);
        var ns = XPathItem.FromNamespace(c, "p"); Assert.Equal("p", ns.Name); Assert.Equal("p", ns.LocalName); Assert.Equal("", ns.NamespaceUri); Assert.Equal("", ns.Prefix); Assert.Null(ns.Node); Assert.Null(ns.Attribute);
        Assert.NotEqual(ns, XPathItem.FromNamespace(r, "p")); Assert.NotEqual(a, XPathItem.FromNode(c)); Assert.NotEqual(ns, XPathItem.FromNode(c)); Assert.NotEqual(ns, a);
        attribute.Value = "after"; r.SetNamespaceDeclaration("p", "urn:new"); Assert.Equal("before", a.StringValue); Assert.Equal("urn:p", ns.StringValue);
        Assert.Equal(ns, XPathItem.FromNamespace(c, "p")); Assert.Equal(a, XPathItem.FromAttribute(attribute));
        attribute.Remove(); var replacement = c.SetAttribute("a", "new", "urn:p", "p"); Assert.NotEqual(a, XPathItem.FromAttribute(replacement));
        Assert.Throws<ArgumentException>(() => XPathItem.FromAttribute(attribute)); Assert.Equal("before", a.StringValue);
        Assert.Equal(TreeValidation.XmlUri, XPathItem.FromNamespace(c, "xml").StringValue);
        Assert.Throws<ArgumentException>(() => XPathItem.FromNamespace(c, "xmlns")); Assert.Throws<ArgumentException>(() => XPathItem.FromNamespace(c, "missing"));
        r.SetNamespaceDeclaration("", "urn:default"); Assert.Equal("urn:default", XPathItem.FromNamespace(c, "").StringValue);
        c.SetNamespaceDeclaration("", ""); Assert.Throws<ArgumentException>(() => XPathItem.FromNamespace(c, ""));
        Assert.Throws<ArgumentException>(() => XPathItem.FromNamespace(d.DocumentNode, "xml")); Assert.Throws<ArgumentNullException>(() => XPathItem.FromNamespace(c, null!));
        c.Remove(); Assert.Equal(c, ns.OwnerElement); Assert.Throws<ArgumentException>(() => XPathItem.FromNamespace(c, "p"));
    }
    [Fact]
    public void NodeSetEnumeratesOnceNormalizesAndOwnsMembershipOrderAndValues()
    {
        var (d, r) = Tree(); r.SetNamespaceDeclaration("z", "urn:z"); r.SetNamespaceDeclaration("a", "urn:a");
        var z = r.SetAttribute("z", "Z"); var a = r.SetAttribute("a", "A"); var text = d.CreateText("text"); r.AppendChild(text);
        XPathItem[] expected = [XPathItem.FromNode(d.DocumentNode), XPathItem.FromNode(r), XPathItem.FromNamespace(r,"a"), XPathItem.FromNamespace(r,"xml"), XPathItem.FromNamespace(r,"z"), XPathItem.FromAttribute(a), XPathItem.FromAttribute(z), XPathItem.FromNode(text)];
        var list = expected.Reverse().Concat(expected).ToList(); int enumerations = 0;
        IEnumerable<XPathItem> Once() { if (++enumerations != 1) throw new InvalidOperationException(); foreach (var item in list) yield return item; }
        var value = XPathValue.FromNodeSet(Once()); Assert.Equal(1, enumerations); Assert.Equal(expected, value.AsNodeSet()); list.Clear(); Assert.Equal(expected, value.AsNodeSet());
        Assert.Throws<NotSupportedException>(() => ((IList<XPathItem>)value.AsNodeSet())[0] = default);
        Assert.Equal("text", value.AsString()); Assert.True(value.AsBoolean());
        text.Value = "different"; z.Remove(); r.Remove(); Assert.Equal(expected, value.AsNodeSet()); Assert.Equal("text", value.AsString());
        Assert.Throws<ArgumentException>(() => XPathValue.FromNodeSet([expected[1]]));
        var (other, or) = Tree(); Assert.Throws<ArgumentException>(() => XPathValue.FromNodeSet([expected[0], XPathItem.FromNode(or)])); GC.KeepAlive(other);
    }
    [Fact]
    public void NodeSetOrderUsesTopologyNotCreationSlots()
    {
        var (d, r) = Tree(); var later = d.CreateElement("later"); var earlier = d.CreateElement("earlier"); r.AppendChild(earlier); r.AppendChild(later);
        var first = XPathItem.FromNode(earlier); var second = XPathItem.FromNode(later);
        Assert.Equal(new[] { first, second }, XPathValue.FromNodeSet([second, first]).AsNodeSet());
        r.AppendChild(earlier); Assert.Equal(new[] { second, first }, XPathValue.FromNodeSet([first, second]).AsNodeSet());
        var a = r.SetAttribute("b", "", "urn:a", "p"); var b = r.SetAttribute("a", "", "urn:z", "q"); var c = r.SetAttribute("a", "", "urn:a", "p");
        Assert.Equal(new[] { XPathItem.FromAttribute(c), XPathItem.FromAttribute(a), XPathItem.FromAttribute(b) }, XPathValue.FromNodeSet([XPathItem.FromAttribute(b), XPathItem.FromAttribute(a), XPathItem.FromAttribute(c)]).AsNodeSet());
    }
    [Fact]
    public void AstSnapshotsAndPayloadValidation()
    {
        var literal = new XPathLiteral(XPathValue.FromNumber(2), new(0,1)); XPathAst[] arguments = [literal];
        var call = new XPathCall(new(new("", "count"),1,1), arguments,new(0,8)); arguments[0] = new XPathLiteral(XPathValue.FromString("changed"),default);
        Assert.Same(literal, call.Arguments[0]); Assert.Throws<NotSupportedException>(() => ((IList<XPathAst>)call.Arguments)[0] = arguments[0]);
        XPathAst[] predicates = [literal]; var step = new XPathStep(XPathAxis.Child,new(XPathNodeTestKind.Name,new("","r"),null),predicates,new(0,1));
        XPathStep[] steps = [step]; var path = new XPathPath(true,null,steps,new(0,1)); var filter = new XPathFilter(path,predicates,default);
        predicates[0] = arguments[0]; steps[0] = new(XPathAxis.Self,new(XPathNodeTestKind.Node,default,null),[],default);
        Assert.Same(step,path.Steps[0]); Assert.Same(literal,step.Predicates[0]); Assert.Same(literal,filter.Predicates[0]);
        Assert.Throws<ArgumentException>(()=>new XPathPath(true,literal,[],default)); Assert.Throws<ArgumentNullException>(()=>new XPathUnaryMinus(null!,default));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathBinary((XPathBinaryOperator)99,literal,literal,default));
        Assert.Throws<ArgumentNullException>(()=>new XPathVariable(default,default)); Assert.Throws<ArgumentException>(()=>new XPathLiteral(default,default));
        Assert.Throws<ArgumentNullException>(()=>new XPathCall(call.Signature,[null!],default)); Assert.Throws<ArgumentException>(()=>new XPathCall(call.Signature,[],default));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathStep((XPathAxis)99,step.Test,[],default));
        Assert.Throws<ArgumentException>(()=>new XPathNodeTest(XPathNodeTestKind.Node,new("","r"),null));
        Assert.Throws<ArgumentException>(()=>new XPathNodeTest(XPathNodeTestKind.Text,default,"pi"));
        var wildcard = XPathExpandedName.NamespaceWildcard("urn:p"); Assert.Equal("urn:p",new XPathNodeTest(XPathNodeTestKind.NamespaceWildcard,wildcard,null).Name.NamespaceUri);
        Assert.Throws<ArgumentException>(()=>new XPathNodeTest(XPathNodeTestKind.Name,wildcard,null));
        Assert.Throws<ArgumentException>(()=>new XPathFunctionSignature(wildcard,0,0)); Assert.Throws<ArgumentException>(()=>new XPathVariable(wildcard,default));
        Assert.Throws<ArgumentException>(()=>new XPathNodeTest(XPathNodeTestKind.NamespaceWildcard,new("urn:p","r"),null));
        Assert.Throws<ArgumentException>(()=>new XPathNodeTest(XPathNodeTestKind.ProcessingInstruction,default,"\uD800"));
    }
    [Fact]
    public void NamesSpansDiagnosticsAndPositiveLimitsValidateConstructorsAndClones()
    {
        foreach(string local in new[]{"", "p:a", "1x", "\uD800"}) Assert.Throws<ArgumentException>(()=>new XPathExpandedName("",local));
        Assert.Equal(new XPathExpandedName("urn:p","根😀"),new XPathExpandedName("urn:p","根😀"));
        Assert.Throws<ArgumentNullException>(()=>new XPathExpandedName(null!,"a")); Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathSourceSpan(-1,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathSourceSpan(int.MaxValue,1)); Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathSourceSpan(1,1) with { Start=int.MaxValue });
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathCompileLimits{MaxTokens=0}); Assert.Throws<ArgumentOutOfRangeException>(()=>new XPathEvaluationLimits{MaxOperations=-1});
        Assert.Null(new XPathCompileLimits{MaxDepth=null}.MaxDepth); Assert.Null(new XPathEvaluationLimits{MaxInputCharacters=null}.MaxInputCharacters);
        var signature = new XPathFunctionSignature(new("urn:p","f"),1,2);
        Assert.Throws<ArgumentOutOfRangeException>(()=>signature with {MaxArguments=0}); Assert.Throws<ArgumentOutOfRangeException>(()=>signature with {MinArguments=3});
        var diag = new XPathDiagnostic("XPATH-SYNTAX",XPathDiagnosticCategory.Syntax,"Bad expression.",new(2,1));
        Assert.Same(diag,new XPathCompileException(diag).Diagnostic); Assert.Throws<ArgumentException>(()=>new XPathEvaluationException(diag));
        Assert.Throws<ArgumentNullException>(()=>new XPathCompileException(null!)); Assert.Throws<ArgumentException>(()=>diag with {Code=""});
        Assert.Throws<ArgumentException>(()=>diag with {Message=new string('x',257)}); Assert.Throws<ArgumentOutOfRangeException>(()=>diag with {Category=(XPathDiagnosticCategory)99});
    }
    [Fact]
    public void StaticAndDynamicSnapshotsValidateAndOwnCallerCollections()
    {
        var name = new XPathExpandedName("urn:p","f"); var signature = new XPathFunctionSignature(name,0,1);
        var namespaces = new Dictionary<string,string>{{"p","urn:p"}}; var signatures = new List<XPathFunctionSignature>{signature};
        var context = XPathStaticContext.FromOptions(new(){Namespaces=namespaces,Functions=signatures}); namespaces.Clear(); signatures.Clear();
        Assert.Equal("urn:p",context.Namespaces["p"]); Assert.Equal(signature,context.Functions[name]); Assert.Equal(28,context.Functions.Count);
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,string>)context.Namespaces).Add("q","urn:q"));
        foreach(var binding in new[]{("","urn:p"),("xmlns","urn:p"),("xml","wrong"),("p","")})
            Assert.Throws<ArgumentException>(()=>XPathStaticContext.FromOptions(new(){Namespaces=new Dictionary<string,string>{{binding.Item1,binding.Item2}}}));
        Assert.Throws<ArgumentException>(()=>XPathStaticContext.FromOptions(new(){Functions=[signature,signature]}));
        Assert.Throws<ArgumentException>(()=>XPathStaticContext.FromOptions(new(){Functions=[new(new("","string"),0,1)]}));
        var variables = new Dictionary<XPathExpandedName,XPathValue>{{new("","v"),XPathValue.FromString("old")}};
        XPathExtensionFunction fn=(_,_)=>default; var functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{name,fn}};
        var dynamic = new XPathDynamicContext(new(){Variables=variables,Functions=functions}); variables.Clear(); functions.Clear();
        Assert.Equal("old",dynamic.Variables[new("","v")].AsString()); Assert.Same(fn,dynamic.Functions[name]);
        Assert.Throws<ArgumentNullException>(()=>new XPathDynamicContext(new(){Variables=new Dictionary<XPathExpandedName,XPathValue>{{default,default}}}));
        Assert.Throws<ArgumentException>(()=>new XPathDynamicContext(new(){Variables=new Dictionary<XPathExpandedName,XPathValue>{{XPathExpandedName.NamespaceWildcard("urn:p"),default}}}));
    }
    [Fact]
    public void FunctionFocusAndSharedBudgetChargeExactlyAndCancelWithToken()
    {
        var (d,r)=Tree(); var budget=new XPathBudget(new(){MaxOperations=2,MaxNodeSetItems=2,MaxStringCharacters=2,MaxInputItems=2,MaxInputCharacters=2},new(0,3),default);
        var context=new XPathFunctionContext(new(XPathItem.FromNode(r),1,2),budget);
        Assert.Equal(r,context.ContextItem.Node); Assert.Equal(1,context.Position); Assert.Equal(2,context.Size);
        context.ChargeOperations(0); context.ChargeOperations(2); Assert.Equal(2,budget.Operations);
        Assert.Throws<ArgumentOutOfRangeException>(()=>context.ChargeOperations(-1)); var error=Assert.Throws<XPathEvaluationException>(()=>context.ChargeOperations(1));
        Assert.Equal("XPATH-EVALUATION-LIMIT",error.Diagnostic.Code); Assert.Equal(new(0,3),error.Diagnostic.Span); Assert.Equal(2,budget.Operations);
        budget.CheckNodeSetSize(2); budget.CheckStringSize(2); budget.CheckInputSize(2,2);
        Assert.Throws<XPathEvaluationException>(()=>budget.CheckNodeSetSize(3)); Assert.Throws<XPathEvaluationException>(()=>budget.CheckStringSize(3)); Assert.Throws<XPathEvaluationException>(()=>budget.CheckInputSize(2,3));
        Assert.Throws<ArgumentException>(()=>new XPathFunctionContext(new(XPathItem.FromNode(r),0,1),budget)); Assert.Throws<ArgumentException>(()=>new XPathFunctionContext(new(default,1,1),budget));
        var unlimited=new XPathBudget(new(){MaxOperations=null},default,default); unlimited.ChargeOperations(long.MaxValue); Assert.Throws<XPathEvaluationException>(()=>unlimited.ChargeOperations(1));
        using var source=new CancellationTokenSource(); var cancel=new XPathBudget(new(),default,source.Token); var function=new XPathFunctionContext(new(XPathItem.FromNode(r),1,1),cancel);
        source.Cancel(); var canceled=Assert.Throws<OperationCanceledException>(()=>function.ChargeOperations(0)); Assert.Equal(source.Token,canceled.CancellationToken); Assert.Equal(source.Token,function.CancellationToken);
        GC.KeepAlive(d);
    }
    [Fact]
    public void CapturedMetadataDoesNotReadMutatedOrDetachedLiveTree()
    {
        var (d,r)=Tree(); var attribute=r.SetAttribute("a","live");
        var identity=new ItemIdentity(XPathItemKind.Attribute,r,attribute,null); attribute.Remove(); r.Remove();
        var captured=new XPathItem(identity,"snapshot","a","urn:captured","p");
        Assert.Equal("snapshot",captured.StringValue); Assert.Equal("p:a",captured.Name); Assert.Equal(attribute,captured.Attribute); Assert.Equal(r,captured.OwnerElement);
        Assert.Throws<ArgumentException>(()=>new XPathItem(new(XPathItemKind.Attribute,r,default,null),"x"));
        Assert.Throws<ArgumentException>(()=>new XPathItem(new(XPathItemKind.Namespace,r,default,null),"x"));
        GC.KeepAlive(d);
    }
    [Fact]
    public void StaticContextCopyChargesAreSeparateAndBounded()
    {
        var options=new XPathCompileOptions{Namespaces=new Dictionary<string,string>{{"p","urn:p"}},Functions=[new(new("urn:p","f"),0,0)],Limits=new(){MaxTokens=2,MaxExpressionCharacters=12}};
        var context=XPathStaticContext.FromOptions(options); Assert.Equal(2,context.ContextTokens); Assert.Equal(12,context.ContextCharacters);
        Assert.Throws<XPathCompileException>(()=>XPathStaticContext.FromOptions(options with {Limits=new(){MaxTokens=1}}));
        Assert.Throws<XPathCompileException>(()=>XPathStaticContext.FromOptions(options with {Limits=new(){MaxExpressionCharacters=11}}));
    }
    [Fact]
    public void CoreSignatureMetadataMatchesAllFrozenArities()
    {
        string[] names=["last","position","count","id","local-name","namespace-uri","name","string","concat","starts-with","contains","substring-before","substring-after","substring","string-length","normalize-space","translate","boolean","not","true","false","lang","number","sum","floor","ceiling","round"];
        Assert.Equal(names,XPathCoreSignatures.All.Select(s=>s.Name.LocalName)); Assert.Equal(27,XPathCoreSignatures.ByName.Count);
        int[] minimum=[0,0,1,1,0,0,0,0,2,2,2,2,2,2,0,0,3,1,1,0,0,1,0,1,1,1,1];
        int?[] maximum=[0,0,1,1,1,1,1,1,null,2,2,2,2,3,1,1,3,1,1,0,0,1,1,1,1,1,1];
        Assert.Equal(minimum,XPathCoreSignatures.All.Select(s=>s.MinArguments)); Assert.Equal(maximum,XPathCoreSignatures.All.Select(s=>s.MaxArguments));
        Assert.Null(XPathCoreSignatures.ByName[new("","concat")].MaxArguments); Assert.Equal(2,XPathCoreSignatures.ByName[new("","concat")].MinArguments);
        Assert.Equal(3,XPathCoreSignatures.ByName[new("","substring")].MaxArguments); Assert.Equal(0,XPathCoreSignatures.ByName[new("","number")].MinArguments);
        Assert.Throws<NotSupportedException>(()=>((IList<XPathFunctionSignature>)XPathCoreSignatures.All).Clear());
    }
}
