using BlueH.Models;

namespace BlueH.Services;

public interface IBluetoothDeviceService
{
    Task<IReadOnlyList<BluetoothAudioEndpoint>> FindPairedAudioEndpointsAsync(CancellationToken cancellationToken);

    IBluetoothAudioEndpointWatcher CreateEndpointWatcher();
}
