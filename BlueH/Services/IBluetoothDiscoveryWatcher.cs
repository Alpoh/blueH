using BlueH.Models;

namespace BlueH.Services;

public interface IBluetoothDiscoveryWatcher : IDisposable
{
    event EventHandler<DiscoveredDevice>? DeviceFound;

    event EventHandler<string>? DeviceLost;

    void Start();

    void Stop();
}
