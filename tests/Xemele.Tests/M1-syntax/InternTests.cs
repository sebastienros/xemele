using System.Globalization;
using System.Reflection;
using Xemele.Parsing.Syntax;
using Xunit;
namespace Xemele.Tests.Syntax;
public sealed class InternTests
{
    private static Dictionary<string, string> Table(XmlParserCore core) =>
        (Dictionary<string, string>)typeof(XmlParserCore).GetField("names", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(core)!;
    [Fact]
    public void DistinctSharedPrefixNamesOwnOnlyMissesAndRepeatsUseCanonicalKeys()
    {
        using var core = new XmlParserCore(new() { Limits = new() { InternedNames = 2001 } });
        var sink = new StreamingTests.CountingSink(); var cancellation = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancellation);
        string prefix = new('p', 512);
        for (int i = 0; i < 2000; i++)
        {
            string name = prefix + i.ToString(CultureInfo.InvariantCulture);
            string source = "<" + name + "/>";
            Assert.Equal(ScanStep.NeedInput, core.Append(source, [], false, sink, cancellation).Step);
            Assert.Equal(ScanStep.NeedInput, core.Append(source, [], false, sink, cancellation).Step);
            Assert.True(Table(core).TryGetValue(name, out string? owned));
            Assert.NotSame(name, owned); Assert.Same(owned, Table(core)[name]);
            Assert.True(Table(core).GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name.AsSpan(), out string? borrowedHit));
            Assert.Same(owned, borrowedHit);
        }
        Assert.Equal(2001, Table(core).Count);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancellation).Step);
    }
    [Fact]
    public void OrdinalLookupIsCultureIndependentAndCaseSensitive()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            List<string>? expected = null;
            foreach (var culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("tr-TR"), new CultureInfo("en-US") })
            {
                CultureInfo.CurrentCulture = culture;
                using var core = new XmlParserCore(new()); var sink = new SyntaxTests.Sink();
                Assert.Equal(ScanStep.Completed, core.Append("<r><I/><i/><İ/><ı/><α/></r>", [], true, sink, TestContext.Current.CancellationToken).Step);
                Assert.Equal(6, Table(core).Count);
                if (expected is null) expected = sink.Events; else Assert.Equal(expected, sink.Events);
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [Fact]
    public void AdversarialInitialBucketCollisionsTriggerRuntimeDefense()
    {
        using var core = new XmlParserCore(new() { Limits = new() { InternedNames = 256 } });
        var table = Table(core); int capacity = table.EnsureCapacity(256);
        var field = typeof(Dictionary<string, string>).GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var initial = (IEqualityComparer<string>)field.GetValue(table)!;
        Assert.Contains("NonRandomized", initial.GetType().FullName!);
        var candidates = new List<string>();
        for (int i = 0; i < 500000 && candidates.Count < 140; i++)
        {
            if ((i & 4095) == 0) TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            string name = "n" + i.ToString("x", CultureInfo.InvariantCulture);
            if ((uint)initial.GetHashCode(name) % (uint)capacity == 0) candidates.Add(name);
        }
        Assert.Equal(140, candidates.Count);
        var sink = new StreamingTests.CountingSink(); var cancellation = TestContext.Current.CancellationToken;
        core.Append("<r>", [], false, sink, cancellation);
        foreach (string name in candidates)
            Assert.Equal(ScanStep.NeedInput, core.Append("<" + name + "/>", [], false, sink, cancellation).Step);
        Assert.NotSame(initial, field.GetValue(table));
        foreach (string name in candidates) Assert.True(table.ContainsKey(name));
        Assert.Equal(141, table.Count);
        Assert.Equal(ScanStep.Completed, core.Append("</r>", [], true, sink, cancellation).Step);
    }
    [Fact]
    public void QuotaRejectsBeforeAllocatingAnotherOwnedKey()
    {
        using var core = new XmlParserCore(new() { Limits = new() { InternedNames = 1 } }); var sink = new StreamingTests.CountingSink();
        var r = core.Append("<r><c/></r>", [], true, sink, TestContext.Current.CancellationToken);
        Assert.Equal("XML.ResourceLimit", r.Diagnostic!.Code); Assert.Single(Table(core));
        Assert.True(Table(core).ContainsKey("r")); Assert.False(Table(core).ContainsKey("c"));
    }
}
