using System.Globalization;
using Windows.Devices.Enumeration;
using BlueH.Diagnostics;
using BlueH.Models;

namespace BlueH.Services;

public sealed class DevicePropertyBatteryService : IBatteryService
{
    private const string ReadBatteryOperation = "Read battery from device properties";
    private const string NoBatteryDetail = "no object in the container carries a battery property";
    private const string OutOfRangeDetail = "reported percentage is out of range";

    private static readonly string[] RequestedProperties = [DevicePropertyReader.BatteryPercentProperty];

    private readonly IDiagnosticsLog _diagnosticsLog;

    public DevicePropertyBatteryService(IDiagnosticsLog diagnosticsLog)
    {
        _diagnosticsLog = diagnosticsLog;
    }

    public async Task<BatteryCharge> ReadChargeAsync(Guid containerId, CancellationToken cancellationToken)
    {
        DeviceInformationCollection devices = await DeviceInformation
            .FindAllAsync(ContainerSelector(containerId), RequestedProperties, DeviceInformationKind.Device)
            .AsTask(cancellationToken);

        int? rawPercent = BatteryPropertyScanner.FindRawPercent(devices.Select(device => device.Properties));

        if (rawPercent is null)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Information, ReadBatteryOperation, NoBatteryDetail);

            return BatteryCharge.Unknown;
        }

        BatteryCharge charge = BatteryCharge.FromPercent(rawPercent.Value);

        _diagnosticsLog.Record(
            charge.IsKnown ? DiagnosticOutcome.Success : DiagnosticOutcome.Failure,
            ReadBatteryOperation,
            charge.IsKnown
                ? rawPercent.Value.ToString(CultureInfo.InvariantCulture)
                : OutOfRangeDetail);

        return charge;
    }

    private static string ContainerSelector(Guid containerId)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"System.Devices.ContainerId:=\"{containerId:B}\"");
    }
}
