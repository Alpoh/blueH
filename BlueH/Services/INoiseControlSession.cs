using BlueH.Models;

namespace BlueH.Services;

public interface INoiseControlSession : IAsyncDisposable
{
    event EventHandler<NoiseControlMode>? ModeReported;

    event EventHandler? Closed;

    Task<NoiseControlMode?> ReadModeAsync(CancellationToken cancellationToken);

    Task SetModeAsync(NoiseControlMode mode, CancellationToken cancellationToken);
}
