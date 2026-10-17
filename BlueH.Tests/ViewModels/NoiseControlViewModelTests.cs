using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class NoiseControlViewModelTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid OtherEarbudsContainer = Guid.Parse("bc517db5-4dda-5729-93eb-d8285225d6cf");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const string DeviceFailure = "the remote device refused the connection";

    private FakeNoiseControlService _service = null!;
    private FakeSettingsStore _settingsStore = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeNoiseControlSession _earbudsSession = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new FakeNoiseControlService();
        _settingsStore = new FakeSettingsStore();
        _uiScheduler = new RecordingUiScheduler();
        _diagnosticsLog = new InMemoryDiagnosticsLog(new FakeTimeProvider());
        _earbudsSession = new FakeNoiseControlSession { ModeToReport = NoiseControlMode.Transparency };
        _service.Sessions[EarbudsContainer] = _earbudsSession;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _earbudsSession.DisposeAsync();
    }

    [Test]
    public void WithNoDeviceNothingIsOpenedOrShown()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Unavailable));
            Assert.That(viewModel.IsShown, Is.False);
            Assert.That(_service.OpenCalls, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(null)]
    public void ADeviceThatIsNotConnectedOpensNothing(bool? isConnected)
    {
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected);

        Assert.That(_service.OpenCalls, Is.Empty);
    }

    [Test]
    public void AConnectedDeviceShowsTheModeItReports()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Ready));
            Assert.That(viewModel.IsShown, Is.True);
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Transparency));
            Assert.That(SelectedOptions(viewModel), Is.EqualTo(new[] { NoiseControlMode.Transparency }));
        });
    }

    [Test]
    public void TheOptionsAreOffTransparencyAndNoiseCancellingInThatOrder()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();

        Assert.That(
            viewModel.Options.Select(option => option.Mode),
            Is.EqualTo(new[] { NoiseControlMode.Off, NoiseControlMode.Transparency, NoiseControlMode.NoiseCancelling }));
    }

    [Test]
    public void AnUnrecognisedReportedModeSelectsNothingAndSaysSo()
    {
        _earbudsSession.ModeToReport = null;
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsModeUnknown, Is.True);
            Assert.That(SelectedOptions(viewModel), Is.Empty);
        });
    }

    [Test]
    public void ADeviceWithoutNoiseControlIsUnsupportedAndHidden()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Speaker(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Unsupported));
            Assert.That(viewModel.IsShown, Is.False);
        });
    }

    [Test]
    public void ARefusedConnectionFailsVisiblyAndIsLogged()
    {
        _service.OpenFailureToThrow = new IOException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Failed));
            Assert.That(viewModel.IsShown, Is.True);
            Assert.That(viewModel.ErrorMessage, Does.Contain(DeviceFailure));
            Assert.That(viewModel.RetryCommand.CanExecute(null), Is.True);
            Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
        });
    }

    [Test]
    public void AFailedFirstReadClosesTheSession()
    {
        _earbudsSession.ReadFailureToThrow = new TimeoutException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Failed));
            Assert.That(_earbudsSession.IsDisposed, Is.True);
        });
    }

    [Test]
    public async Task RetryingAfterAFailureOpensAgain()
    {
        _service.OpenFailureToThrow = new IOException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);
        _service.OpenFailureToThrow = null;

        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_service.OpenCalls, Has.Count.EqualTo(2));
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Ready));
            Assert.That(viewModel.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public void ANewRecordForTheSameConnectedDeviceDoesNotReopen()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        viewModel.Device = Earbuds(isConnected: true) with { Battery = BatteryCharge.FromPercent(50) };

        Assert.That(_service.OpenCalls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectingAModeSendsItAndSelectsIt()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.NoiseCancelling).SelectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_earbudsSession.SetCalls, Is.EqualTo(new[] { NoiseControlMode.NoiseCancelling }));
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.NoiseCancelling));
            Assert.That(SelectedOptions(viewModel), Is.EqualTo(new[] { NoiseControlMode.NoiseCancelling }));
        });
    }

    [Test]
    public async Task SelectingTheCurrentModeSendsNothing()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.Transparency).SelectCommand.ExecuteAsync(null);

        Assert.That(_earbudsSession.SetCalls, Is.Empty);
    }

    [Test]
    public async Task AFailedChangeKeepsThePreviousModeAndReannouncesEverySelection()
    {
        _earbudsSession.SetFailureToThrow = new InvalidOperationException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);
        NoiseControlOptionViewModel noiseCancelling = Option(viewModel, NoiseControlMode.NoiseCancelling);
        List<string?> announced = [];
        noiseCancelling.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        await noiseCancelling.SelectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Transparency));
            Assert.That(noiseCancelling.IsSelected, Is.False);
            Assert.That(announced, Does.Contain(nameof(NoiseControlOptionViewModel.IsSelected)));
            Assert.That(viewModel.ErrorMessage, Does.Contain(DeviceFailure));
            Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
        });
    }

    [Test]
    public void WhileAChangeIsRunningNoOtherModeCanBeSelected()
    {
        _earbudsSession.PendingSet = new TaskCompletionSource();
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _ = Option(viewModel, NoiseControlMode.NoiseCancelling).SelectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsChangingMode, Is.True);
            Assert.That(viewModel.Options.Any(option => option.SelectCommand.CanExecute(null)), Is.False);
        });
    }

    [Test]
    public void NoModeCanBeSelectedBeforeTheSessionIsReady()
    {
        _service.OpenFailureToThrow = new IOException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.That(viewModel.Options.Any(option => option.SelectCommand.CanExecute(null)), Is.False);
    }

    [Test]
    public void AModeChangedOnTheEarbudsIsShownOnTheUiThread()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _earbudsSession.Report(NoiseControlMode.Off);

        Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Transparency));
        _uiScheduler.RunPendingWork();
        Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Off));
    }

    [Test]
    public void DisconnectingClosesTheSessionAndHidesTheControl()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        viewModel.Device = Earbuds(isConnected: false);

        Assert.Multiple(() =>
        {
            Assert.That(_earbudsSession.IsDisposed, Is.True);
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Unavailable));
            Assert.That(viewModel.Mode, Is.Null);
        });
    }

    [Test]
    public async Task ReconnectingOpensAFreshSession()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);
        viewModel.Device = Earbuds(isConnected: false);
        await using FakeNoiseControlSession fresh = new() { ModeToReport = NoiseControlMode.Off };
        _service.Sessions[EarbudsContainer] = fresh;

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(_service.OpenCalls, Has.Count.EqualTo(2));
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Off));
        });
    }

    [Test]
    public void SwitchingToAnotherDeviceClosesTheFirstSession()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        viewModel.Device = Speaker(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(_earbudsSession.IsDisposed, Is.True);
            Assert.That(_service.OpenCalls, Is.EqualTo(new[] { EarbudsContainer, SpeakerContainer }));
        });
    }

    [Test]
    public void ALostChannelFailsVisiblyWithoutReopeningByItself()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _earbudsSession.Close();
        _uiScheduler.RunPendingWork();
        viewModel.Device = Earbuds(isConnected: true) with { Battery = BatteryCharge.FromPercent(50) };

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Failed));
            Assert.That(viewModel.HasErrorMessage, Is.True);
            Assert.That(_earbudsSession.IsDisposed, Is.True);
            Assert.That(_service.OpenCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void EventsQueuedBeforeTheSessionClosedAreIgnored()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);
        _earbudsSession.Report(NoiseControlMode.Off);
        _earbudsSession.Close();

        viewModel.Device = Earbuds(isConnected: false);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Mode, Is.Null);
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Unavailable));
        });
    }

    [Test]
    public void SuspendingClosesTheSessionAndHidesTheControl()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        viewModel.IsSuspended = true;

        Assert.Multiple(() =>
        {
            Assert.That(_earbudsSession.IsDisposed, Is.True);
            Assert.That(viewModel.IsShown, Is.False);
        });
    }

    [Test]
    public void ASuspendedControlOpensNothingForAConnectedDevice()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.IsSuspended = true;

        viewModel.Device = Earbuds(isConnected: true);

        Assert.That(_service.OpenCalls, Is.Empty);
    }

    [Test]
    public async Task ResumingWhileStillConnectedOpensAFreshSession()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);
        viewModel.IsSuspended = true;
        await using FakeNoiseControlSession fresh = new() { ModeToReport = NoiseControlMode.Off };
        _service.Sessions[EarbudsContainer] = fresh;

        viewModel.IsSuspended = false;

        Assert.Multiple(() =>
        {
            Assert.That(_service.OpenCalls, Has.Count.EqualTo(2));
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Off));
        });
    }

    [Test]
    public void DisposingClosesTheSession()
    {
        NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        viewModel.Dispose();

        Assert.That(_earbudsSession.IsDisposed, Is.True);
    }

    [Test]
    public async Task ASelectedModeIsStoredForThatDevice()
    {
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.NoiseCancelling).SelectCommand.ExecuteAsync(null);

        Assert.That(StoredModes(), Is.EqualTo(new Dictionary<Guid, NoiseControlMode>
        {
            [EarbudsContainer] = NoiseControlMode.NoiseCancelling
        }));
    }

    [Test]
    public async Task AFailedChangeStoresNothing()
    {
        _earbudsSession.SetFailureToThrow = new InvalidOperationException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.NoiseCancelling).SelectCommand.ExecuteAsync(null);

        Assert.That(_settingsStore.SaveCount, Is.Zero);
    }

    [Test]
    public async Task StoringAModeKeepsTheOtherSettings()
    {
        _settingsStore.Settings = new AppSettings("es", IsDeveloperModeEnabled: true);
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.Off).SelectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(_settingsStore.Settings.LanguageTag, Is.EqualTo("es"));
            Assert.That(_settingsStore.Settings.IsDeveloperModeEnabled, Is.True);
        });
    }

    [Test]
    public async Task AStoreFailureIsLoggedWithoutUndoingTheChange()
    {
        _settingsStore.SaveFailureToThrow = new IOException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        await Option(viewModel, NoiseControlMode.Off).SelectCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Off));
            Assert.That(_diagnosticsLog.Entries.Last().Outcome, Is.EqualTo(DiagnosticOutcome.Failure));
        });
    }

    [Test]
    public void TheStoredModeIsReappliedOnConnect()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.NoiseCancelling));
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(_earbudsSession.SetCalls, Is.EqualTo(new[] { NoiseControlMode.NoiseCancelling }));
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.NoiseCancelling));
        });
    }

    [Test]
    public void AStoredModeTheDeviceAlreadyHasIsNotSentAgain()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.Transparency));
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.That(_earbudsSession.SetCalls, Is.Empty);
    }

    [Test]
    public void AModeStoredForAnotherDeviceIsNotApplied()
    {
        StoreModes((OtherEarbudsContainer, NoiseControlMode.NoiseCancelling));
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.That(_earbudsSession.SetCalls, Is.Empty);
    }

    [Test]
    public void AFailedReapplyIsShownButLeavesTheControlUsable()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.NoiseCancelling));
        _earbudsSession.SetFailureToThrow = new InvalidOperationException(DeviceFailure);
        using NoiseControlViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Status, Is.EqualTo(NoiseControlStatus.Ready));
            Assert.That(viewModel.Mode, Is.EqualTo(NoiseControlMode.Transparency));
            Assert.That(viewModel.ErrorMessage, Does.Contain(DeviceFailure));
        });
    }

    [Test]
    public void AModeChangedOnTheEarbudsReplacesTheStoredOne()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.Transparency));
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _earbudsSession.Report(NoiseControlMode.NoiseCancelling);
        _uiScheduler.RunPendingWork();

        Assert.That(StoredModes()[EarbudsContainer], Is.EqualTo(NoiseControlMode.NoiseCancelling));
    }

    [Test]
    public void AModeReportedWhileConnectingDoesNotReplaceTheStoredOne()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.NoiseCancelling));
        _earbudsSession.PendingSet = new TaskCompletionSource();
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _earbudsSession.Report(NoiseControlMode.Off);
        _uiScheduler.RunPendingWork();

        Assert.That(StoredModes()[EarbudsContainer], Is.EqualTo(NoiseControlMode.NoiseCancelling));
    }

    [Test]
    public void AReportOfTheStoredModeWritesNothing()
    {
        StoreModes((EarbudsContainer, NoiseControlMode.Transparency));
        using NoiseControlViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        _earbudsSession.Report(NoiseControlMode.Transparency);
        _uiScheduler.RunPendingWork();

        Assert.That(_settingsStore.SaveCount, Is.Zero);
    }

    private NoiseControlViewModel CreateViewModel()
    {
        return new NoiseControlViewModel(_service, _settingsStore, _uiScheduler, _diagnosticsLog);
    }

    private void StoreModes(params (Guid ContainerId, NoiseControlMode Mode)[] modes)
    {
        _settingsStore.Settings = AppSettings.Default with
        {
            NoiseControlModes = modes.ToDictionary(entry => entry.ContainerId, entry => entry.Mode)
        };
    }

    private IReadOnlyDictionary<Guid, NoiseControlMode> StoredModes()
    {
        return _settingsStore.Settings.NoiseControlModes ?? new Dictionary<Guid, NoiseControlMode>();
    }

    private static NoiseControlOptionViewModel Option(NoiseControlViewModel viewModel, NoiseControlMode mode)
    {
        return viewModel.Options.Single(option => option.Mode == mode);
    }

    private static NoiseControlMode[] SelectedOptions(NoiseControlViewModel viewModel)
    {
        return [.. viewModel.Options.Where(option => option.IsSelected).Select(option => option.Mode)];
    }

    private static BluetoothAudioDevice Earbuds(bool? isConnected)
    {
        return new BluetoothAudioDevice(
            EarbudsContainer,
            "OPPO Enco Air5",
            BluetoothAudioDeviceKind.Headphones,
            isConnected,
            BatteryCharge.Unknown);
    }

    private static BluetoothAudioDevice Speaker(bool? isConnected)
    {
        return new BluetoothAudioDevice(
            SpeakerContainer,
            "SRS-XE300",
            BluetoothAudioDeviceKind.Loudspeaker,
            isConnected,
            BatteryCharge.Unknown);
    }
}
