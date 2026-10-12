using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
using Ledger = Xemele.Tests.M8Facets.OperationTests.Ledger;
namespace Xemele.Tests.M8Facets;

public sealed class FacetNumberTests
{
    [Theory]
    [InlineData(1)][InlineData(256)][InlineData(4096)]
    public void AllZeroCountsCostOneScanPerDigitAndNoGrowth(int length)
    {
        string input = new('0', length); var count = new Ledger();
        Assert.True(FacetNumber.TryParse(input, count, out var value)); Assert.True(value.IsZero);
        Assert.Equal(length, count.Work); Assert.Equal(0, count.Components);
        ExactParse(input, length, 0);
    }
    [Theory]
    [InlineData("1", 2)][InlineData("00001", 6)][InlineData("+00001", 7)]
    [InlineData("9999999999999999999", 38)][InlineData("-0000", 5)]
    public void NativeConversionHasIndependentLinearCosts(string input, long work)
    { var count = new Ledger(); Assert.True(FacetNumber.TryParse(input, count, out _)); Assert.Equal(work, count.Work); Assert.Equal(0, count.Components); ExactParse(input, work, 0); }
    [Theory]
    [InlineData(20)][InlineData(256)][InlineData(4096)]
    public void CanonicalCopyReservesOneOwnedStringBeforeItsChargedDigits(int length)
    {
        string input = new('9', length); var count = new Ledger();
        count.Callback = () => { if (count.Work == length) Assert.Equal(0, count.Components); if (count.Work > length) Assert.Equal(1, count.Components); };
        Assert.True(FacetNumber.TryParse(input, count, out _)); Assert.Equal(2L * length, count.Work); Assert.Equal(1, count.Components);
        ExactParse(input, 2L * length, 1);
        var growth = Op(null, 1); Assert.True(growth.ChargeComponents(1, default));
        Assert.False(FacetNumber.TryParse(input, growth, out _)); Assert.False(growth.IsComplete);
        Assert.Null(growth.RemainingWork);
    }
    [Theory]
    [InlineData("1234567890123456789012345", "1234567890123456789012345", 27, 0)]
    [InlineData("1234567890123456789012345", "1234567890123456789012346", 27, -1)]
    [InlineData("1234567890123456789012345", "2234567890123456789012345", 3, -1)]
    [InlineData("1234567890123456789012345", "12345678901234567890123450", 2, -1)]
    [InlineData("1234567890123456789012345", "1", 1, 1)]
    [InlineData("9999999999999999999", "1", 1, 1)]
    public void ComparisonsChargeDispatchLengthAndOnlyInspectedDigits(string left, string right, long work, int sign)
    {
        Assert.True(FacetNumber.TryParse(left, Op(), out var a)); Assert.True(FacetNumber.TryParse(right, Op(), out var b));
        var count = new Ledger(); Assert.Equal(sign, Math.Sign(a.Compare(b, count)!.Value)); Assert.Equal(work, count.Work); Assert.Equal(0, count.Components);
        var exact = Op(work); Assert.Equal(sign, Math.Sign(a.Compare(b, exact)!.Value)); Assert.Equal(0, exact.RemainingWork);
        var shortOp = Op(work); Assert.True(shortOp.Charge(1, default)); Assert.Null(a.Compare(b, shortOp)); Assert.False(shortOp.IsComplete);
    }
    [Fact]
    public void LongEqualAndLateDifferentCanonicalCountsHaveLinearComparisonCosts()
    {
        const int length = 4096;
        Assert.True(FacetNumber.TryParse(new string('9', length), Op(), out var a));
        Assert.True(FacetNumber.TryParse(new string('9', length - 1) + "8", Op(), out var b));
        foreach (var right in new[] { a, b })
        {
            var ledger = new Ledger(); Assert.NotNull(a.Compare(right, ledger)); Assert.Equal(length + 2, ledger.Work); Assert.Equal(0, ledger.Components);
            var exact = Op(length + 2); Assert.NotNull(a.Compare(right, exact)); Assert.Equal(0, exact.RemainingWork);
            var shortOp = Op(length + 1); Assert.Null(a.Compare(right, shortOp)); Assert.False(shortOp.IsComplete);
        }
    }
    [Theory]
    [InlineData(100, 50)][InlineData(19, 24)][InlineData(100, 105)]
    public void CancellationWithNullCapsOccursInsideScanNativeConversionAndCopy(int length, int cancelAt)
    {
        using var cancellation = new CancellationTokenSource(); var count = new Ledger(cancellation, cancelAt);
        Assert.Null(count.RemainingWork); Assert.Null(count.Limits.EvaluationWork);
        Assert.Throws<OperationCanceledException>(() => FacetNumber.TryParse(new string('9', length), count, out _));
        Assert.Equal(cancelAt, count.Work);
    }
    [Fact]
    public void CancellationAndCallerExceptionsInsideDigitComparisonPropagate()
    {
        Assert.True(FacetNumber.TryParse(new string('9', 100), Op(), out var a));
        using var cancellation = new CancellationTokenSource(); var count = new Ledger(cancellation, 7);
        Assert.Throws<OperationCanceledException>(() => a.Compare(a, count)); Assert.Equal(7, count.Work);
        var marker = new InvalidOperationException("caller"); var caller = new Ledger(); caller.Callback = () => { if (caller.Work == 5) throw marker; };
        Assert.Same(marker, Assert.Throws<InvalidOperationException>(() => a.Compare(a, caller)));
    }
    [Theory]
    [InlineData("-1")][InlineData("+")][InlineData("12x")]
    public void InvalidLexicalValuesRemainCleanRefusals(string input)
    { var op = Op(); Assert.False(FacetNumber.TryParse(input, op, out _)); Assert.True(op.IsComplete); }
    private static void ExactParse(string input, long work, int components)
    {
        var exact = Op(work, components == 0 ? null : components); Assert.True(FacetNumber.TryParse(input, exact, out _)); Assert.Equal(0, exact.RemainingWork);
        var shortOp = Op(work); Assert.True(shortOp.Charge(1, default)); Assert.False(FacetNumber.TryParse(input, shortOp, out _)); Assert.False(shortOp.IsComplete);
    }
}
