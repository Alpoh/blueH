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
    public void AddingADeviceOpensThePairingPanelAndSearches()
    {
        FakeTimeProvider timeProvider = new();
        InMemoryDiagnosticsLog diagnosticsLog = new(timeProvider);
        RecordingUiScheduler uiScheduler = new();
        FakeBluetoothPairingService pairingService = new();
        using PairingViewModel pairing = new(pairingService, uiScheduler, diagnosticsLog, timeProvider);
        using DevicesViewModel devices = new(
            new FakeBluetoothDeviceService(),
            new FakeBatteryService(),
            uiScheduler,
            diagnosticsLog,
            timeProvider);
        DevicesPageViewModel page = new(devices, pairing);

        page.AddDeviceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(pairing.IsOpen, Is.True);
            Assert.That(pairing.IsSearching, Is.True);
            Assert.That(page.AddDeviceLabel, Is.Not.Empty);
        });
    }
}
