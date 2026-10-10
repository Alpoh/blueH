using BlueH.Models;
using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class FakeAudioEndpointService : IAudioEndpointService
{
    private TaskCompletionSource? _gate;

    public List<AudioEndpoint> Endpoints { get; } = [];

    public List<(string MmDeviceId, int VolumePercent)> VolumeCalls { get; } = [];

    public List<(string MmDeviceId, bool IsMuted)> MuteCalls { get; } = [];

    public List<(string MmDeviceId, AudioEndpointRole Role)> DefaultCalls { get; } = [];

    public Exception? FailureToThrow { get; set; }

    public void BlockUntilReleased()
    {
        _gate = new TaskCompletionSource();
    }

    public void Release()
    {
        _gate?.SetResult();
        _gate = null;
    }

    public Task<IReadOnlyList<AudioEndpoint>> FindRenderEndpointsAsync(CancellationToken cancellationToken)
    {
        if (FailureToThrow is not null)
        {
            return Task.FromException<IReadOnlyList<AudioEndpoint>>(FailureToThrow);
        }

        return Task.FromResult<IReadOnlyList<AudioEndpoint>>([.. Endpoints]);
    }

    public async Task SetVolumePercentAsync(string mmDeviceId, int volumePercent, CancellationToken cancellationToken)
    {
        VolumeCalls.Add((mmDeviceId, volumePercent));

        await WaitIfBlockedAsync();
    }

    public async Task SetMutedAsync(string mmDeviceId, bool isMuted, CancellationToken cancellationToken)
    {
        MuteCalls.Add((mmDeviceId, isMuted));

        if (FailureToThrow is not null)
        {
            throw FailureToThrow;
        }

        await WaitIfBlockedAsync();
    }

    public async Task SetDefaultAsync(string mmDeviceId, AudioEndpointRole role, CancellationToken cancellationToken)
    {
        DefaultCalls.Add((mmDeviceId, role));

        if (FailureToThrow is not null)
        {
            throw FailureToThrow;
        }

        await WaitIfBlockedAsync();
    }

    private async Task WaitIfBlockedAsync()
    {
        if (_gate is not null)
        {
            await _gate.Task;
        }
    }
}
