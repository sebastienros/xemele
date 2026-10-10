using System.Text;
using System.Reflection;
using Xemele.Parsing.Input;
using Xunit;
namespace Xemele.Tests.Input;

public class InputTests
{
    private static (string Text, long[] Offsets, XmlDiagnostic? Error) Run(byte[] bytes, int split, int capacity = 2, string? declaration = null)
    {
        using var decoder = new XmlInputDecoder(new());
        var text = new StringBuilder(); var offsets = new List<long>();
        var chars = new char[capacity]; var maps = new long[capacity];
        int start = 0;
        foreach (int end in new[] { split, bytes.Length })
        {
            bool final = end == bytes.Length;
            for (int attempts = 0; ; attempts++)
            {
                Assert.True(attempts <= bytes.Length + 5);
                var result = decoder.Decode(bytes.AsSpan(start, end - start), chars, maps, final, TestContext.Current.CancellationToken);
                Assert.InRange(result.ConsumedBytes, 0, end - start);
                Assert.InRange(result.WrittenCharacters, 0, capacity);
                text.Append(chars, 0, result.WrittenCharacters); offsets.AddRange(maps.AsSpan(0, result.WrittenCharacters).ToArray());
                start += result.ConsumedBytes;
                if (result.Step == DecodeStep.InvalidInput) return (text.ToString(), offsets.ToArray(), result.Diagnostic);
                Assert.Null(result.Diagnostic);
                if (result.Step == DecodeStep.Completed)
                {
                    var error = decoder.ValidateDeclaration(declaration ?? "", new(null, 1, 1, 0));
                    return (text.ToString(), offsets.ToArray(), error);
                }
                if (result.Step == DecodeStep.NeedInput) { Assert.Equal(end, start); Assert.False(final); break; }
                Assert.True(result.ConsumedBytes != 0 || result.WrittenCharacters != 0);
            }
        }
        throw new Exception("No completion");
    }
    [Fact]
    public void EverySplitAndCapacityMatchesStrictOracleAndOriginalScalarMapping()
    {
        string[] texts = ["", "<r/>", "<r>é\r\n😀\t中\rX\n</r>", "\uFEFF<r/>"];
        foreach (var encoding in new Encoding[] {new UTF8Encoding(false,true), new UnicodeEncoding(false,false,true), new UnicodeEncoding(true,false,true)})
        foreach (bool bom in new[] {false,true})
        foreach (string text in texts)
        {
            byte[] preamble = bom ? encoding switch { UTF8Encoding => [0xEF,0xBB,0xBF], UnicodeEncoding u when u.CodePage==1200 => [0xFF,0xFE], _ => [0xFE,0xFF] } : [];
            // BOM-less UTF16 requires its XML signature; other sources are ambiguous by design.
            if (!bom && ((encoding is UnicodeEncoding && !text.StartsWith('<')) || text.StartsWith('\uFEFF'))) continue;
            byte[] bytes = [..preamble, ..encoding.GetBytes(text)];
            var expected = new List<long>(); long offset = preamble.Length;
            foreach (Rune rune in text.EnumerateRunes()) { for(int i=0;i<rune.Utf16SequenceLength;i++) expected.Add(offset); offset += encoding.GetByteCount(rune.ToString()); }
            foreach (int capacity in new[] {2,3,7,128})
            for(int split=0;split<=bytes.Length;split++)
            {
                string? label = encoding is UnicodeEncoding ? encoding.CodePage==1200 ? "UTF-16LE" : "UTF-16BE" : null;
                var result=Run(bytes,split,capacity,label);
                Assert.Null(result.Error); Assert.Equal(text,result.Text); Assert.Equal(expected,result.Offsets);
            }
        }
    }
    [Fact]
    public void InvalidUtf8EverySplitReportsSequenceStartAndScalarLocation()
    {
        byte[][] invalid = [[0x80],[0xC0,0xAF],[0xE0,0x80,0x80],[0xED,0xA0,0x80],[0xF4,0x90,0x80,0x80],[0xF5,0x80,0x80,0x80],[0xC2],[0xE2,0x82],[0xF0,0x9F,0x98],[0xE2,0x28,0xA1]];
        byte[] prefix = Encoding.UTF8.GetBytes("😀\r\nX");
        foreach(var suffix in invalid)
        {
            byte[] bytes = [..prefix,..suffix];
            for(int split=0;split<=bytes.Length;split++)
            {
                var r=Run(bytes,split); Assert.NotNull(r.Error); Assert.Equal("INPUT.InvalidEncoding",r.Error.Code);
                Assert.Equal(prefix.Length,r.Error.Location.ByteOffset); Assert.Equal(2,r.Error.Location.LineNumber); Assert.Equal(2,r.Error.Location.LinePosition);
                Assert.Equal("😀\r\nX",r.Text);
            }
        }
    }
    [Fact]
    public void Utf16UnmatchedPairsOddBytesAndIncompleteBomRejectAtEverySplit()
    {
        foreach(bool little in new[]{true,false})
        {
            var encoding = new UnicodeEncoding(!little,false,true);
            byte[] preamble=little ? [0xFF,0xFE] : [0xFE,0xFF];
            byte[][] bad = little ? [[0x00,0xDC],[0x00,0xD8],[0x00,0xD8,0x41,0],[0x41]] : [[0xDC,0],[0xD8,0],[0xD8,0,0,0x41],[0x41]];
            foreach(var suffix in bad)
            {
                byte[] prefix=[..preamble,..encoding.GetBytes("<r>😀\r\nX")]; byte[] bytes=[..prefix,..suffix];
                for(int split=0;split<=bytes.Length;split++)
                { var r=Run(bytes,split); Assert.Equal("INPUT.InvalidEncoding",r.Error!.Code); Assert.Equal(prefix.Length,r.Error.Location.ByteOffset); Assert.Equal(2,r.Error.Location.LinePosition); }
            }
        }
        foreach(byte[] bytes in new byte[][] { [0xEF],[0xEF,0xBB],[0xFF],[0xFE] })
            for(int split=0;split<=bytes.Length;split++) Assert.Equal("INPUT.InvalidEncoding",Run(bytes,split).Error!.Code);
    }
    [Fact]
    public void UnsupportedSignaturesRejectWithoutOutputAtEverySplit()
    {
        foreach(byte[] bytes in new byte[][] { [0,0,0xFE,0xFF],[0xFF,0xFE,0,0],[0,0,0,0x3C],[0x3C,0,0,0],[0,0,0x3C,0],[0,0x3C,0,0],[0x4C,0x6F,0xA7,0x94] })
            for(int split=0;split<=bytes.Length;split++) {var r=Run(bytes,split);Assert.Equal("",r.Text);Assert.Equal("INPUT.UnsupportedEncoding",r.Error!.Code);Assert.Equal(0,r.Error.Location.ByteOffset);}
    }
    [Fact]
    public void DeclarationLabelsMatchFullyAsciiInsensitiveAndNeverSwitchEncoding()
    {
        (byte[] Bytes,string[] Good,string[] Mismatch)[] cases = [
            (Encoding.UTF8.GetBytes("<r/>"),["","utf-8","UtF-8"],["UTF-16","UTF-16LE","UTF-16BE"]),
            ([0xFF,0xFE,0x3C,0,0x72,0],["","utf-16","UTF-16LE"],["UTF-8","UTF-16BE"]),
            ([0xFE,0xFF,0,0x3C,0,0x72],["","UTF-16","UTF-16BE"],["UTF-8","UTF-16LE"]),
            ([0x3C,0,0x72,0],["UTF-16LE"],["","UTF-16","UTF-8","UTF-16BE"]),
            ([0,0x3C,0,0x72],["UTF-16BE"],["","UTF-16","UTF-8","UTF-16LE"])
        ];
        foreach(var c in cases)
        {
            foreach(string label in c.Good) Assert.Null(Run(c.Bytes,0,2,label).Error);
            foreach(string label in c.Mismatch) Assert.Equal("INPUT.EncodingMismatch",Run(c.Bytes,0,2,label).Error!.Code);
            foreach(string label in new[]{"utf8","UTF-8x","UTF-16L","ASCII","ſTF-8","UTF-8 ",new string('x',10000)})
                Assert.Equal("INPUT.UnsupportedEncoding",Run(c.Bytes,0,2,label).Error!.Code);
        }
    }
    [Fact]
    public void LifecycleCapacityCancellationAndSnapshotsAreEnforced()
    {
        Assert.Throws<ArgumentNullException>(()=>new XmlInputDecoder(null!));
        Assert.Throws<ArgumentException>(()=>new XmlInputDecoder(new(),new Uri("relative",UriKind.Relative)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new XmlInputDecoder(new(){Limits=new(){ExternalBytes=0}}));
        using var d=new XmlInputDecoder(new());
        Assert.Throws<InvalidOperationException>(()=>d.ValidateDeclaration("",default));
        Assert.Throws<ArgumentException>(()=>d.Decode([],new char[1],new long[1],false,TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(()=>d.Decode([],new char[2],new long[3],false,TestContext.Current.CancellationToken));
        Assert.Equal(XmlInputEncoding.Undetermined,d.Encoding);
        var r=d.Decode("abcd"u8,new char[2],new long[2],true,TestContext.Current.CancellationToken); Assert.Equal(DecodeStep.NeedOutput,r.Step);
        Assert.Throws<InvalidOperationException>(()=>d.Decode([],new char[2],new long[2],false,TestContext.Current.CancellationToken));
        r=d.Decode("abcd"u8[r.ConsumedBytes..],new char[2],new long[2],true,TestContext.Current.CancellationToken); Assert.Equal(DecodeStep.Completed,r.Step);
        Assert.Null(d.ValidateDeclaration("UTF-8",default));
        Assert.Throws<InvalidOperationException>(()=>d.ValidateDeclaration("UTF-8",default));
        Assert.Throws<InvalidOperationException>(()=>d.Decode([],new char[2],new long[2],true,TestContext.Current.CancellationToken));
        d.Dispose();d.Dispose();Assert.Throws<ObjectDisposedException>(()=>d.ValidateDeclaration("",default));
        Assert.Throws<ObjectDisposedException>(()=>d.Encoding);
        using var cancelled=new XmlInputDecoder(new());using var token=new CancellationTokenSource();token.Cancel();
        var ex=Assert.Throws<OperationCanceledException>(()=>cancelled.Decode([],new char[2],new long[2],true,token.Token));Assert.Equal(token.Token,ex.CancellationToken);
        Assert.Throws<InvalidOperationException>(()=>cancelled.Decode([],new char[2],new long[2],true,TestContext.Current.CancellationToken));
    }
    [Fact]
    public void CarryIsOwnedAndBudgetsEnforceOriginalBytesWithCheckedCapacity()
    {
        using var d=new XmlInputDecoder(new());byte[] bytes=[0xEF,0xBB];
        Assert.Equal(DecodeStep.NeedInput,d.Decode(bytes,new char[2],new long[2],false,TestContext.Current.CancellationToken).Step);Array.Fill(bytes,(byte)0);
        char[] chars=new char[2];long[] offsets=new long[2];
        var r=d.Decode([0xBF,0x41],chars,offsets,true,TestContext.Current.CancellationToken);Assert.Equal(DecodeStep.Completed,r.Step);Assert.Equal('A',chars[0]);Assert.Equal(3,offsets[0]);
        foreach(long limit in new[]{3L,4L})
        {
            using var bounded=new XmlInputDecoder(new(){Limits=new(){ExternalBytes=limit}});
            r=bounded.Decode("abcd"u8,new char[8],new long[8],true,TestContext.Current.CancellationToken);
            Assert.Equal(limit==3?DecodeStep.InvalidInput:DecodeStep.Completed,r.Step);
        }
        using var overflow=new XmlInputDecoder(new(){Limits=new(){ExternalBytes=null}});
        overflow.Decode("abcd"u8,new char[8],new long[8],false,TestContext.Current.CancellationToken);
        var budget=typeof(XmlInputDecoder).GetField("budget",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overflow)!;
        var counts=(long[])budget.GetType().GetField("counts",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(budget)!;
        overflow.Decode([],chars,offsets,false,TestContext.Current.CancellationToken);
        counts[7]=long.MaxValue;
        Assert.Equal(DecodeStep.InvalidInput,overflow.Decode("z"u8,chars,offsets,true,TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void ByteAtATimeDrainAndDeclarationBeforeMalformedBodyHaveStableOrdering()
    {
        byte[] bytes=Encoding.UTF8.GetBytes("<r>é😀\r\n中</r>");
        using var d=new XmlInputDecoder(new());char[] chars=new char[2];long[] maps=new long[2];
        var text=new StringBuilder();var offsets=new List<long>();
        for(int i=0;i<bytes.Length;i++)
        {
            int used=0;int attempts=0;
            while(true)
            {
                Assert.True(attempts++<6);
                var r=d.Decode(bytes.AsSpan(i+used,1-used),chars,maps,i==bytes.Length-1,TestContext.Current.CancellationToken);
                used+=r.ConsumedBytes;text.Append(chars,0,r.WrittenCharacters);offsets.AddRange(maps.AsSpan(0,r.WrittenCharacters).ToArray());
                if(r.Step!=DecodeStep.NeedOutput){Assert.Equal(1,used);Assert.NotEqual(DecodeStep.InvalidInput,r.Step);break;}
            }
        }
        var oracle=Run(bytes,0);Assert.Equal(oracle.Text,text.ToString());Assert.Equal(oracle.Offsets,offsets);
        // Adapter framing is independent of transport chunks: submit only one new
        // byte until reconciliation. The decoder does not parse XML declarations.
        foreach(int transportChunk in new[]{1,2,7,1000})
        {
            using var framed=new XmlInputDecoder(new());
            byte[] source=[..Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='UTF-16'?>"),0xFF];
            int consumed=0;var prolog=new StringBuilder();
            while(!prolog.ToString().EndsWith("?>",StringComparison.Ordinal))
            {
                int transportEnd=Math.Min(source.Length,((consumed/transportChunk)+1)*transportChunk);
                Assert.True(transportEnd>consumed);
                var r=framed.Decode(source.AsSpan(consumed,1),chars,maps,false,TestContext.Current.CancellationToken);
                consumed+=r.ConsumedBytes;prolog.Append(chars,0,r.WrittenCharacters);Assert.Null(r.Diagnostic);
            }
            var declarationAt=new XmlLocation(null,1,1,0);
            var diagnostic=framed.ValidateDeclaration("UTF-16",declarationAt);
            Assert.Equal("INPUT.EncodingMismatch",diagnostic!.Code);Assert.Equal(source.Length-1,consumed);
            Assert.Throws<InvalidOperationException>(()=>framed.Decode(source.AsSpan(consumed),chars,maps,true,TestContext.Current.CancellationToken));
        }
    }
    [Fact]
    public void ContiguousErrorConsumptionStopsBeforeOffendingSequence()
    {
        foreach(byte[] bytes in new byte[][] { [0xFF,0x41,0x41,0x41], [0xEF,0xBB,0xBF,0xFF] })
        {
            using var d=new XmlInputDecoder(new());
            var r=d.Decode(bytes,new char[8],new long[8],true,TestContext.Current.CancellationToken);
            Assert.Equal(DecodeStep.InvalidInput,r.Step);Assert.Equal(bytes[0]==0xEF?3:0,r.ConsumedBytes);
            Assert.Equal(r.ConsumedBytes,r.Diagnostic!.Location.ByteOffset);
        }
        using var validPrefix=new XmlInputDecoder(new());
        var result=validPrefix.Decode([0x41,0x42,0xFF,0x44],new char[8],new long[8],true,TestContext.Current.CancellationToken);
        Assert.Equal(2,result.ConsumedBytes);Assert.Equal(2,result.WrittenCharacters);Assert.Equal(2,result.Diagnostic!.Location.ByteOffset);
    }
    [Fact]
    public void WarmDecodeHasZeroAllocationWithBoundedState()
    {
        using var d=new XmlInputDecoder(new(){Limits=new(){ExternalBytes=null}});
        byte[] input=Encoding.UTF8.GetBytes("<r>é😀\r\n中</r>");char[] output=new char[100];long[] offsets=new long[100];
        for(int i=0;i<10000;i++)d.Decode(input,output,offsets,false,TestContext.Current.CancellationToken);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<10000;i++)d.Decode(input,output,offsets,false,TestContext.Current.CancellationToken);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
    }
}
