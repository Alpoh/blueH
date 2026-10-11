using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using BlueH.Diagnostics;
using BlueH.Models;
using BlueH.Services;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class DevicesViewModel : ViewModelBase, IDisposable
{
    public static readonly TimeSpan DefaultBatteryPollingInterval = TimeSpan.FromMinutes(1);

    private const string FindDevicesOperation = "Find paired audio devices";
    private const string WatchDevicesOperation = "Watch audio devices";
    private const string SaveSelectionOperation = "Save selected device";
    private const string RefreshBatteriesOperation = "Refresh batteries";

    private readonly IBluetoothDeviceService _deviceService;
    private readonly IBatteryService _batteryService;
    private readonly IUiScheduler _uiScheduler;
    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, BluetoothAudioEndpoint> _endpoints = [];
    private readonly Dictionary<Guid, BatteryCharge> _batteries = [];

    private IBluetoothAudioEndpointWatcher? _watcher;
    private ITimer? _batteryTimer;
    private Guid[] _connectedContainers = [];
    private Guid? _selectedContainerId;
    private string? _errorDetail;
    private bool _batteryRefreshInFlight;

    public DevicesViewModel(
        IBluetoothDeviceService deviceService,
        IBatteryService batteryService,
        IUiScheduler uiScheduler,
        IDiagnosticsLog diagnosticsLog,
        ISettingsStore settingsStore,
        TimeProvider timeProvider)
    {
        _deviceService = deviceService;
        _batteryService = batteryService;
        _uiScheduler = uiScheduler;
        _diagnosticsLog = diagnosticsLog;
        _settingsStore = settingsStore;
        _timeProvider = timeProvider;
        _selectedContainerId = ReadPersistedSelection();
    }

    public ObservableCollection<BluetoothAudioDevice> Devices { get; } = [];

    public Guid? SelectedContainerId
    {
        get => _selectedContainerId;
        set
        {
            if (SetField(ref _selectedContainerId, value))
            {
                PersistSelection(value);
                OnPropertyChanged(nameof(SelectedDevice));
            }
        }
    }

    public BluetoothAudioDevice? SelectedDevice =>
        Devices.FirstOrDefault(device => device.ContainerId == _selectedContainerId);

    public IReadOnlyList<Guid> ConnectedContainerIds => _connectedContainers;

    public bool HasError => _errorDetail is not null;

    public bool HasNoDevices => Devices.Count == 0 && !HasError;

    public string? ErrorDetail
    {
        get => _errorDetail;
        private set
        {
            if (SetField(ref _errorDetail, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(HasNoDevices));
            }
        }
    }

    public string Header => AppResources.DevicesHeader;

    public string EmptyMessage => AppResources.DevicesEmptyMessage;

    public string ErrorMessage => AppResources.DevicesErrorMessage;

    public string NameHeader => AppResources.DeviceNameHeader;

    public string KindHeader => AppResources.DeviceKindHeader;

    public string ConnectionHeader => AppResources.DeviceConnectionHeader;

    public string BatteryHeader => AppResources.DeviceBatteryHeader;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<BluetoothAudioEndpoint> endpoints =
                await _deviceService.FindPairedAudioEndpointsAsync(cancellationToken);

            _endpoints.Clear();

            foreach (BluetoothAudioEndpoint endpoint in endpoints)
            {
                _endpoints[endpoint.Id] = endpoint;
            }

            ErrorDetail = null;
            ApplyEndpoints();

            _diagnosticsLog.Record(
                DiagnosticOutcome.Success,
                FindDevicesOperation,
                Devices.Count.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            ErrorDetail = failure.Message;
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, FindDevicesOperation, failure.Message);
        }
    }

    public async Task RefreshBatteriesAsync(CancellationToken cancellationToken)
    {
        if (_batteryRefreshInFlight)
        {
            return;
        }

        _batteryRefreshInFlight = true;

        try
        {
            foreach (Guid containerId in _connectedContainers)
            {
                BatteryCharge charge = await _batteryService.ReadChargeAsync(containerId, cancellationToken);

                _uiScheduler.Post(() => ApplyBattery(containerId, charge));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception failure)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, RefreshBatteriesOperation, failure.Message);
        }
        finally
        {
            _batteryRefreshInFlight = false;
        }
    }

    public void StartWatching()
    {
        if (_watcher is not null)
        {
            return;
        }

        _watcher = _deviceService.CreateEndpointWatcher();
        _watcher.EndpointAdded += OnEndpointAdded;
        _watcher.EndpointUpdated += OnEndpointUpdated;
        _watcher.EndpointRemoved += OnEndpointRemoved;
        _watcher.Start();

        _diagnosticsLog.Record(DiagnosticOutcome.Information, WatchDevicesOperation);
    }

    public void StartBatteryPolling(TimeSpan interval)
    {
        _batteryTimer ??= _timeProvider.CreateTimer(
            _ => BeginBatteryRefresh(),
            state: null,
            interval,
            interval);
    }

    public void Dispose()
    {
        _batteryTimer?.Dispose();
        _batteryTimer = null;

        if (_watcher is null)
        {
            return;
        }

        _watcher.EndpointAdded -= OnEndpointAdded;
        _watcher.EndpointUpdated -= OnEndpointUpdated;
        _watcher.EndpointRemoved -= OnEndpointRemoved;
        _watcher.Stop();
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnEndpointAdded(object? sender, BluetoothAudioEndpoint endpoint)
    {
        _uiScheduler.Post(() =>
        {
            _endpoints[endpoint.Id] = endpoint;
            ApplyEndpoints();
        });
    }

    private void OnEndpointUpdated(object? sender, BluetoothAudioEndpoint endpoint)
    {
        _uiScheduler.Post(() =>
        {
            if (!_endpoints.ContainsKey(endpoint.Id))
            {
                return;
            }

            _endpoints[endpoint.Id] = endpoint;
            ApplyEndpoints();
        });
    }

    private void OnEndpointRemoved(object? sender, string endpointId)
    {
        _uiScheduler.Post(() =>
        {
            if (_endpoints.Remove(endpointId))
            {
                ApplyEndpoints();
            }
        });
    }

    private void ApplyBattery(Guid containerId, BatteryCharge charge)
    {
        _batteries[containerId] = charge;

        int index = IndexOf(containerId);

        if (index >= 0)
        {
            Devices[index] = Devices[index] with { Battery = charge };
            OnPropertyChanged(nameof(SelectedDevice));
        }
    }

    private void ApplyEndpoints()
    {
        IReadOnlyList<BluetoothAudioDevice> incoming = BluetoothAudioDevice.FromEndpoints(_endpoints.Values);

        for (int index = Devices.Count - 1; index >= 0; index--)
        {
            if (!incoming.Any(device => device.ContainerId == Devices[index].ContainerId))
            {
                Devices.RemoveAt(index);
            }
        }

        foreach (BluetoothAudioDevice device in incoming)
        {
            BluetoothAudioDevice withBattery = device with
            {
                Battery = _batteries.TryGetValue(device.ContainerId, out BatteryCharge charge)
                    ? charge
                    : BatteryCharge.Unknown
            };

            int existing = IndexOf(withBattery.ContainerId);

            if (existing < 0)
            {
                Devices.Add(withBattery);
            }
            else if (Devices[existing] != withBattery)
            {
                Devices[existing] = withBattery;
            }
        }

        OnPropertyChanged(nameof(HasNoDevices));
        OnPropertyChanged(nameof(SelectedDevice));

        UpdateConnectedContainers(incoming);
    }

    private void UpdateConnectedContainers(IReadOnlyList<BluetoothAudioDevice> devices)
    {
        Guid[] previous = _connectedContainers;

        _connectedContainers =
        [
            .. devices
                .Where(device => device.IsConnected == true)
                .Select(device => device.ContainerId)
        ];

        bool connectedContainersChanged = _connectedContainers.Length != previous.Length
            || _connectedContainers.Except(previous).Any();

        if (connectedContainersChanged)
        {
            OnPropertyChanged(nameof(ConnectedContainerIds));
        }

        if (_connectedContainers.Except(previous).Any())
        {
            BeginBatteryRefresh();
        }
    }

    private void BeginBatteryRefresh()
    {
        _ = RefreshBatteriesAsync(CancellationToken.None);
    }

    private int IndexOf(Guid containerId)
    {
        for (int index = 0; index < Devices.Count; index++)
        {
            if (Devices[index].ContainerId == containerId)
            {
                return index;
            }
        }

        return -1;
    }

    private Guid? ReadPersistedSelection()
    {
        string? persisted = _settingsStore.Load().Settings.SelectedDeviceId;

        return Guid.TryParse(persisted, out Guid containerId) ? containerId : null;
    }

    private void PersistSelection(Guid? containerId)
    {
        try
        {
            _settingsStore.Save(new AppSettings(containerId?.ToString()));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, SaveSelectionOperation, failure.Message);
        }
    }
}
