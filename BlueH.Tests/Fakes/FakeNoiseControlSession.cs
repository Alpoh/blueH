using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeNoiseControlSession : INoiseControlSession
{
    public NoiseControlMode? ModeToReport { get; set; } = NoiseControlMode.Off;

    public Exception? ReadFailureToThrow { get; set; }

    public Exception? SetFailureToThrow { get; set; }

    public TaskCompletionSource? PendingSet { get; set; }

    public List<NoiseControlMode> SetCalls { get; } = [];

    public bool IsDisposed { get; private set; }

    public event EventHandler<NoiseControlMode>? ModeReported;

    public event EventHandler? Closed;

    public Task<NoiseControlMode?> ReadModeAsync(CancellationToken cancellationToken)
    {
        return ReadFailureToThrow is null
            ? Task.FromResult(ModeToReport)
            : Task.FromException<NoiseControlMode?>(ReadFailureToThrow);
    }

    public Task SetModeAsync(NoiseControlMode mode, CancellationToken cancellationToken)
    {
        SetCalls.Add(mode);

        if (SetFailureToThrow is not null)
        {
            return Task.FromException(SetFailureToThrow);
        }

        return PendingSet?.Task ?? Task.CompletedTask;
    }

    public void Report(NoiseControlMode mode)
    {
        ModeReported?.Invoke(this, mode);
    }

    public void Close()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;

        return ValueTask.CompletedTask;
    }
}
