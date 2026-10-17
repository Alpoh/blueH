using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeNoiseControlService : INoiseControlService
{
    public Dictionary<Guid, FakeNoiseControlSession> Sessions { get; } = [];

    public List<Guid> OpenCalls { get; } = [];

    public Exception? OpenFailureToThrow { get; set; }

    public TaskCompletionSource<INoiseControlSession?>? PendingOpen { get; set; }

    public Task<INoiseControlSession?> OpenAsync(Guid containerId, CancellationToken cancellationToken)
    {
        OpenCalls.Add(containerId);

        if (PendingOpen is not null)
        {
            return PendingOpen.Task;
        }

        if (OpenFailureToThrow is not null)
        {
            return Task.FromException<INoiseControlSession?>(OpenFailureToThrow);
        }

        return Task.FromResult<INoiseControlSession?>(Sessions.GetValueOrDefault(containerId));
    }
}
