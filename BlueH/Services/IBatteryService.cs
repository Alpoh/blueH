using BlueH.Models;

namespace BlueH.Services;

public interface IBatteryService
{
    Task<BatteryCharge> ReadChargeAsync(Guid containerId, CancellationToken cancellationToken);
}
