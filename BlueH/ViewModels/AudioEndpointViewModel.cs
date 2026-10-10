using System.Windows.Input;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;

namespace BlueH.ViewModels;

public sealed class AudioEndpointViewModel : ViewModelBase
{
    private const string ApplyVolumeOperation = "Apply endpoint volume";
    private const string ToggleMuteOperation = "Toggle endpoint mute";
    private const string MakeDefaultOperation = "Make endpoint default";

    private readonly IAudioEndpointService _endpointService;
    private readonly IDiagnosticsLog _diagnosticsLog;

    private string _name;
    private int _volumePercent;
    private bool? _isMuted;
    private bool _isDefaultForPlayback;
    private bool _isDefaultForCommunications;
    private int? _pendingVolumePercent;
    private bool _volumeApplyInFlight;

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
    }

    public string MmDeviceId { get; }

    public Guid ContainerId { get; }

    public ICommand ToggleMuteCommand { get; }

    public ICommand MakeDefaultForPlaybackCommand { get; }

    public ICommand MakeDefaultForCommunicationsCommand { get; }

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
