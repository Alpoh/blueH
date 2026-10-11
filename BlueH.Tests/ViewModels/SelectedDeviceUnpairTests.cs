using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Tests.Fakes;
using BlueH.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BlueH.Tests.ViewModels;

[TestFixture]
public class SelectedDeviceUnpairTests
{
    private static readonly Guid EarbudsContainer = Guid.Parse("0ac66fc7-8a44-50f8-ae2b-25ffdd18b301");
    private static readonly Guid SpeakerContainer = Guid.Parse("62137bd6-c515-5357-8cb8-fdfef0bf1ecc");

    private const string EarbudsName = "OPPO Enco Air5";
    private const string UnpairFailure = "access denied";

    private FakeBluetoothPairingService _pairingService = null!;
    private InMemoryDiagnosticsLog _diagnosticsLog = null!;
    private FakeTimeProvider _timeProvider = null!;

    [SetUp]
    public void SetUp()
    {
        _pairingService = new FakeBluetoothPairingService();
        _timeProvider = new FakeTimeProvider();
        _diagnosticsLog = new InMemoryDiagnosticsLog(_timeProvider);
    }

    [Test]
    public void UnpairingAsksForConfirmationFirst()
    {
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();

        viewModel.UnpairCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsConfirmingUnpair, Is.True);
            Assert.That(viewModel.UnpairConfirmationText, Does.Contain(EarbudsName));
            Assert.That(_pairingService.UnpairCalls, Is.Empty);
            Assert.That(viewModel.UnpairCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void CancellingTheConfirmationUnpairsNothing()
    {
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        viewModel.CancelUnpairCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsConfirmingUnpair, Is.False);
            Assert.That(viewModel.UnpairConfirmationText, Is.Null);
            Assert.That(_pairingService.UnpairCalls, Is.Empty);
        });
    }

    [Test]
    public void ConfirmingIsImpossibleWithoutAskingFirst()
    {
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();

        Assert.That(viewModel.ConfirmUnpairCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task ConfirmingUnpairsTheSelectedDevice()
    {
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        await Confirm(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(_pairingService.UnpairCalls, Is.EqualTo(new[] { EarbudsContainer }));
            Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.UnpairedMessage).And.Not.Empty);
            Assert.That(viewModel.IsConfirmingUnpair, Is.False);
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public async Task ADeviceThatWasNotPairedSaysSo()
    {
        _pairingService.UnpairingResultToReturn = new PairingResult(PairingOutcome.AlreadyDone, "AlreadyUnpaired");
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        await Confirm(viewModel);

        Assert.That(viewModel.StatusMessage, Is.EqualTo(viewModel.NotPairedMessage).And.Not.Empty);
    }

    [Test]
    public async Task ARefusedUnpairIsShownAndLogged()
    {
        _pairingService.UnpairingResultToReturn = new PairingResult(PairingOutcome.Failed, "AccessDenied");
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        await Confirm(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessage, Does.Contain("AccessDenied"));
            Assert.That(
                _diagnosticsLog.Entries.Any(entry => entry.Outcome == DiagnosticOutcome.Failure),
                Is.True);
        });
    }

    [Test]
    public async Task AFailingUnpairIsShownAndLogged()
    {
        _pairingService.FailureToThrow = new InvalidOperationException(UnpairFailure);
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        await Confirm(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusMessage, Does.Contain(UnpairFailure));
            Assert.That(viewModel.IsBusy, Is.False);
            Assert.That(
                _diagnosticsLog.Entries.Any(entry =>
                    entry.Outcome == DiagnosticOutcome.Failure && entry.Detail == UnpairFailure),
                Is.True);
        });
    }

    [Test]
    public void SelectingAnotherDeviceWithdrawsTheConfirmation()
    {
        SelectedDeviceViewModel viewModel = CreateViewModelWithEarbuds();
        viewModel.UnpairCommand.Execute(null);

        viewModel.Device = new BluetoothAudioDevice(
            SpeakerContainer,
            "SRS-XE300",
            BluetoothAudioDeviceKind.Loudspeaker,
            IsConnected: true,
            BatteryCharge.Unknown);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsConfirmingUnpair, Is.False);
            Assert.That(viewModel.ConfirmUnpairCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void UnpairingNeedsADevice()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();

        Assert.That(viewModel.UnpairCommand.CanExecute(null), Is.False);
    }

    private static Task Confirm(SelectedDeviceViewModel viewModel)
    {
        return ((AsyncRelayCommand)viewModel.ConfirmUnpairCommand).ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));
    }

    private SelectedDeviceViewModel CreateViewModelWithEarbuds()
    {
        SelectedDeviceViewModel viewModel = CreateViewModel();
        viewModel.Device = new BluetoothAudioDevice(
            EarbudsContainer,
            EarbudsName,
            BluetoothAudioDeviceKind.Headphones,
            IsConnected: true,
            BatteryCharge.Unknown);

        return viewModel;
    }

    private SelectedDeviceViewModel CreateViewModel()
    {
        return new SelectedDeviceViewModel(
            new FakeBluetoothConnectionService(),
            _pairingService,
            _diagnosticsLog,
            _timeProvider);
    }
}
