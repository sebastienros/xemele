using System.Collections;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M4Compile;
public sealed class ResolutionTests
{
    [Fact]
    public void PolledColonAndOrdinalComparisonsHaveIndependentStringOracle()
    {
        foreach(string name in new[]{"", "name", ":", "p:x",new string('名',255)+":x",new string('名',256)+":x",new string('名',257)+":x",new string('名',8192),new string('名',8192)+":根😀"})
            Assert.Equal(name.IndexOf(':'),XPathResolution.FindColon(name,TestContext.Current.CancellationToken));
        foreach(string left in new[]{"","p","根😀",new string('名',8192)}) foreach(string right in new[]{left,left+"x",left.Length==0?"":left[..^1]+"x"})
            Assert.Equal(string.Equals(left,right,StringComparison.Ordinal),XPathResolution.Equal(left,right,TestContext.Current.CancellationToken));
    }
    [Fact]
    public void LongExpandedFunctionNamesAndPrefixBindingsResolveWithoutHashLookup()
    {
        string local=new('名',8192), prefix=new('前',4096), uri="urn:"+new string('根',8192);
        var signature=new XPathFunctionSignature(new(uri,local),0,1);
        var options=new XPathCompileOptions{Namespaces=new Dictionary<string,string>{{prefix,uri}},Functions=[signature],Limits=new(){MaxExpressionCharacters=null}};
        var context=XPathStaticContext.FromOptions(options,TestContext.Current.CancellationToken);
        var compiler=new XPathCompiler();
        Assert.Same(signature,Assert.IsType<XPathCall>(compiler.Compile(prefix+":"+local+"()",context,TestContext.Current.CancellationToken)).Signature);
        Assert.Equal(local,Assert.IsType<XPathVariable>(compiler.Compile("$"+local,context,TestContext.Current.CancellationToken)).Name.LocalName);
        Assert.Equal(uri,Assert.Single(Assert.IsType<XPathPath>(compiler.Compile(prefix+":*",context,TestContext.Current.CancellationToken)).Steps).Test.Name.NamespaceUri);
        var guarded=new EnumerationOnlyFunctions(new Dictionary<XPathExpandedName,XPathFunctionSignature>{{signature.Name,signature}});
        Assert.Same(signature,XPathResolution.FindFunction(guarded,new(uri,local),TestContext.Current.CancellationToken));
        Assert.Null(XPathResolution.FindFunction(guarded,new(uri,local+"x"),TestContext.Current.CancellationToken));
        Assert.Null(XPathResolution.FindFunction(guarded,new(uri+"x",local),TestContext.Current.CancellationToken));
    }
    [Fact]
    public void CancellationWhileEnumeratingResolutionRegistrationsKeepsSuppliedToken()
    {
        string local=new('名',8192),uri="urn:"+new string('根',8192);var signature=new XPathFunctionSignature(new(uri,local),0,0);
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var functions=new EnumerationOnlyFunctions(new Dictionary<XPathExpandedName,XPathFunctionSignature>{{signature.Name,signature}},cancellation.Cancel);
        var error=Assert.Throws<OperationCanceledException>(()=>XPathResolution.FindFunction(functions,signature.Name,cancellation.Token));
        Assert.Equal(cancellation.Token,error.CancellationToken);
        Assert.Throws<OperationCanceledException>(()=>XPathResolution.FindColon(local,cancellation.Token));
        Assert.Throws<OperationCanceledException>(()=>XPathResolution.Equal(uri,uri,cancellation.Token));
    }
    [Fact]
    public async Task CancellationDuringActualLongResolutionScansKeepsSuppliedToken()
    {
        // Invoke resolution primitives directly; lexical parsing is absent from
        // these operations. Large owned input is prepared before cancellation.
        string name=new('名',32_000_000),uri="urn:"+new string('根',32_000_000);
        await Canceled(token=>XPathResolution.FindColon(name,token));
        await Canceled(token=>XPathResolution.Equal(name,name,token));
        await Canceled(token=>XPathResolution.Equal(uri,uri,token));
        var expanded=new XPathExpandedName(uri,name,TestContext.Current.CancellationToken);
        var signature=new XPathFunctionSignature(expanded,0,0);
        var registrations=new EnumerationOnlyFunctions(new Dictionary<XPathExpandedName,XPathFunctionSignature>{{expanded,signature}});
        await Canceled(token=>XPathResolution.FindFunction(registrations,expanded,token));
        async Task Canceled(Action<CancellationToken> scan)
        {
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var started=new ManualResetEventSlim();
            var operation=Task.Run(()=>{started.Set(); for(int i=0;i<128;i++) scan(cancellation.Token);},TestContext.Current.CancellationToken);
            started.Wait(TestContext.Current.CancellationToken);await Task.Delay(1,TestContext.Current.CancellationToken);cancellation.Cancel();
            var error=await Assert.ThrowsAsync<OperationCanceledException>(async()=>await operation.WaitAsync(TimeSpan.FromSeconds(10),TestContext.Current.CancellationToken));
            Assert.Equal(cancellation.Token,error.CancellationToken);
        }
    }
    // Hash/indexer/equality lookup is forbidden: the production resolver must
    // enumerate immutable registrations and compare their names in bounded chunks.
    private sealed class EnumerationOnlyFunctions(IReadOnlyDictionary<XPathExpandedName,XPathFunctionSignature> values,Action? beforeFirst=null) : IReadOnlyDictionary<XPathExpandedName,XPathFunctionSignature>
    {
        public int Count=>values.Count;
        public IEnumerable<XPathExpandedName> Keys=>values.Keys;
        public IEnumerable<XPathFunctionSignature> Values=>values.Values;
        public XPathFunctionSignature this[XPathExpandedName key]=>throw new InvalidOperationException("Unbounded indexer forbidden.");
        public bool ContainsKey(XPathExpandedName key)=>throw new InvalidOperationException("Unbounded hashing forbidden.");
        public bool TryGetValue(XPathExpandedName key,out XPathFunctionSignature value)=>throw new InvalidOperationException("Unbounded hashing forbidden.");
        public IEnumerator<KeyValuePair<XPathExpandedName,XPathFunctionSignature>> GetEnumerator()
        {beforeFirst?.Invoke();foreach(var entry in values)yield return entry;}
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
