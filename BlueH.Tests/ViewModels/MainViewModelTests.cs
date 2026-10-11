using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class MainViewModelTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");

    private const string EarbudsName = "OPPO Enco Air5";
    private const string ClassicEndpointId = "Bluetooth#Bluetooth-18:5c:a1:1d:b0:ff";
    private const uint WearableHeadsetMinorClass = 1;

    private FakeBluetoothDeviceService _deviceService = null!;
    private FakeAudioEndpointService _endpointService = null!;
    private FakeMediaTransportService _mediaService = null!;
    private FakeBatteryService _batteryService = null!;
    private FakeSettingsStore _settingsStore = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _deviceService = new FakeBluetoothDeviceService();
        _endpointService = new FakeAudioEndpointService();
        _mediaService = new FakeMediaTransportService();
        _batteryService = new FakeBatteryService();
        _settingsStore = new FakeSettingsStore();
        _uiScheduler = new RecordingUiScheduler();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _mediaService.Dispose();
    }

    [Test]
    public async Task ADeviceBecomingConnectedRefreshesTheAudioOutputs()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: false));
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.StartWatching();
        int refreshesBefore = _endpointService.FindCalls;

        _deviceService.Watcher.RaiseUpdated(Classic(isConnected: true));
        _uiScheduler.RunPendingWork();

        Assert.That(_endpointService.FindCalls, Is.GreaterThan(refreshesBefore));
    }

    [Test]
    public async Task ADeviceBecomingDisconnectedRefreshesTheAudioOutputs()
    {
        _deviceService.Endpoints.Add(Classic());
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.StartWatching();
        int refreshesBefore = _endpointService.FindCalls;

        _deviceService.Watcher.RaiseUpdated(Classic(isConnected: false));
        _uiScheduler.RunPendingWork();

        Assert.That(_endpointService.FindCalls, Is.GreaterThan(refreshesBefore));
    }

    [Test]
    public void SelectingADeviceRefreshesTheAudioOutputs()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        int refreshesBefore = _endpointService.FindCalls;

        devices.SelectedContainerId = EarbudsContainer;

        Assert.Multiple(() =>
        {
            Assert.That(_endpointService.FindCalls, Is.GreaterThan(refreshesBefore));
            Assert.That(viewModel.Endpoints.ContainerFilter, Is.EqualTo(EarbudsContainer));
        });
    }

    [Test]
    public async Task ARenameLeavesTheAudioOutputsAlone()
    {
        _deviceService.Endpoints.Add(Classic());
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.StartWatching();
        int refreshesBefore = _endpointService.FindCalls;

        _deviceService.Watcher.RaiseUpdated(Classic(name: "OPPO Enco Air5 renamed"));
        _uiScheduler.RunPendingWork();

        Assert.That(_endpointService.FindCalls, Is.EqualTo(refreshesBefore));
    }

    [Test]
    public async Task SelectingADeviceHandsItToTheDevicePanel()
    {
        _deviceService.Endpoints.Add(Classic());
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.That(viewModel.SelectedDevice.Device?.ContainerId, Is.EqualTo(EarbudsContainer));
    }

    [Test]
    public async Task TheDevicePanelFollowsLiveConnectionChanges()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: false));
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;
        devices.StartWatching();

        _deviceService.Watcher.RaiseUpdated(Classic(isConnected: true));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.SelectedDevice.Device?.IsConnected, Is.True);
    }

    private static BluetoothAudioEndpoint Classic(bool? isConnected = true, string name = EarbudsName)
    {
        return new BluetoothAudioEndpoint(
            ClassicEndpointId,
            EarbudsContainer,
            name,
            isConnected,
            WearableHeadsetMinorClass);
    }

    private DevicesViewModel CreateDevicesViewModel()
    {
        return new DevicesViewModel(
            _deviceService,
            _batteryService,
            _uiScheduler,
            _diagnosticsLog,
            _settingsStore,
            _timeProvider);
    }

    private MainViewModel CreateViewModel(DevicesViewModel devices)
    {
        return new MainViewModel(
            devices,
            new SelectedDeviceViewModel(
                new FakeBluetoothConnectionService(),
                new FakeBluetoothPairingService(),
                _diagnosticsLog,
                _timeProvider),
            new SettingsViewModel(_settingsStore, _diagnosticsLog, activeLanguageTag: null),
            new PairingViewModel(new FakeBluetoothPairingService(), _uiScheduler, _diagnosticsLog, _timeProvider),
            new EndpointsViewModel(_endpointService, _uiScheduler, _diagnosticsLog),
            new MediaTransportViewModel(_mediaService, _uiScheduler, _diagnosticsLog),
            new DiagnosticsViewModel(_diagnosticsLog, _uiScheduler));
    }
}
