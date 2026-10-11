namespace Xemele.Html;
// Reused operation-owned scratch. Strings are materialized only for persistent DOM data.
internal sealed class LegacyBuffer
{
 private char[] storage=new char[256];
 internal int Length {get;private set;}
 internal ReadOnlySpan<char> Written=>storage.AsSpan(0,Length);
 internal void Clear()=>Length=0;
 internal void Append(char value){if(Length==storage.Length)Array.Resize(ref storage,checked(storage.Length*2));storage[Length++]=value;}
 public override string ToString()=>new(Written);
}
