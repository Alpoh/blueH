using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class DevicesViewModelTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const string EarbudsName = "OPPO Enco Air5";
    private const string SpeakerName = "SRS-XE300";
    private const string ClassicEndpointId = "Bluetooth#Bluetooth-18:5c:a1:1d:b0:ff";
    private const string LowEnergyEndpointId = "BluetoothLE#BluetoothLE-18:5c:a1:1d:b0:ff";
    private const uint WearableHeadsetMinorClass = 1;
    private const uint LoudspeakerMinorClass = 5;

    private FakeBluetoothDeviceService _deviceService = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeSettingsStore _settingsStore = null!;

    [SetUp]
    public void SetUp()
    {
        _deviceService = new FakeBluetoothDeviceService();
        _uiScheduler = new RecordingUiScheduler();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
        _settingsStore = new FakeSettingsStore();
    }

    [Test]
    public async Task RefreshListsThePairedDevicesByName()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _deviceService.Endpoints.Add(Classic(SpeakerContainer, SpeakerName, LoudspeakerMinorClass, "speaker"));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.Devices.Select(device => device.Name), Is.EqualTo(new[] { EarbudsName, SpeakerName }));
    }

    [Test]
    public async Task RefreshShowsOneRowForADeviceReportedByBothItsEndpoints()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _deviceService.Endpoints.Add(LowEnergy(EarbudsContainer, EarbudsName));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.Devices, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RefreshTellsHeadphonesApartFromASpeaker()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _deviceService.Endpoints.Add(Classic(SpeakerContainer, SpeakerName, LoudspeakerMinorClass, "speaker"));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(
            viewModel.Devices.Select(device => device.Kind),
            Is.EqualTo(new[] { BluetoothAudioDeviceKind.Headphones, BluetoothAudioDeviceKind.Loudspeaker }));
    }

    [Test]
    public async Task RefreshRecordsTheDeviceCountInDiagnostics()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(_diagnosticsLog.Entries[0].Detail, Is.EqualTo("1"));
    }

    [Test]
    public async Task AnEmptyResultIsReportedAsHavingNoDevices()
    {
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.HasNoDevices, Is.True);
    }

    [Test]
    public async Task AFailedLookupIsSurfacedRatherThanSwallowed()
    {
        _deviceService.FailureToReturn = new InvalidOperationException("the radio is off");
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.ErrorDetail, Is.EqualTo("the radio is off"));
    }

    [Test]
    public async Task AFailedLookupIsRecordedInDiagnostics()
    {
        _deviceService.FailureToReturn = new InvalidOperationException("the radio is off");
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(_diagnosticsLog.Entries[0].Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public async Task AFailedLookupIsNotMistakenForAnEmptyList()
    {
        _deviceService.FailureToReturn = new InvalidOperationException("the radio is off");
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.HasNoDevices, Is.False);
    }

    [Test]
    public async Task ASuccessfulRefreshClearsAnEarlierError()
    {
        _deviceService.FailureToReturn = new InvalidOperationException("the radio is off");
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);

        _deviceService.FailureToReturn = null;
        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.HasError, Is.False);
    }

    [Test]
    public async Task CancellationIsNotTreatedAsAFailure()
    {
        _deviceService.FailureToReturn = new OperationCanceledException();
        using DevicesViewModel viewModel = CreateViewModel();

        Assert.That(
            async () => await viewModel.RefreshAsync(CancellationToken.None),
            Throws.InstanceOf<OperationCanceledException>());

        await Task.CompletedTask;
    }

    [Test]
    public void StartWatchingStartsTheWatcher()
    {
        using DevicesViewModel viewModel = CreateViewModel();

        viewModel.StartWatching();

        Assert.That(_deviceService.Watcher.IsStarted, Is.True);
    }

    [Test]
    public void StartWatchingTwiceDoesNotCreateASecondWatcher()
    {
        using DevicesViewModel viewModel = CreateViewModel();

        viewModel.StartWatching();
        viewModel.StartWatching();

        Assert.That(_deviceService.WatchersCreated, Is.EqualTo(1));
    }

    [Test]
    public void AnAddedEndpointAppearsInTheList()
    {
        using DevicesViewModel viewModel = CreateViewModel();
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseAdded(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices.Select(device => device.Name), Is.EqualTo(new[] { EarbudsName }));
    }

    [Test]
    public void WatcherEventsNeverTouchTheCollectionOffTheUiThread()
    {
        using DevicesViewModel viewModel = CreateViewModel();
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseAdded(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));

        Assert.That(viewModel.Devices, Is.Empty);
    }

    [Test]
    public async Task ARemovedEndpointLeavesTheList()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseRemoved(ClassicEndpointId);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Is.Empty);
    }

    [Test]
    public async Task RemovingOneOfTwoEndpointsKeepsTheDeviceListed()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _deviceService.Endpoints.Add(LowEnergy(EarbudsContainer, EarbudsName));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseRemoved(LowEnergyEndpointId);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RemovingAnEndpointThatWasNeverListedChangesNothing()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseRemoved("some-endpoint-nobody-knows");
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AnUpdatedEndpointChangesTheExistingRowRatherThanAddingOne()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass, isConnected: false));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseUpdated(
            Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass, isConnected: true));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AnUpdatedEndpointIsReflectedInTheRow()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass, isConnected: false));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseUpdated(
            Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass, isConnected: true));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices[0].IsConnected, Is.True);
    }

    [Test]
    public void AnUpdateForAnEndpointNobodyListedIsIgnored()
    {
        using DevicesViewModel viewModel = CreateViewModel();
        viewModel.StartWatching();

        _deviceService.Watcher.RaiseUpdated(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Is.Empty);
    }

    [Test]
    public void DisposingStopsAndReleasesTheWatcher()
    {
        DevicesViewModel viewModel = CreateViewModel();
        viewModel.StartWatching();

        viewModel.Dispose();

        Assert.That(
            new[] { _deviceService.Watcher.IsStopped, _deviceService.Watcher.IsDisposed },
            Has.All.True);
    }

    [Test]
    public void DisposingStopsTheViewModelListening()
    {
        DevicesViewModel viewModel = CreateViewModel();
        viewModel.StartWatching();
        viewModel.Dispose();

        _deviceService.Watcher.RaiseAdded(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.Devices, Is.Empty);
    }

    [Test]
    public void TheSelectedDeviceIsReadFromSettingsOnCreation()
    {
        _settingsStore.Settings = new AppSettings(EarbudsContainer.ToString());

        using DevicesViewModel viewModel = CreateViewModel();

        Assert.That(viewModel.SelectedContainerId, Is.EqualTo(EarbudsContainer));
    }

    [Test]
    public void AMeaninglessPersistedSelectionIsIgnored()
    {
        _settingsStore.Settings = new AppSettings("not a guid");

        using DevicesViewModel viewModel = CreateViewModel();

        Assert.That(viewModel.SelectedContainerId, Is.Null);
    }

    [Test]
    public void SelectingADeviceSavesIt()
    {
        using DevicesViewModel viewModel = CreateViewModel();

        viewModel.SelectedContainerId = EarbudsContainer;

        Assert.That(_settingsStore.Settings.SelectedDeviceId, Is.EqualTo(EarbudsContainer.ToString()));
    }

    [Test]
    public void SelectingTheSameDeviceTwiceSavesOnce()
    {
        using DevicesViewModel viewModel = CreateViewModel();

        viewModel.SelectedContainerId = EarbudsContainer;
        viewModel.SelectedContainerId = EarbudsContainer;

        Assert.That(_settingsStore.SaveCount, Is.EqualTo(1));
    }

    [Test]
    public void AFailureToSaveTheSelectionIsLoggedRatherThanThrown()
    {
        _settingsStore.SaveFailureToThrow = new IOException("the disk is full");
        using DevicesViewModel viewModel = CreateViewModel();

        viewModel.SelectedContainerId = EarbudsContainer;

        Assert.That(_diagnosticsLog.Entries[0].Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
    }

    [Test]
    public async Task TheSelectionSurvivesARefresh()
    {
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();
        await viewModel.RefreshAsync(CancellationToken.None);
        viewModel.SelectedContainerId = EarbudsContainer;

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.SelectedDevice?.Name, Is.EqualTo(EarbudsName));
    }

    [Test]
    public async Task ASelectionPointingAtADeviceThatIsGoneResolvesToNothing()
    {
        _settingsStore.Settings = new AppSettings(SpeakerContainer.ToString());
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.SelectedDevice, Is.Null);
    }

    [Test]
    public async Task ASelectionPointingAtADeviceThatIsGoneIsNotErased()
    {
        _settingsStore.Settings = new AppSettings(SpeakerContainer.ToString());
        _deviceService.Endpoints.Add(Classic(EarbudsContainer, EarbudsName, WearableHeadsetMinorClass));
        using DevicesViewModel viewModel = CreateViewModel();

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.That(viewModel.SelectedContainerId, Is.EqualTo(SpeakerContainer));
    }

    private DevicesViewModel CreateViewModel()
    {
        return new DevicesViewModel(_deviceService, _uiScheduler, _diagnosticsLog, _settingsStore);
    }

    private static BluetoothAudioEndpoint Classic(
        Guid containerId,
        string name,
        uint minorClass,
        string? id = null,
        bool? isConnected = true)
    {
        return new BluetoothAudioEndpoint(id ?? ClassicEndpointId, containerId, name, isConnected, minorClass);
    }

    private static BluetoothAudioEndpoint LowEnergy(Guid containerId, string name)
    {
        return new BluetoothAudioEndpoint(LowEnergyEndpointId, containerId, name, IsConnected: false, MinorClass: null);
    }
}
