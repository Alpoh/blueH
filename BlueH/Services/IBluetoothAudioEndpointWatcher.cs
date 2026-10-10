using BlueH.Models;

namespace BlueH.Services;

public interface IBluetoothAudioEndpointWatcher : IDisposable
{
    event EventHandler<BluetoothAudioEndpoint>? EndpointAdded;

    event EventHandler<BluetoothAudioEndpoint>? EndpointUpdated;

    event EventHandler<string>? EndpointRemoved;

    void Start();

    void Stop();
}
