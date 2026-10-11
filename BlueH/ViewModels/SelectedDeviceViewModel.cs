using System.Globalization;
using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class SelectedDeviceViewModel : ViewModelBase
{
    public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(20);

    private const string ConnectOperation = "Connect device";
    private const string DisconnectOperation = "Disconnect device";
    private const string UnpairOperation = "Unpair device";
    private const string TimedOutDetail = "timed out waiting for the connection state";

    private readonly IBluetoothConnectionService _connectionService;
    private readonly IBluetoothPairingService _pairingService;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncRelayCommand _connectCommand;
    private readonly AsyncRelayCommand _disconnectCommand;
    private readonly AsyncRelayCommand _cancelCommand;
    private readonly AsyncRelayCommand _unpairCommand;
    private readonly AsyncRelayCommand _confirmUnpairCommand;
    private readonly AsyncRelayCommand _cancelUnpairCommand;

    private BluetoothAudioDevice? _device;
    private ConnectionActivity _activity;
    private string? _statusMessage;
    private bool _isConfirmingUnpair;
    private CancellationTokenSource? _attemptCancellation;
    private TaskCompletionSource? _awaitedStateReached;
    private (Guid ContainerId, bool IsConnected)? _awaitedState;

    public SelectedDeviceViewModel(
        IBluetoothConnectionService connectionService,
        IBluetoothPairingService pairingService,
        IDiagnosticsLog diagnosticsLog,
        TimeProvider timeProvider)
    {
        _connectionService = connectionService;
        _pairingService = pairingService;
        _diagnosticsLog = diagnosticsLog;
        _timeProvider = timeProvider;
        _connectCommand = new AsyncRelayCommand(
            _ => ChangeConnectionAsync(connect: true),
            _ => _activity == ConnectionActivity.Idle && _device is { IsConnected: not true });
        _disconnectCommand = new AsyncRelayCommand(
            _ => ChangeConnectionAsync(connect: false),
            _ => _activity == ConnectionActivity.Idle && _device is { IsConnected: true });
        _cancelCommand = new AsyncRelayCommand(_ => CancelAttemptAsync(), _ => _activity != ConnectionActivity.Idle);
        _unpairCommand = new AsyncRelayCommand(
            _ => BeginUnpairConfirmation(),
            _ => _activity == ConnectionActivity.Idle && _device is not null && !_isConfirmingUnpair);
        _confirmUnpairCommand = new AsyncRelayCommand(_ => UnpairAsync(), _ => _isConfirmingUnpair);
        _cancelUnpairCommand = new AsyncRelayCommand(_ => EndUnpairConfirmation(), _ => _isConfirmingUnpair);
    }

    public BluetoothAudioDevice? Device
    {
        get => _device;
        set
        {
            Guid? previousContainer = _device?.ContainerId;

            if (!SetField(ref _device, value))
            {
                return;
            }

            if (previousContainer != value?.ContainerId)
            {
                _attemptCancellation?.Cancel();
                IsConfirmingUnpair = false;
                StatusMessage = null;
            }

            if (_awaitedState is { } awaited
                && value?.ContainerId == awaited.ContainerId
                && value.IsConnected == awaited.IsConnected)
            {
                _awaitedStateReached?.TrySetResult();
            }

            OnPropertyChanged(nameof(HasDevice));
            OnPropertyChanged(nameof(HasNoDevice));
            RaiseCommandStates();
        }
    }

    public bool HasDevice => _device is not null;

    public bool HasNoDevice => _device is null;

    public ConnectionActivity Activity
    {
        get => _activity;
        private set
        {
            if (SetField(ref _activity, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy => _activity != ConnectionActivity.Idle;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsConfirmingUnpair
    {
        get => _isConfirmingUnpair;
        private set
        {
            if (SetField(ref _isConfirmingUnpair, value))
            {
                OnPropertyChanged(nameof(UnpairConfirmationText));
                RaiseCommandStates();
            }
        }
    }

    public string? UnpairConfirmationText => _isConfirmingUnpair && _device is { } device
        ? string.Format(CultureInfo.CurrentCulture, AppResources.DeviceUnpairConfirmFormat, device.Name)
        : null;

    public ICommand UnpairCommand => _unpairCommand;

    public ICommand ConfirmUnpairCommand => _confirmUnpairCommand;

    public ICommand CancelUnpairCommand => _cancelUnpairCommand;

    public string UnpairLabel => AppResources.DeviceUnpairLabel;

    public string ConfirmUnpairLabel => AppResources.DeviceConfirmUnpairLabel;

    public string UnpairingMessage => AppResources.DeviceUnpairingMessage;

    public string UnpairedMessage => AppResources.DeviceUnpairedMessage;

    public string NotPairedMessage => AppResources.DeviceNotPairedMessage;

    public ICommand ConnectCommand => _connectCommand;

    public ICommand DisconnectCommand => _disconnectCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string NoDeviceMessage => AppResources.SelectedDeviceNoneMessage;

    public string ConnectLabel => AppResources.DeviceConnectLabel;

    public string DisconnectLabel => AppResources.DeviceDisconnectLabel;

    public string CancelLabel => AppResources.DeviceCancelLabel;

    public string ConnectingMessage => AppResources.DeviceConnectingMessage;

    public string DisconnectingMessage => AppResources.DeviceDisconnectingMessage;

    public string TimedOutMessage => AppResources.DeviceConnectionTimedOutMessage;

    public string CancelledMessage => AppResources.DeviceConnectionCancelledMessage;

    private async Task ChangeConnectionAsync(bool connect)
    {
        if (_device is not { } device)
        {
            return;
        }

        string operation = connect ? ConnectOperation : DisconnectOperation;
        using CancellationTokenSource cancellation = new();
        _attemptCancellation = cancellation;
        _awaitedState = (device.ContainerId, connect);
        _awaitedStateReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Activity = connect ? ConnectionActivity.Connecting : ConnectionActivity.Disconnecting;
        StatusMessage = connect ? ConnectingMessage : DisconnectingMessage;

        try
        {
            if (connect)
            {
                await _connectionService.ConnectAsync(device.ContainerId, cancellation.Token);
            }
            else
            {
                await _connectionService.DisconnectAsync(device.ContainerId, cancellation.Token);
            }

            if (_device?.IsConnected != connect)
            {
                await WaitForAwaitedStateAsync(cancellation.Token);
            }

            StatusMessage = null;
        }
        catch (OperationCanceledException)
        {
            if (_device?.ContainerId == device.ContainerId)
            {
                StatusMessage = CancelledMessage;
            }
        }
        catch (TimeoutException)
        {
            StatusMessage = TimedOutMessage;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, TimedOutDetail);
        }
        catch (Exception failure)
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.DeviceConnectionFailedFormat,
                failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, operation, failure.Message);
        }
        finally
        {
            _awaitedState = null;
            _awaitedStateReached = null;
            _attemptCancellation = null;
            Activity = ConnectionActivity.Idle;
        }
    }

    private Task BeginUnpairConfirmation()
    {
        IsConfirmingUnpair = true;

        return Task.CompletedTask;
    }

    private Task EndUnpairConfirmation()
    {
        IsConfirmingUnpair = false;

        return Task.CompletedTask;
    }

    private async Task UnpairAsync()
    {
        IsConfirmingUnpair = false;

        if (_device is not { } device)
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        _attemptCancellation = cancellation;
        Activity = ConnectionActivity.Unpairing;
        StatusMessage = UnpairingMessage;

        try
        {
            PairingResult result = await _pairingService.UnpairAsync(device.ContainerId, cancellation.Token);

            StatusMessage = result.Outcome switch
            {
                PairingOutcome.Succeeded => UnpairedMessage,
                PairingOutcome.AlreadyDone => NotPairedMessage,
                _ => string.Format(
                    CultureInfo.CurrentCulture,
                    AppResources.DeviceUnpairFailedFormat,
                    result.PlatformStatus)
            };

            if (result.Outcome == PairingOutcome.Failed)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, UnpairOperation, result.PlatformStatus);
            }
        }
        catch (OperationCanceledException)
        {
            if (_device?.ContainerId == device.ContainerId)
            {
                StatusMessage = CancelledMessage;
            }
        }
        catch (Exception failure)
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.DeviceUnpairFailedFormat,
                failure.Message);
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, UnpairOperation, failure.Message);
        }
        finally
        {
            _attemptCancellation = null;
            Activity = ConnectionActivity.Idle;
        }
    }

    private Task CancelAttemptAsync()
    {
        _attemptCancellation?.Cancel();

        return Task.CompletedTask;
    }

    private async Task WaitForAwaitedStateAsync(CancellationToken cancellationToken)
    {
        Task reached = _awaitedStateReached!.Task;
        Task timeout = Task.Delay(ConnectionTimeout, _timeProvider, cancellationToken);
        Task first = await Task.WhenAny(reached, timeout);

        cancellationToken.ThrowIfCancellationRequested();

        if (first != reached)
        {
            throw new TimeoutException();
        }
    }

    private void RaiseCommandStates()
    {
        _connectCommand.RaiseCanExecuteChanged();
        _disconnectCommand.RaiseCanExecuteChanged();
        _cancelCommand.RaiseCanExecuteChanged();
        _unpairCommand.RaiseCanExecuteChanged();
        _confirmUnpairCommand.RaiseCanExecuteChanged();
        _cancelUnpairCommand.RaiseCanExecuteChanged();
    }
}
