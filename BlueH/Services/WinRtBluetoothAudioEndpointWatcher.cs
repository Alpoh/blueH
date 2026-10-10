using Windows.Devices.Enumeration;
using BlueH.Diagnostics;
using BlueH.Models;

namespace BlueH.Services;

public sealed class WinRtBluetoothAudioEndpointWatcher : IBluetoothAudioEndpointWatcher
{
    private const string StartOperation = "Start Bluetooth endpoint watcher";
    private const string StopOperation = "Stop Bluetooth endpoint watcher";

    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly Dictionary<string, DeviceInformation> _known = [];
    private readonly Lock _gate = new();

    private DeviceWatcher? _watcher;

    public WinRtBluetoothAudioEndpointWatcher(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public event EventHandler<BluetoothAudioEndpoint>? EndpointAdded;

    public event EventHandler<BluetoothAudioEndpoint>? EndpointUpdated;

    public event EventHandler<string>? EndpointRemoved;

    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        _watcher = DeviceInformation.CreateWatcher(
            WinRtBluetoothDeviceService.ClassicAudioSelector,
            WinRtBluetoothDeviceService.RequestedProperties);

        _watcher.Added += OnAdded;
        _watcher.Updated += OnUpdated;
        _watcher.Removed += OnRemoved;
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

        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        _watcher = null;

        lock (_gate)
        {
            _known.Clear();
        }

        _diagnosticsLog.Record(DiagnosticOutcome.Information, StopOperation);
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        lock (_gate)
        {
            _known[device.Id] = device;
        }

        EndpointAdded?.Invoke(this, WinRtBluetoothDeviceService.ToEndpoint(device));
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        DeviceInformation? device;

        lock (_gate)
        {
            if (!_known.TryGetValue(update.Id, out device))
            {
                return;
            }

            device.Update(update);
        }

        EndpointUpdated?.Invoke(this, WinRtBluetoothDeviceService.ToEndpoint(device));
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_gate)
        {
            _known.Remove(update.Id);
        }

        EndpointRemoved?.Invoke(this, update.Id);
    }
}
