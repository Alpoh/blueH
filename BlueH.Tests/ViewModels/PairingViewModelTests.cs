using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class PairingViewModelTests
{
    private const string Pin = "123456";
    private const string ProvidedPin = "0000";
    private const string PairingFailure = "the radio is off";

    private static readonly TimeSpan PairingDeadline = TimeSpan.FromSeconds(5);
    private static readonly DiscoveredDevice Speaker = new("Bluetooth#Bluetoothac:f2:3c:e1:15:94-78:5e:a2:fa:71:91", "SRS-XE300");
    private static readonly DiscoveredDevice Earbuds = new("Bluetooth#Bluetoothac:f2:3c:e1:15:94-18:5c:a1:1d:b0:ff", "OPPO Enco Air5");

    private FakeBluetoothPairingService _pairingService = null!;
    private RecordingUiScheduler _uiScheduler = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _pairingService = new FakeBluetoothPairingService();
        _uiScheduler = new RecordingUiScheduler();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [Test]
    public void SearchingStartsDiscoveryAndListsWhatIsFound()
    {
        using PairingViewModel viewModel = CreateViewModel();

        viewModel.SearchCommand.Execute(null);
        _pairingService.Watcher.RaiseFound(Speaker);
        _uiScheduler.RunPendingWork();

        Assert.Multiple(() =>
        {
            Assert.That(_pairingService.Watcher.IsStarted, Is.True);
            Assert.That(viewModel.IsSearching, Is.True);
            Assert.That(viewModel.FoundDevices, Is.EqualTo(new[] { Speaker }));
        });
    }

    [Test]
    public void ADeviceFoundTwiceIsListedOnce()
    {
        using PairingViewModel viewModel = CreateViewModel();
        viewModel.SearchCommand.Execute(null);

        _pairingService.Watcher.RaiseFound(Speaker);
        _pairingService.Watcher.RaiseFound(Speaker);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.FoundDevices, Has.Count.EqualTo(1));
    }

    [Test]
    public void ALostDeviceLeavesTheList()
    {
        using PairingViewModel viewModel = CreateViewModel();
        viewModel.SearchCommand.Execute(null);
        _pairingService.Watcher.RaiseFound(Speaker);
        _pairingService.Watcher.RaiseFound(Earbuds);

        _pairingService.Watcher.RaiseLost(Speaker.EndpointId);
        _uiScheduler.RunPendingWork();

        Assert.That(viewModel.FoundDevices, Is.EqualTo(new[] { Earbuds }));
    }

    [Test]
    public void SearchAndStopAreExclusive()
    {
        using PairingViewModel viewModel = CreateViewModel();
        bool stopBeforeSearching = viewModel.StopSearchCommand.CanExecute(null);

        viewModel.SearchCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(stopBeforeSearching, Is.False);
            Assert.That(viewModel.SearchCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.StopSearchCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void StoppingTheSearchReleasesTheWatcher()
    {
        using PairingViewModel viewModel = CreateViewModel();
        viewModel.SearchCommand.Execute(null);
        FakeDiscoveryWatcher watcher = _pairingService.Watcher;

        viewModel.StopSearchCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(watcher.IsStopped, Is.True);
            Assert.That(watcher.IsDisposed, Is.True);
            Assert.That(watcher.HasSubscribers, Is.False);
            Assert.That(viewModel.IsSearching, Is.False);
        });
    }

    [Test]
    public void DisposingStopsTheSearch()
    {
        PairingViewModel viewModel = CreateViewModel();
        viewModel.SearchCommand.Execute(null);

        viewModel.Dispose();

        Assert.That(_pairingService.Watcher.IsStopped, Is.True);
    }

    [Test]
    public async Task PairingSucceedsAndTheDeviceLeavesTheFoundList()
    {
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker, Earbuds);

        await Finished(Pair(viewModel, Speaker));

        Assert.Multiple(() =>
        {
            Assert.That(_pairingService.PairCalls, Is.EqualTo(new[] { Speaker.EndpointId }));
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.OutcomeMessage(PairingOutcome.Succeeded)));
            Assert.That(viewModel.FoundDevices, Is.EqualTo(new[] { Earbuds }));
            Assert.That(viewModel.IsPairing, Is.False);
        });
    }

    [Test]
    public async Task AJustWorksDeviceIsAcceptedWithoutAsking()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.ConfirmOnly, Pin: null);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        await Finished(Pair(viewModel, Speaker));

        Assert.Multiple(() =>
        {
            Assert.That(_pairingService.ChallengeResponse?.IsAccepted, Is.True);
            Assert.That(viewModel.HasChallenge, Is.False);
        });
    }

    [Test]
    public async Task APinToConfirmIsShownAndAcceptingIt()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.ConfirmPinMatch, Pin);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _uiScheduler.RunPendingWork();
        bool challengeShown = viewModel.HasChallenge;
        string? challengeText = viewModel.ChallengeText;
        viewModel.AcceptChallengeCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(challengeShown, Is.True);
            Assert.That(challengeText, Does.Contain(Pin));
            Assert.That(_pairingService.ChallengeResponse?.IsAccepted, Is.True);
            Assert.That(viewModel.HasChallenge, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.OutcomeMessage(PairingOutcome.Succeeded)));
        });
    }

    [Test]
    public async Task APinToDisplayIsShown()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.DisplayPin, Pin);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _uiScheduler.RunPendingWork();
        string? challengeText = viewModel.ChallengeText;
        viewModel.AcceptChallengeCommand.Execute(null);
        await Finished(pairing);

        Assert.That(challengeText, Does.Contain(Pin));
    }

    [Test]
    public async Task RejectingTheChallengeReportsTheRejection()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.ConfirmPinMatch, Pin);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _uiScheduler.RunPendingWork();
        viewModel.RejectChallengeCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(_pairingService.ChallengeResponse?.IsAccepted, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.OutcomeMessage(PairingOutcome.RejectedByUser)));
            Assert.That(viewModel.FoundDevices, Is.EqualTo(new[] { Speaker }));
        });
    }

    [Test]
    public async Task AProvidedPinIsRequiredAndPassedThrough()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.ProvidePin, Pin: null);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _uiScheduler.RunPendingWork();
        bool needsPin = viewModel.NeedsPinInput;
        bool acceptableWithoutPin = viewModel.AcceptChallengeCommand.CanExecute(null);
        viewModel.PinInput = $" {ProvidedPin} ";
        viewModel.AcceptChallengeCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(needsPin, Is.True);
            Assert.That(acceptableWithoutPin, Is.False);
            Assert.That(_pairingService.ChallengeResponse?.Pin, Is.EqualTo(ProvidedPin));
        });
    }

    [TestCase(PairingOutcome.AuthenticationFailed)]
    [TestCase(PairingOutcome.DeviceNotReady)]
    [TestCase(PairingOutcome.Failed)]
    public async Task AFailedOutcomeIsShownLoggedAndKeepsTheDeviceListed(PairingOutcome outcome)
    {
        _pairingService.PairingResultToReturn = new PairingResult(outcome, "PlatformStatus");
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        await Finished(Pair(viewModel, Speaker));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.OutcomeMessage(outcome)).And.Not.Empty);
            Assert.That(viewModel.FoundDevices, Is.EqualTo(new[] { Speaker }));
            Assert.That(
                _diagnosticsLog.Entries.Any(entry => entry.Outcome == DiagnosticOutcome.Failure),
                Is.True);
        });
    }

    [Test]
    public void EveryOutcomeHasItsOwnMessage()
    {
        using PairingViewModel viewModel = CreateViewModel();

        string[] messages = [.. Enum.GetValues<PairingOutcome>().Select(viewModel.OutcomeMessage)];

        Assert.That(messages, Is.Unique.And.All.Not.Empty);
    }

    [Test]
    public async Task CancellingAPairingReportsCancellation()
    {
        _pairingService.HoldsPairingOpen = true;
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        bool cancellable = viewModel.CancelPairingCommand.CanExecute(null);
        viewModel.CancelPairingCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(cancellable, Is.True);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.CancelledMessage).And.Not.Empty);
            Assert.That(viewModel.IsPairing, Is.False);
        });
    }

    [Test]
    public async Task CancellingWhileAChallengeIsOpenClosesIt()
    {
        _pairingService.ChallengeToRaise = new PairingChallenge(PairingChallengeKind.ConfirmPinMatch, Pin);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _uiScheduler.RunPendingWork();
        viewModel.CancelPairingCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasChallenge, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.CancelledMessage));
        });
    }

    [Test]
    public async Task APairingThatNeverEndsTimesOut()
    {
        _pairingService.HoldsPairingOpen = true;
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        Task pairing = Pair(viewModel, Speaker);
        _timeProvider.Advance(PairingViewModel.PairingTimeout);
        await Finished(pairing);

        Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.TimedOutMessage).And.Not.Empty);
    }

    [Test]
    public async Task PairingIsNotReentrant()
    {
        _pairingService.HoldsPairingOpen = true;
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker, Earbuds);

        Task pairing = Pair(viewModel, Speaker);
        bool secondAllowed = viewModel.PairCommand.CanExecute(Earbuds);
        viewModel.CancelPairingCommand.Execute(null);
        await Finished(pairing);

        Assert.Multiple(() =>
        {
            Assert.That(secondAllowed, Is.False);
            Assert.That(viewModel.PairCommand.CanExecute(Earbuds), Is.True);
        });
    }

    [Test]
    public async Task AnExceptionIsShownAndLogged()
    {
        _pairingService.FailureToThrow = new InvalidOperationException(PairingFailure);
        using PairingViewModel viewModel = await SearchingViewModelWith(Speaker);

        await Finished(Pair(viewModel, Speaker));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessage, Does.Contain(PairingFailure));
            Assert.That(
                _diagnosticsLog.Entries.Any(entry =>
                    entry.Outcome == DiagnosticOutcome.Failure && entry.Detail == PairingFailure),
                Is.True);
        });
    }

    private static Task Pair(PairingViewModel viewModel, DiscoveredDevice device)
    {
        return ((AsyncRelayCommand)viewModel.PairCommand).ExecuteAsync(device);
    }

    private static Task Finished(Task pairing)
    {
        return pairing.WaitAsync(PairingDeadline);
    }

    private Task<PairingViewModel> SearchingViewModelWith(params DiscoveredDevice[] devices)
    {
        PairingViewModel viewModel = CreateViewModel();
        viewModel.SearchCommand.Execute(null);

        foreach (DiscoveredDevice device in devices)
        {
            _pairingService.Watcher.RaiseFound(device);
        }

        _uiScheduler.RunPendingWork();

        return Task.FromResult(viewModel);
    }

    private PairingViewModel CreateViewModel()
    {
        return new PairingViewModel(_pairingService, _uiScheduler, _diagnosticsLog, _timeProvider);
    }
}
