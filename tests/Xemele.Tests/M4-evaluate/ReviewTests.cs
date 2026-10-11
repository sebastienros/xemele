using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M4Evaluate;
public sealed class ReviewTests
{
    public static IEnumerable<object[]> NodeBooleanRelations()
    {
        foreach (var (op,apply) in new (string,Func<double,double,bool>)[]{("<",(a,b)=>a<b),("<=",(a,b)=>a<=b),(">",(a,b)=>a>b),(">=",(a,b)=>a>=b)})
            foreach(var (path,number) in new[]{("/r/missing",0d),("/r/zero",1d),("/r/one",1d),("/r/text",1d)})
                foreach(var (boolean,b) in new[]{("false()",0d),("true()",1d)})
                {
                    yield return new object[]{path+" "+op+" "+boolean,apply(number,b)};
                    yield return new object[]{boolean+" "+op+" "+path,apply(b,number)};
                }
    }
    [Theory,MemberData(nameof(NodeBooleanRelations))]
    public void AllRelationalOperatorsConvertWholeNodeSetsWhenOtherOperandIsBoolean(string source,bool expected)
    {
        const string xml="<r><zero>0</zero><one>1</one><text>nonnumeric</text></r>";
        var document=XmlDocument.Parse(xml,cancellationToken:TestContext.Current.CancellationToken);
        var actual=XPathExpression.Compile(source,cancellationToken:TestContext.Current.CancellationToken).Evaluate(document.RootElement).AsBoolean();
        var reference=new System.Xml.XPath.XPathDocument(new StringReader(xml)).CreateNavigator();
        Assert.Equal(expected,actual);
        Assert.Equal(expected,(bool)reference.Evaluate(source));
    }
    [Fact]
    public void DetachedContextRejectsBeforeDocumentInputLimitsOrUserDispatch()
    {
        var document=XmlDocument.Parse("<r><a/><a/><a/></r>",cancellationToken:TestContext.Current.CancellationToken);
        var detached=document.CreateElement("detached");bool called=false;
        var name=new XPathExpandedName("urn:e","f");
        var expression=XPathExpression.Compile("e:f()",new(){Namespaces=new Dictionary<string,string>{{"e","urn:e"}},Functions=[new(name,0,0)]},TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(()=>expression.Evaluate(detached,new(){Limits=new(){MaxInputItems=1},Functions=new Dictionary<XPathExpandedName,XPathExtensionFunction>{{name,(_,_)=>{called=true;return default;}}}}));
        Assert.False(called);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompileSetupLimitsReportTheFullPublicExpressionSpan(bool characters)
    {
        const string source = "1 + 2";
        var options = new XPathCompileOptions
        {
            Namespaces = new Dictionary<string,string> { ["p"] = "urn:p", ["q"] = "urn:q" },
            Limits = characters ? new() { MaxExpressionCharacters = 2 } : new() { MaxTokens = 1 }
        };
        var error = Assert.Throws<XPathCompileException>(() => XPathExpression.Compile(source, options, TestContext.Current.CancellationToken));
        Assert.Equal("XPATH-COMPILE-LIMIT", error.Diagnostic.Code);
        Assert.Equal(new(0, source.Length), error.Diagnostic.Span);
        var name = new XPathExpandedName("urn:e", "f");
        var functions = new XPathCompileOptions { Functions = [new(name,0,0)], Limits = new() { MaxExpressionCharacters = 2 } };
        Assert.Equal(new(0,source.Length), Assert.Throws<XPathCompileException>(() => XPathExpression.Compile(source,functions,TestContext.Current.CancellationToken)).Diagnostic.Span);
    }
    [Fact]
    public void CallerEnumerationExceptionsRetainIdentityAcrossCompileAndEvaluateSetup()
    {
        Exception[] errors = [
            new XPathCompileException(new("XPATH-COMPILE-LIMIT",XPathDiagnosticCategory.CompileLimit,"caller",new(1,1))),
            new XPathEvaluationException(new("XPATH-EVALUATION-LIMIT",XPathDiagnosticCategory.EvaluationLimit,"caller",new(1,1))),
            new OperationCanceledException(), new InvalidOperationException("caller")
        ];
        var root = new XmlDocument().DocumentNode;
        var expression = XPathExpression.Compile("1",cancellationToken:TestContext.Current.CancellationToken);
        foreach (var error in errors)
        {
            Assert.Same(error,Assert.ThrowsAny<Exception>(() => XPathExpression.Compile("1",new(){Namespaces=new ThrowingDictionary<string,string>(error)},TestContext.Current.CancellationToken)));
            Assert.Same(error,Assert.ThrowsAny<Exception>(() => XPathExpression.Compile("1",new(){Functions=new ThrowingList<XPathFunctionSignature>(error)},TestContext.Current.CancellationToken)));
            Assert.Same(error,Assert.ThrowsAny<Exception>(() => expression.Evaluate(root,new(){Variables=new ThrowingDictionary<XPathExpandedName,XPathValue>(error)})));
            Assert.Same(error,Assert.ThrowsAny<Exception>(() => expression.Evaluate(root,new(){Functions=new ThrowingDictionary<XPathExpandedName,XPathExtensionFunction>(error)})));
        }
    }
    private sealed class ThrowingDictionary<TKey,TValue>(Exception error) : IReadOnlyDictionary<TKey,TValue> where TKey:notnull
    {
        public int Count => 1;
        public IEnumerable<TKey> Keys => throw error;
        public IEnumerable<TValue> Values => throw error;
        public TValue this[TKey key] => throw error;
        public bool ContainsKey(TKey key) => throw error;
        public bool TryGetValue(TKey key,out TValue value) => throw error;
        public IEnumerator<KeyValuePair<TKey,TValue>> GetEnumerator() => throw error;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class ThrowingList<T>(Exception error) : IReadOnlyList<T>
    {
        public int Count => 1;
        public T this[int index] => throw error;
        public IEnumerator<T> GetEnumerator() => throw error;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

}
