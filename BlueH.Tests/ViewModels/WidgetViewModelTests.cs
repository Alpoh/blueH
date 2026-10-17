using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class WidgetViewModelTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const uint WearableHeadsetMinorClass = 1;
    private const uint LoudspeakerMinorClass = 5;

    private FakeBluetoothDeviceService _deviceService = null!;
    private FakeBatteryService _batteryService = null!;
    private FakeMediaTransportService _mediaService = null!;
    private FakeBluetoothConnectionService _connectionService = null!;
    private FakeSettingsStore _settingsStore = null!;
    private FakeNoiseControlService _noiseControlService = null!;
    private FakeNoiseControlSession _earbudsSession = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private FakeTimeProvider _timeProvider = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private DevicesViewModel _devices = null!;
    private SelectedDeviceViewModel _selectedDevice = null!;
    private MediaTransportViewModel _media = null!;
    private SettingsViewModel _settings = null!;
    private FakeAudioEndpointService _endpointService = null!;
    private DisplayModeViewModel _displayMode = null!;

    [SetUp]
    public void SetUp()
    {
        _deviceService = new FakeBluetoothDeviceService();
        _batteryService = new FakeBatteryService();
        _mediaService = new FakeMediaTransportService();
        _connectionService = new FakeBluetoothConnectionService();
        _settingsStore = new FakeSettingsStore();
        _noiseControlService = new FakeNoiseControlService();
        _earbudsSession = new FakeNoiseControlSession { ModeToReport = NoiseControlMode.NoiseCancelling };
        _noiseControlService.Sessions[EarbudsContainer] = _earbudsSession;
        _uiScheduler = new RecordingUiScheduler();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
        _devices = new DevicesViewModel(_deviceService, _batteryService, _uiScheduler, _diagnosticsLog, _timeProvider);
        _selectedDevice = new SelectedDeviceViewModel(
            _connectionService,
            new FakeBluetoothPairingService(),
            _diagnosticsLog,
            _timeProvider);
        _media = new MediaTransportViewModel(_mediaService, _uiScheduler, _diagnosticsLog);
        _settings = new SettingsViewModel(
            _settingsStore,
            _diagnosticsLog,
            new FakeApplicationInfo(),
            new FakeExternalLinkOpener(),
            activeLanguageTag: null);
        _endpointService = new FakeAudioEndpointService();
        _displayMode = new DisplayModeViewModel();
    }

    [TearDown]
    public async Task TearDown()
    {
        _devices.Dispose();
        _media.Dispose();
        _mediaService.Dispose();
        await _earbudsSession.DisposeAsync();
    }

    [Test]
    public async Task ItFollowsTheFirstConnectedDevice()
    {
        _deviceService.Endpoints.Add(Speaker(isConnected: false));
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(widget.Device?.ContainerId, Is.EqualTo(EarbudsContainer));
            Assert.That(widget.HasDevice, Is.True);
        });
    }

    [Test]
    public async Task WithNothingConnectedItSaysSo()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: false));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(widget.Device, Is.Null);
            Assert.That(widget.HasDevice, Is.False);
            Assert.That(widget.NoDeviceMessage, Is.Not.Empty);
        });
    }

    [Test]
    public async Task ItFollowsADeviceThatConnectsLater()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: false));
        using WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);
        _deviceService.Endpoints.Clear();
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));

        await _devices.RefreshAsync(CancellationToken.None);

        Assert.That(widget.Device?.ContainerId, Is.EqualTo(EarbudsContainer));
    }

    [Test]
    public async Task ItShowsTheNoiseControlOfTheFollowedDevice()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);

        Assert.That(widget.NoiseControl.Mode, Is.EqualTo(NoiseControlMode.NoiseCancelling));
    }

    [Test]
    public async Task TheWidgetAndTheDevicePageShareOneConnection()
    {
        SharedNoiseControlService shared = new(_noiseControlService);
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget(shared);
        using NoiseControlViewModel page = new(shared, _settingsStore, _uiScheduler, _diagnosticsLog);
        await _devices.RefreshAsync(CancellationToken.None);

        page.Device = widget.Device;

        Assert.That(_noiseControlService.OpenCalls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DisconnectingTheFollowedDeviceFromItsPageClosesTheWidgetsChannel()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);
        _selectedDevice.Device = widget.Device;

        _selectedDevice.DisconnectCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(widget.NoiseControl.IsSuspended, Is.True);
            Assert.That(_earbudsSession.IsDisposed, Is.True);
        });
    }

    [Test]
    public async Task DisconnectingAnotherDeviceLeavesTheWidgetAlone()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        _deviceService.Endpoints.Add(Speaker(isConnected: true));
        using WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);
        _selectedDevice.Device = _devices.Devices.Single(device => device.ContainerId == SpeakerContainer);

        _selectedDevice.DisconnectCommand.Execute(null);

        Assert.That(widget.NoiseControl.IsSuspended, Is.False);
    }

    [Test]
    public void TheWidgetSwitchesModeAndExitsThroughTheSharedDisplayMode()
    {
        using WidgetViewModel widget = CreateWidget();

        Assert.That(widget.DisplayMode, Is.SameAs(_displayMode));
    }

    [Test]
    public async Task ItShowsTheVolumeOfTheFollowedDevice()
    {
        _endpointService.Endpoints.Add(Output("speaker", SpeakerContainer, isDefaultForPlayback: true));
        _endpointService.Endpoints.Add(Output("earbuds", EarbudsContainer, isDefaultForPlayback: false));
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(widget.Volume?.MmDeviceId, Is.EqualTo("earbuds"));
            Assert.That(widget.HasVolume, Is.True);
        });
    }

    [Test]
    public async Task ItPrefersTheDevicesDefaultPlaybackOutput()
    {
        _endpointService.Endpoints.Add(Output("earbuds-hands-free", EarbudsContainer, isDefaultForPlayback: false));
        _endpointService.Endpoints.Add(Output("earbuds-stereo", EarbudsContainer, isDefaultForPlayback: true));
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(widget.Volume?.MmDeviceId, Is.EqualTo("earbuds-stereo"));
    }

    [Test]
    public async Task WithoutADeviceThereIsNoVolume()
    {
        _endpointService.Endpoints.Add(Output("speaker", SpeakerContainer, isDefaultForPlayback: true));
        _deviceService.Endpoints.Add(Earbuds(isConnected: false));
        using WidgetViewModel widget = CreateWidget();

        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(widget.Volume, Is.Null);
            Assert.That(widget.HasVolume, Is.False);
        });
    }

    [Test]
    public async Task TheVolumeFollowsADeviceThatConnectsLater()
    {
        _endpointService.Endpoints.Add(Output("earbuds", EarbudsContainer, isDefaultForPlayback: true));
        _deviceService.Endpoints.Add(Earbuds(isConnected: false));
        using WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();
        List<string?> changed = [];
        widget.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _deviceService.Endpoints.Clear();
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));

        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(widget.Volume?.MmDeviceId, Is.EqualTo("earbuds"));
            Assert.That(changed, Does.Contain(nameof(WidgetViewModel.Volume)));
            Assert.That(changed, Does.Contain(nameof(WidgetViewModel.HasVolume)));
        });
    }

    [Test]
    public async Task TheVolumeGoesAwayWhenTheDeviceDisconnects()
    {
        _endpointService.Endpoints.Add(Output("earbuds", EarbudsContainer, isDefaultForPlayback: true));
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        using WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();
        _deviceService.Endpoints.Clear();
        _deviceService.Endpoints.Add(Earbuds(isConnected: false));

        await _devices.RefreshAsync(CancellationToken.None);
        _uiScheduler.RunPendingWork();

        Assert.That(widget.Volume, Is.Null);
    }

    [Test]
    public void WhereTheWidgetIsDroppedIsRemembered()
    {
        using WidgetViewModel widget = CreateWidget();

        widget.RememberPosition(new WidgetPosition(900, 60));

        Assert.That(_settingsStore.Settings.WidgetPosition, Is.EqualTo(new WidgetPosition(900, 60)));
    }

    [Test]
    public void AlwaysOnTopFollowsTheSetting()
    {
        using WidgetViewModel widget = CreateWidget();
        List<string?> changed = [];
        widget.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _settings.IsWidgetTopmost = false;

        Assert.Multiple(() =>
        {
            Assert.That(widget.IsTopmost, Is.False);
            Assert.That(changed, Does.Contain(nameof(WidgetViewModel.IsTopmost)));
        });
    }

    [Test]
    public async Task DisposingClosesTheWidgetsChannel()
    {
        _deviceService.Endpoints.Add(Earbuds(isConnected: true));
        WidgetViewModel widget = CreateWidget();
        await _devices.RefreshAsync(CancellationToken.None);

        widget.Dispose();

        Assert.That(_earbudsSession.IsDisposed, Is.True);
    }

    private WidgetViewModel CreateWidget(INoiseControlService? noiseControlService = null)
    {
        return new WidgetViewModel(
            _devices,
            _selectedDevice,
            _media,
            new NoiseControlViewModel(noiseControlService ?? _noiseControlService, _settingsStore, _uiScheduler, _diagnosticsLog),
            new EndpointsViewModel(_endpointService, _uiScheduler, _diagnosticsLog),
            _displayMode,
            _settings);
    }

    private static AudioEndpoint Output(string mmDeviceId, Guid containerId, bool isDefaultForPlayback)
    {
        return new AudioEndpoint(mmDeviceId, containerId, mmDeviceId, 40, false, isDefaultForPlayback, false);
    }

    private static BluetoothAudioEndpoint Earbuds(bool? isConnected)
    {
        return new BluetoothAudioEndpoint(
            "Bluetooth#Bluetooth-18:5c:a1:1d:b0:ff",
            EarbudsContainer,
            "OPPO Enco Air5",
            isConnected,
            WearableHeadsetMinorClass);
    }

    private static BluetoothAudioEndpoint Speaker(bool? isConnected)
    {
        return new BluetoothAudioEndpoint(
            "Bluetooth#Bluetooth-78:5e:a2:fa:71:91",
            SpeakerContainer,
            "SRS-XE300",
            isConnected,
            LoudspeakerMinorClass);
    }
}
