using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class SelectedDeviceConnectionTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const string ConnectionFailure = "no Bluetooth audio endpoint for the container";

    private static readonly TimeSpan AttemptDeadline = TimeSpan.FromSeconds(5);

    private FakeBluetoothConnectionService _connectionService = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _connectionService = new FakeBluetoothConnectionService();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [Test]
    public void WithNoDeviceNothingCanBeDone()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasNoDevice, Is.True);
            Assert.That(viewModel.ConnectCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.DisconnectCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.CancelCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void ADisconnectedDeviceOffersConnectOnly()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: false);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ConnectCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.DisconnectCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void AConnectedDeviceOffersDisconnectOnly()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: true);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ConnectCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.DisconnectCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void ADeviceOfUnknownStateCanBeConnected()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();

        viewModel.Device = Earbuds(isConnected: null);

        Assert.That(viewModel.ConnectCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task ConnectingWaitsUntilTheDeviceReportsConnected()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task attempt = Connect(viewModel);
        bool busyWhileWaiting = viewModel.IsBusy;
        string? messageWhileWaiting = viewModel.StatusMessage;
        viewModel.Device = Earbuds(isConnected: true);
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(_connectionService.ConnectCalls, Is.EqualTo(new[] { EarbudsContainer }));
            Assert.That(busyWhileWaiting, Is.True);
            Assert.That(messageWhileWaiting, Is.EqualTo(viewModel.ConnectingMessage).And.Not.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task DisconnectingWaitsUntilTheDeviceReportsDisconnected()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: true);

        Task attempt = ((AsyncRelayCommand)viewModel.DisconnectCommand).ExecuteAsync(null);
        string? messageWhileWaiting = viewModel.StatusMessage;
        viewModel.Device = Earbuds(isConnected: false);
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(_connectionService.DisconnectCalls, Is.EqualTo(new[] { EarbudsContainer }));
            Assert.That(messageWhileWaiting, Is.EqualTo(viewModel.DisconnectingMessage).And.Not.Empty);
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task AnUnrelatedUpdateDoesNotEndTheWait()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task attempt = Connect(viewModel);
        viewModel.Device = Earbuds(isConnected: false) with { Battery = BatteryCharge.FromPercent(80) };

        Assert.Multiple(() =>
        {
            Assert.That(attempt.IsCompleted, Is.False);
            Assert.That(viewModel.IsBusy, Is.True);
        });

        viewModel.Device = Earbuds(isConnected: true);
        await Finished(attempt);
    }

    [Test]
    public async Task ADeviceThatNeverConnectsTimesOut()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task attempt = Connect(viewModel);
        _timeProvider.Advance(SelectedDeviceViewModel.ConnectionTimeout - TimeSpan.FromSeconds(1));
        bool stillWaitingBeforeTheTimeout = !attempt.IsCompleted;
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(stillWaitingBeforeTheTimeout, Is.True);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.TimedOutMessage).And.Not.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(
                _diagnosticsLog.Entries.Any(entry => entry.Outcome == DiagnosticOutcome.Failure),
                Is.True);
        });
    }

    [Test]
    public async Task CancellingStopsTheWait()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task attempt = Connect(viewModel);
        bool cancelEnabledWhileWaiting = viewModel.CancelCommand.CanExecute(null);
        viewModel.CancelCommand.Execute(null);
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(cancelEnabledWhileWaiting, Is.True);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.CancelledMessage).And.Not.Empty);
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.CancelCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task SelectingAnotherDeviceAbandonsTheAttemptSilently()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task attempt = Connect(viewModel);
        viewModel.Device = new BluetoothAudioDevice(
            SpeakerContainer,
            "SRS-XE300",
            BluetoothAudioDeviceKind.Loudspeaker,
            IsConnected: true,
            BatteryCharge.Unknown);
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task AFailedConnectionIsShownAndLogged()
    {
        _connectionService.FailureToThrow = new InvalidOperationException(ConnectionFailure);
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        await Connect(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessage, Does.Contain(ConnectionFailure));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(
                _diagnosticsLog.Entries.Any(entry =>
                    entry.Outcome == DiagnosticOutcome.Failure && entry.Detail == ConnectionFailure),
                Is.True);
        });
    }

    [Test]
    public async Task ConnectingIsNotReentrant()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);

        Task first = Connect(viewModel);
        bool enabledWhileWaiting = viewModel.ConnectCommand.CanExecute(null);
        Task second = Connect(viewModel);
        viewModel.Device = Earbuds(isConnected: true);
        await Finished(Task.WhenAll(first, second));

        Assert.Multiple(() =>
        {
            Assert.That(enabledWhileWaiting, Is.False);
            Assert.That(_connectionService.ConnectCalls, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TheEndOfAnAttemptAnnouncesTheCommandStates()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = Earbuds(isConnected: false);
        Task attempt = Connect(viewModel);
        int cancelChanges = 0;
        int disconnectChanges = 0;
        viewModel.CancelCommand.CanExecuteChanged += (_, _) => cancelChanges++;
        viewModel.DisconnectCommand.CanExecuteChanged += (_, _) => disconnectChanges++;

        _timeProvider.Advance(SelectedDeviceViewModel.ConnectionTimeout);
        await Finished(attempt);

        Assert.Multiple(() =>
        {
            Assert.That(cancelChanges, Is.GreaterThan(0));
            Assert.That(disconnectChanges, Is.GreaterThan(0));
        });
    }

    private static Task Finished(Task attempt)
    {
        return attempt.WaitAsync(AttemptDeadline);
    }

    private static Task Connect(SelectedDeviceViewModel viewModel)
    {
        return ((AsyncRelayCommand)viewModel.ConnectCommand).ExecuteAsync(null);
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

    private SelectedDeviceViewModel CreateViewModel()
    {
        return new SelectedDeviceViewModel(_connectionService, new FakeBluetoothPairingService(), _diagnosticsLog, _timeProvider);
    }
}
