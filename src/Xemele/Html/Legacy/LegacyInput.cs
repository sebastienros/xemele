using System.Buffers;
using System.Text;
using System.Runtime.CompilerServices;
namespace Xemele.Html;
// Owns bounded transport/lookahead buffers only. Caller input is borrowed through return.
internal ref struct LegacyInput
{
 private ReadOnlySpan<char> characters;
 private ReadOnlySpan<byte> bytes;
 private readonly Stream? stream;
 private readonly byte[]? transport;
 private int byteIndex, byteCount, charIndex,windowEnd;
 private bool transportEof,nameMode;
 private long byteOffset, scalars;
 private readonly XmlResourceLimits limits;
 private readonly CancellationToken cancellation;
 private readonly Scalar[] lookahead;
 private int start,count;
 private int line=1,column=1;
 private bool skipLf;
 private int encoding; // 0 Latin1, 1 UTF8, 2 UTF16LE, 3 UTF16BE, 4 ASCII, 5 UTF32LE, 6 UTF32BE
 private readonly bool encoded;
 private bool encodingLocked,stopped;
 private ReplayBytes replay;
 private int replayCount;
 internal LegacyInput(ReadOnlySpan<char> source,XmlResourceLimits limits)
 {
  this=default;characters=source;this.limits=limits;lookahead=new Scalar[128];line=column=1;
 }
 internal LegacyInput(ReadOnlySpan<byte> source,Stream? stream,XmlResourceLimits limits,CancellationToken cancellation)
 {
  this=default;this.stream=stream;this.limits=limits;this.cancellation=cancellation;encoded=true;lookahead=new Scalar[128];line=column=1;
  if(stream is null) {bytes=source;windowEnd=Math.Min(4000,source.Length);}
  else {transport=new byte[4064];while(byteCount<4000){cancellation.ThrowIfCancellationRequested();int n=stream.Read(transport.AsSpan(byteCount,4000-byteCount));if(n==0){transportEof=true;break;}byteCount+=n;}bytes=transport.AsSpan(0,byteCount);windowEnd=byteCount;}
  encoding=LegacyEncoding.Detect(bytes[..windowEnd],out int bom);encodingLocked=bom>0;byteIndex=bom;byteOffset=bom;
 }
 internal void NameMode(bool enabled)=>nameMode=enabled;
 internal void DeclareEncoding(ReadOnlySpan<char> label)
 {
  if(!encoded||encodingLocked)return;int selected=LegacyCharsets.Label(label);
  // Legacy meta labels must be ASCII-compatible; a UTF16 declaration is ignored.
  if(selected is 2 or 3||selected<0)return;if(selected==4)selected=0;encoding=selected;encodingLocked=true;
 }
 internal XmlLocation Location {get {Peek();return count==0?new(null,line,column,encoded?byteOffset:null):lookahead[start].Location;}}
 internal int Peek(int distance=0)
 {
  if((uint)distance>=128)throw new ArgumentOutOfRangeException(nameof(distance));
  while(count<=distance){var next=ReadScalar();if(next.Value<0)return -1;lookahead[(start+count)%128]=next;count++;}
  return lookahead[(start+distance)%128].Value;
 }
 internal int Take(){int value=Peek();if(count!=0){start=(start+1)%128;count--;}return value;}
 internal void Skip(int n){for(int i=0;i<n;i++)Take();}
 internal bool Match(ReadOnlySpan<char> value,int offset=0,bool fold=false){for(int i=0;i<value.Length;i++){int c=Peek(offset+i);if(fold&&c is >=65 and <=90)c+=32;if(c!=value[i])return false;}return true;}
 private Scalar ReadScalar()
 {
  while(true)
  {
   cancellation.ThrowIfCancellationRequested();long offset=byteOffset;int c=Raw();if(c<0)return new(-1,default);
   if(++scalars>limits.DocumentCharacters)Fail("HTML-legacy-document-limit","Decoded input quota exceeded.",new(null,line,column,encoded?offset:null));
   if(skipLf){skipLf=false;if(c==10)continue;}
   var location=new XmlLocation(null,line,column,encoded?offset:null);
   if(c==13){skipLf=true;c=10;}if(c==10){line++;column=1;}else column++;
   return new(c,location);
  }
 }
 private int Raw()
 {
  if(stopped)return -1;
  if(!encoded){if(charIndex>=characters.Length)return -1;var result=Rune.DecodeFromUtf16(characters[charIndex..],out Rune r,out int used);charIndex+=used==0?1:used;return result==OperationStatus.Done?r.Value:0xFFFD;}
  Prefetch();int b=Byte();if(b<0)return -1;
  int currentEncoding=encoding;
  if(encoding==0&&!encodingLocked&&b>=128){if(nameMode)currentEncoding=1;else{encoding=LegacyEncoding.Sniff(bytes.Slice(byteIndex-1,windowEnd-byteIndex+1));encodingLocked=true;currentEncoding=encoding;}}
  if(currentEncoding==0||currentEncoding>=10){int mapped=LegacyCharsets.Map(currentEncoding,b);if(mapped==0&&b>=128){stopped=true;return -1;}return mapped;}if(currentEncoding==4)return b<128?b:0xFFFD;
  if(encoding is 2 or 3){int b2=Byte();if(b2<0){stopped=true;return -1;}int u=encoding==2?b|(b2<<8):(b<<8)|b2;if(u is >=0xD800 and <=0xDBFF){int c=Byte(),d=Byte();if(c<0||d<0){stopped=true;return -1;}int low=encoding==2?c|(d<<8):(c<<8)|d;if(low is >=0xDC00 and <=0xDFFF)return 0x10000+((u-0xD800)<<10)+low-0xDC00;stopped=true;return -1;}if(u is >=0xDC00 and <=0xDFFF){stopped=true;return -1;}return u;}
  if(encoding is 5 or 6){int c=Byte(),d=Byte(),e=Byte();if(c<0||d<0||e<0)return 0xFFFD;long cp=encoding==5?(long)b|((long)c<<8)|((long)d<<16)|((long)e<<24):((long)b<<24)|((long)c<<16)|((long)d<<8)|(long)(uint)e;return cp<=0x10FFFF&&cp is not (>=0xD800 and <=0xDFFF)?(int)cp:0xFFFD;}
  if(b<128)return b;int n=b is >=0xC2 and <=0xDF?1:b is >=0xE0 and <=0xEF?2:b is >=0xF0 and <=0xF4?3:0;if(n==0)return 0xFFFD;int value=b&((1<<(6-n))-1);
  Span<byte> tail=stackalloc byte[3];int read=0;
  for(int i=0;i<n;i++){int next=Byte();if(next<0)return InvalidUtf8(tail[..read]);tail[read++]=(byte)next;if(next is <0x80 or >0xBF)return InvalidUtf8(tail[..read]);value=(value<<6)|(next&63);}
  return value>0x10FFFF||value is >=0xD800 and <=0xDFFF||value<(n==1?128:n==2?2048:65536)?InvalidUtf8(tail[..read]):value;
 }
 private int InvalidUtf8(scoped ReadOnlySpan<byte> tail){for(int i=tail.Length-1;i>=0;i--)replay[replayCount++]=tail[i];byteOffset-=tail.Length;return 0xFFFD;}
 private void Prefetch()
 {
  int threshold=nameMode?32:64;if(windowEnd-byteIndex>threshold)return;
  if(stream==null){windowEnd=(int)Math.Min(bytes.Length,(long)windowEnd+4000);return;}
  if(transportEof)return;
  int remaining=windowEnd-byteIndex;bytes.Slice(byteIndex,remaining).CopyTo(transport);byteIndex=0;byteCount=remaining;int target=remaining+4000;
  while(byteCount<target){cancellation.ThrowIfCancellationRequested();int n=stream.Read(transport.AsSpan(byteCount,target-byteCount));if(n==0){transportEof=true;break;}byteCount+=n;}
  windowEnd=byteCount;bytes=transport.AsSpan(0,byteCount);
 }
 private int Byte()
 {
  if(replayCount!=0){byteOffset++;return replay[--replayCount];}
  if(byteIndex>=bytes.Length){if(stream is null)return -1;cancellation.ThrowIfCancellationRequested();if(transportEof)return -1;byteCount=stream.Read(transport.AsSpan(0,4000));byteIndex=0;windowEnd=byteCount;bytes=transport.AsSpan(0,byteCount);if(byteCount==0){transportEof=true;return -1;}}
  byteOffset++;return bytes[byteIndex++];
 }
 internal static void Fail(string code,string message,XmlLocation location)=>throw new XmlParseException([new(code,XmlDiagnosticSeverity.Fatal,message,location)]);
 [InlineArray(4)]private struct ReplayBytes {private byte first;}
 private readonly record struct Scalar(int Value,XmlLocation Location);
}
internal static class LegacyEncoding
{
 internal static int Detect(ReadOnlySpan<byte> bytes,out int bom)
 {
  bom=0;
  if(bytes.StartsWith<byte>([0xEF,0xBB,0xBF])){bom=3;return 1;}
  if(bytes.StartsWith<byte>([0xFF,0xFE,0,0])){bom=4;return 5;}
  if(bytes.StartsWith<byte>([0,0,0xFE,0xFF])){bom=4;return 6;}
  if(bytes.StartsWith<byte>([0xFF,0xFE])){bom=2;return 2;}if(bytes.StartsWith<byte>([0xFE,0xFF])){bom=2;return 3;}
  return 0;
 }
 internal static int Sniff(ReadOnlySpan<byte> bytes)
 {
  // libxml2's legacy HTTP-EQUIV/CONTENT/CHARSET scan, not XML declarations.
  int at=Find(bytes,"http-equiv"u8);if(at>=0){int content=Find(bytes[(at+10)..],"content"u8);if(content>=0){at+=10+content;int next=Find(bytes[at..],"charset="u8);if(next>=0){at+=next+8;int n=0;while(at+n<bytes.Length&&LabelByte(bytes[at+n]))n++;return Label(bytes.Slice(at,n));}}}
  return 0;
 }
 private static bool Space(byte c)=>c is 9 or 10 or 12 or 13 or 32;
 private static bool LabelByte(byte c)=>c is >=(byte)'a' and <=(byte)'z' or >=(byte)'A' and <=(byte)'Z' or >=(byte)'0' and <=(byte)'9' or (byte)'-' or (byte)'_' or (byte)':' or (byte)'/';
 internal static int Find(ReadOnlySpan<byte> source,ReadOnlySpan<byte> word){for(int i=0;i<=source.Length-word.Length;i++){int j=0;for(;j<word.Length;j++){byte c=source[i+j];if(c is >=65 and <=90)c+=32;if(c!=word[j])break;}if(j==word.Length)return i;}return -1;}
 private static int Label(ReadOnlySpan<byte> label)
 {if(label.Length>32)return 1;Span<char> chars=stackalloc char[32];for(int i=0;i<label.Length;i++)chars[i]=(char)label[i];int selected=LegacyCharsets.Label(chars[..label.Length]);return selected<0?1:selected==4?0:selected;}
}
