using System.Text;
using Xemele.Parsing.Input;
using Xunit;
namespace Xemele.Tests.EncodingCompatibility;
public class EncodingTests
{
    private static readonly string[] Labels = ["US-ASCII", "ISO-8859-1", "Windows-1252"];
    private static XmlInputDecoder Selected(string label)
    {
        var decoder = new XmlInputDecoder(new());
        Span<char> output = stackalloc char[4]; Span<long> maps = stackalloc long[4];
        Assert.Equal(DecodeStep.NeedInput, decoder.Decode("abcd"u8, output, maps, false, TestContext.Current.CancellationToken).Step);
        Assert.Null(decoder.ValidateDeclaration(label, new(null, 1, 1, 0)));
        return decoder;
    }
    [Fact]
    public void EveryByteHasExactStrictMappingAndAbsoluteOffset()
    {
        // Independent explicit Windows mapping, not runtime-provider fallback.
        int[] controls = [8364,-1,8218,402,8222,8230,8224,8225,710,8240,352,8249,338,-1,381,-1,
            -1,8216,8217,8220,8221,8226,8211,8212,732,8482,353,8250,339,-1,382,376];
        foreach (string label in Labels)
        for (int b = 0; b < 256; b++)
        {
            using var d = Selected(label);
            char[] output = new char[2]; long[] maps = new long[2];
            var r = d.Decode([(byte)b], output, maps, true, TestContext.Current.CancellationToken);
            int expected = label == "US-ASCII" && b > 127 ? -1 : label == "Windows-1252" && b is >=128 and <=159 ? controls[b-128] : b;
            if (expected < 0)
            {
                Assert.Equal(DecodeStep.InvalidInput, r.Step); Assert.Equal(0,r.ConsumedBytes); Assert.Equal(0,r.WrittenCharacters);
                Assert.Equal("INPUT.InvalidEncoding",r.Diagnostic!.Code); Assert.Equal(4,r.Diagnostic.Location.ByteOffset);
            }
            else
            {
                Assert.Equal(DecodeStep.Completed,r.Step); Assert.Equal(1,r.ConsumedBytes); Assert.Equal(1,r.WrittenCharacters);
                Assert.Equal((char)expected,output[0]); Assert.Equal(4,maps[0]);
            }
        }
    }
    [Theory]
    [InlineData("US-ASCII", "ASCII")]
    [InlineData("ISO-8859-1", "é")]
    [InlineData("Windows-1252", "€é—")]
    public void PublicDomAcrossEveryTransportSplit(string label, string expected)
    {
        byte[] body = label == "US-ASCII" ? "ASCII"u8.ToArray() : label == "ISO-8859-1" ? [0xE9] : [0x80,0xE9,0x97];
        byte[] source = [..Encoding.ASCII.GetBytes($"<?xml version='1.0' encoding='{label.ToLowerInvariant()}'?><r>"),..body,.."</r>"u8];
        for(int split=1;split<source.Length;split++)
        {
            using var stream = new SplitStream(source, split);
            Assert.Equal(expected,XmlDocument.Load(stream,cancellationToken:TestContext.Current.CancellationToken).RootElement.GetTextContent());
            Assert.True(stream.CanRead);
        }
    }
    [Fact]
    public void ExactLabelsAliasesProviderIndependenceAndUnicodeLock()
    {
        foreach(string label in Labels)
        {
            using var d=Selected(label.ToLowerInvariant());
            Assert.NotEqual(XmlInputEncoding.Utf8,d.Encoding);
            foreach(byte[] prefix in new byte[][] { [0xEF,0xBB,0xBF,0x41], [0xFF,0xFE,0x41,0], [0xFE,0xFF,0,0x41], [0x3C,0,0x72,0], [0,0x3C,0,0x72] })
            {
                using var locked=new XmlInputDecoder(new());
                locked.Decode(prefix,new char[8],new long[8],false,TestContext.Current.CancellationToken);
                Assert.Equal("INPUT.EncodingMismatch",locked.ValidateDeclaration(label,default)!.Code);
            }
        }
        foreach(string alias in new[]{"ASCII","ascii","Latin1","latin-1","ISO8859-1","ISO-8859-2","cp1252","windows1252","Windows-1252x","US-ASCII ","İSO-8859-1","UTF-32","IBM037"})
        {
            using var d=new XmlInputDecoder(new());d.Decode("abcd"u8,new char[4],new long[4],false,TestContext.Current.CancellationToken);
            Assert.Equal("INPUT.UnsupportedEncoding",d.ValidateDeclaration(alias,default)!.Code);
        }
        // No registration is needed even when the framework has no codepage provider.
        using var windows=Selected("Windows-1252");
        Assert.Equal(DecodeStep.Completed,windows.Decode([0x80],new char[2],new long[2],true,TestContext.Current.CancellationToken).Step);
    }
    [Fact]
    public void LegacyErrorsAndXmlLegalityAreDistinctAndPrologIsAscii()
    {
        foreach((string label,byte value,string code) in new[]{("US-ASCII",(byte)0xE9,"INPUT.InvalidEncoding"),("Windows-1252",(byte)0x81,"INPUT.InvalidEncoding"),("ISO-8859-1",(byte)0,"XML.Syntax")})
        {
            byte[] prefix=Encoding.ASCII.GetBytes($"<?xml version='1.0' encoding='{label}'?><r>\r\nX");
            byte[] xml=[..prefix,value,.."</r>"u8];
            Assert.False(XmlDocument.TryParse(xml.AsSpan(),out var document,out var diagnostics,cancellationToken:TestContext.Current.CancellationToken));
            Assert.Null(document);Assert.Equal(code,diagnostics[0].Code);Assert.Equal(prefix.Length,diagnostics[0].Location.ByteOffset);
            Assert.Equal(2,diagnostics[0].Location.LineNumber);Assert.Equal(2,diagnostics[0].Location.LinePosition);
        }
        using var nonAscii=new XmlInputDecoder(new());
        nonAscii.Decode("éabc"u8,new char[8],new long[8],false,TestContext.Current.CancellationToken);
        Assert.Equal("INPUT.InvalidEncoding",nonAscii.ValidateDeclaration("ISO-8859-1",default)!.Code);
        byte[] missing=[.."<r>"u8,0xE9,.."</r>"u8];
        Assert.False(XmlDocument.TryParse(missing.AsSpan(),out _,out var absent,cancellationToken:TestContext.Current.CancellationToken));
        Assert.Equal("INPUT.InvalidEncoding",absent[0].Code);
    }
    [Fact]
    public void EveryBodySplitCapacityAndErrorLocationPreservesOriginalBytes()
    {
        foreach(string label in Labels)
        foreach(int capacity in new[]{2,3,8})
        for(int split=0;split<=7;split++)
        {
            using var d=Selected(label);byte[] source=label=="US-ASCII" ? [65,13,10,66,67,68,69] : [65,13,10,66,0xE9,68,69];
            var text=new StringBuilder();var offsets=new List<long>();var output=new char[capacity];var maps=new long[capacity];int consumed=0;
            foreach(int end in new[]{split,source.Length})
            {
                if(consumed==source.Length)break;
                while(true)
                {
                    var r=d.Decode(source.AsSpan(consumed,end-consumed),output,maps,end==source.Length,TestContext.Current.CancellationToken);
                    Assert.Null(r.Diagnostic);text.Append(output,0,r.WrittenCharacters);offsets.AddRange(maps.AsSpan(0,r.WrittenCharacters).ToArray());consumed+=r.ConsumedBytes;
                    if(r.Step!=DecodeStep.NeedOutput)break;
                }
            }
            Assert.Equal(label=="US-ASCII"?"A\r\nBCDE":"A\r\nBéDE",text.ToString());Assert.Equal(Enumerable.Range(4,7).Select(x=>(long)x),offsets);
        }
    }
    [Fact]
    public void WarmLegacyConversionAndDeclarationMatchingAllocateZero()
    {
        foreach(string label in Labels)
        {
            using var d=Selected(label);byte[] input="abcd\r\n"u8.ToArray();char[] output=new char[8];long[] maps=new long[8];
            for(int i=0;i<10000;i++)d.Decode(input,output,maps,false,TestContext.Current.CancellationToken);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)d.Decode(input,output,maps,false,TestContext.Current.CancellationToken);
            Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
        }
    }
    private sealed class SplitStream(byte[] source,int split):MemoryStream(source,false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length,Position<split?split-(int)Position:(int)Length-(int)Position)]);
    }
}
