namespace Xemele.Html;
// Operation-owned pool: borrow lookup; allocate only on first persistence of a name.
internal sealed class LegacyNamePool(int? maximum)
{
 private readonly Dictionary<string,string> entries=new(StringComparer.Ordinal);
 internal string Get(ReadOnlySpan<char> name)
 {
  var lookup=entries.GetAlternateLookup<ReadOnlySpan<char>>();if(lookup.TryGetValue(name,out string? value))return value;
  if(maximum is int max&&entries.Count>=max)throw new InvalidOperationException("Legacy interned-name quota exceeded.");
  value=new string(name);entries.Add(value,value);return value;
 }
}
