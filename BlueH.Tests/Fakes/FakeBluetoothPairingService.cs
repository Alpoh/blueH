using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeBluetoothPairingService : IBluetoothPairingService
{
    private TaskCompletionSource<PairingResult>? _pendingPairing;

    public FakeDiscoveryWatcher Watcher { get; private set; } = new();

    public List<string> PairCalls { get; } = [];

    public List<Guid> UnpairCalls { get; } = [];

    public PairingChallenge? ChallengeToRaise { get; set; }

    public PairingResponse? ChallengeResponse { get; private set; }

    public PairingResult PairingResultToReturn { get; set; } = new(PairingOutcome.Succeeded, "Paired");

    public PairingResult UnpairingResultToReturn { get; set; } = new(PairingOutcome.Succeeded, "Unpaired");

    public Exception? FailureToThrow { get; set; }

    public bool HoldsPairingOpen { get; set; }

    public IBluetoothDiscoveryWatcher CreateDiscoveryWatcher()
    {
        Watcher = new FakeDiscoveryWatcher();

        return Watcher;
    }

    public async Task<PairingResult> PairAsync(
        string endpointId,
        Func<PairingChallenge, CancellationToken, Task<PairingResponse>> respond,
        CancellationToken cancellationToken)
    {
        PairCalls.Add(endpointId);

        if (FailureToThrow is not null)
        {
            throw FailureToThrow;
        }

        if (ChallengeToRaise is not null)
        {
            ChallengeResponse = await respond(ChallengeToRaise, cancellationToken);

            if (!ChallengeResponse.IsAccepted)
            {
                return new PairingResult(PairingOutcome.RejectedByUser, "RejectedByHandler");
            }
        }

        if (HoldsPairingOpen)
        {
            _pendingPairing = new TaskCompletionSource<PairingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using CancellationTokenRegistration registration =
                cancellationToken.Register(() => _pendingPairing.TrySetCanceled(cancellationToken));

            return await _pendingPairing.Task;
        }

        return PairingResultToReturn;
    }

    public Task<PairingResult> UnpairAsync(Guid containerId, CancellationToken cancellationToken)
    {
        UnpairCalls.Add(containerId);

        return FailureToThrow is null
            ? Task.FromResult(UnpairingResultToReturn)
            : Task.FromException<PairingResult>(FailureToThrow);
    }
}

public sealed class FakeDiscoveryWatcher : IBluetoothDiscoveryWatcher
{
    public event EventHandler<DiscoveredDevice>? DeviceFound;

    public event EventHandler<string>? DeviceLost;

    public bool IsStarted { get; private set; }

    public bool IsStopped { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool HasSubscribers => DeviceFound is not null || DeviceLost is not null;

    public void Start()
    {
        IsStarted = true;
    }

    public void Stop()
    {
        IsStopped = true;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public void RaiseFound(DiscoveredDevice device)
    {
        DeviceFound?.Invoke(this, device);
    }

    public void RaiseLost(string endpointId)
    {
        DeviceLost?.Invoke(this, endpointId);
    }
}
