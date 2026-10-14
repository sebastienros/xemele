namespace Xemele;

// Owned stable slots. Only the small first page grows; subsequent pages never
// move, so a large DOM does not repeatedly allocate/copy its entire storage.
internal sealed class SlotArena<T> where T : struct
{
    private const int PageShift = 8;
    private const int PageSize = 1 << PageShift;
    private const int PageMask = PageSize - 1;
    private T[] data = [];
    private T[][] pages = [];
    private int capacity;
    internal int Count { get; private set; }
    internal ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < PageSize) return ref data[index];
            return ref pages[index >> PageShift][index & PageMask];
        }
    }
    internal void EnsureCapacity(int required)
    {
        if (required <= capacity) return;
        if (required > Array.MaxLength) throw new InvalidOperationException("Tree slot capacity exceeded.");
        if (required <= PageSize)
        {
            int length = Math.Min(PageSize, Math.Max(required, Math.Max(4, data.Length * 2)));
            Array.Resize(ref data, length); capacity = length; return;
        }
        if (data.Length != PageSize) Array.Resize(ref data, PageSize);
        int needed = (int)(((long)required + PageMask) >> PageShift);
        if (pages.Length < needed) Array.Resize(ref pages, Math.Max(needed, Math.Max(4, pages.Length * 2)));
        pages[0] = data;
        for (int i = Math.Max(1, capacity >> PageShift); i < needed; i++) pages[i] ??= new T[PageSize];
        capacity = (int)Math.Min(Array.MaxLength, (long)needed * PageSize);
    }
    internal void Add(T value)
    {
        EnsureCapacity(checked(Count + 1)); this.AddUnchecked(value);
    }
    private void AddUnchecked(T value)
    {
        if (Count < PageSize) data[Count] = value;
        else pages[Count >> PageShift][Count & PageMask] = value;
        Count++;
    }
}
