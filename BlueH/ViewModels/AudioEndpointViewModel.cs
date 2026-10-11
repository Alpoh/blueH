using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class AudioEndpointViewModel : ViewModelBase, INotifyDataErrorInfo
{
    private const string ApplyVolumeOperation = "Apply endpoint volume";
    private const string ToggleMuteOperation = "Toggle endpoint mute";
    private const string MakeDefaultOperation = "Make endpoint default";

    private readonly IAudioEndpointService _endpointService;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    private string _name;
    private int _volumePercent;
    private bool? _isMuted;
    private bool _isDefaultForPlayback;
    private bool _isDefaultForCommunications;
    private int? _pendingVolumePercent;
    private bool _volumeApplyInFlight;
    private bool _isEditingVolume;
    private string _volumeInput = string.Empty;

    public AudioEndpointViewModel(
        AudioEndpoint endpoint,
        IAudioEndpointService endpointService,
        IDiagnosticsLog diagnosticsLog)
    {
        _endpointService = endpointService;
        _diagnosticsLog = diagnosticsLog;
        MmDeviceId = endpoint.MmDeviceId;
        ContainerId = endpoint.ContainerId;
        _name = endpoint.Name;
        _volumePercent = endpoint.VolumePercent ?? VolumeScale.MinimumPercent;
        _isMuted = endpoint.IsMuted;
        _isDefaultForPlayback = endpoint.IsDefaultForPlayback;
        _isDefaultForCommunications = endpoint.IsDefaultForCommunications;

        ToggleMuteCommand = new AsyncRelayCommand(_ => ToggleMuteAsync());
        MakeDefaultForPlaybackCommand = new AsyncRelayCommand(
            _ => MakeDefaultAsync(AudioEndpointRole.Playback),
            _ => !_isDefaultForPlayback);
        MakeDefaultForCommunicationsCommand = new AsyncRelayCommand(
            _ => MakeDefaultAsync(AudioEndpointRole.Communications),
            _ => !_isDefaultForCommunications);
        BeginEditVolumeCommand = new RelayCommand(_ => BeginEditVolume());
        CommitVolumeInputCommand = new RelayCommand(_ => CommitVolumeInput(), _ => _isEditingVolume);
        CancelVolumeEditCommand = new RelayCommand(_ => CancelVolumeEdit(), _ => _isEditingVolume);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string MmDeviceId { get; }

    public Guid ContainerId { get; }

    public ICommand ToggleMuteCommand { get; }

    public ICommand MakeDefaultForPlaybackCommand { get; }

    public ICommand MakeDefaultForCommunicationsCommand { get; }

    public ICommand BeginEditVolumeCommand { get; }

    public ICommand CommitVolumeInputCommand { get; }

    public ICommand CancelVolumeEditCommand { get; }

    public bool HasErrors => _errors.Count > 0;

    public string Name
    {
        get => _name;
        private set => SetField(ref _name, value);
    }

    public bool? IsMuted
    {
        get => _isMuted;
        private set => SetField(ref _isMuted, value);
    }

    public bool IsDefaultForPlayback
    {
        get => _isDefaultForPlayback;
        private set => SetField(ref _isDefaultForPlayback, value);
    }

    public bool IsDefaultForCommunications
    {
        get => _isDefaultForCommunications;
        private set => SetField(ref _isDefaultForCommunications, value);
    }

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (SetField(ref _volumePercent, value))
            {
                BeginApplyVolume(value);
            }
        }
    }

    public bool IsEditingVolume
    {
        get => _isEditingVolume;
        private set => SetField(ref _isEditingVolume, value);
    }

    public string VolumeInput
    {
        get => _volumeInput;
        set
        {
            if (SetField(ref _volumeInput, value))
            {
                ValidateVolumeInput();
            }
        }
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.ToArray();
        }

        return _errors.TryGetValue(propertyName, out string? message)
            ? new[] { message }
            : Array.Empty<string>();
    }

    public void Update(AudioEndpoint endpoint)
    {
        Name = endpoint.Name;
        IsMuted = endpoint.IsMuted;
        IsDefaultForPlayback = endpoint.IsDefaultForPlayback;
        IsDefaultForCommunications = endpoint.IsDefaultForCommunications;

        if (!_volumeApplyInFlight && endpoint.VolumePercent is int volumePercent)
        {
            SetField(ref _volumePercent, volumePercent, nameof(VolumePercent));
        }
    }

    private void BeginEditVolume()
    {
        VolumeInput = _volumePercent.ToString(CultureInfo.CurrentCulture);
        IsEditingVolume = true;
    }

    private void CommitVolumeInput()
    {
        VolumePercentInput input = VolumePercentInput.Parse(_volumeInput, CultureInfo.CurrentCulture);

        if (!input.IsValid)
        {
            SetVolumeInputError(input.Status);

            return;
        }

        IsEditingVolume = false;
        ClearVolumeInputError();
        VolumePercent = input.VolumePercent;
    }

    private void CancelVolumeEdit()
    {
        IsEditingVolume = false;
        ClearVolumeInputError();
    }

    private void ValidateVolumeInput()
    {
        VolumePercentInput input = VolumePercentInput.Parse(_volumeInput, CultureInfo.CurrentCulture);

        if (input.IsValid)
        {
            ClearVolumeInputError();

            return;
        }

        SetVolumeInputError(input.Status);
    }

    private void SetVolumeInputError(VolumeInputStatus status)
    {
        _errors[nameof(VolumeInput)] = status == VolumeInputStatus.OutOfRange
            ? string.Format(
                CultureInfo.CurrentCulture,
                AppResources.EndpointVolumeOutOfRangeFormat,
                VolumeScale.MinimumPercent,
                VolumeScale.MaximumPercent)
            : AppResources.EndpointVolumeNotANumberMessage;

        RaiseErrorsChanged();
    }

    private void ClearVolumeInputError()
    {
        if (_errors.Remove(nameof(VolumeInput)))
        {
            RaiseErrorsChanged();
        }
    }

    private void RaiseErrorsChanged()
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(VolumeInput)));
        OnPropertyChanged(nameof(HasErrors));
    }

    private void BeginApplyVolume(int volumePercent)
    {
        _pendingVolumePercent = volumePercent;

        if (_volumeApplyInFlight)
        {
            return;
        }

        _ = ApplyPendingVolumeAsync();
    }

    private async Task ApplyPendingVolumeAsync()
    {
        _volumeApplyInFlight = true;

        try
        {
            while (_pendingVolumePercent is int volumePercent)
            {
                _pendingVolumePercent = null;

                await _endpointService.SetVolumePercentAsync(MmDeviceId, volumePercent, CancellationToken.None);
            }
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ApplyVolumeOperation, failure.Message);
        }
        finally
        {
            _volumeApplyInFlight = false;
        }
    }

    private async Task ToggleMuteAsync()
    {
        bool target = _isMuted != true;

        try
        {
            await _endpointService.SetMutedAsync(MmDeviceId, target, CancellationToken.None);
            IsMuted = target;
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ToggleMuteOperation, failure.Message);
        }
    }

    private async Task MakeDefaultAsync(AudioEndpointRole role)
    {
        try
        {
            await _endpointService.SetDefaultAsync(MmDeviceId, role, CancellationToken.None);
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, MakeDefaultOperation, failure.Message);
        }
    }
}
