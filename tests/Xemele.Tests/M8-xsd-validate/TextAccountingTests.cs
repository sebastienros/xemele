using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdValidate;
public sealed class TextAccountingTests
{
    [Fact]
    public void FirstFragmentIsBorrowedAndFinalBorrowIsNotCopied()
    {
        var op=new Ledger();var text=new XsdTextBuffer();string input=new('x',4096);
        text.Append(input,op,default);Assert.Same(input,text.Materialize(op,default));Assert.Equal(0,op.Work);Assert.Equal(0,op.Components);
    }
    [Theory]
    [InlineData(12,true)] [InlineData(11,false)]
    public void TwoFragmentsChargeBufferAndMaterializationBeforeCopies(int maximum,bool expected)
    {
        // Second fragment: buffer reservation1 + old2 + appended3 =6 work.
        // Final owned string: reservation1 + copied5 =6 work. Two components.
        var op=new Ledger(maximum);var text=new XsdTextBuffer();text.Append("ab",op,default);text.Append("cde",op,default);
        Assert.Equal(6,op.Work);Assert.Equal(1,op.Components);
        if(expected) { Assert.Equal("abcde",text.Materialize(op,default));Assert.Equal(12,op.Work);Assert.Equal(2,op.Components); }
        else { Assert.Throws<XsdEvaluationStopped>(()=>text.Materialize(op,default));Assert.False(op.IsComplete);Assert.Equal(11,op.Work); }
    }
    [Fact]
    public void MaterializationCancellationAndComponentReservation()
    {
        var text=new XsdTextBuffer();var op=new Ledger();text.Append("ab",op,default);text.Append("cde",op,default);op.CancelAt=8;
        Assert.Throws<OperationCanceledException>(()=>text.Materialize(op,default));Assert.Equal(8,op.Work);
        text=new();op=new(null,1);text.Append("ab",op,default);text.Append("cde",op,default);
        Assert.Throws<XsdEvaluationStopped>(()=>text.Materialize(op,default));Assert.Equal(1,op.Components);
    }
    [Theory]
    [InlineData(6,true)] [InlineData(5,false)]
    public void TrimChargesBothWhitespaceAndBoundaryInspections(int maximum,bool expected)
    {
        var op=new Ledger(maximum);
        if(expected) { Assert.Equal("x",new string(XsdEvaluationText.TrimXml("  x  ",op,default)));Assert.Equal(6,op.Work); }
        else Assert.Throws<XsdEvaluationStopped>(()=>Trim(op));
        static void Trim(Ledger op)=>XsdEvaluationText.TrimXml("  x  ",op,default);
    }
    [Theory]
    [InlineData(4097,true)] [InlineData(4096,false)]
    public void TokenSeparatorBoundaryCountsEverySecondScanUnit(int maximum,bool expected)
    {
        var op=new Ledger(maximum);string text=new string('a',4096)+" b";
        if(expected) { Assert.Equal(4096,XsdEvaluationText.IndexOf(text,' ',op,default));Assert.Equal(4097,op.Work); }
        else Assert.Throws<XsdEvaluationStopped>(()=>XsdEvaluationText.IndexOf(text,' ',op,default));
    }
    [Fact]
    public void CancellationDuringSecondScanAfterCompletedNormalizationWithNullCaps()
    {
        var op=new Ledger();var library=new XmlDatatypeRegistry().Find(EvaluatorTests.Xs)!;
        string normalized=library.Normalize(new string('a',4096)+" "+new string('b',4096),XmlWhitespaceRule.Collapse,op);
        long normalizationWork=op.Work;op.CancelAt=normalizationWork+128;
        Assert.Throws<OperationCanceledException>(()=>XsdEvaluationText.IndexOf(normalized,' ',op,default));
        Assert.Equal(normalizationWork+128,op.Work);
    }
    [Fact]
    public void LongPaddedXsiAndLongUriListTokensRemainCorrectWithNullCaps()
    {
        var options=new ValidationOptions { MaxOperations=null,Limits=new() { TokenCharacters=null,DocumentCharacters=null,EvaluationWork=null } };
        var p=EvaluatorTests.Prepare("<xs:element name='r' type='xs:int' nillable='true'/>");string padding=new(' ',4096);
        Assert.True(p.Run($"<r xmlns:xs='{EvaluatorTests.Xs}' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='{padding}xs:int{padding}' xsi:nil='{padding}true{padding}'/>",options).IsValid);
        p=EvaluatorTests.Prepare("<xs:element name='r'/>");
        Assert.True(p.Run("<r xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:schemaLocation='urn:"+new string('a',4096)+" https://example.invalid/"+new string('b',4096)+"'/>",options).IsValid);
        p=EvaluatorTests.Prepare("<xs:element name='r' type='xs:NMTOKENS'/>");
        Assert.True(p.Run("<r>"+new string('a',4096)+" "+new string('b',4096)+"</r>",options).IsValid);
    }
    private sealed class Ledger(long? maximum=null,int? componentMaximum=null) : ISchemaOperation
    {
        private readonly CancellationTokenSource cancellation=new();
        internal long Work;internal int Components;internal long? CancelAt;
        public XmlResourceLimits Limits { get; }=new() { DocumentCharacters=null,TokenCharacters=null,Depth=null,AttributesPerElement=null,EvaluationWork=null,Diagnostics=null };
        public CancellationToken CancellationToken=>cancellation.Token;
        public bool IsComplete { get; private set; }=true;
        public long? RemainingWork=>maximum is long cap?cap-Work:null;
        public bool Charge(long amount,XmlLocation location)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if(maximum is long cap && amount>cap-Work) { IsComplete=false;return false; }
            Work+=amount;if(CancelAt is long after && Work>=after)cancellation.Cancel();return IsComplete;
        }
        public bool ChargeComponents(int count,XmlLocation location)
        { if(componentMaximum is int cap && count>cap-Components) { IsComplete=false;return false; }Components+=count;return IsComplete; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic)=>true;
        public void Refuse(string code,string message,XmlLocation location)=>IsComplete=false;
    }
}
