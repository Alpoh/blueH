using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeBluetoothAudioEndpointWatcher : IBluetoothAudioEndpointWatcher
{
    public event EventHandler<BluetoothAudioEndpoint>? EndpointAdded;

    public event EventHandler<BluetoothAudioEndpoint>? EndpointUpdated;

    public event EventHandler<string>? EndpointRemoved;

    public bool IsStarted { get; private set; }

    public bool IsStopped { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool HasSubscribers =>
        EndpointAdded is not null || EndpointUpdated is not null || EndpointRemoved is not null;

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

    public void RaiseAdded(BluetoothAudioEndpoint endpoint)
    {
        EndpointAdded?.Invoke(this, endpoint);
    }

    public void RaiseUpdated(BluetoothAudioEndpoint endpoint)
    {
        EndpointUpdated?.Invoke(this, endpoint);
    }

    public void RaiseRemoved(string endpointId)
    {
        EndpointRemoved?.Invoke(this, endpointId);
    }
}
