using Xemele;
using Xemele.Parsing.Input;
// Compile against the exact internal interface; this is a source-side sample,
// not an advertised public parser. Grammar validation belongs to M1-syntax.
using IXmlInputDecoder decoder = new XmlInputDecoder(new XmlParseOptions());
Span<char> output=stackalloc char[8];Span<long> mappings=stackalloc long[8];
ReadOnlySpan<byte> input=[0xEF,0xBB,0xBF,0x3C,0x72,0x2F,0x3E];
DecodeResult result=decoder.Decode(input,output,mappings,true,default);
if(result.Step!=DecodeStep.Completed || result.ConsumedBytes!=7 || result.WrittenCharacters!=4 ||
   !output[..4].SequenceEqual("<r/>") || !mappings[..4].SequenceEqual([3L,4L,5L,6L]) ||
   decoder.Encoding!=XmlInputEncoding.Utf8 || decoder.ValidateDeclaration("UTF-8",new(null,1,1,3))!=null)
    throw new Exception("Decoder usage failed.");
Console.WriteLine("UTF8 BOM: 7 bytes, 4 raw UTF16 units, mappings 3/4/5/6; declaration reconciled.");
