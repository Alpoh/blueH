using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using BlueH.Diagnostics;
using BlueH.Models;

namespace BlueH.Services;

public sealed class GattBatteryService : IBatteryService
{
    private const string ReadBatteryOperation = "Read battery over GATT";
    private const string NoLowEnergyDeviceDetail = "the container has no paired Bluetooth LE device";

    private readonly IDiagnosticsLog _diagnosticsLog;

    public GattBatteryService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public async Task<BatteryCharge> ReadChargeAsync(Guid containerId, CancellationToken cancellationToken)
    {
        string? lowEnergyId = await FindLowEnergyDeviceIdAsync(containerId, cancellationToken);

        if (lowEnergyId is null)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ReadBatteryOperation, NoLowEnergyDeviceDetail);

            return BatteryCharge.Unknown;
        }

        using BluetoothLEDevice? device = await BluetoothLEDevice.FromIdAsync(lowEnergyId).AsTask(cancellationToken);

        if (device is null)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, ReadBatteryOperation, lowEnergyId);

            return BatteryCharge.Unknown;
        }

        GattDeviceServicesResult services = await device
            .GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);

        if (services.Status != GattCommunicationStatus.Success)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ReadBatteryOperation, services.Status.ToString());

            return BatteryCharge.Unknown;
        }

        foreach (GattDeviceService service in services.Services)
        {
            BatteryCharge charge = await ReadFromServiceAsync(service, cancellationToken);

            if (charge.IsKnown)
            {
                return charge;
            }
        }

        return BatteryCharge.Unknown;
    }

    private static async Task<string?> FindLowEnergyDeviceIdAsync(Guid containerId, CancellationToken cancellationToken)
    {
        DeviceInformationCollection candidates = await DeviceInformation
            .FindAllAsync(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                [DevicePropertyReader.ContainerIdProperty])
            .AsTask(cancellationToken);

        return candidates
            .FirstOrDefault(candidate => DevicePropertyReader.ReadContainerId(candidate.Properties) == containerId)
            ?.Id;
    }

    private async Task<BatteryCharge> ReadFromServiceAsync(
        GattDeviceService service,
        CancellationToken cancellationToken)
    {
        GattCharacteristicsResult characteristics = await service
            .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached)
            .AsTask(cancellationToken);

        if (characteristics.Status != GattCommunicationStatus.Success)
        {
            _diagnosticsLog.Record(
                DiagnosticOutcome.Information,
                ReadBatteryOperation,
                characteristics.Status.ToString());

            return BatteryCharge.Unknown;
        }

        foreach (GattCharacteristic characteristic in characteristics.Characteristics)
        {
            GattReadResult read = await characteristic
                .ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);

            if (read.Status != GattCommunicationStatus.Success || read.Value is null || read.Value.Length == 0)
            {
                continue;
            }

            using DataReader reader = DataReader.FromBuffer(read.Value);

            return BatteryCharge.FromPercent(reader.ReadByte());
        }

        return BatteryCharge.Unknown;
    }
}
