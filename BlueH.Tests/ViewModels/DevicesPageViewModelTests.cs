using BlueH.Diagnostics;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using BlueH.ViewModels.Pages;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class DevicesPageViewModelTests
{
    [Test]
    public void AddingADeviceAsksForTheAddDevicePage()
    {
        FakeTimeProvider timeProvider = new();
        using DevicesViewModel devices = new(
            new FakeBluetoothDeviceService(),
            new FakeBatteryService(),
            new RecordingUiScheduler(),
            new InMemoryDiagnosticsLog(timeProvider),
            timeProvider);
        DevicesPageViewModel page = new(devices);
        int requests = 0;
        page.AddDeviceRequested += (_, _) => requests++;

        page.AddDeviceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.EqualTo(1));
            Assert.That(page.AddDeviceLabel, Is.Not.Empty);
        });
    }
}
