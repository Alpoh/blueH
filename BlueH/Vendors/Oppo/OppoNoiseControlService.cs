using System.IO;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using BlueH.Diagnostics;
using BlueH.Services;

namespace BlueH.Vendors.Oppo;

public sealed class OppoNoiseControlService : INoiseControlService
{
    public static readonly Guid VendorServiceUuid = Guid.Parse("0000079a-d102-11e1-9b23-00025b00a5a5");

    private const string OpenOperation = "Open OPPO channel";
    private const string NoServiceDetail = "no OPPO vendor service for the container";
    private const string ServiceUnavailableDetail = "the OPPO vendor service could not be opened";

    private static readonly string[] RequestedProperties = [DevicePropertyReader.ServiceContainerIdProperty];

    private readonly IDiagnosticsLog _diagnosticsLog;
    private readonly TimeProvider _timeProvider;

    public OppoNoiseControlService(IDiagnosticsLog diagnosticsLog, TimeProvider timeProvider)
    {
        _diagnosticsLog = diagnosticsLog;
        _timeProvider = timeProvider;
    }

    public async Task<INoiseControlSession?> OpenAsync(Guid containerId, CancellationToken cancellationToken)
    {
        try
        {
            string? serviceId = await FindServiceIdAsync(containerId, cancellationToken);

            if (serviceId is null)
            {
                _diagnosticsLog.Record(DiagnosticOutcome.Information, OpenOperation, NoServiceDetail);

                return null;
            }

            OppoNoiseControlSession session = await ConnectAsync(serviceId, cancellationToken);
            _diagnosticsLog.Record(DiagnosticOutcome.Success, OpenOperation, serviceId);

            return session;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _diagnosticsLog.Record(DiagnosticOutcome.Failure, OpenOperation, failure.Message);

            throw;
        }
    }

    private static async Task<string?> FindServiceIdAsync(Guid containerId, CancellationToken cancellationToken)
    {
        string selector = RfcommDeviceService.GetDeviceSelector(RfcommServiceId.FromUuid(VendorServiceUuid));
        DeviceInformationCollection services = await DeviceInformation
            .FindAllAsync(selector, RequestedProperties)
            .AsTask(cancellationToken);

        return services
            .FirstOrDefault(service => DevicePropertyReader.ReadServiceContainerId(service.Properties) == containerId)
            ?.Id;
    }

    private async Task<OppoNoiseControlSession> ConnectAsync(string serviceId, CancellationToken cancellationToken)
    {
        RfcommDeviceService service = await RfcommDeviceService.FromIdAsync(serviceId).AsTask(cancellationToken)
            ?? throw new IOException(ServiceUnavailableDetail);
        StreamSocket socket = new();

        try
        {
            DeviceAccessStatus access = await service.RequestAccessAsync().AsTask(cancellationToken);

            if (access != DeviceAccessStatus.Allowed)
            {
                throw new UnauthorizedAccessException(access.ToString());
            }

            await socket
                .ConnectAsync(
                    service.ConnectionHostName,
                    service.ConnectionServiceName,
                    SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication)
                .AsTask(cancellationToken);
        }
        catch
        {
            socket.Dispose();
            service.Dispose();

            throw;
        }

        RfcommVendorChannel channel = new(service, socket);
        OppoNoiseControlSession session = new(channel, _diagnosticsLog, _timeProvider);
        channel.StartReading();

        return session;
    }
}
