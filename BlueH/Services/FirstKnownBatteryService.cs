using BlueH.Models;

namespace BlueH.Services;

public sealed class FirstKnownBatteryService : IBatteryService
{
    private readonly IReadOnlyList<IBatteryService> _routes;

    public FirstKnownBatteryService(IReadOnlyList<IBatteryService> routes)
    {
        _routes = routes;
    }

    public async Task<BatteryCharge> ReadChargeAsync(Guid containerId, CancellationToken cancellationToken)
    {
        foreach (IBatteryService route in _routes)
        {
            BatteryCharge charge = await route.ReadChargeAsync(containerId, cancellationToken);

            if (charge.IsKnown)
            {
                return charge;
            }
        }

        return BatteryCharge.Unknown;
    }
}
