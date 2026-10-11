namespace Xemele;

// Owned graph-construction scratch only; stable ties retain original document order.
internal static class CatalogSort
{
    internal static T[] StableSort<T>(T[] values, Comparison<T> compare, CancellationToken token)
    {
        if (values.Length < 2) { token.ThrowIfCancellationRequested(); return values; }
        T[] scratch = new T[values.Length];
        for (long width = 1; width < values.Length; width *= 2)
        {
            for (long start = 0; start < values.Length; start += width * 2)
            {
                int left = (int)start, middle = (int)Math.Min(start + width, values.Length), right = middle;
                int end = (int)Math.Min(start + width * 2, values.Length);
                for (int output = left; output < end; output++)
                {
                    token.ThrowIfCancellationRequested();
                    bool takeLeft = right == end || (left < middle && compare(values[left], values[right]) <= 0);
                    token.ThrowIfCancellationRequested();
                    scratch[output] = takeLeft ? values[left++] : values[right++];
                }
            }
            (values, scratch) = (scratch, values);
        }
        return values;
    }
}
