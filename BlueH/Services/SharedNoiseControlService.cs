namespace BlueH.Services;

public sealed class SharedNoiseControlService : INoiseControlService
{
    private readonly INoiseControlService _inner;
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, SharedNoiseControlConnection> _connections = [];

    public SharedNoiseControlService(INoiseControlService inner)
    {
        _inner = inner;
    }

    public async Task<INoiseControlSession?> OpenAsync(Guid containerId, CancellationToken cancellationToken)
    {
        SharedNoiseControlConnection connection;

        lock (_gate)
        {
            if (!_connections.TryGetValue(containerId, out SharedNoiseControlConnection? existing))
            {
                existing = new SharedNoiseControlConnection(containerId, _inner.OpenAsync(containerId, CancellationToken.None), Forget);
                _connections[containerId] = existing;
            }

            connection = existing;
            connection.AddUser();
        }

        try
        {
            INoiseControlSession? session = await connection.Opening.WaitAsync(cancellationToken);

            if (session is null)
            {
                await ReleaseAsync(connection);

                return null;
            }

            return connection.CreateLease(session, ReleaseAsync);
        }
        catch
        {
            if (connection.Opening.IsFaulted)
            {
                Forget(connection);
            }

            await ReleaseAsync(connection);

            throw;
        }
    }

    private void Forget(SharedNoiseControlConnection connection)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connection.ContainerId, out SharedNoiseControlConnection? current)
                && ReferenceEquals(current, connection))
            {
                _connections.Remove(connection.ContainerId);
            }
        }
    }

    private async Task ReleaseAsync(SharedNoiseControlConnection connection)
    {
        bool wasLastUser;

        lock (_gate)
        {
            wasLastUser = connection.RemoveUser();
        }

        if (!wasLastUser)
        {
            return;
        }

        Forget(connection);
        await connection.CloseAsync();
    }
}
