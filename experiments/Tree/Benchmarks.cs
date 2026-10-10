using BenchmarkDotNet.Attributes;
namespace TreeSpike;

[MemoryDiagnoser, ShortRunJob]
public class Construction
{
    [Params(1024, 16384)] public int Count { get; set; }
    [Params(false, true)] public bool Unique { get; set; }
    private Tokens _tokens = null!;
    [GlobalSetup] public void Setup() => _tokens = new Tokens(Count, Unique);
    [Benchmark(Baseline = true)] public ObjectTree Objects() => Factory.Objects(_tokens);
    [Benchmark] public ArenaTree Arena() => Factory.Arena(_tokens);
}

[MemoryDiagnoser, ShortRunJob]
public class Traversal
{
    [Params(1024, 16384)] public int Count { get; set; }
    private ArenaTree _arena = null!; private ObjectTree _objects = null!;
    [GlobalSetup] public void Setup() { var tokens = new Tokens(Count, false); _arena = Factory.Arena(tokens); _objects = Factory.Objects(tokens); }
    [Benchmark(Baseline = true)] public long Objects() => _objects.Traverse();
    [Benchmark] public long Arena() => _arena.Traverse();
}

[MemoryDiagnoser, ShortRunJob]
public class Mutation
{
    [Params(1024, 16384)] public int Count { get; set; }
    private ArenaTree _arena = null!; private ObjectTree.Node _leaf = null!, _root = null!, _parent = null!;
    [GlobalSetup] public void Setup()
    {
        _arena = new ArenaTree(); var tree = new ObjectTree();
        _arena.Add("root", "", -1); _arena.Add("parent", "", 0);
        _root = tree.Add("root", "", null); _parent = tree.Add("parent", "", _root);
        for (int i = 2; i < Count; i++) { _arena.Add("leaf", "", 1); _leaf = tree.Add("leaf", "", _parent); }
    }
    // Round trip restores the exact structure every invocation.
    [Benchmark(Baseline = true)] public ObjectTree.Node Objects() { _leaf.MoveTo(_root); _leaf.MoveTo(_parent); return _leaf; }
    [Benchmark] public Handle Arena() { var leaf = new Handle(_arena, Count - 1); leaf.MoveTo(new Handle(_arena, 0)); leaf.MoveTo(new Handle(_arena, 1)); return leaf; }
}

[MemoryDiagnoser, ShortRunJob]
public class Names
{
    [Params(false, true)] public bool Unique { get; set; }
    [Params(8, 48)] public int Length { get; set; }
    private Tokens _tokens = null!;
    private Dictionary<string, string> _table = null!;
    [GlobalSetup] public void Setup()
    {
        _tokens = new Tokens(1024, Unique, Length); _table = new(StringComparer.Ordinal);
        for (int i = 0; i < 1024; i++) Factory.Intern(_table, _tokens.At(i));
    }
    [Benchmark(Baseline = true)] public int TemporaryLookup()
    {
        int sum = 0;
        for (int i = 0; i < 1024; i++) sum += _table[_tokens.At(i).ToString()].Length;
        return sum;
    }
    [Benchmark] public int BorrowedLookup()
    {
        int sum = 0; var lookup = _table.GetAlternateLookup<ReadOnlySpan<char>>();
        for (int i = 0; i < 1024; i++) sum += lookup[_tokens.At(i)].Length;
        return sum;
    }
    [Benchmark] public int BorrowedIntern()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal); int sum = 0;
        for (int i = 0; i < 1024; i++) sum += Factory.Intern(table, _tokens.At(i)).Length;
        return sum;
    }
    [Benchmark] public int TemporaryIntern()
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal); int sum = 0;
        for (int i = 0; i < 1024; i++)
        {
            string name = _tokens.At(i).ToString();
            if (!table.TryGetValue(name, out string? value)) { value = name; table.Add(name, name); }
            sum += value.Length;
        }
        return sum;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class Text
{
    [Params(32, 8192)] public int Length { get; set; }
    private string _source = null!;
    [GlobalSetup] public void Setup() => _source = new string('文', Length + 64);
    [Benchmark(Baseline = true)] public string Owned() => new(_source.AsSpan(32, Length));
    [Benchmark] public ReadOnlyMemory<char> RetainedView() => _source.AsMemory(32, Length);
}
