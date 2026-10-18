using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using BlueH.Diagnostics;

namespace BlueH.Services;

public sealed class WinRtAudioEndpointChangeWatcher : IAudioEndpointChangeWatcher
{
    private const string StartOperation = "Start audio endpoint watcher";
    private const string StopOperation = "Stop audio endpoint watcher";
    private const string FollowLevelsOperation = "Follow endpoint levels";

    private readonly IDiagnosticsLog _diagnosticsLog;

    private DeviceWatcher? _watcher;
    private CoreAudioLevelNotifications? _levels;
    private volatile bool _initialEnumerationCompleted;

    public WinRtAudioEndpointChangeWatcher(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public event EventHandler? Changed;

    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        _initialEnumerationCompleted = false;
        _levels = new CoreAudioLevelNotifications(_diagnosticsLog);
        _levels.LevelsChanged += OnLevelsChanged;
        _watcher = DeviceInformation.CreateWatcher(MediaDevice.GetAudioRenderSelector());
        _watcher.Added += OnAdded;
        _watcher.Updated += OnUpdated;
        _watcher.Removed += OnRemoved;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
        _watcher.Start();

        _diagnosticsLog.Record(DiagnosticOutcome.Information, StartOperation);
    }

    public void Stop()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Added -= OnAdded;
        _watcher.Updated -= OnUpdated;
        _watcher.Removed -= OnRemoved;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;

        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        _watcher = null;

        if (_levels is not null)
        {
            _levels.LevelsChanged -= OnLevelsChanged;
            _levels.Dispose();
            _levels = null;
        }

        _diagnosticsLog.Record(DiagnosticOutcome.Information, StopOperation);
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation endpoint)
    {
        FollowLevelsAndRaiseChanged();
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        RaiseChangedAfterInitialEnumeration();
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        FollowLevelsAndRaiseChanged();
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        _initialEnumerationCompleted = true;
        FollowLevelsAndRaiseChanged();
    }

    private void OnLevelsChanged(object? sender, EventArgs e)
    {
        RaiseChangedAfterInitialEnumeration();
    }

    private void FollowLevelsAndRaiseChanged()
    {
        if (!_initialEnumerationCompleted)
        {
            return;
        }

        try
        {
            _levels?.FollowActiveEndpoints();
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, FollowLevelsOperation, failure.Message);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseChangedAfterInitialEnumeration()
    {
        if (_initialEnumerationCompleted)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
