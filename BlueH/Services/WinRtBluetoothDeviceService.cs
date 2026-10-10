using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BlueH.Diagnostics;
using BlueH.Models;

namespace BlueH.Services;

public sealed class WinRtBluetoothDeviceService : IBluetoothDeviceService
{
    private const string AudioVideoMajorClassClause = " AND System.Devices.Aep.Bluetooth.Cod.Major:=4";
    private const string FindEndpointsOperation = "Enumerate Bluetooth audio endpoints";
    private const string DiscardedEndpointOperation = "Discard endpoint without a container id";

    internal static readonly string[] RequestedProperties =
    [
        DevicePropertyReader.ContainerIdProperty,
        DevicePropertyReader.IsConnectedProperty,
        DevicePropertyReader.MinorClassProperty
    ];

    private readonly IDiagnosticsLog _diagnosticsLog;

    public WinRtBluetoothDeviceService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    internal static string ClassicAudioSelector =>
        BluetoothDevice.GetDeviceSelectorFromPairingState(true) + AudioVideoMajorClassClause;

    public async Task<IReadOnlyList<BluetoothAudioEndpoint>> FindPairedAudioEndpointsAsync(
        CancellationToken cancellationToken)
    {
        BluetoothAudioEndpoint[] classic = await FindAsync(ClassicAudioSelector, cancellationToken);
        HashSet<Guid> audioContainers = [.. classic.Select(endpoint => endpoint.ContainerId)];

        BluetoothAudioEndpoint[] lowEnergy = await FindAsync(
            BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
            cancellationToken);

        BluetoothAudioEndpoint[] endpoints =
        [
            .. classic,
            .. lowEnergy.Where(endpoint => audioContainers.Contains(endpoint.ContainerId))
        ];

        _diagnosticsLog.Record(
            DiagnosticOutcome.Success,
            FindEndpointsOperation,
            FormatEndpointSummary(classic.Length, endpoints.Length - classic.Length));

        return endpoints;
    }

    public IBluetoothAudioEndpointWatcher CreateEndpointWatcher()
    {
        return new WinRtBluetoothAudioEndpointWatcher(_diagnosticsLog);
    }

    internal static BluetoothAudioEndpoint ToEndpoint(DeviceInformation device)
    {
        return new BluetoothAudioEndpoint(
            device.Id,
            DevicePropertyReader.ReadContainerId(device.Properties),
            device.Name,
            DevicePropertyReader.ReadConnectionState(device.Properties),
            DevicePropertyReader.ReadMinorClass(device.Properties));
    }

    private static string FormatEndpointSummary(int classicCount, int lowEnergyCount)
    {
        return $"classic={classicCount} lowEnergy={lowEnergyCount}";
    }

    private async Task<BluetoothAudioEndpoint[]> FindAsync(string selector, CancellationToken cancellationToken)
    {
        DeviceInformationCollection devices = await DeviceInformation
            .FindAllAsync(selector, RequestedProperties)
            .AsTask(cancellationToken);

        List<BluetoothAudioEndpoint> endpoints = [];

        foreach (DeviceInformation device in devices)
        {
            BluetoothAudioEndpoint endpoint = ToEndpoint(device);

            if (endpoint.ContainerId == Guid.Empty)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Failure, DiscardedEndpointOperation, endpoint.Id);
                continue;
            }

            endpoints.Add(endpoint);
        }

        return [.. endpoints];
    }
}
