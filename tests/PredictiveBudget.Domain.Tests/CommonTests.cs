using PredictiveBudget.Domain.Common;

namespace PredictiveBudget.Domain.Tests;

public sealed class CommonTests
{
    [Fact]
    public void Money_AdditionAndSubtraction_WorkForMatchingCurrencies()
    {
        var first = new Money(125.50m, "CAD");
        var second = new Money(25.25m, "CAD");

        Assert.Equal(new Money(150.75m, "CAD"), first + second);
        Assert.Equal(new Money(100.25m, "CAD"), first - second);
    }

    [Fact]
    public void Money_Operators_ThrowForCurrencyMismatch()
    {
        var first = new Money(125.50m, "CAD");
        var second = new Money(25.25m, "USD");

        Assert.Throws<InvalidOperationException>(() => _ = first + second);
        Assert.Throws<InvalidOperationException>(() => _ = first - second);
    }

    [Fact]
    public void DateRange_Contains_IsInclusive()
    {
        var range = new DateRange(new DateOnly(2026, 3, 20), new DateOnly(2026, 3, 22));

        Assert.True(range.Contains(new DateOnly(2026, 3, 20)));
        Assert.True(range.Contains(new DateOnly(2026, 3, 21)));
        Assert.True(range.Contains(new DateOnly(2026, 3, 22)));
        Assert.False(range.Contains(new DateOnly(2026, 3, 23)));
    }
}
