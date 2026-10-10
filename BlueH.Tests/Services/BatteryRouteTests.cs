using BlueH.Models;
using BlueH.Services;
using BlueH.Tests.Fakes;

namespace BlueH.Tests.Services;

[TestFixture]
public class BatteryRouteTests
{
    private static readonly Guid AnyContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");

    [Test]
    public void TheScannerTakesTheValueFromWhicheverObjectCarriesIt()
    {
        IReadOnlyDictionary<string, object?>[] propertySets =
        [
            new Dictionary<string, object?>(),
            new Dictionary<string, object?> { ["System.Devices.Connected"] = true },
            new Dictionary<string, object?> { [DevicePropertyReader.BatteryPercentProperty] = (byte)80 }
        ];

        Assert.That(BatteryPropertyScanner.FindRawPercent(propertySets), Is.EqualTo(80));
    }

    [Test]
    public void TheScannerTakesTheFirstValueWhenSeveralObjectsCarryOne()
    {
        IReadOnlyDictionary<string, object?>[] propertySets =
        [
            new Dictionary<string, object?> { [DevicePropertyReader.BatteryPercentProperty] = (byte)80 },
            new Dictionary<string, object?> { [DevicePropertyReader.BatteryPercentProperty] = (byte)40 }
        ];

        Assert.That(BatteryPropertyScanner.FindRawPercent(propertySets), Is.EqualTo(80));
    }

    [Test]
    public void TheScannerReportsNothingWhenNoObjectCarriesABattery()
    {
        IReadOnlyDictionary<string, object?>[] propertySets =
        [
            new Dictionary<string, object?>(),
            new Dictionary<string, object?> { ["System.Devices.Connected"] = true }
        ];

        Assert.That(BatteryPropertyScanner.FindRawPercent(propertySets), Is.Null);
    }

    [Test]
    public void TheScannerHandlesHavingNothingToScan()
    {
        Assert.That(BatteryPropertyScanner.FindRawPercent([]), Is.Null);
    }

    [Test]
    public void TheBatteryByteWindowsActuallyReturnsIsRead()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.BatteryPercentProperty] = (byte)80
        };

        Assert.That(DevicePropertyReader.ReadBatteryPercent(properties), Is.EqualTo(80));
    }

    [Test]
    public void AnAbsentBatteryPropertyStaysUnknownRatherThanBecomingZero()
    {
        Assert.That(DevicePropertyReader.ReadBatteryPercent(new Dictionary<string, object?>()), Is.Null);
    }

    [Test]
    public void ABatteryPropertyOfTheWrongTypeStaysUnknown()
    {
        Dictionary<string, object?> properties = new()
        {
            [DevicePropertyReader.BatteryPercentProperty] = "80"
        };

        Assert.That(DevicePropertyReader.ReadBatteryPercent(properties), Is.Null);
    }

    [Test]
    public async Task TheChainStopsAtTheFirstRouteThatKnowsTheCharge()
    {
        FakeBatteryService working = new();
        working.SetCharge(AnyContainer, BatteryCharge.FromPercent(80));
        FakeBatteryService neverReached = new();
        FirstKnownBatteryService chain = new([working, neverReached]);

        await chain.ReadChargeAsync(AnyContainer, CancellationToken.None);

        Assert.That(neverReached.ReadCount, Is.Zero);
    }

    [Test]
    public async Task TheChainFallsThroughToTheNextRouteWhenTheChargeIsUnknown()
    {
        FakeBatteryService unknown = new();
        FakeBatteryService working = new();
        working.SetCharge(AnyContainer, BatteryCharge.FromPercent(40));
        FirstKnownBatteryService chain = new([unknown, working]);

        BatteryCharge charge = await chain.ReadChargeAsync(AnyContainer, CancellationToken.None);

        Assert.That(charge.ChargePercent, Is.EqualTo(40));
    }

    [Test]
    public async Task TheChainReportsUnknownWhenNoRouteKnows()
    {
        FirstKnownBatteryService chain = new([new FakeBatteryService(), new FakeBatteryService()]);

        BatteryCharge charge = await chain.ReadChargeAsync(AnyContainer, CancellationToken.None);

        Assert.That(charge, Is.EqualTo(BatteryCharge.Unknown));
    }

    [Test]
    public async Task AnEmptyChainReportsUnknownRatherThanThrowing()
    {
        FirstKnownBatteryService chain = new([]);

        Assert.That(await chain.ReadChargeAsync(AnyContainer, CancellationToken.None), Is.EqualTo(BatteryCharge.Unknown));
    }
}
