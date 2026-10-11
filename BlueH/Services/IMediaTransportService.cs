using BlueH.Models;

namespace BlueH.Services;

public interface IMediaTransportService : IDisposable
{
    event EventHandler? SessionChanged;

    Task StartAsync(CancellationToken cancellationToken);

    Task<MediaSession?> ReadCurrentSessionAsync(CancellationToken cancellationToken);

    Task<bool> TrySendAsync(MediaTransportAction action, CancellationToken cancellationToken);
}
