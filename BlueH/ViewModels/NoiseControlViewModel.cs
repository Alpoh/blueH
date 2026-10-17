using System.Globalization;
using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using BlueH.Themes;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class NoiseControlViewModel : ViewModelBase, IDisposable
{
    private const string OpenOperation = "Open noise control";
    private const string ChangeOperation = "Change noise control mode";
    private const string RestoreOperation = "Restore noise control preference";
    private const string StorePreferenceOperation = "Store noise control preference";
    private const string CloseOperation = "Close noise control";

    private readonly INoiseControlService _service;
    private readonly ISettingsStore _settingsStore;
    private readonly IUiScheduler _uiScheduler;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly AsyncRelayCommand _retryCommand;

    private BluetoothAudioDevice? _device;
    private Guid? _targetContainer;
    private INoiseControlSession? _session;
    private CancellationTokenSource? _sessionCancellation;
    private int _generation;
    private bool _isSettled;
    private bool _isSuspended;
    private NoiseControlStatus _status;
    private NoiseControlMode? _mode;
    private bool _isChangingMode;
    private string? _errorMessage;

    public NoiseControlViewModel(
        INoiseControlService service,
        ISettingsStore settingsStore,
        IUiScheduler uiScheduler,
        IDiagnosticsLog diagnosticsLog)
    {
        _service = service;
        _settingsStore = settingsStore;
        _uiScheduler = uiScheduler;
        _diagnosticsLog = diagnosticsLog;
        _retryCommand = new AsyncRelayCommand(_ => SynchronizeAsync(reopen: true), _ => _status == NoiseControlStatus.Failed);
        Options =
        [
            CreateOption(NoiseControlMode.Off, AppResources.NoiseControlOffLabel, Glyphs.NoiseControlOff),
            CreateOption(NoiseControlMode.Transparency, AppResources.NoiseControlTransparencyLabel, Glyphs.Transparency),
            CreateOption(NoiseControlMode.NoiseCancelling, AppResources.NoiseControlNoiseCancellingLabel, Glyphs.NoiseCancelling)
        ];
    }

    public BluetoothAudioDevice? Device
    {
        get => _device;
        set
        {
            if (SetField(ref _device, value))
            {
                _ = SynchronizeAsync(reopen: false);
            }
        }
    }

    public bool IsSuspended
    {
        get => _isSuspended;
        set
        {
            if (SetField(ref _isSuspended, value))
            {
                _ = SynchronizeAsync(reopen: false);
            }
        }
    }

    public IReadOnlyList<NoiseControlOptionViewModel> Options { get; }

    public NoiseControlStatus Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(IsShown));
                OnPropertyChanged(nameof(IsConnecting));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsModeUnknown));
                RaiseCommandStates();
            }
        }
    }

    public bool IsShown => _status is NoiseControlStatus.Connecting or NoiseControlStatus.Ready or NoiseControlStatus.Failed;

    public bool IsConnecting => _status == NoiseControlStatus.Connecting;

    public bool IsReady => _status == NoiseControlStatus.Ready;

    public bool IsFailed => _status == NoiseControlStatus.Failed;

    public NoiseControlMode? Mode => _mode;

    public bool IsModeUnknown => _status == NoiseControlStatus.Ready && _mode is null;

    public bool IsChangingMode
    {
        get => _isChangingMode;
        private set
        {
            if (SetField(ref _isChangingMode, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => _errorMessage is not null;

    public AsyncRelayCommand RetryCommand => _retryCommand;

    public string Header => AppResources.NoiseControlHeader;

    public string ConnectingMessage => AppResources.NoiseControlConnectingMessage;

    public string UnknownModeMessage => AppResources.NoiseControlUnknownModeMessage;

    public string RetryLabel => AppResources.NoiseControlRetryLabel;

    public void Dispose()
    {
        _generation++;
        _targetContainer = null;
        _sessionCancellation?.Cancel();
        _ = CloseSessionAsync();
    }

    private NoiseControlOptionViewModel CreateOption(NoiseControlMode mode, string label, string glyph)
    {
        return new NoiseControlOptionViewModel(
            mode,
            label,
            glyph,
            SelectModeAsync,
            () => _status == NoiseControlStatus.Ready && !_isChangingMode);
    }

    private async Task SynchronizeAsync(bool reopen)
    {
        Guid? wanted = !_isSuspended && _device is { IsConnected: true } device ? device.ContainerId : null;

        if (!reopen && wanted == _targetContainer)
        {
            return;
        }

        _targetContainer = wanted;
        int generation = ++_generation;
        _sessionCancellation?.Cancel();
        _sessionCancellation?.Dispose();
        _sessionCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _sessionCancellation.Token;

        await CloseSessionAsync();
        _isSettled = false;
        ErrorMessage = null;
        ShowMode(null);

        if (wanted is not Guid containerId)
        {
            Status = NoiseControlStatus.Unavailable;

            return;
        }

        Status = NoiseControlStatus.Connecting;

        try
        {
            INoiseControlSession? session = await _service.OpenAsync(containerId, cancellationToken);

            if (generation != _generation)
            {
                if (session is not null)
                {
                    await session.DisposeAsync();
                }

                return;
            }

            if (session is null)
            {
                Status = NoiseControlStatus.Unsupported;

                return;
            }

            Attach(session);
            NoiseControlMode? reported = await session.ReadModeAsync(cancellationToken);

            if (generation != _generation)
            {
                return;
            }

            ShowMode(reported);
            Status = NoiseControlStatus.Ready;
            await RestorePreferenceAsync(containerId, session, generation, cancellationToken);
            _isSettled = generation == _generation;
        }
        catch (Exception failure)
        {
            if (generation != _generation)
            {
                return;
            }

            _diagnosticsLog.Record(DiagnosticOutcome.Failure, OpenOperation, failure.Message);
            await CloseSessionAsync();
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, AppResources.NoiseControlOpenFailedFormat, failure.Message);
            Status = NoiseControlStatus.Failed;
        }
    }

    private async Task RestorePreferenceAsync(
        Guid containerId,
        INoiseControlSession session,
        int generation,
        CancellationToken cancellationToken)
    {
        if (StoredMode(containerId) is not { } stored || stored == _mode)
        {
            return;
        }

        try
        {
            await session.SetModeAsync(stored, cancellationToken);

            if (generation == _generation)
            {
                ShowMode(stored);
                _diagnosticsLog.Record(DiagnosticOutcome.Success, RestoreOperation, stored.ToString());
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            if (generation != _generation)
            {
                return;
            }

            _diagnosticsLog.Record(DiagnosticOutcome.Failure, RestoreOperation, failure.Message);
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, AppResources.NoiseControlRestoreFailedFormat, failure.Message);
        }
    }

    private async Task SelectModeAsync(NoiseControlMode mode)
    {
        if (_session is not { } session || _targetContainer is not Guid containerId || mode == _mode)
        {
            return;
        }

        IsChangingMode = true;
        ErrorMessage = null;

        try
        {
            await session.SetModeAsync(mode, _sessionCancellation?.Token ?? CancellationToken.None);
            StoreMode(containerId, mode);

            if (ReferenceEquals(session, _session))
            {
                ShowMode(mode);
            }
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ChangeOperation, failure.Message);

            if (ReferenceEquals(session, _session))
            {
                ErrorMessage = string.Format(CultureInfo.CurrentCulture, AppResources.NoiseControlChangeFailedFormat, failure.Message);
                ShowMode(_mode);
            }
        }
        finally
        {
            IsChangingMode = false;
        }
    }

    private void Attach(INoiseControlSession session)
    {
        _session = session;
        session.ModeReported += OnModeReported;
        session.Closed += OnSessionClosed;
    }

    private async Task CloseSessionAsync()
    {
        if (_session is not { } session)
        {
            return;
        }

        _session = null;
        session.ModeReported -= OnModeReported;
        session.Closed -= OnSessionClosed;

        try
        {
            await session.DisposeAsync();
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, CloseOperation, failure.Message);
        }
    }

    private void OnModeReported(object? sender, NoiseControlMode mode)
    {
        _uiScheduler.Post(() =>
        {
            if (!ReferenceEquals(sender, _session))
            {
                return;
            }

            ShowMode(mode);

            if (_isSettled && _targetContainer is Guid containerId)
            {
                StoreMode(containerId, mode);
            }
        });
    }

    private void OnSessionClosed(object? sender, EventArgs e)
    {
        _uiScheduler.Post(() =>
        {
            if (!ReferenceEquals(sender, _session))
            {
                return;
            }

            _generation++;
            _ = CloseSessionAsync();
            ShowMode(null);
            ErrorMessage = AppResources.NoiseControlChannelLostMessage;
            Status = NoiseControlStatus.Failed;
        });
    }

    private void ShowMode(NoiseControlMode? mode)
    {
        _mode = mode;
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsModeUnknown));

        foreach (NoiseControlOptionViewModel option in Options)
        {
            option.ShowSelection(option.Mode == mode);
        }
    }

    private NoiseControlMode? StoredMode(Guid containerId)
    {
        return _settingsStore.Load().Settings.NoiseControlModes?.TryGetValue(containerId, out NoiseControlMode stored) == true
            ? stored
            : null;
    }

    private void StoreMode(Guid containerId, NoiseControlMode mode)
    {
        if (StoredMode(containerId) == mode)
        {
            return;
        }

        try
        {
            AppSettings current = _settingsStore.Load().Settings;
            Dictionary<Guid, NoiseControlMode> modes = current.NoiseControlModes is null
                ? []
                : new Dictionary<Guid, NoiseControlMode>(current.NoiseControlModes);
            modes[containerId] = mode;
            _settingsStore.Save(current with { NoiseControlModes = modes });
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, StorePreferenceOperation, failure.Message);
        }
    }

    private void RaiseCommandStates()
    {
        _retryCommand.RaiseCanExecuteChanged();

        foreach (NoiseControlOptionViewModel option in Options)
        {
            option.RaiseCanExecuteChanged();
        }
    }
}
