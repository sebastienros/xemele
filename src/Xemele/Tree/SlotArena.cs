namespace Xemele;
// Document-local value slots. References are used only within non-growing mutation
// primitives; public handles retain owner plus index, never a movable array view.
internal sealed class SlotArena<T> where T : struct
{
    private T[] data = [];
    internal int Count { get; private set; }
    internal ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return ref data[index];
        }
    }
    internal void EnsureCapacity(int required)
    {
        if (required <= data.Length) return;
        if (required > Array.MaxLength) throw new InvalidOperationException("Tree slot capacity exceeded.");
        // 1.5x growth bounds slack while preserving amortized append. No pooling:
        // document lifetime owns all initialized slot values and their strings.
        int capacity = (int)Math.Min(Array.MaxLength, Math.Max(required, Math.Max(4L, data.Length + (long)data.Length / 2)));
        Array.Resize(ref data, capacity);
    }
    internal void Add(T value)
    {
        EnsureCapacity(checked(Count + 1)); data[Count] = value; Count++;
    }
}
