using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeBluetoothDeviceService : IBluetoothDeviceService
{
    public List<BluetoothAudioEndpoint> Endpoints { get; } = [];

    public Exception? FailureToReturn { get; set; }

    public FakeBluetoothAudioEndpointWatcher Watcher { get; } = new();

    public int WatchersCreated { get; private set; }

    public Task<IReadOnlyList<BluetoothAudioEndpoint>> FindPairedAudioEndpointsAsync(
        CancellationToken cancellationToken)
    {
        if (FailureToReturn is not null)
        {
            return Task.FromException<IReadOnlyList<BluetoothAudioEndpoint>>(FailureToReturn);
        }

        return Task.FromResult<IReadOnlyList<BluetoothAudioEndpoint>>([.. Endpoints]);
    }

    public IBluetoothAudioEndpointWatcher CreateEndpointWatcher()
    {
        WatchersCreated++;

        return Watcher;
    }
}
