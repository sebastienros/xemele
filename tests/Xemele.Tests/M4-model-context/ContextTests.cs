using Xunit;
using Xemele.XPath;
using Xemele.XPath.Internal;
namespace Xemele.Tests.M4ModelContext;

public sealed class ContextTests
{
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(4096)]
    public void OrdinalComparisonChargesActualInspectedCharacters(int length)
    {
        string a = new('x', length);
        string b = new(a.ToCharArray());
        var budget = new CountingBudget();
        Assert.True(ContextComparison.Equal(a, b, TestContext.Current.CancellationToken, budget));
        Assert.Equal(1 + 2L * length, budget.Count);
        b = b[..^1] + "y";
        budget = new();
        Assert.False(ContextComparison.Equal(a, b, TestContext.Current.CancellationToken, budget));
        Assert.Equal(1 + 2L * length, budget.Count);
        budget = new();
        Assert.False(ContextComparison.Equal(a, a + "y", TestContext.Current.CancellationToken, budget));
        Assert.Equal(1, budget.Count);
    }
    [Fact]
    public void DynamicContextCancelsDuringActualDuplicateNameComparison()
    {
        using var source = new CancellationTokenSource();
        const int length = 4096;
        string uri = new('u', length);
        var a = new XPathExpandedName(uri, new string('a', length));
        var b = new XPathExpandedName(new string(uri.ToCharArray()), new string('a', length - 1) + "b");
        // Two validated key scans precede insertion comparison. Cancellation
        // starts after 512 actual compared URI units, not during validation.
        var setup = 2L * (1 + length + length);
        var budget = new CountingBudget(source, setup + 1 + 2 * 512);
        var options = new XPathContext { CancellationToken = source.Token, Variables = new OrderedInput<XPathExpandedName, XPathValue>(new(a, XPathValue.FromNumber(1)), new(b, XPathValue.FromNumber(2))) };
        var error = Assert.Throws<OperationCanceledException>(() => new XPathDynamicContext(options, budget));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(setup + 1 + 2 * 512, budget.Count);
    }
    [Fact]
    public void DynamicContextCountsComparisonInAdditionToInputValidation()
    {
        var a = new XPathExpandedName("uri", "aaa");
        var b = new XPathExpandedName("uri", "aab");
        var budget = new CountingBudget();
        var context = new XPathDynamicContext(new()
        {
            Variables = new OrderedInput<XPathExpandedName, XPathValue>(new(a, XPathValue.FromNumber(1)), new(b, XPathValue.FromNumber(2)))
        }, budget);
        // Initial/completion checkpoints charge zero; each key validation is7,
        // insertion compares URI and local strings:7+7 additional units.
        Assert.Equal(28, budget.Count);
        Assert.Equal(2, context.Variables.Count);
    }
    [Fact]
    public void MapsOwnEntriesPreserveOrderAndRejectMutationAndDuplicates()
    {
        var map = new ContextMap<string, int>((a, b) => ContextComparison.Equal(a, b, TestContext.Current.CancellationToken))
        {
            { "first", 1 },
            { "second", 2 }
        };
        map.Seal();
        Assert.Equal(["first", "second"], map.Keys);
        Assert.Equal([1, 2], map.Values);
        Assert.Equal(2, map["second"]);
        Assert.False(map.ContainsKey("third"));
        Assert.Throws<InvalidOperationException>(() => map.Add("third", 3));
        var duplicates = new ContextMap<string, int>((a, b) => ContextComparison.Equal(a, b, TestContext.Current.CancellationToken))
        {
            { "x", 1 }
        };
        Assert.Throws<ArgumentException>(() => duplicates.Add(new string(['x']), 2));
    }
    [Fact]
    public void StaticContextOwnsInputsAndValidatesSignatureKeysWithoutHashing()
    {
        string uri = new('u', 4096);
        string local = new('n', 4096);
        var name = new XPathExpandedName(uri, local);
        var signature = new XPathFunctionSignature(name, 0, 0);
        var context = new XPathStaticContext(new OrderedInput<string, string>([new("p", "urn:p")]), new OrderedInput<XPathExpandedName, XPathFunctionSignature>([new(name, signature)]), new());
        Assert.Equal("urn:p", context.Namespaces["p"]);
        Assert.Same(signature, context.Functions[new(new string(uri.ToCharArray()), new string(local.ToCharArray()))]);
        var other = new XPathFunctionSignature(new(uri, local + "x"), 0, 0);
        Assert.Throws<ArgumentException>(() => new XPathStaticContext(new Dictionary<string, string>(), new OrderedInput<XPathExpandedName, XPathFunctionSignature>([new(name, other)]), new()));
    }
    [Fact]
    public void StaticSetupLimitUsesSuppliedExpressionSpan()
    {
        var span = new XPathSourceSpan(0, 7);
        var error = Assert.Throws<XPathCompileException>(() => XPathStaticContext.FromOptions(new() { Namespaces = new OrderedInput<string, string>([new("p", "urn:p")]), Limits = new() { MaxExpressionCharacters = 1 } }, TestContext.Current.CancellationToken, span));
        Assert.Equal("XPATH-COMPILE-LIMIT", error.Diagnostic.Code);
        Assert.Equal(span, error.Diagnostic.Span);
        var legacy = Assert.Throws<XPathCompileException>(() => XPathStaticContext.FromOptions(new() { Namespaces = new Dictionary<string, string> { { "p", "urn:p" } }, Limits = new() { MaxExpressionCharacters = 1 } }, TestContext.Current.CancellationToken));
        Assert.Equal(default, legacy.Diagnostic.Span);
    }
    [Fact]
    public void CallerEnumerationEngineShapedExceptionRetainsIdentity()
    {
        var sentinel = new XPathCompileException(new("XPATH-COMPILE-LIMIT", XPathDiagnosticCategory.CompileLimit, "Caller failure.", new(3, 2)));
        var input = new ThrowingInput(sentinel);
        var actual = Assert.Throws<XPathCompileException>(() => XPathStaticContext.FromOptions(new() { Namespaces = input }, TestContext.Current.CancellationToken, new(0, 9)));
        Assert.Same(sentinel, actual);
    }
    [Fact]
    public void InvalidCoreAndDuplicateRegistrationsStillReject()
    {
        var count = new XPathExpandedName("", "count");
        Assert.Throws<ArgumentException>(() => new XPathStaticContext(new Dictionary<string, string>(), new Dictionary<XPathExpandedName, XPathFunctionSignature> { { count, new(count, 0, 0) } }, new()));
        var signature = new XPathFunctionSignature(new("urn:x", "f"), 0, 0);
        Assert.Throws<ArgumentException>(() => XPathStaticContext.FromOptions(new() { Functions = [signature, signature] }, TestContext.Current.CancellationToken));
    }
    private sealed class CountingBudget(CancellationTokenSource? source = null, long cancelAt = long.MaxValue) : IXPathBudget
    {
        internal long Count;
        public CancellationToken CancellationToken => source?.Token ?? default;
        public void ChargeOperations(long amount)
        {
            Count += amount;
            if (Count >= cancelAt)
            {
                source!.Cancel();
            }
        }
        public void CheckStringSize(int count)
        {
        }
        public void CheckNodeSetSize(int count)
        {
        }
    }
    private class OrderedInput<K, V>(params KeyValuePair<K, V>[] entries) : IReadOnlyDictionary<K, V> where K : notnull
    {
        public int Count => entries.Length;
        public IEnumerable<K> Keys => throw new InvalidOperationException("Input keys materialization forbidden.");
        public IEnumerable<V> Values => throw new InvalidOperationException("Input values materialization forbidden.");
        public V this[K key] => throw new InvalidOperationException("Input lookup forbidden.");
        public bool ContainsKey(K key) => throw new InvalidOperationException("Input hashing forbidden.");
        public bool TryGetValue(K key, out V value) => throw new InvalidOperationException("Input hashing forbidden.");
        public virtual IEnumerator<KeyValuePair<K, V>> GetEnumerator() => ((IEnumerable<KeyValuePair<K, V>>)entries).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class ThrowingInput(Exception error) : OrderedInput<string, string>
    {
        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() => throw error;
    }
}
