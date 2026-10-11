namespace BlueH.Services;

public interface IBluetoothConnectionService
{
    Task ConnectAsync(Guid containerId, CancellationToken cancellationToken);

    Task DisconnectAsync(Guid containerId, CancellationToken cancellationToken);
}
