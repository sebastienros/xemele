using Xemele.Validation;
using Xunit;
using P = Xemele.Tests.M8XsdValidate.EvaluatorTests;
namespace Xemele.Tests.M8XsdInstanceHintOrdering;

public sealed class BoundaryTests
{
    internal static XmlResourceLimits Unlimited => new() { Depth = null, TokenCharacters = null, DocumentCharacters = null, Diagnostics = null, EvaluationWork = null, AttributesPerElement = null };
    private static string Xml => $"<r {OrderingTests.Prefixes}><p:a/><b xsi:schemaLocation='urn:p one'/><c xsi:schemaLocation='urn:p two'/></r>";
    [Theory]
    [InlineData("MaxOperations")]
    [InlineData("EvaluationWork")]
    [InlineData("Both")]
    public void ExactSharedWorkSucceedsAndOneShortRefuses(string limit)
    {
        var p = P.Prepare("<xs:element name='r'/>");
        const long cap = 1_000_000;
        var hook = new OrderingTests.Hook();
        var original = p.Run(Xml, new()
        {
            MaxOperations = cap,
            Limits = Unlimited
        }, hook);
        Assert.True(original.IsComplete);
        Assert.False(original.IsValid);
        var work = cap - hook.Remaining!.Value;
        ValidationOptions Options(long value) => new()
        {
            MaxOperations = limit == "EvaluationWork" ? null : value,
            Limits = Unlimited with
            {
                EvaluationWork = limit == "MaxOperations" ? null : value
            }
        };
        var exact = p.Run(Xml, Options(work), new OrderingTests.Hook());
        Assert.True(exact.IsComplete);
        Assert.Equal(2, exact.Diagnostics.Count);
        var shortResult = p.Run(Xml, Options(work - 1), new OrderingTests.Hook());
        Assert.False(shortResult.IsComplete);
        Assert.Equal("VALIDATION.Limit", shortResult.Diagnostics[^1].Code);
    }
    [Theory]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void ExactDiagnosticLimitAndOneShort(int cap, bool complete)
    {
        var result = P.Prepare("<xs:element name='r'/>").Run(Xml, new()
        {
            MaxOperations = null,
            Limits = Unlimited with
            {
                Diagnostics = cap
            }
        });
        Assert.Equal(complete, result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal(cap, result.Diagnostics.Count);
        Assert.Equal(complete ? "XSD.HintOrder" : "VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Fact]
    public void OrderingScanDoesNotDoubleChargeDocumentCharacters()
    {
        var xml = $"<r xmlns:xsi='{OrderingTests.Xsi}' xsi:schemaLocation='urn:p one'/>";
        // Text() owns scalar document accounting: r1, declaration prefix3/URI41,
        // attribute local14/URI41/prefix3/value9 =112. Ordering only charges work.
        var characters = 1 + 3 + OrderingTests.Xsi.Length + 14 + OrderingTests.Xsi.Length + 3 + 9;
        var p = P.Prepare("<xs:element name='r'/>");
        Assert.True(p.Run(xml, new()
        {
            MaxOperations = null,
            Limits = Unlimited with
            {
                DocumentCharacters = characters
            }
        }).IsValid);
        var result = p.Run(xml, new()
        {
            MaxOperations = null,
            Limits = Unlimited with
            {
                DocumentCharacters = characters - 1
            }
        });
        Assert.False(result.IsComplete);
        Assert.Equal("VALIDATION.Limit", result.Diagnostics[^1].Code);
    }
    [Theory]
    [InlineData("namespace-token")]
    [InlineData("location-token")]
    [InlineData("whitespace")]
    public void CancellationInsideLongBorrowedTokenWithNullCaps(string kind)
    {
        using var op = new Ledger();
        var text = kind == "whitespace" ? new string(' ', 8192) : new string('a', 8192);
        if (kind == "location-token")
        {
            var first = "urn:p ".AsSpan();
            Assert.True(XsdHintNamespaces.Token(ref first, op, default, out _));
        }
        var before = op.Work;
        op.CancelAt = before + 1024;
        Assert.Throws<OperationCanceledException>(() => Scan(text, op));
        Assert.Equal(before + 1024, op.Work);
        Assert.Equal(0, op.Components);
        Assert.Null(op.RemainingWork);
        static void Scan(string input, Ledger operation)
        {
            var remaining = input.AsSpan();
            XsdHintNamespaces.Token(ref remaining, operation, default, out _);
        }
    }
    [Fact]
    public void CancellationInsideLongOrdinalComparisonWithNullCaps()
    {
        var stored = "urn:" + new string('a', 8192);
        var names = new XsdHintNamespaces();
        using var op = new Ledger();
        names.Add(stored, op, default);
        op.CancelAt = op.Work + 1024;
        Assert.Throws<OperationCanceledException>(() => names.Contains(stored, op, default));
        Assert.Equal(op.CancelAt, op.Work);
        Assert.Null(op.RemainingWork);
    }
    [Theory]
    [InlineData("token")]
    [InlineData("comparison")]
    public void PrimitiveExactWorkBoundaryAndOneShort(string kind)
    {
        using var measure = new Ledger();
        Operation(measure);
        var required = measure.Work;
        using var exact = new Ledger(required);
        Operation(exact);
        Assert.Equal(required, exact.Work);
        using var shortOp = new Ledger(required - 1);
        Assert.Throws<XsdEvaluationStopped>(() => Operation(shortOp));
        Assert.False(shortOp.IsComplete);
        void Operation(Ledger op)
        {
            if (kind == "comparison")
            {
                Assert.True(XsdHintNamespaces.Equal("urn:abcdefgh", "urn:abcdefgh", op, default));
            }
            else
            {
                var remaining = " \tlong-token\r\n".AsSpan();
                Assert.True(XsdHintNamespaces.Token(ref remaining, op, default, out var token));
                Assert.True(token.SequenceEqual("long-token"));
            }
        }
    }
    [Fact]
    public void BorrowedTableGrowthAndEntryReservationsPrecedeAllocation()
    {
        var names = new XsdHintNamespaces();
        using var denied = new Ledger(components: 0);
        Assert.Throws<XsdEvaluationStopped>(() => names.Add("urn:a", denied, default));
        Assert.Equal(0, denied.Components);
        using var allowed = new Ledger(components: 2);
        names.Add("urn:a", allowed, default);
        Assert.Equal(2, allowed.Components);
        names.Add("urn:a", allowed, default);
        Assert.Equal(2, allowed.Components);
        Assert.Throws<XsdEvaluationStopped>(() => names.Add("urn:b", allowed, default));
        using var check = new Ledger();
        Assert.True(names.Contains("urn:a", check, default));
        Assert.False(names.Contains("urn:b", check, default));
    }
    [Fact]
    public void CancellationAndDocumentMutationStillPropagateAroundNewHook()
    {
        var p = P.Prepare("<xs:element name='r'/>");
        using var cancellation = new CancellationTokenSource();
        var hook = new OrderingTests.Hook { OnEnter = (node, _) => { if (node.LocalName == "a") { cancellation.Cancel(); } } };
        Assert.Throws<OperationCanceledException>(() => p.Run(Xml, new() { MaxOperations = null, Limits = Unlimited, CancellationToken = cancellation.Token }, hook));
        var document = XmlDocument.Parse(Xml, cancellationToken: TestContext.Current.CancellationToken);
        hook = new()
        {
            OnEnter = (node, _) => { if (node.LocalName == "a") { node.SetAttribute("changed", "yes"); } }
        };
        Assert.Throws<InvalidOperationException>(() => p.Run(document, factory: hook));
    }
    [Fact]
    public async Task RepeatedAndConcurrentRunsHaveIndependentSetsAndDoNotMutateDocuments()
    {
        var p = P.Prepare("<xs:element name='r'/>");
        var timely = $"<r {OrderingTests.Prefixes}><b xsi:schemaLocation='urn:p one'/><p:a/></r>";
        var good = XmlDocument.Parse(timely, cancellationToken: TestContext.Current.CancellationToken);
        var bad = XmlDocument.Parse(Xml, cancellationToken: TestContext.Current.CancellationToken);
        long goodRevision = good.ContentRevision, badRevision = bad.ContentRevision;
        for (var i = 0; i < 4; i++)
        {
            Assert.False(p.Run(bad).IsValid);
            Assert.True(p.Run(good).IsValid);
        }
        var jobs = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var result = p.Run(i % 2 == 0 ? good : bad);
            Assert.True(result.IsComplete);
            Assert.Equal(i % 2 == 0, result.IsValid);
        }, TestContext.Current.CancellationToken)).ToArray();
        await Task.WhenAll(jobs);
        Assert.Equal(goodRevision, good.ContentRevision);
        Assert.Equal(badRevision, bad.ContentRevision);
    }
    internal sealed class Ledger(long? maximum = null, int? components = null) : ISchemaOperation, IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        internal long Work; internal int Components; internal long? CancelAt;
        public XmlResourceLimits Limits => Unlimited;
        public CancellationToken CancellationToken => cancellation.Token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => maximum is long cap ? cap - Work : null;
        public bool Charge(long amount, XmlLocation location)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (maximum is long cap && amount > cap - Work)
            {
                IsComplete = false;
                return false;
            }
            Work += amount;
            if (CancelAt is long at && Work >= at)
            {
                cancellation.Cancel();
            }

            return IsComplete;
        }
        public bool ChargeComponents(int count, XmlLocation location)
        {
            if (components is int cap && count > cap - Components)
            {
                IsComplete = false;
                return false;
            }
            Components += count;
            return IsComplete;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => true;
        public void Refuse(string code, string message, XmlLocation location) => IsComplete = false;
        public void Dispose() => cancellation.Dispose();
    }
}
