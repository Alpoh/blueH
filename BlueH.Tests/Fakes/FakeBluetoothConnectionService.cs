using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeBluetoothConnectionService : IBluetoothConnectionService
{
    public List<Guid> ConnectCalls { get; } = [];

    public List<Guid> DisconnectCalls { get; } = [];

    public Exception? FailureToThrow { get; set; }

    public Task ConnectAsync(Guid containerId, CancellationToken cancellationToken)
    {
        ConnectCalls.Add(containerId);

        return FailureToThrow is null ? Task.CompletedTask : Task.FromException(FailureToThrow);
    }

    public Task DisconnectAsync(Guid containerId, CancellationToken cancellationToken)
    {
        DisconnectCalls.Add(containerId);

        return FailureToThrow is null ? Task.CompletedTask : Task.FromException(FailureToThrow);
    }
}
