namespace BlueH.Models;

public sealed record BluetoothAudioEndpoint(
    string Id,
    Guid ContainerId,
    string Name,
    bool? IsConnected,
    uint? MinorClass);
