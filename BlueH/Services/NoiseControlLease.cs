using BlueH.Models;

namespace BlueH.Services;

internal sealed class NoiseControlLease : INoiseControlSession
{
    private readonly SharedNoiseControlConnection _connection;
    private readonly INoiseControlSession _session;
    private readonly Func<SharedNoiseControlConnection, Task> _release;

    private int _isDisposed;

    public NoiseControlLease(
        SharedNoiseControlConnection connection,
        INoiseControlSession session,
        Func<SharedNoiseControlConnection, Task> release)
    {
        _connection = connection;
        _session = session;
        _release = release;
    }

    public event EventHandler<NoiseControlMode>? ModeReported;

    public event EventHandler? Closed;

    public Task<NoiseControlMode?> ReadModeAsync(CancellationToken cancellationToken)
    {
        return _session.ReadModeAsync(cancellationToken);
    }

    public async Task SetModeAsync(NoiseControlMode mode, CancellationToken cancellationToken)
    {
        await _session.SetModeAsync(mode, cancellationToken);
        _connection.ReportToOthers(this, mode);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _connection.Detach(this);
        await _release(_connection);
    }

    public void RaiseModeReported(NoiseControlMode mode)
    {
        ModeReported?.Invoke(this, mode);
    }

    public void RaiseClosed()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
