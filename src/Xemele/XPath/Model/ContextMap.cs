namespace Xemele.XPath.Internal;

// A context is an owned, insertion-ordered immutable map. Small registration
// inventories use bounded ordinal comparisons instead of caller-sized hashing.
// Linked entries avoid hidden array growth copies. No caller collection is retained.
internal sealed class ContextMap<TKey, TValue>(Func<TKey, TKey, bool> equal) : IReadOnlyDictionary<TKey, TValue>, IDictionary<TKey, TValue> where TKey : notnull
{
    private sealed class Entry(TKey key, TValue value)
    {
        internal readonly TKey Key = key;
        internal readonly TValue Value = value;
        internal Entry? Next;
    }
    private Entry? first, last;
    private bool sealedMap;
    public int Count { get; private set; }
    internal void Add(TKey key, TValue value)
    {
        if (sealedMap) throw new InvalidOperationException("Immutable context map.");
        if (ContainsKey(key)) throw new ArgumentException("Duplicate context binding.", nameof(key));
        if (Count == int.MaxValue) throw new ArgumentException("Context capacity exceeded.", nameof(key));
        var entry = new Entry(key, value);
        if (last is null) first = entry; else last.Next = entry;
        last = entry; Count++;
    }
    internal ContextMap<TKey, TValue> Seal() { sealedMap = true; return this; }
    public bool TryGetValue(TKey key, out TValue value)
    {
        for (var entry = first; entry is not null; entry = entry.Next)
            if (equal(entry.Key, key)) { value = entry.Value; return true; }
        value = default!; return false;
    }
    public bool ContainsKey(TKey key) => TryGetValue(key, out _);
    public TValue this[TKey key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
    public IEnumerable<TKey> Keys { get { foreach (var entry in this) yield return entry.Key; } }
    public IEnumerable<TValue> Values { get { foreach (var entry in this) yield return entry.Value; } }
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    { for (var entry = first; entry is not null; entry = entry.Next) yield return new(entry.Key, entry.Value); }
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;
    TValue IDictionary<TKey, TValue>.this[TKey key] { get => this[key]; set => throw new NotSupportedException(); }
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Array.AsReadOnly(Keys.ToArray());
    ICollection<TValue> IDictionary<TKey, TValue>.Values => Array.AsReadOnly(Values.ToArray());
    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => throw new NotSupportedException();
    bool IDictionary<TKey, TValue>.Remove(TKey key) => throw new NotSupportedException();
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> value) => throw new NotSupportedException();
    void ICollection<KeyValuePair<TKey, TValue>>.Clear() => throw new NotSupportedException();
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> value) => throw new NotSupportedException();
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> value) =>
        TryGetValue(value.Key, out var found) && EqualityComparer<TValue>.Default.Equals(found, value.Value);
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (index < 0 || index > array.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (Count > array.Length - index) throw new ArgumentException("Insufficient destination.", nameof(array));
        foreach (var entry in this) array[index++] = entry;
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
internal static class ContextComparison
{
    internal static bool Equal(string left, string right, CancellationToken token, IXPathBudget? budget = null)
    {
        token.ThrowIfCancellationRequested(); budget?.ChargeOperations(1);
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++)
        {
            token.ThrowIfCancellationRequested(); budget?.ChargeOperations(2);
            if (left[i] != right[i]) return false;
        }
        token.ThrowIfCancellationRequested(); return true;
    }
    internal static bool EqualName(XPathExpandedName left, XPathExpandedName right, CancellationToken token, IXPathBudget? budget = null) =>
        Equal(left.NamespaceUri, right.NamespaceUri, token, budget) && Equal(left.LocalName, right.LocalName, token, budget);
}
