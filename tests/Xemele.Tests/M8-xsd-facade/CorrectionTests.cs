using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFacade;
public sealed class CorrectionTests
{
    [Theory]
    [InlineData("element", "default")] [InlineData("element", "fixed")]
    [InlineData("attribute", "default")] [InlineData("attribute", "fixed")]
    public void CanonicalConstraintMustSatisfyDeclaredPatternBeforePublication(string declaration, string constraint)
    {
        string type = "<xs:simpleType name='P'><xs:restriction base='xs:int'><xs:pattern value='0[0-9]'/></xs:restriction></xs:simpleType>";
        Assert.Throws<SchemaCompileException>(() => PublicTests.Compile(type + $"<xs:{declaration} name='unused' type='P' {constraint}='01'/>"));
    }
    [Theory]
    [InlineData("element", "default")] [InlineData("element", "fixed")]
    [InlineData("attribute", "default")] [InlineData("attribute", "fixed")]
    public void CanonicalCompatibleConstraintPublishes(string declaration, string constraint)
    {
        string type = "<xs:simpleType name='P'><xs:restriction base='xs:int'><xs:pattern value='[0-9]+'/></xs:restriction></xs:simpleType>";
        Assert.NotNull(PublicTests.Compile(type + $"<xs:{declaration} name='unused' type='P' {constraint}='01'/>"));
    }
    [Fact]
    public void ListScanChargesOnlyInspectedUnits()
    {
        var op = new Ledger(); var remaining = "a b c".AsSpan(); int tokens = 0;
        while (!remaining.IsEmpty)
        {
            int end = XsdFacadeText.IndexOf(remaining, ' ', op, default);
            if (end < 0) end = remaining.Length;
            tokens++; remaining = end == remaining.Length ? [] : remaining[(end + 1)..];
        }
        Assert.Equal(3, tokens); Assert.Equal(5, op.Work);
    }
    [Fact]
    public void LongTokenScanPollsInsideBorrowedToken()
    {
        using var cancel = new CancellationTokenSource();
        var op = new Ledger(cancel.Token) { OnCharge = work => { if (work == 17) cancel.Cancel(); } };
        string token = new('n', 4096);
        Assert.Throws<OperationCanceledException>(() => XsdFacadeText.IndexOf(token, ' ', op, default));
        Assert.Equal(17, op.Calls); Assert.Equal(16, op.Work);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void QNamePrefixAndLocalComparisonsPollInsideBorrowedNames(bool prefix)
    {
        using var cancel = new CancellationTokenSource();
        string name = new('n', 2048);
        var context = new SchemaNamespaceContext(new Dictionary<string,string> { [prefix ? name : "p"] = "urn:n" }, "", null);
        var declarations = new Dictionary<SchemaName,XsdNotationDeclaration>
        { [new("urn:n", prefix ? "N" : name)] = new(new("urn:n", prefix ? "N" : name), "n", null, context, default) };
        string text = prefix ? name + ":N" : "p:" + name;
        // Prefix case cancels after the colon scan, inside prefix equality;
        // local case cancels after the short prefix lookup, inside local equality.
        int trigger = prefix ? 2080 : 37;
        var op = new Ledger(cancel.Token);
        op.OnCharge = _ => { if (op.Calls == trigger) cancel.Cancel(); };
        Assert.Throws<OperationCanceledException>(() => XsdFacadeText.IsDeclaredNotation(text, context, declarations, op, default));
        Assert.Equal(trigger, op.Calls);
    }
    private sealed class Ledger(CancellationToken token = default) : ISchemaOperation
    {
        internal long Work; internal int Calls; internal Action<long>? OnCharge;
        public XmlResourceLimits Limits { get; } = new(); public CancellationToken CancellationToken => token;
        public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation at) { Calls++; OnCharge?.Invoke(Work + work); token.ThrowIfCancellationRequested(); Work += work; return true; }
        public bool ChargeComponents(int count, XmlLocation at) { token.ThrowIfCancellationRequested(); return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException();
        public void Refuse(string code, string message, XmlLocation at) => throw new InvalidOperationException();
    }
}
