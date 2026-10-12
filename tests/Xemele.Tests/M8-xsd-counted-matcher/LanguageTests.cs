using Xemele.Validation;
using Xemele.Tests.M8XsdContent;
using Xunit;
namespace Xemele.Tests.M8XsdCountedMatcher;

public sealed class LanguageTests
{
    // Denotational finite sets: no program counters, flags, NFA or matcher code.
    private abstract record Language
    {
        internal abstract HashSet<string> Words(int cap);
        internal abstract string Xml { get; }
    }
    private sealed record Atom(char Name) : Language
    {
        internal override HashSet<string> Words(int cap) => cap > 0 ? [Name.ToString()] : [];
        internal override string Xml => $"<xs:element name='{Name}'/>";
    }
    private sealed record Group(string Kind, params Language[] Children) : Language
    {
        internal override string Xml => $"<xs:{Kind}>{string.Concat(Children.Select(c => c.Xml))}</xs:{Kind}>";
        internal override HashSet<string> Words(int cap)
        {
            if (Kind == "choice") return Children.SelectMany(c => c.Words(cap)).ToHashSet(StringComparer.Ordinal);
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (Kind == "all")
            {
                void Permute(List<Language> remaining, HashSet<string> prefix)
                {
                    if (remaining.Count == 0) { result.UnionWith(prefix); return; }
                    for (int i = 0; i < remaining.Count; i++)
                    { var tail = remaining.ToList(); var item = tail[i]; tail.RemoveAt(i); Permute(tail, Product(prefix, item.Words(cap), cap)); }
                }
                Permute(Children.ToList(), [""]); return result;
            }
            result.Add(""); foreach (var child in Children) result = Product(result, child.Words(cap), cap); return result;
        }
    }
    private sealed record Repeat(Language Body, int Min, int Max) : Language
    {
        internal override string Xml
        {
            get
            {
                string text = Body.Xml; int end = text.IndexOf('>');
                int at = text[end - 1] == '/' ? end - 1 : end;
                return text.Insert(at, $" minOccurs='{Min}' maxOccurs='{Max}'");
            }
        }
        internal override HashSet<string> Words(int cap)
        {
            var result = new HashSet<string>(StringComparer.Ordinal); HashSet<string> power = [""]; var body = Body.Words(cap);
            for (int n = 0; n <= Max; n++) { if (n >= Min) result.UnionWith(power); power = Product(power, body, cap); }
            return result;
        }
    }
    private static HashSet<string> Product(HashSet<string> left, HashSet<string> right, int cap)
    { var result = new HashSet<string>(StringComparer.Ordinal); foreach (string a in left) foreach (string b in right) if (a.Length + b.Length <= cap) result.Add(a + b); return result; }
    private static IEnumerable<string> Universe(string alphabet, int cap)
    {
        List<string> level = [""]; yield return "";
        for (int n = 1; n <= cap; n++) { level = level.SelectMany(w => alphabet.Select(c => w + c)).ToList(); foreach (string word in level) yield return word; }
    }
    public static IEnumerable<object[]> Models()
    {
        yield return [0, "a", 10]; yield return [1, "ab", 9]; yield return [2, "ab", 9]; yield return [3, "abc", 7]; yield return [4, "abc", 7]; yield return [5, "abc", 4];
    }
    private static Language Model(int id)
    {
        Language a = new Atom('a'), b = new Atom('b'), c = new Atom('c');
        return id switch
        {
            0 => new Repeat(new Group("sequence", new Repeat(a, 2, 3)), 2, 3),
            1 => new Repeat(new Group("sequence", new Repeat(new Group("choice", a, b), 2, 3)), 2, 3),
            2 => new Repeat(new Group("sequence", new Repeat(a, 2, 3), new Repeat(b, 0, 1)), 2, 3),
            3 => new Repeat(new Group("choice", new Group("sequence", new Repeat(a, 2, 3), b), c), 2, 3),
            4 => new Group("sequence", new Repeat(new Group("sequence", new Repeat(a, 2, 3), new Repeat(b, 0, 1)), 2, 3), new Repeat(c, 0, 1)),
            5 => new Group("all", a, new Repeat(b, 0, 1), new Repeat(c, 0, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }
    [Theory]
    [MemberData(nameof(Models))]
    public void IndependentFiniteLanguageEnumeration(int modelId, string alphabet, int cap)
    {
        var model = Model(modelId); var expected = model.Words(cap);
        var graph = ContentTests.Schema($"<xs:complexType name='T'>{model.Xml}</xs:complexType>"); var program = ContentTests.Program(graph);
        int cases = 0;
        foreach (string word in Universe(alphabet, cap))
        {
            var op = new OperationTests.Ledger(100_000_000); var cursor = new XsdContentMatcher().CreateCursor(program); bool accepts = true;
            foreach (char ch in word) if (!cursor.Accept(new("", ch.ToString()), op, out _, out _)) { accepts = false; break; }
            accepts = accepts && cursor.Complete(op);
            Assert.True(op.IsComplete, "Refusal must not become semantic invalidity.");
            Assert.True(accepts == expected.Contains(word), $"Model {modelId}, word '{word}', expected {expected.Contains(word)}"); cases++;
        }
        Assert.Equal(Enumerable.Range(0, cap + 1).Sum(n => (int)Math.Pow(alphabet.Length, n)), cases);
    }
    [Theory]
    [InlineData(3, false)] [InlineData(4, true)] [InlineData(6, true)] [InlineData(9, true)] [InlineData(10, false)]
    public void NestedMinimumAndMaximumBoundaries(int count, bool expected)
    {
        var program = ContentTests.Program(ContentTests.Schema($"<xs:complexType name='T'>{Model(0).Xml}</xs:complexType>"));
        Assert.Equal(expected, ContentTests.Accepts(program, Enumerable.Repeat("a", count).ToArray()));
    }
    [Fact]
    public void PartitionGrowthAndExactWorkRefusal()
    {
        var graph = ContentTests.Schema("<xs:complexType name='T'><xs:sequence minOccurs='1' maxOccurs='1000'><xs:element name='a' maxOccurs='unbounded'/></xs:sequence></xs:complexType>");
        var program = ContentTests.Program(graph); var measured = new OperationTests.Ledger(100_000_000);
        void Run(OperationTests.Ledger op, int children)
        { var cursor = new XsdContentMatcher().CreateCursor(program); for (int i = 0; i < children; i++) Assert.True(cursor.Accept(new("", "a"), op, out _, out _)); Assert.True(cursor.Complete(op)); }
        Run(measured, 1000); Assert.True(measured.Work < 1_000_000); Assert.True(measured.Components < 1000);
        Run(new(measured.Work, measured.Components), 1000);
        var refused = new OperationTests.Ledger(measured.Work - 1); Assert.Throws<SchemaCompileException>(() => Run(refused, 1000)); Assert.False(refused.IsComplete);
        var shorter = new OperationTests.Ledger(); Run(shorter, 100); Assert.True(measured.Work < shorter.Work * 11);
        TestContext.Current.TestOutputHelper!.WriteLine($"100 children: work={shorter.Work}, components={shorter.Components}; 1000 children: work={measured.Work}, components={measured.Components}");
    }
    [Fact]
    public void DominanceCancellationFaultsCursorAndPreservesCallerException()
    {
        var graph = ContentTests.Schema("<xs:complexType name='T'><xs:sequence maxOccurs='1000'><xs:element name='a' maxOccurs='unbounded'/></xs:sequence></xs:complexType>"); var program = ContentTests.Program(graph);
        var cursor = new XsdContentMatcher().CreateCursor(program); var warm = new OperationTests.Ledger(); Assert.True(cursor.Accept(new("", "a"), warm, out _, out _));
        using var source = new CancellationTokenSource(); var op = new OperationTests.Ledger(token: source.Token) { OnCharge = n => { if (n == 40) source.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => cursor.Accept(new("", "a"), op, out _, out _)); Assert.Equal(40, op.Calls); Assert.False(cursor.Complete(new OperationTests.Ledger()));
        var error = new InvalidOperationException("caller"); var throwing = new OperationTests.Ledger { OnCharge = _ => throw error };
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => new XsdContentMatcher().CreateCursor(program).Accept(new("", "a"), throwing, out _, out _)));
    }
}
