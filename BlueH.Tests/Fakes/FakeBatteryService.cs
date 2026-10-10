using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeBatteryService : IBatteryService
{
    private readonly Dictionary<Guid, BatteryCharge> _charges = [];

    private TaskCompletionSource? _gate;

    public List<Guid> ReadContainerIds { get; } = [];

    public Exception? FailureToReturn { get; set; }

    public int ReadCount => ReadContainerIds.Count;

    public void SetCharge(Guid containerId, BatteryCharge charge)
    {
        _charges[containerId] = charge;
    }

    public void BlockUntilReleased()
    {
        _gate = new TaskCompletionSource();
    }

    public void Release()
    {
        _gate?.SetResult();
        _gate = null;
    }

    public async Task<BatteryCharge> ReadChargeAsync(Guid containerId, CancellationToken cancellationToken)
    {
        ReadContainerIds.Add(containerId);

        if (FailureToReturn is not null)
        {
            throw FailureToReturn;
        }

        if (_gate is not null)
        {
            await _gate.Task;
        }

        return _charges.TryGetValue(containerId, out BatteryCharge charge) ? charge : BatteryCharge.Unknown;
    }
}
