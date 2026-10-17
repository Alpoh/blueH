using BlueH.Models;

namespace BlueH.Services;

internal sealed class SharedNoiseControlConnection
{
    private readonly Action<SharedNoiseControlConnection> _forget;
    private readonly Lock _gate = new();
    private readonly List<NoiseControlLease> _leases = [];

    private int _userCount;
    private INoiseControlSession? _subscribedSession;

    public SharedNoiseControlConnection(
        Guid containerId,
        Task<INoiseControlSession?> opening,
        Action<SharedNoiseControlConnection> forget)
    {
        ContainerId = containerId;
        Opening = opening;
        _forget = forget;
    }

    public Guid ContainerId { get; }

    public Task<INoiseControlSession?> Opening { get; }

    public void AddUser()
    {
        _userCount++;
    }

    public bool RemoveUser()
    {
        _userCount--;

        return _userCount == 0;
    }

    public NoiseControlLease CreateLease(INoiseControlSession session, Func<SharedNoiseControlConnection, Task> release)
    {
        NoiseControlLease lease = new(this, session, release);

        lock (_gate)
        {
            if (_subscribedSession is null)
            {
                _subscribedSession = session;
                session.ModeReported += OnModeReported;
                session.Closed += OnClosed;
            }

            _leases.Add(lease);
        }

        return lease;
    }

    public void Detach(NoiseControlLease lease)
    {
        lock (_gate)
        {
            _leases.Remove(lease);
        }
    }

    public void ReportToOthers(NoiseControlLease source, NoiseControlMode mode)
    {
        foreach (NoiseControlLease lease in Snapshot())
        {
            if (!ReferenceEquals(lease, source))
            {
                lease.RaiseModeReported(mode);
            }
        }
    }

    public async Task CloseAsync()
    {
        INoiseControlSession? session;

        try
        {
            session = await Opening;
        }
        catch (Exception)
        {
            return;
        }

        if (session is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_subscribedSession is not null)
            {
                _subscribedSession.ModeReported -= OnModeReported;
                _subscribedSession.Closed -= OnClosed;
                _subscribedSession = null;
            }
        }

        await session.DisposeAsync();
    }

    private NoiseControlLease[] Snapshot()
    {
        lock (_gate)
        {
            return [.. _leases];
        }
    }

    private void OnModeReported(object? sender, NoiseControlMode mode)
    {
        foreach (NoiseControlLease lease in Snapshot())
        {
            lease.RaiseModeReported(mode);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _forget(this);

        foreach (NoiseControlLease lease in Snapshot())
        {
            lease.RaiseClosed();
        }
    }
}
