using BlueH.Models;

namespace BlueH.Services;

public interface IBluetoothPairingService
{
    IBluetoothDiscoveryWatcher CreateDiscoveryWatcher();

    Task<PairingResult> PairAsync(
        string endpointId,
        Func<PairingChallenge, CancellationToken, Task<PairingResponse>> respond,
        CancellationToken cancellationToken);

    Task<PairingResult> UnpairAsync(Guid containerId, CancellationToken cancellationToken);
}
