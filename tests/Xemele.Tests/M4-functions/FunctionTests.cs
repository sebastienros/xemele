using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Numerics;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;

namespace Xemele.Tests.M4Functions;

public sealed class FunctionTests
{
    private readonly XPathCoreFunctions library = new();
    private readonly Fixture fixture = new();
    private static XPathValue S(string text) => XPathValue.FromString(text);
    private static XPathValue N(double number) => XPathValue.FromNumber(number);
    private XPathValue Call(string name, params XPathValue[] arguments) => Invoke(name, arguments, new XPathBudget(new(), default, TestContext.Current.CancellationToken));
    private XPathValue Invoke(string name, IReadOnlyList<XPathValue> arguments, IXPathBudget budget) => library.Invoke(XPathCoreSignatures.ByName[new("", name)], fixture.Focus, arguments, fixture, budget);

    [Fact]
    public void EveryFrozenFunctionVectorRunsAgainstProductionLibrary()
    {
        // Exact frozen function-01..27 arguments: compiler/navigation are approved
        // snapshot fakes, never peer implementations. Inventory is checked below.
        (string Name, XPathValue[] Args, object Expected)[] vectors = [
            ("last", [], 1d), ("position", [], 1d), ("count", [fixture.Set(fixture.X1, fixture.X2)], 2d),
            ("id", [S("one one missing")], new[]{fixture.X1}),
            ("local-name", [fixture.Set(fixture.Y)], "y"), ("namespace-uri", [fixture.Set(fixture.Y)], "urn:p"),
            ("name", [fixture.Set(fixture.Y)], "p:y"), ("string", [fixture.Set(fixture.X1)], "ABC"),
            ("concat", [S("a"), S("b"), S("c")], "abc"), ("starts-with", [S("abc"), S("ab")], true),
            ("contains", [S("abc"), S("b")], true), ("substring-before", [S("abc"), S("b")], "a"),
            ("substring-after", [S("abc"), S("b")], "c"), ("substring", [S("12345"), N(1.5), N(2.6)], "234"),
            ("string-length", [S("A😀B")], 3d), ("normalize-space", [S("  A\t B\n ")], "A B"),
            ("translate", [S("abba"), S("ab"), S("X")], "XX"), ("boolean", [fixture.Set(fixture.X1, fixture.X2)], true),
            ("not", [default], true), ("true", [], true), ("false", [], false), ("lang", [S("en")], true),
            ("number", [S(" -2.5 ")], -2.5d), ("sum", [fixture.Set(fixture.A1, fixture.A2)], 3d),
            ("floor", [N(-1.2)], -2d), ("ceiling", [N(-1.2)], -1d), ("round", [N(-1.5)], -1d)
        ];
        Assert.Equal(library.Signatures.Select(s => s.Name.LocalName), vectors.Select(v => v.Name));
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/tasks/M4-functions.json"))) root = root.Parent;
        Assert.NotNull(root);
        using var frozen = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"tests/Contracts/Fixtures/M4/inventory.json")));
        using var owned = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,"tests/fixtures/M4-functions/core-vectors.json")));
        Assert.Equal(JsonSerializer.Serialize(frozen.RootElement.GetProperty("functions")), JsonSerializer.Serialize(owned.RootElement));
        int vectorIndex = 0;
        foreach (var (name, args, expected) in vectors)
        {
            var row = owned.RootElement[vectorIndex++];
            Assert.Equal(name,row.GetProperty("name").GetString());
            var signature = XPathCoreSignatures.ByName[new("",name)];
            Assert.Equal(signature.MinArguments,row.GetProperty("min_arguments").GetInt32());
            Assert.Equal(signature.MaxArguments,row.GetProperty("max_arguments").ValueKind == JsonValueKind.Null ? null : row.GetProperty("max_arguments").GetInt32());
            var output = row.GetProperty("expected");
            if (expected is string expectedText) Assert.Equal(expectedText,output.GetString());
            if (expected is double expectedNumber) Assert.Equal(expectedNumber,output.GetDouble());
            if (expected is bool expectedBoolean) Assert.Equal(expectedBoolean,output.GetBoolean());
            if (expected is XPathItem[]) Assert.Equal("/r/x[1]",Assert.Single(output.EnumerateArray()).GetString());
            XPathValue actual = Call(name, args);
            switch (expected)
            {
                case string text: Assert.Equal(text, actual.AsString()); break;
                case double number: Assert.Equal(number, actual.AsNumber()); break;
                case bool boolean: Assert.Equal(boolean, actual.AsBoolean()); break;
                case XPathItem[] nodes: Assert.Equal(nodes, actual.AsNodeSet()); break;
                default: throw new InvalidOperationException();
            }
        }
    }

    [Fact]
    public void AllSignaturesArityFocusAndCoreOnlyDispatchAreValidated()
    {
        Assert.Same(XPathCoreSignatures.All, library.Signatures);
        Assert.Throws<NotSupportedException>(() => ((IList<XPathFunctionSignature>)library.Signatures).Clear());
        foreach (var signature in library.Signatures)
        {
            var budget = new XPathBudget(new(), default, TestContext.Current.CancellationToken);
            if (signature.MinArguments > 0)
                Assert.Throws<ArgumentException>(() => library.Invoke(signature, fixture.Focus, new XPathValue[signature.MinArguments - 1], fixture, budget));
            if (signature.MaxArguments is int max)
                Assert.Throws<ArgumentException>(() => library.Invoke(signature, fixture.Focus, new XPathValue[max + 1], fixture, budget));
            foreach (var focus in new[]{new XPathFocus(default,1,1), new(fixture.X1,0,1),new(fixture.X1,2,1)})
                Assert.Throws<ArgumentException>(() => library.Invoke(signature, focus, new XPathValue[signature.MinArguments], fixture, budget));
        }
        Assert.Throws<ArgumentException>(() => library.Invoke(new(new("urn:p","true"),0,0),fixture.Focus,[],fixture,new XPathBudget(new(),default,default)));
        Assert.Throws<ArgumentException>(() => library.Invoke(new(new("","true"),0,1),fixture.Focus,[],fixture,new XPathBudget(new(),default,default)));
        foreach (string name in new[]{"count","sum","name","local-name","namespace-uri"})
            Assert.Equal("XPATH-TYPE",Assert.Throws<XPathEvaluationException>(()=>Call(name,N(1))).Diagnostic.Code);
        Assert.Equal(3,library.Invoke(XPathCoreSignatures.ByName[new("","last")],new(fixture.X1,2,3),[],fixture,new XPathBudget(new(),default,default)).AsNumber());
        Assert.Equal(2,library.Invoke(XPathCoreSignatures.ByName[new("","position")],new(fixture.X1,2,3),[],fixture,new XPathBudget(new(),default,default)).AsNumber());
    }

    [Fact]
    public void OptionalArgumentsAndEveryItemKindUseCapturedMetadata()
    {
        Assert.Equal("D",Call("string").AsString()); Assert.True(double.IsNaN(Call("number").AsNumber()));
        Assert.Equal(1,Call("string-length").AsNumber());Assert.Equal("D",Call("normalize-space").AsString());
        Assert.Equal("x",Call("name").AsString());Assert.Equal("x",Call("local-name").AsString());Assert.Equal("",Call("namespace-uri").AsString());
        foreach (var item in fixture.All)
        {
            var set = fixture.Set(item);
            Assert.Equal(item.Name,Call("name",set).AsString());Assert.Equal(item.LocalName,Call("local-name",set).AsString());
            Assert.Equal(item.NamespaceUri,Call("namespace-uri",set).AsString());Assert.Equal(item.StringValue,Call("string",set).AsString());
        }
        foreach (string name in new[]{"name","local-name","namespace-uri","string"}) Assert.Equal("",Call(name,default(XPathValue)).AsString());
        Assert.True(double.IsNaN(Call("number",default(XPathValue)).AsNumber()));Assert.Equal(0,Call("sum",default(XPathValue)).AsNumber());
        Assert.Equal("ABC",Call("string",fixture.Set(fixture.X1,fixture.X2)).AsString());
        Assert.True(double.IsNaN(Call("sum",fixture.Set(fixture.X1,fixture.A1)).AsNumber()));
        fixture.RemoveLiveNodes();
        Assert.Equal("ABC",Call("string",fixture.Set(fixture.X1)).AsString());
        Assert.Equal(new[]{fixture.X1},Call("id",S("one")).AsNodeSet());
    }

    [Theory]
    [InlineData("-0", 0d)] [InlineData("-.5", -.5)] [InlineData("1.", 1d)] [InlineData("\t\r\n -2.5 ", -2.5)]
    [InlineData("", double.NaN)] [InlineData("+2", double.NaN)] [InlineData("1e2", double.NaN)]
    [InlineData("Infinity", double.NaN)] [InlineData(" 1", double.NaN)] [InlineData("−1", double.NaN)]
    public void NumericGrammarIsInvariant(string input, double expected)
    {
        var old=CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");Assert.Equal(expected,Call("number",S(input)).AsNumber()); }
        finally { CultureInfo.CurrentCulture=old; }
    }

    [Fact]
    public void NumericIEEEFormattingRoundingAndBooleanEdges()
    {
        double[] values=[double.NaN,double.NegativeInfinity,double.PositiveInfinity,-0d,0d,double.Epsilon,-double.Epsilon,double.MaxValue,-double.MaxValue,1e20,1e-20,4503599627370495d,Math.BitDecrement(.5),-.5,-.1,-1.5,1.5];
        foreach(double value in values)
        {
            Assert.Equal(XPathValue.FromNumber(value).AsString(),Call("string",N(value)).AsString());
            Assert.Equal(Math.Floor(value),Call("floor",N(value)).AsNumber());Assert.Equal(Math.Ceiling(value),Call("ceiling",N(value)).AsNumber());
            bool expected=value!=0&&!double.IsNaN(value);Assert.Equal(expected,Call("boolean",N(value)).AsBoolean());Assert.Equal(!expected,Call("not",N(value)).AsBoolean());
        }
        Assert.Equal(0,Call("round",N(Math.BitDecrement(.5))).AsNumber());
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(Call("round",N(-.5)).AsNumber()));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(Call("round",N(-.1)).AsNumber()));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(Call("round",N(-0d)).AsNumber()));
        Assert.Equal(2,Call("round",N(1.5)).AsNumber());Assert.Equal(-1,Call("round",N(-1.5)).AsNumber());
        Assert.True(double.IsNaN(Call("round",N(double.NaN)).AsNumber()));Assert.Equal(double.PositiveInfinity,Call("round",N(double.PositiveInfinity)).AsNumber());
        Assert.Equal(double.PositiveInfinity,Call("number",S(new string('9',400))).AsNumber());
        Assert.Equal(1,Call("number",XPathValue.FromBoolean(true)).AsNumber());Assert.Equal(0,Call("number",XPathValue.FromBoolean(false)).AsNumber());
        Assert.Equal("true",Call("string",XPathValue.FromBoolean(true)).AsString());Assert.Equal("false",Call("string",XPathValue.FromBoolean(false)).AsString());
        Assert.True(Call("boolean",S("false")).AsBoolean());Assert.False(Call("boolean",S("")).AsBoolean());
    }

    [Fact]
    public void LongDecimalPrefixesPreserveExactBinary64BoundaryRounding()
    {
        string halfDigits=BigInteger.Pow(5,1075).ToString(CultureInfo.InvariantCulture);
        string half="0."+new string('0',1075-halfDigits.Length)+halfDigits;
        Assert.Equal(0,Call("number",S(half)).AsNumber());
        Assert.Equal(double.Epsilon,Call("number",S(half+new string('0',2000)+"1")).AsNumber());
        string atOne="1.00000000000000011102230246251565404236316680908203125";
        Assert.Equal(1,Call("number",S(atOne+new string('0',2000))).AsNumber());
        Assert.Equal(Math.BitIncrement(1),Call("number",S(atOne+new string('0',2000)+"1")).AsNumber());
        var random=new Random(1979);
        foreach(int length in new[]{1,17,256,1100,1101,5000})
            for(int caseIndex=0;caseIndex<20;caseIndex++)
            {
                var digits=new char[length];for(int i=0;i<digits.Length;i++)digits[i]=(char)('0'+random.Next(10));
                int point=random.Next(length+1);
                string text=(caseIndex%2==0?"-":"")+new string(digits,0,point)+"."+new string(digits,point,length-point);
                Assert.Equal(BitConverter.DoubleToInt64Bits(XPathConversions.ParseNumber(text)),BitConverter.DoubleToInt64Bits(Call("number",S(text)).AsNumber()));
            }
    }

    [Theory]
    [InlineData(1.5,2.6,"234")] [InlineData(0,3,"12")] [InlineData(-1,3,"1")]
    [InlineData(double.NaN,3,"")] [InlineData(1,double.NaN,"")] [InlineData(double.NegativeInfinity,double.PositiveInfinity,"")]
    [InlineData(-42,double.PositiveInfinity,"12345")] [InlineData(double.PositiveInfinity,3,"")] [InlineData(2,-1,"")]
    public void SubstringUsesXPathBoundaryComparisons(double start,double length,string expected)
        =>Assert.Equal(expected,Call("substring",S("12345"),N(start),N(length)).AsString());

    [Fact]
    public void UnicodeScalarsTranslateDuplicatesXmlSpaceAndSearchBoundaries()
    {
        Assert.Equal("😀",Call("substring",S("A😀B"),N(2),N(1)).AsString());
        Assert.Equal("A😀B",Call("substring",S("A😀B"),N(double.NegativeInfinity)).AsString());
        Assert.Equal("",Call("substring",S("A😀B"),N(double.NaN)).AsString());
        Assert.Equal("XYX",Call("translate",S("aba"),S("aba"),S("XYQ")).AsString());
        Assert.Equal("🚀X🚀",Call("translate",S("😀a😀"),S("😀a😀"),S("🚀XQ")).AsString());
        Assert.Equal("😀😀",Call("translate",S("abab"),S("ab"),S("😀")).AsString());
        Assert.Equal("a b",Call("normalize-space",S(" \ta b\r\n ")).AsString());
        Assert.Equal("",Call("normalize-space",S(" \t\r\n ")).AsString());
        foreach (string text in new[]{"","abc","A😀B"})
        {
            Assert.True(Call("contains",S(text),S("")).AsBoolean());Assert.True(Call("starts-with",S(text),S("")).AsBoolean());
            Assert.Equal("",Call("substring-before",S(text),S("")).AsString());Assert.Equal(text,Call("substring-after",S(text),S("")).AsString());
            Assert.False(Call("contains",S(text),S("missing")).AsBoolean());Assert.False(Call("starts-with",S(text),S("missing")).AsBoolean());
        }
        Assert.False(Call("starts-with",S("abc"),S("b")).AsBoolean());Assert.True(Call("contains",S("A😀B"),S("😀")).AsBoolean());
        Assert.Equal("A",Call("substring-before",S("A😀B"),S("😀")).AsString());Assert.Equal("B",Call("substring-after",S("A😀B"),S("😀")).AsString());
        string longText=new string('根',4096)+"😀";Assert.Equal(4097,Call("string-length",S(longText)).AsNumber());
        foreach(string invalid in new[]{"\ud800","\udc00","a\ud800b"})
            foreach(string name in new[]{"string","number","boolean","string-length","normalize-space"})
                Assert.Equal("XPATH-TYPE",Assert.Throws<XPathEvaluationException>(()=>Call(name,S(invalid))).Diagnostic.Code);
    }

    [Fact]
    public void LanguageAndIdUseSnapshotNavigatorAndPropagateCallbackIdentity()
    {
        foreach(string requested in new[]{"en","EN","en-US","EN-us"})Assert.True(Call("lang",S(requested)).AsBoolean());
        foreach(string requested in new[]{"e","en-U","en-US-x","fr",""})Assert.False(Call("lang",S(requested)).AsBoolean());
        fixture.Language="";Assert.False(Call("lang",S("en")).AsBoolean());Assert.True(Call("lang",S("")).AsBoolean());
        fixture.Language=null;Assert.False(Call("lang",S("")).AsBoolean());
        Assert.Equal(new[]{fixture.X1,fixture.X2},Call("id",S("two\tone\r\none missing two")).AsNodeSet());
        Assert.Empty(Call("id",S("one two")).AsNodeSet());Assert.Empty(Call("id",S(" \t\r\n")).AsNodeSet());
        var ids=fixture.Set(fixture.IdText1,fixture.IdText2);
        Assert.Equal(new[]{fixture.X1,fixture.X2},Call("id",ids).AsNodeSet());
        var sentinel=new XPathEvaluationException(new("XPATH-TYPE",XPathDiagnosticCategory.Evaluation,"callback",default));
        fixture.Failure=sentinel;
        Assert.Same(sentinel,Assert.Throws<XPathEvaluationException>(()=>Call("id",S("one"))));
        Assert.Same(sentinel,Assert.Throws<XPathEvaluationException>(()=>Call("lang",S("en"))));
        fixture.Failure=new OperationCanceledException();Assert.Same(fixture.Failure,Assert.Throws<OperationCanceledException>(()=>Call("lang",S("en"))));
    }

    [Fact]
    public void EverySuccessfulFunctionHasExactAndOneOverSharedOperationControls()
    {
        (string Name,XPathValue[] Args)[] cases=[("last",[]),("position",[]),("count",[fixture.Set(fixture.X1)]),("id",[S("two one two")]),
            ("local-name",[]),("namespace-uri",[]),("name",[]),("string",[N(double.Epsilon)]),("concat",[S("a"),N(12)]),
            ("starts-with",[S("abc"),S("ab")]),("contains",[S("abc"),S("c")]),("substring-before",[S("abc"),S("b")]),
            ("substring-after",[S("abc"),S("b")]),("substring",[S("A😀B"),N(2),N(1)]),("string-length",[S("A😀B")]),
            ("normalize-space",[S(" A  B ")]),("translate",[S("aba"),S("ab"),S("😀X")]),("boolean",[N(1)]),("not",[N(1)]),
            ("true",[]),("false",[]),("lang",[S("en")]),("number",[S("1.5")]),("sum",[fixture.Set(fixture.A1,fixture.A2)]),
            ("floor",[N(1.2)]),("ceiling",[N(1.2)]),("round",[N(-.5)])];
        foreach(var (name,args) in cases)
        {
            var observed=new XPathBudget(new(){MaxOperations=null},default,TestContext.Current.CancellationToken);Invoke(name,args,observed);
            long total=observed.Operations;Assert.True(total>1);
            var exact=new XPathBudget(new(){MaxOperations=total},default,TestContext.Current.CancellationToken);Invoke(name,args,exact);Assert.Equal(total,exact.Operations);
            Assert.Equal("XPATH-EVALUATION-LIMIT",Assert.Throws<XPathEvaluationException>(()=>Invoke(name,args,new XPathBudget(new(){MaxOperations=total-1},default,TestContext.Current.CancellationToken))).Diagnostic.Code);
        }
    }

    [Fact]
    public void StringNodeSetAndIntermediateLimitsPreflightGrowth()
    {
        IXPathBudget Budget(int strings=100,int nodes=100)=>new XPathBudget(new(){MaxStringCharacters=strings,MaxNodeSetItems=nodes},default,TestContext.Current.CancellationToken);
        Assert.Equal("abcdef",Invoke("concat",[S("abc"),S("def")],Budget(6)).AsString());
        Assert.Throws<XPathEvaluationException>(()=>Invoke("concat",[S("abc"),S("def")],Budget(5)));
        Assert.Equal("😀😀",Invoke("translate",[S("aa"),S("a"),S("😀")],Budget(4)).AsString());
        Assert.Throws<XPathEvaluationException>(()=>Invoke("translate",[S("aa"),S("a"),S("😀")],Budget(3)));
        // Even discarded/converted intermediates and incoming strings are capped.
        Assert.Throws<XPathEvaluationException>(()=>Invoke("substring",[S("abcdef"),N(1),N(1)],Budget(5)));
        Assert.Throws<XPathEvaluationException>(()=>Invoke("contains",[N(double.Epsilon),S("z")],Budget(100)));
        string formatted=Call("string",N(double.Epsilon)).AsString();Assert.Equal(formatted,Invoke("string",[N(double.Epsilon)],Budget(formatted.Length)).AsString());
        Assert.Throws<XPathEvaluationException>(()=>Invoke("string",[N(double.Epsilon)],Budget(formatted.Length-1)));
        var set=fixture.Set(fixture.X1,fixture.X2);Assert.Equal(2,Invoke("count",[set],Budget(nodes:2)).AsNumber());
        Assert.Throws<XPathEvaluationException>(()=>Invoke("count",[set],Budget(nodes:1)));
        Assert.Equal(2,Invoke("id",[S("one two")],Budget(nodes:2)).AsNodeSet().Count);
        Assert.Throws<XPathEvaluationException>(()=>Invoke("id",[S("one two")],Budget(nodes:1)));
        fixture.IdOverride=[fixture.X1,fixture.X2];Assert.Throws<XPathEvaluationException>(()=>Invoke("id",[S("one")],Budget(nodes:1)));
    }

    [Fact]
    public void CancellationPollsDuringScansCopiesArgumentsAndNavigatorCallbacks()
    {
        using var source=new CancellationTokenSource();source.Cancel();
        Assert.Throws<OperationCanceledException>(()=>Invoke("true",[],new CountingBudget(source,0)));
        foreach(string name in new[]{"string-length","normalize-space","number","id"})
        {
            using var mid=new CancellationTokenSource();var budget=new CountingBudget(mid,300);
            var error=Assert.Throws<OperationCanceledException>(()=>Invoke(name,[S(new string('a',4096))],budget));
            Assert.Equal(mid.Token,error.CancellationToken);Assert.InRange(budget.Operations,300,301);
        }
        // Cancel in a navigator callback: library checkpoints immediately after it.
        using var callback=new CancellationTokenSource();fixture.OnCallback=callback.Cancel;
        Assert.Throws<OperationCanceledException>(()=>Invoke("id",[S("one")],new CountingBudget(callback,long.MaxValue)));
        using var entries=new CancellationTokenSource();fixture.OnCallback=null;
        var args=new ObservedArguments([N(1),N(2)],i=>{if(i==0)entries.Cancel();});
        Assert.Throws<OperationCanceledException>(()=>Invoke("concat",args,new CountingBudget(entries,long.MaxValue)));Assert.Equal(1,args.Reads);
        // Cancel specifically in string.Create filling and copy accounting, after
        // sizing passes: each operation rejects before the next unbounded unit.
        foreach(var (name,args2) in new[]{("normalize-space",new[]{S(" A  B ")}), ("translate",new[]{S("aba"),S("ab"),S("😀X")}), ("concat",new[]{S("abc"),S("def")})})
        {
            var observed=new XPathBudget(new(){MaxOperations=null},default,default);Invoke(name,args2,observed);
            using var copy=new CancellationTokenSource();var bounded=new CountingBudget(copy,observed.Operations-2);
            Assert.Throws<OperationCanceledException>(()=>Invoke(name,args2,bounded));Assert.InRange(bounded.Operations,observed.Operations-2,observed.Operations);
        }
    }

    internal sealed class Fixture : IXPathNavigator
    {
        internal XPathItem X1,X2,Y,A1,A2,IdText1,IdText2;
        internal XPathItem[] All;
        internal string? Language="en-US";internal Exception? Failure;internal Action? OnCallback;internal IReadOnlyList<XPathItem>? IdOverride;
        public XPathItem Root { get; }
        internal XPathFocus Focus=>new(X2,1,1);
        internal Fixture()
        {
            var d=new XmlDocument();var r=d.CreateElement("r");d.DocumentNode.AppendChild(r);
            var x1=d.CreateElement("x");r.AppendChild(x1);x1.AppendChild(d.CreateText("ABC"));var x2=d.CreateElement("x");r.AppendChild(x2);x2.AppendChild(d.CreateText("D"));
            var y=d.CreateElement("y","urn:p","p");r.AppendChild(y);y.SetNamespaceDeclaration("p","urn:p");
            var t1=d.CreateElement("ids1");r.AppendChild(t1);t1.AppendChild(d.CreateText("two one"));var t2=d.CreateElement("ids2");r.AppendChild(t2);t2.AppendChild(d.CreateText("one"));
            Root=XPathItem.FromNode(d.DocumentNode);X1=XPathItem.FromNode(x1);X2=XPathItem.FromNode(x2);Y=XPathItem.FromNode(y);
            A1=XPathItem.FromAttribute(x1.SetAttribute("n","1"));A2=XPathItem.FromAttribute(x2.SetAttribute("n","2"));
            IdText1=XPathItem.FromNode(t1);IdText2=XPathItem.FromNode(t2);
            var comment=d.CreateComment("c");r.AppendChild(comment);var pi=d.CreateProcessingInstruction("go","data");r.AppendChild(pi);
            All=[Root,X1,X2,Y,A1,A2,XPathItem.FromNode(x1.FirstChild),XPathItem.FromNode(comment),XPathItem.FromNode(pi),XPathItem.FromNamespace(y,"p")];
        }
        internal XPathValue Set(params XPathItem[] items)=>XPathValue.FromSnapshotNodeSet(items,new XPathBudget(new(),default,default));
        internal void RemoveLiveNodes(){X1.Node!.Value.Remove();X2.Node!.Value.Remove();}
        public IReadOnlyList<XPathItem> SelectAxis(XPathItem item,XPathAxis axis,IXPathBudget budget)=>throw new NotSupportedException();
        public int CompareDocumentOrder(XPathItem left,XPathItem right,IXPathBudget budget)
        {budget.ChargeOperations(2);return Array.IndexOf(All,left).CompareTo(Array.IndexOf(All,right));}
        public IReadOnlyList<XPathItem> SelectId(string id,IXPathBudget budget)
        {budget.ChargeOperations(1);if(Failure is not null)throw Failure;OnCallback?.Invoke();return IdOverride??(id switch{"one"=>new[]{X1},"two"=>new[]{X2},_=>Array.Empty<XPathItem>()});}
        public string? FindLanguage(XPathItem item,IXPathBudget budget)
        {budget.ChargeOperations(1);if(Failure is not null)throw Failure;OnCallback?.Invoke();return Language;}
    }
    private sealed class CountingBudget(CancellationTokenSource source,long cancelAt) : IXPathBudget
    {
        internal long Operations;public CancellationToken CancellationToken=>source.Token;
        public void ChargeOperations(long amount){source.Token.ThrowIfCancellationRequested();Operations+=amount;if(Operations>=cancelAt)source.Cancel();}
        public void CheckNodeSetSize(int count)=>source.Token.ThrowIfCancellationRequested();public void CheckStringSize(int count)=>source.Token.ThrowIfCancellationRequested();
    }
    private sealed class ObservedArguments(XPathValue[] values,Action<int> before) : IReadOnlyList<XPathValue>
    {
        internal int Reads;public int Count=>values.Length;
        public XPathValue this[int index]{get{Reads++;before(index);return values[index];}}
        public IEnumerator<XPathValue> GetEnumerator()=>((IEnumerable<XPathValue>)values).GetEnumerator();IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
