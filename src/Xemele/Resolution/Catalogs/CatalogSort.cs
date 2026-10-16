namespace Xemele;

// Owned graph-construction scratch only; stable ties retain original document order.
internal static class CatalogSort
{
    internal static T[] StableSort<T>(T[] values, Comparison<T> compare, CancellationToken token)
    {
        if (values.Length < 2)
        {
            token.ThrowIfCancellationRequested();
            return values;
        }
        var scratch = new T[values.Length];
        for (long width = 1; width < values.Length; width *= 2)
        {
            for (long start = 0; start < values.Length; start += width * 2)
            {
                int left = (int)start, middle = (int)Math.Min(start + width, values.Length), right = middle;
                var end = (int)Math.Min(start + width * 2, values.Length);
                for (var output = left; output < end; output++)
                {
                    token.ThrowIfCancellationRequested();
                    var takeLeft = right == end || (left < middle && compare(values[left], values[right]) <= 0);
                    token.ThrowIfCancellationRequested();
                    scratch[output] = takeLeft ? values[left++] : values[right++];
                }
            }
            (values, scratch) = (scratch, values);
        }
        return values;
    }
}
