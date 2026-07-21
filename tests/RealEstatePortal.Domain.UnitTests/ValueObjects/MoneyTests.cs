using Shouldly;
using RealEstatePortal.Domain.ValueObjects;
using System;
using Xunit;

namespace RealEstatePortal.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var money = new Money(1_500_000m, "TRY");

        money.Amount.ShouldBe(1_500_000m);
        money.Currency.ShouldBe("TRY");
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Throws()
    {
        Should.Throw<ArgumentException>(() => new Money(-1m, "TRY"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithMissingCurrency_Throws(string? currency)
    {
        Should.Throw<ArgumentException>(() => new Money(100m, currency!));
    }

    [Fact]
    public void TwoMoneys_WithSameValues_AreEqual()
    {
        var a = new Money(100m, "TRY");
        var b = new Money(100m, "TRY");

        a.ShouldBe(b);          // value-object equality
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void TwoMoneys_WithDifferentCurrency_AreNotEqual()
    {
        new Money(100m, "TRY").ShouldNotBe(new Money(100m, "USD"));
    }
}