using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using BlueH.ViewModels.Pages;
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
    private FakeBluetoothConnectionService _connectionService = null!;
    private FakeBluetoothPairingService _pairingService = null!;
    private FakeBatteryService _batteryService = null!;
    private FakeSettingsStore _settingsStore = null!;
    private FakeNoiseControlService _noiseControlService = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _deviceService = new FakeBluetoothDeviceService();
        _endpointService = new FakeAudioEndpointService();
        _mediaService = new FakeMediaTransportService();
        _connectionService = new FakeBluetoothConnectionService();
        _pairingService = new FakeBluetoothPairingService();
        _batteryService = new FakeBatteryService();
        _settingsStore = new FakeSettingsStore();
        _noiseControlService = new FakeNoiseControlService();
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
    public void TheFullWindowOffersTheSharedDisplayMode()
    {
        DisplayModeViewModel displayMode = new();
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel(), displayMode);

        Assert.That(viewModel.DisplayMode, Is.SameAs(displayMode));
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
            Assert.That(viewModel.DeviceOutputs.ContainerFilter, Is.EqualTo(EarbudsContainer));
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

    [Test]
    public async Task ConnectingFromACardOpensTheDeviceAndStartsConnecting()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: false));
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.ConnectDeviceCommand.Execute(EarbudsContainer);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicePage));
            Assert.That(_connectionService.ConnectCalls, Is.EqualTo(new[] { EarbudsContainer }));
            Assert.That(viewModel.SelectedDevice.IsBusy, Is.True);
        });
    }

    [Test]
    public async Task ConnectingFromACardOfAConnectedDeviceOnlyOpensIt()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.ConnectDeviceCommand.Execute(EarbudsContainer);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicePage));
            Assert.That(_connectionService.ConnectCalls, Is.Empty);
        });
    }

    [Test]
    public void AddingADeviceOpensItsPageAndStartsSearching()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        viewModel.DevicesPage.AddDeviceCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.AddDevicePage));
            Assert.That(viewModel.Navigator.CanGoBack, Is.True);
            Assert.That(viewModel.Pairing.IsSearching, Is.True);
        });
    }

    [Test]
    public void GoingBackFromAddingADeviceStopsTheSearch()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.DevicesPage.AddDeviceCommand.Execute(null);
        FakeDiscoveryWatcher watcher = _pairingService.Watcher;

        viewModel.Navigator.GoBack();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Pairing.IsSearching, Is.False);
            Assert.That(watcher.IsStopped, Is.True);
        });
    }

    [Test]
    public void ChoosingAnotherSectionFromAddingADeviceStopsTheSearch()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.DevicesPage.AddDeviceCommand.Execute(null);

        viewModel.SelectedMenuItem = viewModel.MenuItems.Single(item => item.Page == viewModel.SettingsPage);

        Assert.That(viewModel.Pairing.IsSearching, Is.False);
    }

    [Test]
    public void OpeningADeviceLeavesAnUnrelatedSearchAlone()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        viewModel.Pairing.SearchCommand.Execute(null);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.That(viewModel.Pairing.IsSearching, Is.True);
    }

    [Test]
    public async Task ASuccessfulPairingReturnsToTheDeviceList()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.DevicesPage.AddDeviceCommand.Execute(null);
        DiscoveredDevice speaker = new("Bluetooth#Bluetoothac:f2:3c:e1:15:94-78:5e:a2:fa:71:91", "SRS-XE300");
        _pairingService.Watcher.RaiseFound(speaker);
        _uiScheduler.RunPendingWork();

        await ((AsyncRelayCommand)viewModel.Pairing.PairCommand).ExecuteAsync(speaker).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicesPage));
            Assert.That(viewModel.Pairing.IsSearching, Is.False);
        });
    }

    [Test]
    public void DiagnosticsAreHiddenOutsideDeveloperMode()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        Assert.That(viewModel.MenuItems.Select(item => item.Page), Does.Not.Contain(viewModel.DiagnosticsPage));
    }

    [Test]
    public void DiagnosticsAppearWhenDeveloperModeIsStored()
    {
        _settingsStore.Settings = new AppSettings(IsDeveloperModeEnabled: true);

        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        Assert.That(viewModel.MenuItems.Select(item => item.Page), Does.Contain(viewModel.DiagnosticsPage));
    }

    [Test]
    public void SwitchingDeveloperModeOnAndOffShowsAndHidesDiagnostics()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        viewModel.Settings.IsDeveloperModeEnabled = true;
        bool shownWhenOn = viewModel.MenuItems.Any(item => item.Page == viewModel.DiagnosticsPage);
        viewModel.Settings.IsDeveloperModeEnabled = false;

        Assert.Multiple(() =>
        {
            Assert.That(shownWhenOn, Is.True);
            Assert.That(viewModel.MenuItems.Select(item => item.Page), Does.Not.Contain(viewModel.DiagnosticsPage));
        });
    }

    [Test]
    public void SwitchingDeveloperModeOffOnTheDiagnosticsPageMovesToSettings()
    {
        _settingsStore.Settings = new AppSettings(IsDeveloperModeEnabled: true);
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.SelectedMenuItem = viewModel.MenuItems.Single(item => item.Page == viewModel.DiagnosticsPage);

        viewModel.Settings.IsDeveloperModeEnabled = false;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.SettingsPage));
            Assert.That(viewModel.SelectedMenuItem?.Page, Is.SameAs(viewModel.SettingsPage));
        });
    }

    [Test]
    public void TheAppOpensOnTheDevicesPage()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicesPage));
            Assert.That(viewModel.SelectedMenuItem?.Page, Is.SameAs(viewModel.DevicesPage));
            Assert.That(viewModel.Navigator.CanGoBack, Is.False);
        });
    }

    [Test]
    public void SelectingADeviceOpensItsPage()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicePage));
            Assert.That(viewModel.Navigator.CanGoBack, Is.True);
            Assert.That(viewModel.SelectedMenuItem?.Page, Is.SameAs(viewModel.DevicesPage));
        });
    }

    [Test]
    public async Task OpeningAConnectedDeviceOpensItsNoiseControl()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        _noiseControlService.Sessions[EarbudsContainer] = new FakeNoiseControlSession();
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.Multiple(() =>
        {
            Assert.That(_noiseControlService.OpenCalls, Is.EqualTo(new[] { EarbudsContainer }));
            Assert.That(viewModel.DevicePage.NoiseControl.IsReady, Is.True);
        });
    }

    [Test]
    public async Task LeavingTheDevicePageClosesItsNoiseControl()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        await using FakeNoiseControlSession session = new();
        _noiseControlService.Sessions[EarbudsContainer] = session;
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;

        viewModel.Navigator.GoBackCommand.Execute(null);

        Assert.That(session.IsDisposed, Is.True);
    }

    [Test]
    public async Task DisconnectingClosesTheNoiseControlFirstBecauseItsChannelKeepsTheLinkUp()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        await using FakeNoiseControlSession session = new();
        _noiseControlService.Sessions[EarbudsContainer] = session;
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;

        viewModel.SelectedDevice.DisconnectCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(session.IsDisposed, Is.True);
            Assert.That(_connectionService.DisconnectCalls, Is.EqualTo(new[] { EarbudsContainer }));
        });
    }

    [Test]
    public async Task ACancelledDisconnectReopensTheNoiseControl()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        _noiseControlService.Sessions[EarbudsContainer] = new FakeNoiseControlSession();
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;
        viewModel.SelectedDevice.DisconnectCommand.Execute(null);

        await ((AsyncRelayCommand)viewModel.SelectedDevice.CancelCommand).ExecuteAsync(null);
        await Task.Yield();

        Assert.Multiple(() =>
        {
            Assert.That(_noiseControlService.OpenCalls, Has.Count.EqualTo(2));
            Assert.That(viewModel.DevicePage.NoiseControl.IsReady, Is.True);
        });
    }

    [Test]
    public async Task UnpairingClosesTheNoiseControlFirst()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        await using FakeNoiseControlSession session = new();
        _noiseControlService.Sessions[EarbudsContainer] = session;
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;
        viewModel.SelectedDevice.UnpairCommand.Execute(null);

        viewModel.SelectedDevice.ConfirmUnpairCommand.Execute(null);

        Assert.That(session.IsDisposed, Is.True);
    }

    [Test]
    public async Task DisposingClosesTheOpenNoiseControl()
    {
        _deviceService.Endpoints.Add(Classic(isConnected: true));
        await using FakeNoiseControlSession session = new();
        _noiseControlService.Sessions[EarbudsContainer] = session;
        DevicesViewModel devices = CreateDevicesViewModel();
        MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);
        devices.SelectedContainerId = EarbudsContainer;

        viewModel.Dispose();

        Assert.That(session.IsDisposed, Is.True);
    }

    [Test]
    public void DisposingStopsWatchingTheAudioPageOutputs()
    {
        MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.AllOutputs.StartWatching();

        viewModel.Dispose();

        Assert.That(_endpointService.ChangeWatcher.IsDisposed, Is.True);
    }

    [Test]
    public void DisposingStopsWatchingTheDevicePageOutputs()
    {
        MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());
        viewModel.DeviceOutputs.StartWatching();

        viewModel.Dispose();

        Assert.That(_endpointService.ChangeWatcher.IsDisposed, Is.True);
    }

    [Test]
    public void GoingBackReturnsToTheListAndClearsTheSelection()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        devices.SelectedContainerId = EarbudsContainer;

        viewModel.Navigator.GoBackCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicesPage));
            Assert.That(devices.SelectedContainerId, Is.Null);
        });
    }

    [Test]
    public void TheSameDeviceCanBeOpenedAgainAfterGoingBack()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        devices.SelectedContainerId = EarbudsContainer;
        viewModel.Navigator.GoBack();

        devices.SelectedContainerId = EarbudsContainer;

        Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.DevicePage));
    }

    [Test]
    public void ChoosingAMenuSectionShowsItsPageWithNoWayBack()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        devices.SelectedContainerId = EarbudsContainer;

        viewModel.SelectedMenuItem = viewModel.MenuItems.Single(item => item.Page == viewModel.AudioPage);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Navigator.CurrentPage, Is.SameAs(viewModel.AudioPage));
            Assert.That(viewModel.Navigator.CanGoBack, Is.False);
            Assert.That(devices.SelectedContainerId, Is.Null);
        });
    }

    [Test]
    public void EveryMenuSectionHasALabelAGlyphAndItsOwnPage()
    {
        using MainViewModel viewModel = CreateViewModel(CreateDevicesViewModel());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.MenuItems.Select(item => item.Label), Is.All.Not.Empty);
            Assert.That(viewModel.MenuItems.Select(item => item.Glyph), Is.All.Not.Empty);
            Assert.That(viewModel.MenuItems.Select(item => item.Page), Is.Unique);
            Assert.That(viewModel.MenuItems.Select(item => item.Page), Does.Not.Contain(viewModel.DevicePage));
        });
    }

    [Test]
    public async Task TheDevicePageIsTitledWithTheDeviceName()
    {
        _deviceService.Endpoints.Add(Classic());
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.That(viewModel.Navigator.CurrentPage.Title, Is.EqualTo(EarbudsName));
    }

    [Test]
    public async Task TheAudioPageListsEveryOutputWhileADeviceIsOpen()
    {
        DevicesViewModel devices = CreateDevicesViewModel();
        using MainViewModel viewModel = CreateViewModel(devices);
        await devices.RefreshAsync(CancellationToken.None);

        devices.SelectedContainerId = EarbudsContainer;

        Assert.That(viewModel.AllOutputs.ContainerFilter, Is.Null);
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
            _timeProvider);
    }

    private MainViewModel CreateViewModel(DevicesViewModel devices, DisplayModeViewModel? displayMode = null)
    {
        return new MainViewModel(
            devices,
            new SelectedDeviceViewModel(
                _connectionService,
                new FakeBluetoothPairingService(),
                _diagnosticsLog,
                _timeProvider),
            new SettingsViewModel(
                _settingsStore,
                _diagnosticsLog,
                new FakeApplicationInfo(),
                new FakeExternalLinkOpener(),
                activeLanguageTag: null),
            new PairingViewModel(_pairingService, _uiScheduler, _diagnosticsLog, _timeProvider),
            new EndpointsViewModel(_endpointService, _uiScheduler, _diagnosticsLog),
            new EndpointsViewModel(_endpointService, _uiScheduler, _diagnosticsLog),
            new MediaTransportViewModel(_mediaService, _uiScheduler, _diagnosticsLog),
            new NoiseControlViewModel(_noiseControlService, _settingsStore, _uiScheduler, _diagnosticsLog),
            new DiagnosticsViewModel(_diagnosticsLog, _uiScheduler),
            displayMode ?? new DisplayModeViewModel());
    }
}
