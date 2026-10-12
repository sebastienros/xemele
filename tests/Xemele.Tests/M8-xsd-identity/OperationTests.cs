using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdIdentity;

public sealed class OperationTests
{
    internal sealed class Ledger(long? limit=null,int? components=null,CancellationToken token=default) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; init; } = new();
        public CancellationToken CancellationToken => token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => limit-Work;
        internal long Work; internal int Components,Calls;
        internal readonly List<XmlDiagnostic> Diagnostics=[];
        internal Action<int>? OnCharge;
        public bool Charge(long count,XmlLocation at)
        {
            token.ThrowIfCancellationRequested(); OnCharge?.Invoke(++Calls); token.ThrowIfCancellationRequested();
            if(!IsComplete)return false;
            if(limit is {} cap && count>cap-Work) { Refuse("SCHEMA.Limit","work",at);return false; }
            Work=checked(Work+count);return true;
        }
        public bool ChargeComponents(int count,XmlLocation at)
        {
            token.ThrowIfCancellationRequested();if(!IsComplete)return false;
            if(components is {} cap && count>cap-Components) { Refuse("SCHEMA.Limit","memory",at);return false; }
            Components=checked(Components+count);return true;
        }
        public bool AddDiagnostic(XmlDiagnostic d) { token.ThrowIfCancellationRequested();Diagnostics.Add(d);return IsComplete; }
        public void Refuse(string code,string message,XmlLocation at) { Diagnostics.Add(new(code,XmlDiagnosticSeverity.Fatal,message,at));IsComplete=false; }
    }
    [Fact]
    public void ExactCompilerWorkAndMemoryLimits()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint(selector:".//p:row|q:row",field:"@p:*") + IdentityTests.Constraint("keyref","use","@id","R","K"));
        var compiler=new XsdIdentityCompiler();var observed=new Ledger();compiler.Compile(schema,observed);
        Assert.True(observed.Work>0);Assert.True(observed.Components>0);
        Assert.NotNull(compiler.Compile(schema,new Ledger(observed.Work,observed.Components)));
        Assert.Throws<SchemaCompileException>(()=>compiler.Compile(schema,new Ledger(observed.Work-1)));
        Assert.Throws<SchemaCompileException>(()=>compiler.Compile(schema,new Ledger(components:observed.Components-1)));
    }
    [Fact]
    public void ExactRuntimeLimitsIncludeEveryTupleComparisonAndPropagation()
    {
        var schema=IdentityTests.Schema($"<xs:element name='root'>{IdentityTests.Constraint("keyref","use","@id","R","K")}</xs:element><xs:element name='group'>{IdentityTests.Constraint()}</xs:element>");
        var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><group><row id='a'/><row id='b'/></group><use id='b'/></root>",cancellationToken:TestContext.Current.CancellationToken);
        var observed=new Ledger();IdentityTests.Drive(schema,program,doc,observed);
        IdentityTests.Drive(schema,program,doc,new Ledger(observed.Work,observed.Components));
        var shortWork=new Ledger(observed.Work-1); Assert.Throws<SchemaCompileException>(()=>IdentityTests.Drive(schema,program,doc,shortWork));Assert.False(shortWork.IsComplete);
        var shortMemory=new Ledger(components:observed.Components-1);Assert.Throws<SchemaCompileException>(()=>IdentityTests.Drive(schema,program,doc,shortMemory));Assert.False(shortMemory.IsComplete);
    }
    [Fact]
    public void SharedPrechargedBudgetIsNotResetAtAnyHook()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var compiler=new XsdIdentityCompiler();
        var observed=new Ledger(); var program=compiler.Compile(schema,observed); long work=observed.Work;
        var doc=XmlDocument.Parse("<root><row id='a'/></root>",cancellationToken:TestContext.Current.CancellationToken);
        IdentityTests.Drive(schema,program,doc,observed);
        var exact=new Ledger(observed.Work,observed.Components);program=compiler.Compile(schema,exact);IdentityTests.Drive(schema,program,doc,exact);
        var insufficient=new Ledger(observed.Work-1);program=compiler.Compile(schema,insufficient);
        Assert.Throws<SchemaCompileException>(()=>IdentityTests.Drive(schema,program,doc,insufficient));Assert.True(insufficient.Work>=work);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void MidOperationCancellationAndCallerExceptionsPropagate(bool compile)
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var compiler=new XsdIdentityCompiler();
        var program=compiler.Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><row id='a'/><row id='b'/></root>",cancellationToken:TestContext.Current.CancellationToken);
        using var cts=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancel=new Ledger(token:cts.Token) { OnCharge=n=>{if(n==12)cts.Cancel();} };
        Assert.Throws<OperationCanceledException>(()=>{if(compile)compiler.Compile(schema,cancel);else IdentityTests.Drive(schema,program,doc,cancel);});
        Assert.Equal(12,cancel.Calls);
        var sentinel=new InvalidOperationException("caller");var callback=new Ledger { OnCharge=_=>throw sentinel };
        Assert.Same(sentinel,Assert.Throws<InvalidOperationException>(()=>{if(compile)compiler.Compile(schema,callback);else IdentityTests.Drive(schema,program,doc,callback);}));
    }
    [Fact]
    public void DepthStopsBeforeScopeGrowth()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint(selector:".//row"));var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><group><row id='a'/></group></root>",cancellationToken:TestContext.Current.CancellationToken);
        var op=new Ledger { Limits=new() { Depth=2 } };
        Assert.Throws<SchemaCompileException>(()=>IdentityTests.Drive(schema,program,doc,op));Assert.Contains(op.Diagnostics,d=>d.Code=="XSD.IdentityLimit");
    }
    [Fact]
    public void DiagnosticLimitProducesAnIncompleteResult()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><row/><row/><row/></root>",cancellationToken:TestContext.Current.CancellationToken);
        var op=new SchemaValidationOperation(new() { Limits=new() { Diagnostics=1 } });
        Assert.Throws<SchemaCompileException>(()=>IdentityTests.Drive(schema,program,doc,op));var result=op.Complete();Assert.False(result.IsComplete);Assert.False(result.IsValid);Assert.Single(result.Diagnostics);
    }
    [Fact]
    public void DiagnosticUsesTheSelectedInstanceSourceAndStaleLocationsAreUnknown()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var context=new SchemaCompileContext();var doc=context.Parse("<root>\n <row/>\n</root>").Document;var op=new Ledger();
        IdentityTests.Drive(schema,program,doc,op);var error=Assert.Single(op.Diagnostics);Assert.Equal(2,error.Location.LineNumber);
        doc.RootElement.SetAttribute("mutated","yes");op=new();IdentityTests.Drive(schema,program,doc,op);Assert.All(op.Diagnostics,d=>Assert.Equal(0,d.Location.LineNumber));
    }
    [Fact]
    public void ProgramAndConcurrentSessionsShareNoMutableRunState()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><row id='a'/><row id='b'/></root>",cancellationToken:TestContext.Current.CancellationToken);
        Parallel.For(0,16,new ParallelOptions { CancellationToken=TestContext.Current.CancellationToken },_=>
        { var op=new Ledger();IdentityTests.Drive(schema,program,doc,op);Assert.Empty(op.Diagnostics); });
        var duplicate=XmlDocument.Parse("<root><row id='a'/><row id='a'/></root>",cancellationToken:TestContext.Current.CancellationToken);
        var bad=new Ledger();IdentityTests.Drive(schema,program,duplicate,bad);Assert.NotEmpty(bad.Diagnostics);
        var good=new Ledger();IdentityTests.Drive(schema,program,doc,good);Assert.Empty(good.Diagnostics);
    }
    [Fact]
    public void SourceMutationAndHookMisuseAreExplicit()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var program=new XsdIdentityCompiler().Compile(schema,new Ledger());var factory=new XsdIdentityFactory();
        var doc=XmlDocument.Parse("<root><row id='a'/></root>",cancellationToken:TestContext.Current.CancellationToken);var op=new Ledger();var session=factory.Create(schema,program,doc);
        Assert.Throws<ArgumentException>(()=>factory.Create(schema with { },program,doc));
        Assert.Throws<InvalidOperationException>(()=>session.Complete(op));session.EnterElement(doc.RootElement,schema.Elements[0],op);
        doc.RootElement.SetAttribute("changed","yes");Assert.Throws<InvalidOperationException>(()=>session.LeaveElement(doc.RootElement,new(true,new(IdentityTests.Xs,"anyType"),null),op));
        session=factory.Create(schema,program,doc);session.EnterElement(doc.RootElement,schema.Elements[0],op);
        Assert.Throws<ArgumentException>(()=>session.Attribute(doc.RootElement,new("","id"),new(true,new(IdentityTests.Xs,"string"),null),new Ledger()));
    }
    [Fact]
    public void SkippedOrRepeatedElementsAndAttributesCannotProduceSuccess()
    {
        var schema=IdentityTests.Simple(IdentityTests.Constraint());var program=new XsdIdentityCompiler().Compile(schema,new Ledger());
        var doc=XmlDocument.Parse("<root><row/><row/></root>",cancellationToken:TestContext.Current.CancellationToken);var op=new Ledger();var session=new XsdIdentityFactory().Create(schema,program,doc);
        session.EnterElement(doc.RootElement,schema.Elements[0],op);
        Assert.Throws<ArgumentException>(()=>session.EnterElement(doc.RootElement.LastChild,null,op));
        Assert.Throws<ArgumentException>(()=>session.LeaveElement(doc.RootElement,new(true,new(IdentityTests.Xs,"anyType"),null),op));
        session.EnterElement(doc.RootElement.FirstChild,null,op);
        var registry=new XmlDatatypeRegistry();var value=IdentityTests.Valid(IdentityTests.Typed(registry,"string","x"));
        session.Attribute(doc.RootElement.FirstChild,new("","id"),value,op);
        Assert.Throws<ArgumentException>(()=>session.Attribute(doc.RootElement.FirstChild,new("","id"),value,op));
    }
}
