using System.Collections;

namespace Xemele.XPath.Internal;

// Normalized node-set expressed as entry indexes of the navigator's snapshot.
// Items are materialized on demand; the list is immutable once published.
internal sealed class XPathNodeList(XPathNavigator navigator, int[] indexes, int count) : IReadOnlyList<XPathItem>
{
    public int Count => count;
    public XPathItem this[int index] => (uint)index < (uint)count ? navigator.ItemAt(indexes[index]) : throw new ArgumentOutOfRangeException(nameof(index));
    internal XPathNavigator Navigator => navigator;
    internal int IndexAt(int position) => indexes[position];
    internal XPathItem[] ToItems()
    {
        var items = new XPathItem[count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = navigator.ItemAt(indexes[i]);
        }

        return items;
    }
    public IEnumerator<XPathItem> GetEnumerator()
    {
        for (var i = 0; i < count; i++)
        {
            yield return navigator.ItemAt(indexes[i]);
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Growable index set with the capacity-doubling, uniqueness and charging rules of
// the node-set scratch it replaces. Per-add charges accumulate and post in bulk
// through Flush; the totals equal posting each charge individually.
internal sealed class XPathNodeSelection
{
    private int[] items = [];
    private int capacity;
    private long pending;
    private long addCharges;
    private SortedSet<int>? uniqueIndexes;
    private IndexComparer? indexComparer;
    private int lastUniqueIndex = -1;
    private sealed class IndexComparer(IXPathBudget budget) : IComparer<int>
    {
        internal IXPathBudget Budget = budget;
        public int Compare(int left, int right)
        {
            Budget.ChargeOperations(1);
            return left.CompareTo(right);
        }
    }
    internal int Count
    {
        get; private set;
    }
    internal int this[int position] => items[position];
    internal int[] Items => items;
    internal void Reset()
    {
        Count = 0;
        capacity = 0;
        pending = 0;
        addCharges = 0;
        uniqueIndexes = null;
        indexComparer = null;
        lastUniqueIndex = -1;
    }
    // Sizes storage only; the logical capacity that drives charges is unchanged.
    internal void Presize(int count)
    {
        if (count > 4 && items.Length < count)
        {
            items = new int[count];
        }
    }
    // Equivalent to AddUnique for every member of an empty merge when the members
    // arrive in strictly increasing order: the storage moves instead of being copied.
    internal bool TakeIncreasing(XPathNodeSelection source, XPathNavigator navigator, IXPathBudget budget)
    {
        if (Count != 0 || uniqueIndexes is not null || lastUniqueIndex != -1)
        {
            return false;
        }

        long lookups = 0;
        for (var i = 0; i < source.Count; i++)
        {
            lookups += navigator.LookupCharge(source.items[i], budget) + 1;
        }

        pending += lookups + source.addCharges;
        (items, source.items) = (source.items, items);
        Count = source.Count;
        capacity = source.capacity;
        lastUniqueIndex = Count == 0 ? -1 : items[Count - 1];
        addCharges = source.addCharges;
        source.Reset();
        return true;
    }
    // Copies an existing node-set: exact-size capacity, two operations per item.
    internal void Adopt(XPathNodeList list, IXPathBudget budget)
    {
        budget.CheckNodeSetSize(list.Count);
        budget.ChargeOperations(2L * list.Count);
        Reset();
        if (items.Length < list.Count)
        {
            items = new int[list.Count];
        }

        for (var i = 0; i < list.Count; i++)
        {
            items[i] = list.IndexAt(i);
        }

        Count = capacity = list.Count;
    }
    internal void Add(int index, IXPathBudget budget)
    {
        budget.CheckNodeSetSize(Count + 1);
        AddDeferred(index);
    }
    internal void AddDeferred(int index)
    {
        if (Count == capacity)
        {
            if (Count == Array.MaxLength)
            {
                throw new XPathEvaluationException(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath node-set capacity exceeded.", default));
            }

            capacity = (int)Math.Min(Array.MaxLength, Math.Max(4L, (long)Count * 2));
            pending += 2L * Count;
            addCharges += 2L * Count;
            if (items.Length < capacity)
            {
                Array.Resize(ref items, capacity);
            }
        }
        pending++;
        addCharges++;
        items[Count++] = index;
    }
    internal void AddPending(long amount) => pending += amount;
    internal bool HasLargePending => pending >= 4096;
    internal void Flush(IXPathBudget budget)
    {
        budget.CheckNodeSetSize(Count);
        if (pending != 0)
        {
            var amount = pending;
            pending = 0;
            budget.ChargeOperations(amount);
        }
    }
    internal void AddUnique(int index, XPathNavigator navigator, IXPathBudget budget)
    {
        pending += navigator.LookupCharge(index, budget) + 1;
        // Empty merges and monotone axes need no uniqueness table. A
        // reverse/overlapping merge switches once to a bounded balanced
        // integer tree, with every comparison charged and cancellable.
        if (uniqueIndexes is null && (Count == 0 || lastUniqueIndex >= 0 && index >= lastUniqueIndex))
        {
            if (index == lastUniqueIndex)
            {
                return;
            }

            AddDeferred(index);
            lastUniqueIndex = index;
            return;
        }
        if (uniqueIndexes is null)
        {
            indexComparer = new(budget);
            uniqueIndexes = new(indexComparer);
            for (var i = 0; i < Count; i++)
            {
                budget.ChargeOperations(1 + navigator.LookupCharge(items[i], budget));
                uniqueIndexes.Add(items[i]);
            }
        }
        indexComparer!.Budget = budget;
        if (uniqueIndexes.Contains(index))
        {
            return;
        }

        budget.CheckNodeSetSize(Count + 1);
        uniqueIndexes.Add(index);
        AddDeferred(index);
    }
}
