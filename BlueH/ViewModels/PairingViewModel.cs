using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class PairingViewModel : ViewModelBase, IDisposable
{
    public static readonly TimeSpan PairingTimeout = TimeSpan.FromSeconds(60);

    private const string PairOperation = "Pair device";
    private const string SearchOperation = "Search for devices";

    private readonly IBluetoothPairingService _pairingService;
    private readonly IUiScheduler _uiScheduler;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _stopSearchCommand;
    private readonly AsyncRelayCommand _pairCommand;
    private readonly AsyncRelayCommand _cancelPairingCommand;
    private readonly AsyncRelayCommand _acceptChallengeCommand;
    private readonly AsyncRelayCommand _rejectChallengeCommand;

    private IBluetoothDiscoveryWatcher? _watcher;
    private CancellationTokenSource? _pairingCancellation;
    private TaskCompletionSource<PairingResponse>? _challengeResponse;
    private PairingChallenge? _challenge;
    private string? _pairingDeviceName;
    private string _pinInput = string.Empty;
    private string? _statusMessage;
    private bool _cancelledByUser;

    public PairingViewModel(
        IBluetoothPairingService pairingService,
        IUiScheduler uiScheduler,
        IDiagnosticsLog diagnosticsLog,
        TimeProvider timeProvider)
    {
        _pairingService = pairingService;
        _uiScheduler = uiScheduler;
        _diagnosticsLog = diagnosticsLog;
        _timeProvider = timeProvider;
        _searchCommand = new AsyncRelayCommand(_ => StartSearch(), _ => _watcher is null);
        _stopSearchCommand = new AsyncRelayCommand(_ => StopSearch(), _ => _watcher is not null);
        _pairCommand = new AsyncRelayCommand(
            parameter => PairAsync((DiscoveredDevice)parameter!),
            parameter => parameter is DiscoveredDevice);
        _cancelPairingCommand = new AsyncRelayCommand(_ => CancelPairing(), _ => IsPairing);
        _acceptChallengeCommand = new AsyncRelayCommand(_ => AnswerChallenge(accept: true), _ => CanAcceptChallenge());
        _rejectChallengeCommand = new AsyncRelayCommand(_ => AnswerChallenge(accept: false), _ => HasChallenge);
    }

    public ObservableCollection<DiscoveredDevice> FoundDevices { get; } = [];

    public bool IsSearching => _watcher is not null;

    public bool IsPairing => _pairingCancellation is not null;

    public bool HasChallenge => _challenge is not null;

    public bool NeedsPinInput => _challenge?.Kind == PairingChallengeKind.ProvidePin;

    public string? ChallengeText => _challenge is null ? null : DescribeChallenge(_challenge);

    public string PinInput
    {
        get => _pinInput;
        set
        {
            if (SetField(ref _pinInput, value))
            {
                _acceptChallengeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ICommand SearchCommand => _searchCommand;

    public ICommand StopSearchCommand => _stopSearchCommand;

    public ICommand PairCommand => _pairCommand;

    public ICommand CancelPairingCommand => _cancelPairingCommand;

    public ICommand AcceptChallengeCommand => _acceptChallengeCommand;

    public ICommand RejectChallengeCommand => _rejectChallengeCommand;

    public string Header => AppResources.PairingHeader;

    public string SearchLabel => AppResources.PairingSearchLabel;

    public string StopSearchLabel => AppResources.PairingStopSearchLabel;

    public string SearchingMessage => AppResources.PairingSearchingMessage;

    public string PairLabel => AppResources.PairingPairLabel;

    public string CancelLabel => AppResources.DeviceCancelLabel;

    public string AcceptLabel => AppResources.PairingAcceptLabel;

    public string RejectLabel => AppResources.PairingRejectLabel;

    public string PinInputLabel => AppResources.PairingPinInputLabel;

    public string CancelledMessage => AppResources.PairingCancelledMessage;

    public string TimedOutMessage => AppResources.PairingTimedOutMessage;

    public void Dispose()
    {
        StopWatcher();
        _pairingCancellation?.Cancel();
    }

    public string OutcomeMessage(PairingOutcome outcome)
    {
        return outcome switch
        {
            PairingOutcome.Succeeded => AppResources.PairingSucceededMessage,
            PairingOutcome.AlreadyDone => AppResources.PairingAlreadyPairedMessage,
            PairingOutcome.RejectedByUser => AppResources.PairingRejectedMessage,
            PairingOutcome.AuthenticationFailed => AppResources.PairingAuthenticationFailedMessage,
            PairingOutcome.DeviceNotReady => AppResources.PairingDeviceNotReadyMessage,
            _ => AppResources.PairingFailedMessage
        };
    }

    private static string DescribeChallenge(PairingChallenge challenge)
    {
        return challenge.Kind switch
        {
            PairingChallengeKind.ConfirmPinMatch => string.Format(
                CultureInfo.CurrentCulture,
                AppResources.PairingConfirmPinFormat,
                challenge.Pin),
            PairingChallengeKind.DisplayPin => string.Format(
                CultureInfo.CurrentCulture,
                AppResources.PairingDisplayPinFormat,
                challenge.Pin),
            PairingChallengeKind.ProvidePin => AppResources.PairingProvidePinMessage,
            _ => AppResources.PairingConfirmMessage
        };
    }

    private Task StartSearch()
    {
        FoundDevices.Clear();
        IBluetoothDiscoveryWatcher watcher = _pairingService.CreateDiscoveryWatcher();
        watcher.DeviceFound += OnDeviceFound;
        watcher.DeviceLost += OnDeviceLost;
        _watcher = watcher;

        try
        {
            watcher.Start();
            StatusMessage = SearchingMessage;
        }
        catch (Exception failure)
        {
            StopWatcher();
            StatusMessage = failure.Message;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SearchOperation, failure.Message);
        }

        RaiseSearchStates();

        return Task.CompletedTask;
    }

    private Task StopSearch()
    {
        StopWatcher();
        StatusMessage = null;
        RaiseSearchStates();

        return Task.CompletedTask;
    }

    private void StopWatcher()
    {
        if (_watcher is not { } watcher)
        {
            return;
        }

        watcher.DeviceFound -= OnDeviceFound;
        watcher.DeviceLost -= OnDeviceLost;
        watcher.Stop();
        watcher.Dispose();
        _watcher = null;
    }

    private void RaiseSearchStates()
    {
        OnPropertyChanged(nameof(IsSearching));
        _searchCommand.RaiseCanExecuteChanged();
        _stopSearchCommand.RaiseCanExecuteChanged();
    }

    private void OnDeviceFound(object? sender, DiscoveredDevice device)
    {
        _uiScheduler.Post(() =>
        {
            int existing = IndexOf(device.EndpointId);

            if (existing < 0)
            {
                FoundDevices.Add(device);
            }
            else if (FoundDevices[existing] != device)
            {
                FoundDevices[existing] = device;
            }
        });
    }

    private void OnDeviceLost(object? sender, string endpointId)
    {
        _uiScheduler.Post(() =>
        {
            int existing = IndexOf(endpointId);

            if (existing >= 0)
            {
                FoundDevices.RemoveAt(existing);
            }
        });
    }

    private int IndexOf(string endpointId)
    {
        for (int index = 0; index < FoundDevices.Count; index++)
        {
            if (string.Equals(FoundDevices[index].EndpointId, endpointId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private async Task PairAsync(DiscoveredDevice device)
    {
        using CancellationTokenSource cancellation = new(PairingTimeout, _timeProvider);
        _pairingCancellation = cancellation;
        _cancelledByUser = false;
        _pairingDeviceName = device.Name;
        StatusMessage = string.Format(CultureInfo.CurrentCulture, AppResources.PairingInProgressFormat, device.Name);
        RaisePairingStates();

        try
        {
            PairingResult result = await _pairingService.PairAsync(
                device.EndpointId,
                RespondToChallengeAsync,
                cancellation.Token);

            StatusMessage = OutcomeMessage(result.Outcome);
            _diagnosticsLog.Record(
                result.Outcome is PairingOutcome.Succeeded or PairingOutcome.AlreadyDone
                    ? DiagnosticOutcome.Success
                    : DiagnosticOutcome.Failure,
                PairOperation,
                $"{device.Name} {result.PlatformStatus}");

            if (result.Outcome is PairingOutcome.Succeeded or PairingOutcome.AlreadyDone)
            {
                int existing = IndexOf(device.EndpointId);

                if (existing >= 0)
                {
                    FoundDevices.RemoveAt(existing);
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = _cancelledByUser ? CancelledMessage : TimedOutMessage;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, PairOperation, StatusMessage);
        }
        catch (Exception failure)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, AppResources.PairingErrorFormat, failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, PairOperation, failure.Message);
        }
        finally
        {
            _challengeResponse?.TrySetCanceled();
            _pairingCancellation = null;
            _pairingDeviceName = null;
            SetChallenge(null, null);
            RaisePairingStates();
        }
    }

    private Task<PairingResponse> RespondToChallengeAsync(PairingChallenge challenge, CancellationToken cancellationToken)
    {
        if (challenge.Kind == PairingChallengeKind.ConfirmOnly)
        {
            return Task.FromResult(PairingResponse.Accept());
        }

        TaskCompletionSource<PairingResponse> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
        _uiScheduler.Post(() => SetChallenge(challenge, response));

        return response.Task;
    }

    private void SetChallenge(PairingChallenge? challenge, TaskCompletionSource<PairingResponse>? response)
    {
        _challenge = challenge;
        _challengeResponse = response;
        PinInput = string.Empty;
        OnPropertyChanged(nameof(HasChallenge));
        OnPropertyChanged(nameof(NeedsPinInput));
        OnPropertyChanged(nameof(ChallengeText));
        _acceptChallengeCommand.RaiseCanExecuteChanged();
        _rejectChallengeCommand.RaiseCanExecuteChanged();
    }

    private bool CanAcceptChallenge()
    {
        return HasChallenge && (!NeedsPinInput || !string.IsNullOrWhiteSpace(_pinInput));
    }

    private Task AnswerChallenge(bool accept)
    {
        TaskCompletionSource<PairingResponse>? response = _challengeResponse;
        PairingResponse answer = accept
            ? PairingResponse.Accept(NeedsPinInput ? _pinInput.Trim() : null)
            : PairingResponse.Rejected;

        SetChallenge(null, null);
        response?.TrySetResult(answer);

        if (_pairingDeviceName is { } name)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, AppResources.PairingInProgressFormat, name);
        }

        return Task.CompletedTask;
    }

    private Task CancelPairing()
    {
        _cancelledByUser = true;
        _pairingCancellation?.Cancel();

        return Task.CompletedTask;
    }

    private void RaisePairingStates()
    {
        OnPropertyChanged(nameof(IsPairing));
        _pairCommand.RaiseCanExecuteChanged();
        _cancelPairingCommand.RaiseCanExecuteChanged();
    }
}
