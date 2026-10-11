using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeMediaTransportService : IMediaTransportService
{
    private TaskCompletionSource? _gate;

    public event EventHandler? SessionChanged;

    public MediaSession? Session { get; set; }

    public Exception? FailureToThrow { get; set; }

    public Exception? CommandFailureToThrow { get; set; }

    public bool AcceptsCommands { get; set; } = true;

    public List<MediaTransportAction> SentActions { get; } = [];

    public bool IsStarted { get; private set; }

    public bool IsDisposed { get; private set; }

    public int ReadCalls { get; private set; }

    public bool HasSubscribers => SessionChanged is not null;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        IsStarted = true;

        return Task.CompletedTask;
    }

    public Task<MediaSession?> ReadCurrentSessionAsync(CancellationToken cancellationToken)
    {
        ReadCalls++;

        if (FailureToThrow is not null)
        {
            return Task.FromException<MediaSession?>(FailureToThrow);
        }

        return Task.FromResult(Session);
    }

    public async Task<bool> TrySendAsync(MediaTransportAction action, CancellationToken cancellationToken)
    {
        SentActions.Add(action);

        if (CommandFailureToThrow is not null)
        {
            throw CommandFailureToThrow;
        }

        if (_gate is not null)
        {
            await _gate.Task;
        }

        return AcceptsCommands;
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

    public void RaiseSessionChanged()
    {
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
