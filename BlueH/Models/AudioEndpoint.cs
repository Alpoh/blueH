namespace BlueH.Models;

public sealed record AudioEndpoint(
    string MmDeviceId,
    Guid ContainerId,
    string Name,
    int? VolumePercent,
    bool? IsMuted,
    bool IsDefaultForPlayback,
    bool IsDefaultForCommunications);
