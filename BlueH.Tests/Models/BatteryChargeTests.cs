using BlueH.Models;

namespace BlueH.Tests.Models;

[TestFixture]
public class BatteryChargeTests
{
    [Test]
    public void UnknownHasNoPercentage()
    {
        Assert.That(BatteryCharge.Unknown.ChargePercent, Is.Null);
    }

    [Test]
    public void UnknownSaysItIsNotKnown()
    {
        Assert.That(BatteryCharge.Unknown.IsKnown, Is.False);
    }

    [Test]
    public void TheDefaultValueIsUnknownRatherThanZero()
    {
        Assert.That(default(BatteryCharge), Is.EqualTo(BatteryCharge.Unknown));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(80)]
    [TestCase(100)]
    public void APercentageInRangeIsKept(int chargePercent)
    {
        Assert.That(BatteryCharge.FromPercent(chargePercent).ChargePercent, Is.EqualTo(chargePercent));
    }

    [Test]
    public void AnEmptyBatteryIsKnownRatherThanUnknown()
    {
        Assert.That(BatteryCharge.FromPercent(0).IsKnown, Is.True);
    }

    [Test]
    public void AnEmptyBatteryIsNotEqualToUnknown()
    {
        Assert.That(BatteryCharge.FromPercent(0), Is.Not.EqualTo(BatteryCharge.Unknown));
    }

    [TestCase(-1)]
    [TestCase(101)]
    [TestCase(255)]
    [TestCase(int.MaxValue)]
    public void APercentageOutOfRangeBecomesUnknownRatherThanBeingClamped(int chargePercent)
    {
        Assert.That(BatteryCharge.FromPercent(chargePercent), Is.EqualTo(BatteryCharge.Unknown));
    }
}
