using System.Collections;
using Xemele.XPath;
using Xemele.XPath.Internal;
using Xunit;
namespace Xemele.Tests.M4Model;

public sealed class SnapshotCopyTests
{
    [Fact]
    public void SnapshotResultsRemainValidAfterLiveIdentityRemovalAndReorder()
    {
        var d = new XmlDocument();
        var r = d.CreateElement("r");
        d.DocumentNode.AppendChild(r);
        var a = d.CreateElement("a");
        var b = d.CreateElement("b");
        r.AppendChild(a);
        r.AppendChild(b);
        a.AppendChild(d.CreateText("old"));
        var attribute = a.SetAttribute("v", "original");
        XPathItem[] captured = [XPathItem.FromNode(a), XPathItem.FromAttribute(attribute), XPathItem.FromNode(b)];
        r.AppendChild(a);
        attribute.Remove();
        a.SetTextContent("new");
        a.Remove();
        var budget = new XPathBudget(new()
        {
            MaxNodeSetItems = 3,
            MaxOperations = 3
        }, default, default);
        var result = XPathValue.FromSnapshotNodeSet(captured, budget);
        Assert.Equal(captured, result.AsNodeSet());
        Assert.Equal("old", result.AsString());
        Assert.Equal("original", result.AsNodeSet()[1].StringValue);
        Assert.Equal(3, budget.Operations);
        captured[0] = default;
        Assert.False(result.AsNodeSet()[0].IsDefault);
        Assert.Throws<NotSupportedException>(() => ((IList<XPathItem>)result.AsNodeSet()).Clear());
        Assert.Throws<ArgumentException>(() => XPathValue.FromNodeSet(result.AsNodeSet()));
    }
    [Fact]
    public void SnapshotCopyChecksNodeSetOperationsAndCancellationBeforeGrowth()
    {
        var d = new XmlDocument();
        var item = XPathItem.FromNode(d.DocumentNode);
        XPathItem[] items = [item, item];
        Assert.Throws<XPathEvaluationException>(() => XPathValue.FromSnapshotNodeSet(items, new XPathBudget(new() { MaxNodeSetItems = 1 }, default, default)));
        var operations = new XPathBudget(new()
        {
            MaxOperations = 1
        }, default, default);
        Assert.Throws<XPathEvaluationException>(() => XPathValue.FromSnapshotNodeSet(items, operations));
        Assert.Equal(0, operations.Operations);
        using var source = new CancellationTokenSource();
        var budget = new XPathBudget(new(), default, source.Token);
        var canceled = new ObservedList<XPathItem>(items, index => { if (index == 0) { source.Cancel(); } });
        var error = Assert.Throws<OperationCanceledException>(() => XPathValue.FromSnapshotNodeSet(canceled, budget));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(1, canceled.Reads);
        Assert.Empty(XPathValue.FromSnapshotNodeSet([], new XPathBudget(new(), default, default)).AsNodeSet());
    }
    [Fact]
    public void DynamicCopiesChargeEntriesAndNamesAndStopBeforeOneOverGrowth()
    {
        var variables = new Dictionary<XPathExpandedName, XPathValue> { { new("", "a"), XPathValue.FromString("x") }, { new("", "b"), default } };
        // Four key-validation units plus four actual ordinal insertion comparison units.
        var options = new XPathContext { Variables = variables, Limits = new() { MaxOperations = 8 } };
        var budget = new XPathBudget(options.Limits, default, default);
        var snapshot = new XPathDynamicContext(options, budget);
        Assert.Equal(2, snapshot.Variables.Count);
        Assert.Equal(8, budget.Operations);
        var tooSmall = new XPathBudget(new()
        {
            MaxOperations = 7
        }, default, default);
        Assert.Throws<XPathEvaluationException>(() => new XPathDynamicContext(options, tooSmall));
        Assert.Equal(6, tooSmall.Operations); // Next actual comparison inspects two units atomically; denied before either read.
        Assert.Throws<XPathEvaluationException>(() => new XPathDynamicContext(new() { Variables = new Dictionary<XPathExpandedName, XPathValue> { { new("", "a"), XPathValue.FromString("xx") } }, Limits = new() { MaxStringCharacters = 1 } }));
    }
    [Fact]
    public void ContextSnapshotsStopWhenCallerEnumerationCancels()
    {
        using var source = new CancellationTokenSource();
        var dictionary = new CancelingDictionary<XPathExpandedName, XPathValue>([new(new("", "a"), default), new(new("", "b"), default)], source);
        Assert.Throws<OperationCanceledException>(() => new XPathDynamicContext(new() { Variables = dictionary, CancellationToken = source.Token }));
        Assert.Equal(1, dictionary.Yields);
        using var staticSource = new CancellationTokenSource();
        var namespaces = new CancelingDictionary<string, string>([new("p", "urn:p"), new("q", "urn:q")], staticSource);
        Assert.Throws<OperationCanceledException>(() => XPathStaticContext.FromOptions(new() { Namespaces = namespaces }, staticSource.Token));
        Assert.Equal(1, namespaces.Yields);
    }
    [Fact]
    public void OversizedStaticRegistrationRejectsBeforeLaterInputIsRead()
    {
        var signature = new XPathFunctionSignature(new("urn:too-long", "f"), 0, 0);
        var entries = new ObservedList<XPathFunctionSignature>([signature, new(new("urn:p", "g"), 0, 0)], i => { if (i == 1) { throw new InvalidOperationException("Later entry visited."); } });
        var error = Assert.Throws<XPathCompileException>(() => XPathStaticContext.FromOptions(new() { Functions = entries, Limits = new() { MaxExpressionCharacters = 1 } }, TestContext.Current.CancellationToken));
        Assert.Equal("XPATH-COMPILE-LIMIT", error.Diagnostic.Code);
        Assert.Equal(1, entries.Reads);
    }
    [Fact]
    public void NameScansPollCancellationWithinChargedCadence()
    {
        using var source = new CancellationTokenSource();
        var budget = new CancelAfterBudget(source, 256);
        var options = new XPathContext { Variables = new Dictionary<XPathExpandedName, XPathValue> { { new("", new string('a', 1000)), default } }, CancellationToken = source.Token };
        Assert.Throws<OperationCanceledException>(() => new XPathDynamicContext(options, budget));
        Assert.InRange(budget.Operations, 256, 257);
    }
    private sealed class ObservedList<T>(T[] items, Action<int> before) : IReadOnlyList<T>
    {
        internal int Reads; public int Count => items.Length;
        public T this[int index] { get { Reads++; before(index); return items[index]; } }
        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CancelingDictionary<K, V>(KeyValuePair<K, V>[] entries, CancellationTokenSource source) : IReadOnlyDictionary<K, V> where K : notnull
    {
        internal int Yields; public int Count => entries.Length; public IEnumerable<K> Keys => entries.Select(e => e.Key); public IEnumerable<V> Values => entries.Select(e => e.Value);
        public V this[K key] => throw new NotSupportedException(); public bool ContainsKey(K key) => throw new NotSupportedException(); public bool TryGetValue(K key, out V value)
        {
            value = default!;
            throw new NotSupportedException();
        }
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            foreach (var entry in entries)
            {
                Yields++;
                source.Cancel();
                yield return entry;
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CancelAfterBudget(CancellationTokenSource source, long maximum) : IXPathBudget
    {
        internal long Operations; public CancellationToken CancellationToken => source.Token;
        public void ChargeOperations(long amount)
        {
            source.Token.ThrowIfCancellationRequested();
            Operations += amount;
            if (Operations >= maximum)
            {
                source.Cancel();
            }
        }
        public void CheckNodeSetSize(int count) => source.Token.ThrowIfCancellationRequested(); public void CheckStringSize(int count) => source.Token.ThrowIfCancellationRequested();
    }
}
