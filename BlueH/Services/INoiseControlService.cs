namespace BlueH.Services;

public interface INoiseControlService
{
    Task<INoiseControlSession?> OpenAsync(Guid containerId, CancellationToken cancellationToken);
}
