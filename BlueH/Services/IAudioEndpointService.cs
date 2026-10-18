using BlueH.Models;

namespace BlueH.Services;

public interface IAudioEndpointService
{
    Task<IReadOnlyList<AudioEndpoint>> FindRenderEndpointsAsync(CancellationToken cancellationToken);

    Task SetVolumePercentAsync(string mmDeviceId, int volumePercent, CancellationToken cancellationToken);

    Task SetMutedAsync(string mmDeviceId, bool isMuted, CancellationToken cancellationToken);

    Task SetDefaultAsync(string mmDeviceId, AudioEndpointRole role, CancellationToken cancellationToken);

    IAudioEndpointChangeWatcher CreateChangeWatcher();
}
