namespace Xemele.Html;
// Reused operation-owned scratch. Strings are materialized only for persistent DOM data.
internal sealed class LegacyBuffer
{
    private char[] storage = new char[256];
    internal int Length
    {
        get; private set;
    }
    internal ReadOnlySpan<char> Written => storage.AsSpan(0, Length);
    internal void Clear() => Length = 0;
    internal void Append(char value)
    {
        if (Length == storage.Length)
        {
            Array.Resize(ref storage, checked(storage.Length * 2));
        }

        storage[Length++] = value;
    }
    internal void Append(ReadOnlySpan<char> value)
    {
        Reserve(value.Length);
        value.CopyTo(storage.AsSpan(Length));
        Length += value.Length;
    }
    internal Span<char> Reserve(int count)
    {
        if (storage.Length - Length < count)
        {
            Array.Resize(ref storage, checked(Math.Max(storage.Length * 2, Length + count)));
        }

        return storage.AsSpan(Length, count);
    }
    internal void Advance(int count) => Length += count;
    internal void FoldTail(int count)
    {
        for (var i = Length - count; i < Length; i++)
        {
            var c = storage[i];
            if ((uint)(c - 'A') <= 25)
            {
                storage[i] = (char)(c + 32);
            }
        }
    }
    public override string ToString() => new(Written);
}
