using System.Globalization;
using BlueH.Diagnostics;
using BlueH.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BlueH.Services;

public sealed class WinRtBluetoothDiscoveryWatcher : IBluetoothDiscoveryWatcher
{
    private const string StartOperation = "Start Bluetooth discovery";
    private const string StopOperation = "Stop Bluetooth discovery";
    private const string SkipOperation = "Skip discovered non-audio device";

    private static readonly string[] RequestedProperties = [DevicePropertyReader.MajorClassProperty, DevicePropertyReader.MinorClassProperty];

    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly Dictionary<string, DeviceInformation> _known = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    private DeviceWatcher? _watcher;

    public WinRtBluetoothDiscoveryWatcher(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public event EventHandler<DiscoveredDevice>? DeviceFound;

    public event EventHandler<string>? DeviceLost;

    public void Start()
    {
        if (_watcher is not null)
        {
            return;
        }

        _watcher = DeviceInformation.CreateWatcher(
            BluetoothDevice.GetDeviceSelectorFromPairingState(false),
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);
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

        Announce(device);
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

        Announce(device);
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_gate)
        {
            _known.Remove(update.Id);
        }

        DeviceLost?.Invoke(this, update.Id);
    }

    private void Announce(DeviceInformation device)
    {
        uint? majorClass = DevicePropertyReader.ReadMajorClass(device.Properties);
        uint? minorClass = DevicePropertyReader.ReadMinorClass(device.Properties);

        if (!PairingStatusMapper.IsAudioCandidate(majorClass, minorClass) || !device.Pairing.CanPair)
        {
            _diagnosticsLog.Record(
                DiagnosticOutcome.Information,
                SkipOperation,
                string.Create(CultureInfo.InvariantCulture, $"{device.Name} major={majorClass} minor={minorClass} canPair={device.Pairing.CanPair}"));

            return;
        }

        DeviceFound?.Invoke(this, new DiscoveredDevice(device.Id, device.Name));
    }
}
